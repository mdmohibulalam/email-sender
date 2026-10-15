using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace CampaignStudio.Api;

public sealed class DispatchWorker(IServiceScopeFactory scopes, StudioSettings settings, ILogger<DispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Tick(stop); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Worker cycle failed ({ErrorType}); no message was automatically retried.", e.GetType().Name); }
            try { await Task.Delay(settings.DelayMilliseconds, stop); } catch (OperationCanceledException) { break; }
        }
    }

    public async Task Tick(CancellationToken stop = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Store>();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // An abandoned SMTP attempt may already have been accepted. Never resend it automatically.
        await db.Jobs.Where(x => x.State == "Sending" && x.StartedAt < now - settings.LeaseSeconds)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Unknown").SetProperty(x => x.Detail, "Interrupted attempt; inspect the provider before retrying."), stop);
        var campaign = await db.Campaigns.AsNoTracking().Where(x => (x.State == "Scheduled" || x.State == "Running") && x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt).FirstOrDefaultAsync(stop);
        if (campaign is null) return;
        var job = await db.Jobs.AsNoTracking().Where(x => x.CampaignId == campaign.Id && x.State == "Pending").OrderBy(x => x.Email).FirstOrDefaultAsync(stop);
        if (job is null)
        {
            if (!await db.Jobs.AnyAsync(x => x.CampaignId == campaign.Id && x.State == "Sending", stop))
            {
                var state = await db.Jobs.AnyAsync(x => x.CampaignId == campaign.Id && (x.State == "Unknown" || x.State == "Failed"), stop) ? "NeedsReview" : "Completed";
                await db.Campaigns.Where(x => x.Id == campaign.Id && (x.State == "Scheduled" || x.State == "Running")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, state), stop);
            }
            return;
        }
        if (!await db.Contacts.AnyAsync(x => x.Id == job.ContactId && !x.Suppressed, stop))
        {
            await db.Jobs.Where(x => x.Id == job.Id && x.State == "Pending").ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Suppressed"), stop); return;
        }
        int claimed = await db.Jobs.Where(x => x.Id == job.Id && x.State == "Pending").ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Sending").SetProperty(x => x.StartedAt, now), stop);
        if (claimed == 0) return;
        // Read again after claiming so pause/unsubscribe can prevent a dispatch that has not started.
        if (!await db.Campaigns.AnyAsync(x => x.Id == campaign.Id && (x.State == "Scheduled" || x.State == "Running"), stop))
        {
            var cancelled = await db.Campaigns.AnyAsync(x => x.Id == campaign.Id && x.State == "Cancelled", stop);
            await db.Jobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, cancelled ? "Cancelled" : "Pending"), stop); return;
        }
        if (!await db.Contacts.AnyAsync(x => x.Id == job.ContactId && !x.Suppressed, stop))
        { await Finish(db, job.Id, "Suppressed", "Excluded before dispatch", stop); return; }
        await db.Campaigns.Where(x => x.Id == campaign.Id && x.State == "Scheduled").ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Running"), stop);
        var token = scope.ServiceProvider.GetRequiredService<UnsubscribeTokens>().Create(job.ContactId);
        var unsubscribe = settings.PublicBaseUrl.TrimEnd('/') + "/u/" + token;
        var message = new MimeMessage { MessageId = job.MessageId };
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail.Length == 0 ? "preview@example.com" : settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(job.Email));
        message.Subject = EmailRenderer.Merge(campaign.Subject, job.FirstName, job.LastName, job.Email).Replace('\r', ' ').Replace('\n', ' ');
        message.Headers.Add("List-Unsubscribe", "<" + unsubscribe + ">");
        message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        message.Body = new BodyBuilder
        {
            HtmlBody = EmailRenderer.Html(campaign, job.FirstName, job.LastName, job.Email, unsubscribe),
            TextBody = EmailRenderer.Merge(campaign.Heading + "\n\n" + campaign.Body + "\n\n" + campaign.ButtonLabel + ": " + campaign.ButtonUrl + "\n\n" + campaign.Footer, job.FirstName, job.LastName, job.Email) + "\nUnsubscribe: " + unsubscribe
        }.ToMessageBody();
        bool sendingStarted = false;
        try
        {
            if (campaign.Mode == "Capture")
            {
                var captures = Path.Combine(settings.DataDirectory, "captures"); Directory.CreateDirectory(captures);
                var path = Path.Combine(captures, job.Id + ".eml");
                await message.WriteToAsync(path + ".tmp", stop); File.Move(path + ".tmp", path, true);
                await Finish(db, job.Id, "Captured", "Test email saved locally. No email sent.", stop);
            }
            else
            {
                if (!settings.LiveReady) { await Finish(db, job.Id, "Failed", "Sending configuration unavailable.", stop); return; }
                using var smtp = new SmtpClient { Timeout = 30000 };
                await smtp.ConnectAsync(settings.SmtpHost, settings.SmtpPort, settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, stop);
                if (settings.SmtpUser.Length > 0) await smtp.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword, stop);
                sendingStarted = true;
                await smtp.SendAsync(message, stop);
                await Finish(db, job.Id, "Accepted", "SMTP accepted the email. Inbox delivery is not confirmed.", stop);
                // A failure after acceptance must not rewrite the accepted result.
                try { await smtp.DisconnectAsync(true, stop); } catch (Exception) { }
            }
        }
        catch (Exception)
        {
            // Even cancellation during DATA is ambiguous. Persist with a fresh token on shutdown.
            await Finish(db, job.Id, sendingStarted ? "Unknown" : "Failed",
                sendingStarted ? "SMTP result uncertain; inspect the provider before any retry." : "Could not prepare or connect. Check server settings.", CancellationToken.None);
        }
    }
    private static Task<int> Finish(Store db, Guid id, string state, string detail, CancellationToken stop) => db.Jobs.Where(x => x.Id == id && x.State == "Sending")
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, state).SetProperty(x => x.Detail, detail).SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), stop);
}
