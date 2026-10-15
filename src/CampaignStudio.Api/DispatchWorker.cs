using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Text;

namespace CampaignStudio.Api;

public sealed class DispatchWorker(IServiceScopeFactory scopes, StudioSettings settings, ILogger<DispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Tick(stop); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Worker cycle failed ({ErrorType}).", e.GetType().Name); }
            try { await Task.Delay(250, stop); } catch (OperationCanceledException) { break; }
        }
    }
    public async Task Tick(CancellationToken stop = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Store>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        long now = clock.GetUtcNow().ToUnixTimeSeconds(), millis = clock.GetUtcNow().ToUnixTimeMilliseconds();
        await Recover(db, now, stop);
        try { await Inboxes.ProcessOne(db, scope.ServiceProvider, now, stop); } catch (FormatException) { }
        await SignupForms.ProcessOne(db, settings, scope.ServiceProvider, now, stop);
        var campaign = await db.Campaigns.AsNoTracking().Where(c => (c.State == "Scheduled" || c.State == "Running") && c.ScheduledAt <= now &&
            (db.Jobs.Any(j => j.CampaignId == c.Id && (j.State == "Pending" || j.State == "Deferred") && (j.NextAttemptAt == null || j.NextAttemptAt <= now)) ||
            !db.Jobs.Any(j => j.CampaignId == c.Id && (j.State == "Pending" || j.State == "Deferred" || j.State == "Sending"))))
            .OrderBy(c => c.ScheduledAt).FirstOrDefaultAsync(stop);
        if (campaign is null) return;
        var job = await db.Jobs.AsNoTracking().Where(x => x.CampaignId == campaign.Id && (x.State == "Pending" || x.State == "Deferred") && (x.NextAttemptAt == null || x.NextAttemptAt <= now)).OrderBy(x => x.Email).FirstOrDefaultAsync(stop);
        if (job is null)
        {
            var state = await db.Jobs.AnyAsync(x => x.CampaignId == campaign.Id && (x.State == "Unknown" || x.State == "Failed"), stop) ? "NeedsReview" : "Completed";
            await db.Campaigns.Where(x => x.Id == campaign.Id && (x.State == "Scheduled" || x.State == "Running")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, state), stop); return;
        }
        SenderProfile? sender = null;
        await using (var tx = await db.Database.BeginTransactionAsync(stop))
        {
            if (await db.Campaigns.Where(x => x.Id == campaign.Id && (x.State == "Scheduled" || x.State == "Running")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Running"), stop) == 0) return;
            if (campaign.Mode == "Smtp")
            {
                sender = await db.Senders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == campaign.SenderId, stop);
                if (sender?.VerifiedAt is null || !settings.EnableLiveSending || !settings.PublicLinksReady)
                {
                    await db.Campaigns.Where(x => x.Id == campaign.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Paused").SetProperty(x => x.Issue, "Live sending unavailable. Retest the sender and check HTTPS configuration."), stop);
                    await tx.CommitAsync(stop); return;
                }
                if (await db.Senders.Where(x => x.Id == sender.Id && x.VerifiedAt != null && x.NextSendAt <= millis).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextSendAt, millis + sender.DelayMilliseconds), stop) == 0) return;
            }
            if (await db.Jobs.Where(x => x.Id == job.Id && (x.State == "Pending" || x.State == "Deferred") && (x.NextAttemptAt == null || x.NextAttemptAt <= now)).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Sending").SetProperty(x => x.StartedAt, now).SetProperty(x => x.Attempts, x => x.Attempts + 1), stop) == 0) return;
            job.Attempts++;
            db.Attempts.Add(new SendAttempt { JobId = job.Id, Number = job.Attempts, StartedAt = now }); await db.SaveChangesAsync(stop); await tx.CommitAsync(stop);
        }
        if (!await AudienceRules.CanSend(db, campaign.Id, job.ContactId)) { await Complete(db, campaign, job, new("Stopped", "Excluded or paused before dispatch."), sender, now); return; }
        SmtpOutcome result;
        try
        {
            string baseUrl = settings.PublicLinksReady ? settings.PublicBaseUrl.TrimEnd('/') : "https://preview.invalid";
            string unsubscribe = baseUrl + "/u/" + scope.ServiceProvider.GetRequiredService<UnsubscribeTokens>().Create(job.ContactId);
            var message = new MimeMessage { MessageId = job.MessageId };
            message.From.Add(new MailboxAddress(campaign.SenderName, campaign.SenderEmail.Length > 0 ? campaign.SenderEmail : "preview@example.com"));
            message.To.Add(MailboxAddress.Parse(job.Email));
            if (campaign.ReplyTo.Length > 0) message.ReplyTo.Add(MailboxAddress.Parse(campaign.ReplyTo));
            message.Subject = EmailRenderer.Merge(ScriptTags.Resolve(campaign.TagsJson, campaign.Subject, job.FirstName, job.LastName, job.Email), job.FirstName, job.LastName, job.Email).Replace('\r', ' ').Replace('\n', ' ');
            message.Headers.Add("List-Unsubscribe", "<" + unsubscribe + ">"); message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
            string html = EmailRenderer.Html(campaign, job.FirstName, job.LastName, job.Email, unsubscribe);
            var body = new BodyBuilder { HtmlBody = html, TextBody = EmailDesign.Text(html) + "\nUnsubscribe: " + unsubscribe };
            await MarketingEndpoints.ValidateAssets(db, settings, campaign);
            foreach (var id in EmailDesign.Read<Guid[]>(campaign.AttachmentIdsJson).Distinct())
            {
                var asset = (await db.Attachments.FindAsync(id))!;
                if (asset.RecipientEmail.Length == 0 || asset.RecipientEmail == job.Email)
                    body.Attachments.Add(asset.Name, await File.ReadAllBytesAsync(Path.Combine(settings.DataDirectory, "attachments", id.ToString("N")), stop));
            }
            if (campaign.CalendarJson.Length > 0) body.Attachments.Add("event.ics", Encoding.UTF8.GetBytes(EmailDesign.Calendar(EmailDesign.Read<CalendarEvent>(campaign.CalendarJson), campaign.Id)), ContentType.Parse("text/calendar; charset=utf-8; method=PUBLISH"));
            message.Body = body.ToMessageBody();
            if (campaign.Mode == "Capture")
            {
                if (!await AudienceRules.CanSend(db, campaign.Id, job.ContactId)) result = new("Stopped", "Excluded or paused before capture.");
                else
                {
                    string folder = Path.Combine(settings.DataDirectory, "captures"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, job.Id + ".eml");
                    await message.WriteToAsync(path + ".tmp", stop); File.Move(path + ".tmp", path, true); result = new("Captured", "Test email saved locally. No email sent.");
                }
            }
            else
            {
                sender!.FromEmail = campaign.SenderEmail; sender.FromName = campaign.SenderName; sender.ReplyTo = campaign.ReplyTo; sender.ReturnPath = campaign.ReturnPath;
                string password = sender.PasswordCipher.Length > 0 ? scope.ServiceProvider.GetRequiredService<CredentialVault>().Open(sender.PasswordCipher) : "";
                result = await scope.ServiceProvider.GetRequiredService<ISmtpTransport>().Send(sender, password, message, () => AudienceRules.CanSend(db, campaign.Id, job.ContactId), stop);
            }
        }
        catch (OperationCanceledException) { result = new("InterruptedBeforeSend", "Stopped before dispatch."); }
        catch (Exception) { result = new("PreparationFailed", "Message preparation or attachment verification failed. Review the campaign."); }
        await Complete(db, campaign, job, result, sender, now);
    }
    private async Task Complete(Store db, Campaign campaign, RecipientJob job, SmtpOutcome result, SenderProfile? sender, long now)
    {
        string state = result.State; long? next = null; int retries = job.RetryCount;
        if (state == "Stopped")
        {
            var current = await db.Campaigns.AsNoTracking().FirstAsync(x => x.Id == campaign.Id);
            state = current.State == "Cancelled" ? "Cancelled" : current.State == "Paused" ? "Pending" : "Suppressed";
        }
        if (state == "InterruptedBeforeSend") state = "Pending";
        if (state == "Transient")
        {
            if (retries < (sender?.MaxRetries ?? 0)) { state = "Deferred"; next = now + Math.Min(3600, settings.RetryBaseSeconds * (1L << retries)); retries++; }
            else state = "Failed";
        }
        if (state == "ConnectionBlocked") { state = "Deferred"; next = now; }
        if (state is "Rejected" or "HardBounce" or "PreparationFailed") state = "Failed";
        await using var tx = await db.Database.BeginTransactionAsync();
        if (result.State == "HardBounce")
        {
            await db.Contacts.Where(x => x.Id == job.ContactId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Suppressed, true).SetProperty(x => x.SuppressionReason, "Hard bounce (5.1.1)"));
            AudienceRules.Log(db, job.ContactId, "HardBounce", "SMTP 5.1.1");
            await db.Jobs.Where(x => x.ContactId == job.ContactId && (x.State == "Pending" || x.State == "Deferred")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Suppressed"));
        }
        int changed = await db.Jobs.Where(x => x.Id == job.Id && x.State == "Sending").ExecuteUpdateAsync(s => s.SetProperty(x => x.State, state).SetProperty(x => x.Detail, result.Detail).SetProperty(x => x.FinishedAt, now).SetProperty(x => x.NextAttemptAt, next).SetProperty(x => x.RetryCount, retries));
        if (changed == 0) return;
        await db.Attempts.Where(x => x.JobId == job.Id && x.Number == job.Attempts && x.Outcome == "Sending").ExecuteUpdateAsync(s => s.SetProperty(x => x.Outcome, result.State).SetProperty(x => x.Detail, result.Detail).SetProperty(x => x.FinishedAt, now));
        if (result.State == "ConnectionBlocked" && sender is not null)
        {
            await db.Senders.Where(x => x.Id == sender.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.VerifiedAt, (long?)null).SetProperty(x => x.TestResult, result.Detail));
            await db.Campaigns.Where(x => x.SenderId == sender.Id && (x.State == "Scheduled" || x.State == "Running")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Paused").SetProperty(x => x.Issue, result.Detail));
        }
        else if (result.State is "Unknown" or "PreparationFailed") await db.Campaigns.Where(x => x.Id == campaign.Id && (x.State == "Scheduled" || x.State == "Running")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Paused").SetProperty(x => x.Issue, result.Detail));
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    private async Task Recover(Store db, long now, CancellationToken stop)
    {
        var expired = await db.Jobs.AsNoTracking().Where(x => x.State == "Sending" && x.StartedAt < now - settings.LeaseSeconds).OrderBy(x => x.StartedAt).Take(100).ToListAsync(stop);
        foreach (var job in expired)
        {
            var mode = await db.Campaigns.Where(x => x.Id == job.CampaignId).Select(x => x.Mode).FirstOrDefaultAsync(stop);
            string state = "Unknown";
            if (mode == "Capture")
            {
                state = "Pending"; string path = Path.Combine(settings.DataDirectory, "captures", job.Id + ".eml");
                if (File.Exists(path)) try { using var f = File.OpenRead(path); if ((await MimeMessage.LoadAsync(f, stop)).MessageId == job.MessageId) state = "Captured"; } catch (Exception) { }
            }
            await using var tx = await db.Database.BeginTransactionAsync(stop);
            int updated = await db.Jobs.Where(x => x.Id == job.Id && x.State == "Sending" && x.StartedAt == job.StartedAt).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, state).SetProperty(x => x.Detail, state == "Unknown" ? "Interrupted SMTP attempt; never automatically retried." : "Recovered local capture state."), stop);
            if (updated > 0) await db.Attempts.Where(x => x.JobId == job.Id && x.Outcome == "Sending").ExecuteUpdateAsync(s => s.SetProperty(x => x.Outcome, state).SetProperty(x => x.FinishedAt, now), stop);
            await tx.CommitAsync(stop);
        }
    }
}
