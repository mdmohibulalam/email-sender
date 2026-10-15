using System.Security.Cryptography;
using System.Text;
using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
using MimeKit;
namespace CampaignStudio.Api;

public static class MarketingEndpoints
{
    private static IResult Csv(string name, string header, IEnumerable<string[]> rows) => Results.File(Encoding.UTF8.GetBytes("\uFEFF" + header + "\r\n" + string.Join("\r\n", rows.Select(x => string.Join(',', x.Select(ContactRules.CsvCell))))), "text/csv; charset=utf-8", name);
    public static async Task ValidateAssets(Store db, StudioSettings settings, Campaign campaign)
    {
        long total = 0;
        foreach (var id in EmailDesign.Read<Guid[]>(campaign.AttachmentIdsJson).Distinct())
        {
            var asset = await db.Attachments.FindAsync(id) ?? throw new FormatException("An attachment is missing.");
            var path = Path.Combine(settings.DataDirectory, "attachments", id.ToString("N"));
            if (!File.Exists(path)) throw new FormatException("An attachment file is unavailable.");
            using var stream = File.OpenRead(path);
            if (Convert.ToHexString(SHA256.HashData(stream)) != asset.Sha256) throw new FormatException("An attachment failed its integrity check.");
            total += asset.Size;
        }
        if (total > 20_000_000) throw new FormatException("Attachments must total under 20 MB.");
    }
    public static void Map(RouteGroupBuilder api)
    {
        api.MapDelete("/campaigns/{id:guid}", async (Guid id, Store db) => await db.Campaigns.Where(x => x.Id == id && x.State == "Draft").ExecuteDeleteAsync() > 0 ? Results.Ok(new { deleted = true }) : Results.Conflict(new { error = "Only drafts can be deleted." }));
        api.MapGet("/campaigns/{id:guid}/audience", async (Guid id, Store db) =>
        { var c = await db.Campaigns.FindAsync(id); if (c is null) return Results.NotFound(); var q = await AudienceRules.CampaignAudience(db, c); return Results.Ok(new { total = await q.CountAsync(), sample = await q.Take(20).Select(x => new { x.Id, x.Email }).ToListAsync() }); });
        api.MapGet("/segments", async (Store db) => await db.Segments.OrderBy(x => x.Name).ToListAsync());
        api.MapPost("/segments", async (SegmentInput i, Store db) =>
        {
            if (i.Name is null || i.Name.Trim().Length is < 1 or > 250 || i.RulesJson is null) throw new FormatException("Enter a segment name and rules.");
            AudienceRules.Validate(i.RulesJson, i.Match); var segment = new Segment { Name = i.Name.Trim(), RulesJson = i.RulesJson, Match = i.Match }; db.Add(segment); await db.SaveChangesAsync(); return Results.Ok(segment);
        });
        api.MapGet("/segments/{id:guid}/preview", async (Guid id, Store db) =>
        { var s = await db.Segments.FindAsync(id); if (s is null) return Results.NotFound(); var q = AudienceRules.Apply(db.Contacts.Where(x => !x.Suppressed), s); return Results.Ok(new { total = await q.CountAsync(), items = await q.OrderBy(x => x.Email).Take(100).ToListAsync() }); });
        api.MapDelete("/segments/{id:guid}", async (Guid id, Store db) =>
        { if (await db.Campaigns.AnyAsync(x => x.SegmentId == id && x.State == "Draft")) throw new FormatException("Remove this segment from drafts before deleting it."); return Results.Ok(new { deleted = await db.Segments.Where(x => x.Id == id).ExecuteDeleteAsync() }); });
        api.MapPost("/lists/move", async (ListMoveInput i, Store db) =>
        {
            if (i.Sources is null || i.Sources.Length is < 1 or > 100 || i.Sources.Any(x => x is null || x.Length > 250) || i.Destination is null || i.Destination.Trim().Length is < 1 or > 250) throw new FormatException("Choose source lists and a destination.");
            var q = db.Contacts.Where(x => i.Sources.Contains(x.ListName));
            if (i.SegmentId is Guid id) { var segment = await db.Segments.FindAsync(id) ?? throw new FormatException("Segment not found."); q = AudienceRules.Apply(q, segment); }
            var ids = q.Select(x => x.Id);
            int moved = await db.Contacts.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.ListName, i.Destination.Trim()));
            return Results.Ok(new { moved, suppressionPreserved = true });
        });
        api.MapGet("/exclusions", async (Store db) => await db.Exclusions.OrderBy(x => x.Kind).ThenBy(x => x.Value).ToListAsync());
        api.MapPost("/exclusions", async (ExclusionInput i, Store db) =>
        {
            if (i.Value is null) throw new FormatException("Enter an email or domain.");
            string value = i.Value.Trim().ToLowerInvariant();
            if (i.Kind is not ("Email" or "Domain") || value.Length > 254 || (i.Kind == "Email" ? !ContactRules.ValidEmail(value) : Uri.CheckHostName(value) != UriHostNameType.Dns || !value.Contains('.') || value.Contains('@'))) throw new FormatException("Enter a valid email or domain.");
            var exclusion = new Exclusion { Value = value, Kind = i.Kind }; db.Add(exclusion); await db.SaveChangesAsync(); return Results.Ok(exclusion);
        });
        api.MapDelete("/exclusions/{id:guid}", async (Guid id, Store db) => Results.Ok(new { deleted = await db.Exclusions.Where(x => x.Id == id).ExecuteDeleteAsync() }));
        api.MapGet("/contacts/{id:guid}/history", async (Guid id, Store db) => await db.ConsentEvents.Where(x => x.ContactId == id).OrderByDescending(x => x.At).ToListAsync());
        api.MapGet("/consent/export", async (Store db) => Csv("consent-history.csv", "Email,Action,Source,UTC timestamp", (await (from e in db.ConsentEvents join c in db.Contacts on e.ContactId equals c.Id orderby e.At select new { c.Email, e.Action, e.Source, e.At }).ToListAsync()).Select(x => new[] { x.Email, x.Action, x.Source, DateTimeOffset.FromUnixTimeSeconds(x.At).ToString("O") })));
        api.MapGet("/library", async (string? kind, Store db) => await db.Library.Where(x => kind == null || x.Kind == kind).OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync());
        api.MapPost("/library", async (LibraryInput i, Store db) =>
        {
            if (i.Name is null || i.Name.Trim().Length is < 1 or > 250 || i.Category is null || i.Category.Length > 100 || i.Content is null || i.Content.Length > 500000 || i.Kind is not ("Snippet" or "Template" or "Note")) throw new FormatException("Check library name, category, type, and content size.");
            if (i.Kind == "Template") EmailDesign.CleanHtml(i.Content);
            var item = new LibraryItem { Name = i.Name.Trim(), Category = i.Category, Kind = i.Kind, Content = i.Content }; db.Add(item); await db.SaveChangesAsync(); return Results.Ok(item);
        });
        api.MapPut("/library/{id:guid}", async (Guid id, LibraryInput i, Store db) =>
        {
            if (i.Name is null || i.Name.Trim().Length is < 1 or > 250 || i.Content is null || i.Content.Length > 500000 || i.Category is null || i.Category.Length > 100 || i.Kind is not ("Snippet" or "Template" or "Note")) throw new FormatException("Invalid library item.");
            var item = await db.Library.FindAsync(id); if (item is null) return Results.NotFound(); item.Name = i.Name; item.Content = i.Content; item.Category = i.Category; item.Kind = i.Kind; item.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); await db.SaveChangesAsync(); return Results.Ok(item);
        });
        api.MapDelete("/library/{id:guid}", async (Guid id, Store db) => Results.Ok(new { deleted = await db.Library.Where(x => x.Id == id).ExecuteDeleteAsync() }));
        api.MapGet("/attachments", async (Store db) => await db.Attachments.OrderBy(x => x.Name).ToListAsync());
        api.MapPost("/attachments", async (AttachmentInput i, Store db, StudioSettings settings) =>
        {
            if (i.Name is null || i.Name.Length is < 1 or > 200 || i.Name.IndexOfAny(['\r', '\n', '/', '\\']) >= 0 || i.Base64 is null || i.Base64.Length > 6_700_000 || i.RecipientEmail is null || i.RecipientEmail.Length > 0 && !ContactRules.ValidEmail(i.RecipientEmail)) throw new FormatException("Choose a file under 5 MB and an optional valid recipient email.");
            byte[] bytes = Convert.FromBase64String(i.Base64); if (bytes.Length is < 1 or > 5_000_000) throw new FormatException("Files must be between 1 byte and 5 MB.");
            var asset = new Attachment { Name = i.Name, RecipientEmail = ContactRules.Normalize(i.RecipientEmail), Size = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
            string folder = Path.Combine(settings.DataDirectory, "attachments"); Directory.CreateDirectory(folder); string path = Path.Combine(folder, asset.Id.ToString("N"));
            await File.WriteAllBytesAsync(path + ".tmp", bytes); File.Move(path + ".tmp", path);
            try { db.Add(asset); await db.SaveChangesAsync(); } catch { File.Delete(path); throw; } return Results.Ok(asset);
        });
        api.MapPost("/messages/import", async (HtmlImportInput i) =>
        {
            if (i.Name is null || i.Name.Length > 250 || i.Content is null || i.Content.Length > 2_000_000 || i.Format is not ("html" or "eml")) throw new FormatException("Import an HTML or EML file under 2 MB.");
            string html = i.Content, subject = "";
            if (i.Format == "eml") { using var stream = new MemoryStream(Encoding.UTF8.GetBytes(i.Content)); var message = await MimeMessage.LoadAsync(stream); html = message.HtmlBody ?? "<p>" + System.Net.WebUtility.HtmlEncode(message.TextBody ?? "") + "</p>"; subject = message.Subject ?? ""; }
            return Results.Ok(new { name = i.Name, subject, sourceHtml = html, safePreview = EmailDesign.CleanHtml(html) });
        });
        api.MapPost("/links/check", async (HtmlImportInput i, CancellationToken ct) =>
        { if (i.Content is null || i.Content.Length > 500000) throw new FormatException("HTML exceeds 500 KB."); return Results.Ok(await PublicLinkChecker.Check(i.Content, ct)); });
        api.MapPost("/maintenance/backup", async (Store db, StudioSettings settings) =>
        {
            if (!db.Database.IsSqlite()) throw new FormatException("Use PostgreSQL's server backup tools for this workspace.");
            string folder = Path.Combine(settings.DataDirectory, "backups"); Directory.CreateDirectory(folder); string name = "workspace-" + Guid.NewGuid().ToString("N") + ".db", path = Path.Combine(folder, name);
            await db.Database.OpenConnectionAsync(); using var target = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()); target.Open(); ((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(target);
            return Results.Ok(new { name, note = "Database backup created on this server. Back up attachments, captures, and the unsubscribe/encryption secret separately." });
        });
        api.MapPost("/maintenance/optimize", async (Store db) =>
        { if (db.Database.IsSqlite()) await db.Database.ExecuteSqlRawAsync("PRAGMA optimize;"); else await db.Database.ExecuteSqlRawAsync("ANALYZE;"); return Results.Ok(new { optimized = true }); });
    }
}
