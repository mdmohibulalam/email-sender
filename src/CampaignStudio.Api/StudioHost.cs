using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace CampaignStudio.Api;

public static class StudioHost
{
    public static WebApplication Build(string[] args, StudioSettings? supplied = null, string? webRoot = null, Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args, WebRootPath = webRoot ?? Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });
        var settings = supplied ?? StudioSettings.FromConfiguration(builder.Configuration);
        if (settings.ApiKey.Length < 32 || settings.TokenSecret.Length < 32)
            throw new InvalidOperationException("Set Studio__ApiKey and Studio__TokenSecret to distinct random values of at least 32 characters. See README.");
        if (settings.ApiKey == settings.TokenSecret) throw new InvalidOperationException("Use different API and unsubscribe secrets.");
        Directory.CreateDirectory(settings.DataDirectory);
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8 * 1024 * 1024);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<UnsubscribeTokens>();
        builder.Services.AddSingleton<CredentialVault>();
        builder.Services.AddSingleton<SignupTokens>();
        builder.Services.AddSingleton<ImageAssets>();
        builder.Services.AddSingleton<EngagementTracking>();
        builder.Services.AddSingleton<ExternalDesigner>();
        builder.Services.AddSingleton<ISmtpTransport, SmtpTransport>();
        builder.Services.AddSingleton<IInboxReader, Pop3Reader>();
        builder.Services.AddHttpClient<JotformClient>(c => { c.Timeout = TimeSpan.FromSeconds(15); c.MaxResponseContentBufferSize = 2_000_000; }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<WorkspaceLease>();
        if (!string.IsNullOrWhiteSpace(settings.Postgres))
        {
            builder.Services.AddDbContext<PostgresStore>(o => o.UseNpgsql(settings.Postgres));
            builder.Services.AddScoped<Store>(s => s.GetRequiredService<PostgresStore>());
        }
        else
        {
            builder.Services.AddDbContext<SqliteStore>(o => o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(settings.DataDirectory, "studio.db"), ForeignKeys = true }.ToString()));
            builder.Services.AddScoped<Store>(s => s.GetRequiredService<SqliteStore>());
        }
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 240, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        if (settings.StartWorker) { builder.Services.AddHostedService<DispatchWorker>(); builder.Services.AddHostedService<SignupWorker>(); builder.Services.AddHostedService<InboxWorker>(); }
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        try
        {
            app.Services.GetRequiredService<WorkspaceLease>();
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Store>();
            SchemaUpgrade.Apply(db, settings);
            if (settings.LiveReady && !db.Senders.Any())
            {
                db.Senders.Add(new SenderProfile { Name = "Configured SMTP", Host = settings.SmtpHost, Port = settings.SmtpPort, Username = settings.SmtpUser,
                    PasswordCipher = app.Services.GetRequiredService<CredentialVault>().Seal(settings.SmtpPassword), FromEmail = settings.FromEmail, FromName = settings.FromName }); db.SaveChanges();
            }
        }
        catch { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); throw; }
        app.UseRateLimiter();
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; frame-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'none'; form-action 'self'";
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.Headers.CacheControl = "no-store";
                string provided = ctx.Request.Headers.Authorization.ToString();
                var expected = SHA256.HashData(Encoding.UTF8.GetBytes("Bearer " + settings.ApiKey));
                if (!CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(Encoding.UTF8.GetBytes(provided))))
                { ctx.Response.StatusCode = 401; await ctx.Response.WriteAsJsonAsync(new { error = "Enter the workspace access key to connect." }); return; }
            }
            try { await next(); }
            catch (FormatException e) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = e.Message }); }
            catch (System.Text.Json.JsonException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = "Invalid JSON data." }); }
            catch (DbUpdateException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { error = "Data changed or this record already exists. Refresh and try again." }); }
            catch (BadHttpRequestException ex) { ctx.Response.StatusCode = ex.StatusCode; await ctx.Response.WriteAsJsonAsync(new { error = "Request exceeds its limit or is invalid." }); }
            catch (Exception) { ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { error = "Operation failed. Review the workspace settings and try again.", requestId = ctx.TraceIdentifier }); }
        });
        app.UseDefaultFiles(); app.UseStaticFiles();
        app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.2.0" }));
        var api = app.MapGroup("/api");
        api.MapGet("/settings", async (Store db) => new { version = "0.2.0", liveReady = settings.EnableLiveSending && settings.PublicLinksReady && await db.Senders.AnyAsync(x => x.VerifiedAt != null),
            liveSendingEnabled = settings.EnableLiveSending, publicLinksReady = settings.PublicLinksReady,
            fromEmail = settings.FromEmail, fromName = settings.FromName, storage = settings.Postgres is null ? "Local SQLite" : "PostgreSQL",
            execution = "This server owns its campaign queue", sendingMode = "Capture emails until live SMTP is configured" });
        Connections.Map(api);
        ExternalDesigner.Map(api);
        WorkspaceWidgets.Map(api);
        Inboxes.Map(api);
        JotformImport.Map(api);
        MarketingEndpoints.Map(api);
        SignupForms.Map(app, api);
        app.Services.GetRequiredService<ImageAssets>().Map(app, api);
        app.Services.GetRequiredService<EngagementTracking>().Map(app, api);
        api.MapGet("/overview", async (Store db) => new
        {
            contacts = await db.Contacts.CountAsync(), eligible = await db.Contacts.CountAsync(x => !x.Suppressed),
            suppressed = await db.Contacts.CountAsync(x => x.Suppressed), campaigns = await db.Campaigns.CountAsync(),
            captured = await db.Jobs.CountAsync(x => x.State == "Captured"), accepted = await db.Jobs.CountAsync(x => x.State == "Accepted")
        });
        api.MapGet("/contacts", async (Store db, string? search, string? list, bool? suppressed, int? page, string? sort) =>
        {
            if (page is < 1 or > 1000000 || search?.Length > 250 || list?.Length > 250) return Error("Invalid page or filter length.");
            var q = db.Contacts.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.Email.Contains(search) || x.FirstName.Contains(search) || x.LastName.Contains(search));
            if (!string.IsNullOrWhiteSpace(list)) q = q.Where(x => x.ListName == list);
            if (suppressed.HasValue) q = q.Where(x => x.Suppressed == suppressed);
            return Results.Ok(new { total = await q.CountAsync(), items = await ContactSorting.Apply(q, sort).Skip(Math.Max(0, (page ?? 1) - 1) * 100).Take(100).ToListAsync() });
        });
        api.MapGet("/lists", async (Store db) => await db.Contacts.Select(x => x.ListName).Distinct().OrderBy(x => x).ToListAsync());
        api.MapPost("/contacts", async (ContactInput input, Store db) =>
        {
            if (!input.ConsentConfirmed || string.IsNullOrWhiteSpace(input.ConsentSource)) return Error("Confirm permission and record where it came from.");
            if (string.IsNullOrWhiteSpace(input.Email) || !ContactRules.ValidEmail(ContactRules.Normalize(input.Email))) return Error("Enter a valid email address.");
            if (!ValidContactText(input.FirstName, input.LastName, input.Company, input.ListName, input.ConsentSource)) return Error("Contact fields must be at most 250 characters; list and consent source cannot be empty.");
            var email = ContactRules.Normalize(input.Email);
            if (await db.Contacts.AnyAsync(x => x.Email == email)) return Results.Conflict(new { error = "This email is already in your audience." });
            var c = new Contact { Email = email, FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), Company = input.Company.Trim(),
                ListName = input.ListName.Trim(), ConsentSource = input.ConsentSource.Trim() };
            db.Add(c); AudienceRules.Log(db, c.Id, "Subscribed", c.ConsentSource); await db.SaveChangesAsync(); return Results.Ok(c);
        });
        api.MapPost("/contacts/import", async (ImportInput input, Store db) =>
        {
            if (!input.ConsentConfirmed || string.IsNullOrWhiteSpace(input.ConsentSource)) return Error("Confirm permission and record the consent source.");
            if (input.Csv is null || Encoding.UTF8.GetByteCount(input.Csv) > 5_000_000) return Error("Import a CSV under 5 MB.");
            if (!ValidContactText("", "", "", input.ListName, input.ConsentSource) || input.EmailColumn is < 0 or > 99
                || input.FirstNameColumn is < -1 or > 99 || input.LastNameColumn is < -1 or > 99 || input.CompanyColumn is < -1 or > 99) return Error("Check your column mapping and list name.");
            var rows = Csv.Parse(input.Csv); if (rows.Count > 10001) return Error("Import at most 10,000 rows per batch in this pilot.");
            var candidates = rows.Skip(input.HasHeader ? 1 : 0).Select(r => input.EmailColumn < r.Length ? ContactRules.Normalize(r[input.EmailColumn]) : "").Distinct().ToArray();
            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var batch in candidates.Chunk(256)) foreach (var email in await db.Contacts.Where(x => batch.Contains(x.Email)).Select(x => x.Email).ToListAsync()) existing.Add(email);
            var issues = new List<ImportIssue>(); int added = 0, duplicate = 0;
            for (int i = input.HasHeader ? 1 : 0; i < rows.Count; i++)
            {
                string Get(int col) => col >= 0 && col < rows[i].Length ? rows[i][col].Trim() : "";
                var email = ContactRules.Normalize(Get(input.EmailColumn));
                if (!ContactRules.ValidEmail(email)) { issues.Add(new(i + 1, email, "Invalid email format")); continue; }
                if (existing.Contains(email)) { duplicate++; issues.Add(new(i + 1, email, "Duplicate (existing status preserved)")); continue; }
                if (!ValidContactText(Get(input.FirstNameColumn), Get(input.LastNameColumn), Get(input.CompanyColumn), input.ListName, input.ConsentSource))
                { issues.Add(new(i + 1, email, "Field exceeds 250 characters")); continue; }
                existing.Add(email); var importedContact = new Contact { Email = email, FirstName = Get(input.FirstNameColumn), LastName = Get(input.LastNameColumn),
                    Company = Get(input.CompanyColumn), ListName = input.ListName.Trim(), ConsentSource = input.ConsentSource.Trim() }; db.Contacts.Add(importedContact); AudienceRules.Log(db, importedContact.Id, "Subscribed", input.ConsentSource); added++;
            }
            await db.SaveChangesAsync(); return Results.Ok(new { added, duplicates = duplicate, rejected = issues.Count - duplicate, issues });
        });
        api.MapGet("/contacts/export", async (Store db) => CsvFile("contacts.csv", "Email,First name,Last name,Company,List,Status,Consent source\r\n" +
            string.Join("\r\n", (await db.Contacts.OrderBy(x => x.Email).ToListAsync()).Select(x => string.Join(',',
                new[] { x.Email, x.FirstName, x.LastName, x.Company, x.ListName, x.Suppressed ? "Suppressed" : "Subscribed", x.ConsentSource }.Select(ContactRules.CsvCell))))));
        api.MapPost("/contacts/{id:guid}/suppress", async (Guid id, SuppressionInput input, Store db) =>
        {
            if (input.Reason is null || input.Reason.Length > 250) return Error("Reason must be at most 250 characters.");
            var c = await db.Contacts.FindAsync(id); if (c is null) return Results.NotFound();
            await Suppress(db, c, input.Reason); return Results.Ok(c);
        });
        api.MapGet("/campaigns", async (Store db) => await db.Campaigns.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync());
        api.MapPost("/preview", async (CampaignInput input, Guid? contactId, Store db) =>
        {
            var error = ValidateCampaign(input); if (error is not null) return Error(error);
            var c = new Campaign(); Apply(c, input);
            var contact = contactId.HasValue ? await db.Contacts.FindAsync(contactId.Value) : null;
            return Results.Content(contact is null ? EmailRenderer.Html(c) : EmailRenderer.Html(c, contact.FirstName, contact.LastName, contact.Email), "text/html");
        });
        api.MapPost("/campaigns", async (CampaignInput input, Store db) =>
        {
            var error = ValidateCampaign(input); if (error is not null) return Error(error);
            var c = new Campaign(); Apply(c, input); db.Add(c); await db.SaveChangesAsync(); return Results.Ok(c);
        });
        api.MapPut("/campaigns/{id:guid}", async (Guid id, CampaignInput input, Store db) =>
        {
            var error = ValidateCampaign(input); if (error is not null) return Error(error);
            await using var tx = await db.Database.BeginTransactionAsync();
            // Acquire a row write lock before reading; scheduling and edits serialize in both databases.
            var locked = await db.Campaigns.Where(x => x.Id == id && x.State == "Draft").ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Draft"));
            if (locked == 0) return Results.Conflict(new { error = "Only drafts can be edited. Duplicate this campaign to change it." });
            var c = await db.Campaigns.FindAsync(id);
            if (c!.Revision != input.ExpectedRevision) return Results.Conflict(new { error = "This draft changed in another editor. Reload or save a separate copy." });
            Apply(c, input); c.Revision++; await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(c);
        });
        api.MapPost("/campaigns/{id:guid}/duplicate", async (Guid id, Store db) =>
        {
            var old = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); if (old is null) return Results.NotFound();
            old.Id = Guid.NewGuid(); old.Name = old.Name.Length > 240 ? old.Name[..240] + " copy" : old.Name + " copy";
            old.State = "Draft"; old.ScheduledAt = null; old.Revision = 0; old.SenderId = null; old.Issue = ""; old.Mode = "Capture"; old.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            db.Add(old); await db.SaveChangesAsync(); return Results.Ok(old);
        });
        api.MapGet("/campaigns/{id:guid}/preview", async (Guid id, Store db) =>
        { var c = await db.Campaigns.FindAsync(id); return c is null ? Results.NotFound() : Results.Content(EmailRenderer.Html(c), "text/html"); });
        api.MapGet("/campaigns/{id:guid}/html", async (Guid id, Store db) =>
        { var c = await db.Campaigns.FindAsync(id); return c is null ? Results.NotFound() : Results.File(Encoding.UTF8.GetBytes(EmailRenderer.Html(c)), "text/html", "email.html"); });
        api.MapPost("/campaigns/{id:guid}/schedule", async (Guid id, ScheduleInput input, Store db, TimeProvider clock) =>
        {
            if (input.Mode is not ("Capture" or "Smtp")) return Error("Choose Capture or Smtp.");
            SenderProfile? sender = null;
            if (input.Mode == "Smtp")
            {
                sender = input.SenderId.HasValue ? await db.Senders.FindAsync(input.SenderId.Value) : null;
                if (!settings.EnableLiveSending || !settings.PublicLinksReady || !input.ConfirmLiveSend || sender?.VerifiedAt is null) return Error("Select a tested SMTP connection, configure HTTPS links, enable live sending, and confirm sending.");
            }
            var now = clock.GetUtcNow().ToUnixTimeSeconds();
            if (input.ScheduledAt < now - 60 || input.ScheduledAt > now + 366 * 86400L) return Error("Choose a time between now and one year from now.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var locked = await db.Campaigns.Where(x => x.Id == id && x.State == "Draft").ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Preparing"));
            if (locked == 0) return Results.Conflict(new { error = "This campaign has already been scheduled or does not exist." });
            var c = await db.Campaigns.FindAsync(id);
            if (string.IsNullOrWhiteSpace(c!.Subject)) return Error("Add a subject before scheduling.");
            var audience = await (await AudienceRules.CampaignAudience(db, c)).ToListAsync();
            if (audience.Count == 0) return Error("This list has no subscribed contacts.");
            await MarketingEndpoints.ValidateAssets(db, settings, c);
            c.SenderId = sender?.Id; c.SenderEmail = sender?.FromEmail ?? "preview@example.com"; c.SenderName = sender?.FromName ?? "Campaign Studio"; c.ReplyTo = sender?.ReplyTo ?? ""; c.ReturnPath = sender?.ReturnPath ?? "";
            foreach (var contact in audience) db.Jobs.Add(new RecipientJob { CampaignId = id, ContactId = contact.Id, Email = contact.Email,
                FirstName = contact.FirstName, LastName = contact.LastName, MessageId = $"{id:N}.{contact.Id:N}@{(sender is null ? "campaign-studio.local" : sender.FromEmail.Split('@').Last())}" });
            c.State = "Scheduled"; c.Mode = input.Mode; c.ScheduledAt = Math.Max(now, input.ScheduledAt);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { campaign = c, recipients = audience.Count });
        });
        api.MapPost("/campaigns/{id:guid}/pause", async (Guid id, Store db) => await ChangeState(db, id, new[] { "Scheduled", "Running" }, "Paused"));
        api.MapPost("/campaigns/{id:guid}/resume", async (Guid id, Store db) =>
        {
            var c = await db.Campaigns.FindAsync(id); if (c is null) return Results.NotFound();
            if (c.Mode == "Smtp" && (!settings.EnableLiveSending || !settings.PublicLinksReady || !await db.Senders.AnyAsync(x => x.Id == c.SenderId && x.VerifiedAt != null))) return Error("Retest the SMTP connection before resuming.");
            return await ChangeState(db, id, new[] { "Paused" }, "Scheduled");
        });
        api.MapPost("/campaigns/{id:guid}/cancel", async (Guid id, Store db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var r = await ChangeState(db, id, new[] { "Scheduled", "Running", "Paused" }, "Cancelled");
            await db.Jobs.Where(x => x.CampaignId == id && (x.State == "Pending" || x.State == "Deferred")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Cancelled"));
            await tx.CommitAsync(); return r;
        });
        api.MapGet("/campaigns/{id:guid}/report", async (Guid id, Store db) =>
        {
            if (!await db.Campaigns.AnyAsync(x => x.Id == id)) return Results.NotFound();
            return Results.Ok(new { counts = await db.Jobs.Where(x => x.CampaignId == id).GroupBy(x => x.State).Select(g => new { state = g.Key, count = g.Count() }).ToListAsync(),
                jobs = await db.Jobs.Where(x => x.CampaignId == id).OrderBy(x => x.Email).Take(200).ToListAsync(), deliveryEventsAvailable = false });
        });
        api.MapGet("/campaigns/{id:guid}/report/export", async (Guid id, Store db) => CsvFile("campaign-report.csv", "Email,State,Detail,Message ID\r\n" +
            string.Join("\r\n", (await db.Jobs.Where(x => x.CampaignId == id).OrderBy(x => x.Email).ToListAsync()).Select(x =>
                string.Join(',', new[] { x.Email, x.State, x.Detail, x.MessageId }.Select(ContactRules.CsvCell))))));
        api.MapGet("/jobs/{id:guid}/message", async (Guid id, Store db) =>
        {
            var job = await db.Jobs.FindAsync(id); var path = Path.Combine(settings.DataDirectory, "captures", id + ".eml");
            return job?.State == "Captured" && File.Exists(path) ? Results.File(await File.ReadAllBytesAsync(path), "message/rfc822", "test-email.eml") : Results.NotFound();
        });
        app.MapGet("/u/{token}", (string token, UnsubscribeTokens tokens) =>
            tokens.TryRead(token, out _) ? Results.Content("<!doctype html><html><head><meta charset='utf-8'><title>Unsubscribe</title></head><body style='font:18px Arial;padding:40px'><h1>Email preferences</h1><p>Stop future marketing emails from this workspace.</p><form method='post'><button type='submit'>Unsubscribe</button></form></body></html>", "text/html") : Results.NotFound());
        app.MapPost("/u/{token}", async (string token, UnsubscribeTokens tokens, Store db) =>
        {
            if (!tokens.TryRead(token, out var id)) return Results.NotFound();
            var c = await db.Contacts.FindAsync(id); if (c is null) return Results.NotFound();
            await Suppress(db, c, "Unsubscribed");
            return Results.Content("<!doctype html><html><body style='font:18px Arial;padding:40px'><h1>You have been unsubscribed.</h1><p>No further marketing emails will be started for this address.</p></body></html>", "text/html");
        });
        return app;
    }

    private static bool ValidContactText(params string[] fields) => fields.All(x => x is not null && x.Length <= 250)
        && !string.IsNullOrWhiteSpace(fields[3]) && !string.IsNullOrWhiteSpace(fields[4]);
    private static string? ValidateCampaign(CampaignInput i)
    {
        var fields = new[] { i.Name, i.Subject, i.Preheader, i.Heading, i.Body, i.ButtonLabel, i.ButtonUrl, i.Footer, i.Accent, i.ListName };
        if (fields.Any(x => x is null)) return "Fill in all campaign fields.";
        if (string.IsNullOrWhiteSpace(i.Name) || string.IsNullOrWhiteSpace(i.ListName)) return "Add a campaign name and audience list.";
        if (i.Name.Length > 250 || i.Subject.Length > 250 || i.Preheader.Length > 250 || i.Heading.Length > 500 || i.Body.Length > 20000
            || i.ButtonLabel.Length > 150 || i.ButtonUrl.Length > 2000 || i.Footer.Length > 2000 || i.ListName.Length > 250) return "A campaign field exceeds its size limit.";
        if (i.Subject.Contains('\r') || i.Subject.Contains('\n')) return "The subject must be a single line.";
        if (!EmailRenderer.ValidAccent(i.Accent)) return "Choose a valid accent color.";
        if (string.IsNullOrWhiteSpace(i.BlocksJson) && string.IsNullOrWhiteSpace(i.SourceHtml) && i.ButtonLabel.Length > 0 && !EmailRenderer.ValidUrl(i.ButtonUrl)) return "Button links must start with https:// or http://.";
        if (i.BlocksJson is null || i.SourceHtml is null || i.AttachmentIdsJson is null || i.CalendarJson is null || i.UtmCampaign is null || i.HistoryMode is not ("All" or "Sent" or "NotSent") || i.UtmCampaign.Length > 250) return "Check advanced campaign settings.";
        ScriptTags.Validate(i.TagsJson);
        if (i.BlocksJson.Length > 0) EmailDesign.Blocks(i.BlocksJson);
        if (i.SourceHtml.Length > 0) EmailDesign.CleanHtml(i.SourceHtml);
        if (i.AttachmentIdsJson.Length > 5000 || EmailDesign.Read<Guid[]>(i.AttachmentIdsJson).Length > 20) return "Attach at most 20 files.";
        if (i.CalendarJson.Length > 0) EmailDesign.Calendar(EmailDesign.Read<CalendarEvent>(i.CalendarJson), Guid.Empty);
        return null;
    }
    private static void Apply(Campaign c, CampaignInput i)
    {
        c.TrackEngagement = i.TrackEngagement; c.TagsJson = i.TagsJson; c.BlocksJson = i.BlocksJson; c.SourceHtml = i.SourceHtml; c.SegmentId = i.SegmentId; c.HistoryCampaignId = i.HistoryCampaignId; c.HistoryMode = i.HistoryMode;
        c.AttachmentIdsJson = i.AttachmentIdsJson; c.CalendarJson = i.CalendarJson; c.UtmCampaign = i.UtmCampaign;
        c.Name = i.Name.Trim(); c.Subject = i.Subject; c.Preheader = i.Preheader; c.Heading = i.Heading; c.Body = i.Body;
        c.ButtonLabel = i.ButtonLabel; c.ButtonUrl = i.ButtonUrl; c.Footer = i.Footer; c.Accent = i.Accent; c.ListName = i.ListName.Trim();
    }
    private static IResult Error(string message) => Results.BadRequest(new { error = message });
    private static IResult CsvFile(string name, string csv) => Results.File(Encoding.UTF8.GetBytes("\uFEFF" + csv), "text/csv; charset=utf-8", name);
    private static async Task<IResult> ChangeState(Store db, Guid id, string[] from, string to)
    {
        var n = await db.Campaigns.Where(x => x.Id == id && from.Contains(x.State)).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, to));
        return n == 0 ? Results.Conflict(new { error = "This action is unavailable in the current campaign state." }) : Results.Ok(new { state = to });
    }
    private static async Task Suppress(Store db, Contact c, string reason)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        AudienceRules.Log(db, c.Id, "Unsubscribed", reason);
        c.Suppressed = true; c.SuppressionReason = reason; await db.SaveChangesAsync();
        await db.Jobs.Where(x => x.ContactId == c.Id && (x.State == "Pending" || x.State == "Deferred")).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Suppressed"));
        await tx.CommitAsync();
    }
}
