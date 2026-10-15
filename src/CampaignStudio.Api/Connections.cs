using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
namespace CampaignStudio.Api;

public static class Connections
{
    public static object Public(SenderProfile s) => new { s.Id, s.Name, s.Host, s.Port, s.Username, s.FromEmail, s.FromName, s.ReplyTo, s.ReturnPath, s.DelayMilliseconds, s.MaxRetries, s.MaxConnections, s.DeliveryMode, s.VerifiedAt, s.TestResult, s.Revision, hasPassword = s.PasswordCipher.Length > 0 };
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/connections", async (Store db) => (await db.Senders.OrderBy(x => x.Name).ToListAsync()).Select(Public));
        api.MapPost("/connections", async (SenderInput input, Store db, CredentialVault vault) =>
        {
            var sender = new SenderProfile(); Apply(sender, input, vault); db.Add(sender); await db.SaveChangesAsync(); return Results.Ok(Public(sender));
        });
        api.MapPut("/connections/{id:guid}", async (Guid id, SenderInput input, Store db, CredentialVault vault) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            if (await db.Senders.Where(x => x.Id == id && x.Revision == input.ExpectedRevision).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, x => x.Revision + 1)) == 0) return Results.Conflict(new { error = "Connection changed. Reload before saving." });
            if (await db.Campaigns.AnyAsync(x => x.SenderId == id && (x.State == "Scheduled" || x.State == "Running"))) return Results.Conflict(new { error = "Pause campaigns using this connection before editing it." });
            var sender = (await db.Senders.FindAsync(id))!; Apply(sender, input, vault); await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(Public(sender));
        });
        api.MapPost("/connections/{id:guid}/test", async (Guid id, Store db, CredentialVault vault, ISmtpTransport transport, CancellationToken ct) =>
        {
            var sender = await db.Senders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct); if (sender is null) return Results.NotFound();
            var result = await transport.Test(sender, sender.PasswordCipher.Length == 0 ? "" : vault.Open(sender.PasswordCipher), ct);
            long? verified = result.State == "Verified" ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : null;
            var count = await db.Senders.Where(x => x.Id == id && x.Revision == sender.Revision).ExecuteUpdateAsync(s => s.SetProperty(x => x.VerifiedAt, verified).SetProperty(x => x.TestResult, result.Detail), ct);
            return count == 0 ? Results.Conflict(new { error = "Connection changed during the test. Test again." }) : Results.Ok(new { verified = verified.HasValue, detail = result.Detail });
        });
        api.MapGet("/jobs/{id:guid}/attempts", async (Guid id, Store db) => await db.Attempts.Where(x => x.JobId == id).OrderBy(x => x.Number).ToListAsync());
        api.MapPost("/campaigns/{id:guid}/retry-failed", async (Guid id, RetryInput input, Store db, StudioSettings settings, TimeProvider clock) =>
        {
            if (!input.ConfirmRetry) throw new FormatException("Explicitly confirm retrying known failures.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var locked = await db.Campaigns.Where(x => x.Id == id && (x.State == "NeedsReview" || x.State == "Paused" || x.State == "Completed")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "PreparingRetry"));
            if (locked == 0) return Results.Conflict(new { error = "Pause or finish the campaign before retrying failures." });
            var campaign = (await db.Campaigns.FindAsync(id))!;
            if (campaign.Mode == "Smtp" && (!settings.PublicLinksReady || !settings.EnableLiveSending || !await db.Senders.AnyAsync(x => x.Id == campaign.SenderId && x.VerifiedAt != null))) throw new FormatException("Retest the sender and enable live sending first.");
            int count = await db.Jobs.Where(x => x.CampaignId == id && x.State == "Failed" && db.Contacts.Any(c => c.Id == x.ContactId && !c.Suppressed)).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Pending").SetProperty(x => x.RetryCount, 0).SetProperty(x => x.NextAttemptAt, (long?)null));
            if (count == 0) throw new FormatException("There are no eligible known failures. Accepted and Unknown jobs are never retried here.");
            campaign.State = "Scheduled"; campaign.ScheduledAt = clock.GetUtcNow().ToUnixTimeSeconds(); campaign.Issue = ""; await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { retried = count });
        });
    }
    private static void Apply(SenderProfile s, SenderInput i, CredentialVault vault)
    {
        var fields = new[] { i.DeliveryMode, i.Name, i.Host, i.Username, i.Password, i.FromEmail, i.FromName, i.ReplyTo, i.ReturnPath };
        if (fields.Any(x => x is null || x.Contains('\r') || x.Contains('\n')) || i.Name.Length is < 1 or > 250 || i.Host.Length > 253 || i.DeliveryMode is not ("Smtp" or "Direct") || i.DeliveryMode == "Smtp" && (i.Host.Length == 0 || Uri.CheckHostName(i.Host) == UriHostNameType.Unknown) || i.Username.Length > 500 || i.Password.Length > 2000 || i.FromName.Length > 250 || !ContactRules.ValidEmail(i.FromEmail) || i.ReplyTo.Length > 0 && !ContactRules.ValidEmail(i.ReplyTo) || i.ReturnPath.Length > 0 && !ContactRules.ValidEmail(i.ReturnPath) || i.Port is < 1 or > 65535 || i.DelayMilliseconds is < 10 or > 600000 || i.MaxRetries is < 0 or > 5 || i.MaxConnections is < 1 or > 100) throw new FormatException("Check sender, hostname, port, pacing, and retry limits.");
        s.Name = i.Name.Trim(); s.Host = i.Host; s.Port = i.Port; s.Username = i.Username; s.FromEmail = ContactRules.Normalize(i.FromEmail); s.FromName = i.FromName; s.ReplyTo = i.ReplyTo; s.ReturnPath = i.ReturnPath;
        s.MaxConnections = i.MaxConnections; s.DeliveryMode = i.DeliveryMode; s.DelayMilliseconds = i.DelayMilliseconds; s.MaxRetries = i.MaxRetries; s.VerifiedAt = null; s.TestResult = "Changed; test before sending.";
        if (i.Password.Length > 0) s.PasswordCipher = vault.Seal(i.Password); else if (i.ClearPassword) s.PasswordCipher = "";
    }
}
