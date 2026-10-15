using CampaignStudio.Api;
using CampaignStudio.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

int passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label); passed++;
}
var parsed = Csv.Parse("\uFEFFEmail,Name\r\n\"a@example.com\",\"Ada, A\"\r\n\"b@example.com\",\"Multi\nline \"\"name\"\"\"\r\n");
Check(parsed.Count == 3 && parsed[1][1] == "Ada, A" && parsed[2][1] == "Multi\nline \"name\"", "RFC CSV: BOM, quoted comma, multiline, escaped quotes");
try { Csv.Parse("\"unfinished"); throw new Exception("CSV accepted malformed input"); } catch (FormatException) { Check(true, "Malformed CSV is rejected"); }
Check(ContactRules.CsvCell("=SUM(1,2)").StartsWith("\"'="), "Spreadsheet formula injection is escaped");
Check(ContactRules.Normalize(" User+tag@Example.com ") == "user+tag@example.com", "Normalize case/space while preserving plus-addresses");
Check(!ContactRules.ValidEmail("user\r\n@example.com") && ContactRules.ValidEmail("user+tag@example.com"), "Email syntax rejects whitespace/header injection");
var template = new Campaign { Heading = "Hello {{firstName}}", Body = "<script>alert(1)</script>", ButtonUrl = "javascript:alert(1)" };
var html = EmailRenderer.Html(template, "<img src=x>");
Check(!html.Contains("<script>") && !html.Contains("javascript:") && html.Contains("&lt;img"), "Email rendering escapes content and rejects active links");
Check(EmailRenderer.Merge("Hi {{firstName}}", "", "", "x@example.com") == "Hi there", "Missing first name has a readable fallback");
var dir = Path.Combine(Path.GetTempPath(), "campaign-studio-tests-" + Guid.NewGuid());
var settings = new StudioSettings { ApiKey = new string('a', 64), TokenSecret = new string('b', 64), DataDirectory = dir, StartWorker = false, LeaseSeconds = 5 };
var host = StudioHost.Build(["--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default", "Warning"], settings,
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/CampaignStudio.Api/wwwroot")));
await host.StartAsync();
string url = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client = new HttpClient { BaseAddress = new Uri(url) };
using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
client.DefaultRequestHeaders.Authorization = new("Bearer", settings.ApiKey);
async Task<JsonElement> Json(HttpResponseMessage response)
{ response.EnsureSuccessStatusCode(); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone(); }
CampaignInput MakeCampaign(string name = "Test", string list = "Main audience") => new(name, "Hello {{firstName}}", "Preview", "Hi {{firstName}}", "A message", "Open", "https://example.com", "Example Inc · Test address", "#176b56", list);
async Task<Guid> CreateCampaign(string name = "Test", string list = "Main audience") => (await Json(await client.PostAsJsonAsync("/api/campaigns", MakeCampaign(name, list)))).GetProperty("id").GetGuid();
async Task Tick()
{
    var worker = new DispatchWorker(host.Services.GetRequiredService<IServiceScopeFactory>(), settings, host.Services.GetRequiredService<ILogger<DispatchWorker>>());
    await worker.Tick();
}
try
{
    Check((await anonymous.GetAsync("/api/contacts")).StatusCode == HttpStatusCode.Unauthorized, "Every private API rejects missing authentication");
    Check((await client.PostAsJsonAsync("/api/contacts", new ContactInput("no@example.com"))).StatusCode == HttpStatusCode.BadRequest, "Contacts require permission and source");
    var import = new ImportInput("Email,First,Last,Company\nADA@example.com,Ada,Lovelace,Example\nada@example.com,Ada,Duplicate,Example\nbad-address,No,Email,Example\nlin@example.com,Lin,Chen,Example", ConsentSource:"Test signup", ConsentConfirmed:true);
    var imported = await Json(await client.PostAsJsonAsync("/api/contacts/import", import));
    Check(imported.GetProperty("added").GetInt32() == 2 && imported.GetProperty("duplicates").GetInt32() == 1 && imported.GetProperty("rejected").GetInt32() == 1, "Import atomically classifies valid, duplicate, and rejected rows");
    Check((await client.PostAsJsonAsync("/api/contacts/import", import with { EmailColumn = -1 })).StatusCode == HttpStatusCode.BadRequest, "Invalid column mappings are rejected");
    var contacts = (await Json(await client.GetAsync("/api/contacts"))).GetProperty("items");
    Guid ada = contacts.EnumerateArray().First(x => x.GetProperty("email").GetString() == "ada@example.com").GetProperty("id").GetGuid();
    Guid lin = contacts.EnumerateArray().First(x => x.GetProperty("email").GetString() == "lin@example.com").GetProperty("id").GetGuid();
    Guid c = await CreateCampaign();
    var scheduled = await Json(await client.PostAsJsonAsync($"/api/campaigns/{c}/schedule", new ScheduleInput(DateTimeOffset.UtcNow.ToUnixTimeSeconds())));
    Check(scheduled.GetProperty("recipients").GetInt32() == 2, "Scheduling freezes eligible recipients");
    Check((await client.PostAsJsonAsync($"/api/campaigns/{c}/schedule", new ScheduleInput(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))).StatusCode == HttpStatusCode.Conflict, "Scheduling twice cannot create duplicate jobs");
    Check((await client.PutAsJsonAsync($"/api/campaigns/{c}", MakeCampaign("Changed"))).StatusCode == HttpStatusCode.Conflict, "Scheduled message content is immutable");
    await Json(await client.PostAsync($"/api/campaigns/{c}/pause", null));
    await Tick();
    using (var scope = host.Services.CreateScope()) Check(await scope.ServiceProvider.GetRequiredService<Store>().Jobs.CountAsync(x => x.State == "Captured") == 0, "Paused campaigns do not dispatch");
    var tokens = host.Services.GetRequiredService<UnsubscribeTokens>();
    string token = tokens.Create(ada);
    Check(!tokens.TryRead(token[..^1] + (token[^1] == 'a' ? "b" : "a"), out _), "Unsubscribe tokens reject tampering");
    Check((await anonymous.GetAsync("/u/" + token)).IsSuccessStatusCode, "Unsubscribe confirmation is public");
    using (var scope = host.Services.CreateScope()) Check(!(await scope.ServiceProvider.GetRequiredService<Store>().Contacts.FindAsync(ada))!.Suppressed, "GET/scanner visits never unsubscribe");
    Check((await anonymous.PostAsync("/u/" + token, new FormUrlEncodedContent(new Dictionary<string,string>{{"List-Unsubscribe","One-Click"}}))).IsSuccessStatusCode, "One-click POST confirms suppression");
    await Json(await client.PostAsJsonAsync("/api/contacts/import", import));
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Store>();
        Check((await db.Contacts.FindAsync(ada))!.Suppressed, "Reimport preserves suppression");
        Check(await db.Jobs.AnyAsync(x => x.ContactId == ada && x.State == "Suppressed"), "Unsubscribe blocks previously queued jobs");
    }
    await Json(await client.PostAsync($"/api/campaigns/{c}/resume", null));
    await Tick(); await Tick();
    Guid capturedId;
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Store>();
        var job = await db.Jobs.SingleAsync(x => x.ContactId == lin && x.CampaignId == c); capturedId = job.Id;
        Check(job.State == "Captured" && File.Exists(Path.Combine(dir, "captures", job.Id + ".eml")), "Capture creates an EML without sending mail");
        Check((await db.Campaigns.FindAsync(c))!.State == "Completed", "Terminal jobs complete their campaign");
    }
    var eml = await client.GetStringAsync($"/api/jobs/{capturedId}/message");
    Check(eml.Contains("List-Unsubscribe-Post") && eml.Contains("Lin") && eml.Contains("multipart/alternative"), "Captured MIME includes personalization, text/HTML, and unsubscribe headers");
    var report = await Json(await client.GetAsync($"/api/campaigns/{c}/report"));
    Check(!report.GetProperty("deliveryEventsAvailable").GetBoolean(), "Reports do not invent delivery events");
    var duplicate = await Json(await client.PostAsync($"/api/campaigns/{c}/duplicate", null));
    Guid copy = duplicate.GetProperty("id").GetGuid();
    Check(duplicate.GetProperty("state").GetString() == "Draft", "Duplicate creates an editable draft");
    await Json(await client.PostAsJsonAsync($"/api/campaigns/{copy}/schedule", new ScheduleInput(DateTimeOffset.UtcNow.ToUnixTimeSeconds())));
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Store>();
        var interruptedCampaign = (await db.Campaigns.FindAsync(copy))!; interruptedCampaign.Mode = "Smtp";
        var job = await db.Jobs.SingleAsync(x => x.CampaignId == copy); job.State = "Sending"; job.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(); await db.SaveChangesAsync();
    }
    await Tick();
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Store>();
        Check((await db.Jobs.SingleAsync(x => x.CampaignId == copy)).State == "Unknown", "Interrupted sends become Unknown rather than being resent");
        Check((await db.Campaigns.FindAsync(copy))!.State == "NeedsReview", "Unknown results require review");
    }
    Guid future = await CreateCampaign("Tomorrow");
    await Json(await client.PostAsJsonAsync($"/api/campaigns/{future}/schedule", new ScheduleInput(DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds())));
    await Tick();
    using (var scope = host.Services.CreateScope()) Check(await scope.ServiceProvider.GetRequiredService<Store>().Jobs.AnyAsync(x => x.CampaignId == future && x.State == "Pending"), "Future schedules are not dispatched early");
    await Json(await client.PostAsync($"/api/campaigns/{future}/cancel", null));
    using (var scope = host.Services.CreateScope()) Check(await scope.ServiceProvider.GetRequiredService<Store>().Jobs.AnyAsync(x => x.CampaignId == future && x.State == "Cancelled"), "Cancel marks all remaining recipients");
    Guid live = await CreateCampaign("Live unavailable");
    Check((await client.PostAsJsonAsync($"/api/campaigns/{live}/schedule", new ScheduleInput(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "Smtp", true))).StatusCode == HttpStatusCode.BadRequest, "Live sending is disabled until configuration is ready");
    Guid noAudience = await CreateCampaign("No audience", "Empty list");
    Check((await client.PostAsJsonAsync($"/api/campaigns/{noAudience}/schedule", new ScheduleInput(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))).StatusCode == HttpStatusCode.BadRequest, "Empty audiences cannot schedule");
    using (var scope = host.Services.CreateScope()) Check((await scope.ServiceProvider.GetRequiredService<Store>().Campaigns.FindAsync(noAudience))!.State == "Draft", "Failed schedule transactions restore the draft state");
    Check((await client.GetStringAsync("/api/contacts/export")).Contains("Suppressed"), "CSV export includes subscription status");
    using (var scope = host.Services.CreateScope()) Check(await scope.ServiceProvider.GetRequiredService<Store>().Contacts.CountAsync() == 2, "Data survives new database contexts");
    await host.StopAsync(); await host.DisposeAsync();
    host = StudioHost.Build(["--urls", url, "--Logging:LogLevel:Default", "Warning"], settings,
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/CampaignStudio.Api/wwwroot")));
    await host.StartAsync();
    var restartedContacts = await Json(await client.GetAsync("/api/contacts"));
    Check(restartedContacts.GetProperty("total").GetInt32() == 2 && restartedContacts.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("suppressed").GetBoolean()), "Contacts and suppression survive a server restart");
    var restartedCampaigns = await Json(await client.GetAsync("/api/campaigns"));
    Check(restartedCampaigns.EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == noAudience && x.GetProperty("state").GetString() == "Draft"), "Drafts survive a server restart");
    Check((await Json(await client.GetAsync($"/api/campaigns/{copy}/report"))).GetProperty("jobs")[0].GetProperty("state").GetString() == "Unknown", "Interrupted-job review state survives a server restart");
    await ExtendedChecks.Run(Check);
    Console.WriteLine($"\n{passed} checks passed. No real emails sent.");
}
finally
{
    await host.StopAsync(); await host.DisposeAsync();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    Directory.Delete(dir, true);
}
