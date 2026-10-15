using CampaignStudio.Api;
using CampaignStudio.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MimeKit;

public static class ExtendedChecks
{
    public sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; public void Advance(int seconds) => Now = Now.AddSeconds(seconds); }
    public sealed class Transport : ISmtpTransport
    {
        public SmtpOutcome Outcome = new("Accepted", "Sandbox accepted");
        public int Sends;
        public Func<Task>? BeforeGate;
        public Task<SmtpOutcome> Test(SenderProfile s, string p, CancellationToken ct) => Task.FromResult(new SmtpOutcome("Verified", "Sandbox credentials verified"));
        public async Task<SmtpOutcome> Send(SenderProfile s, string p, MimeMessage message, Func<Task<bool>> gate, CancellationToken ct)
        { if (BeforeGate is not null) await BeforeGate(); if (!await gate()) return new("Stopped", "Suppressed after authentication"); Sends++; return Outcome; }
    }
    public static async Task Run(Action<bool,string> check)
    {
        var dir = Path.Combine(Path.GetTempPath(), "studio-extended-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        var settings = new StudioSettings { DataDirectory = dir, ApiKey = new string('x',64), TokenSecret = new string('z',64), StartWorker = false, EnableLiveSending = true, PublicBaseUrl = "https://studio.example", RetryBaseSeconds = 2 };
        var vault = new CredentialVault(settings); var encrypted = vault.Seal("private-password");
        check(encrypted != vault.Seal("private-password") && vault.Open(encrypted) == "private-password", "Credentials use fresh nonces and authenticated encryption");
        try { new CredentialVault(new StudioSettings { TokenSecret = new string('w',64) }).Open(encrypted); throw new Exception("Wrong key accepted"); } catch (System.Security.Cryptography.CryptographicException) { check(true, "Wrong encryption secret cannot decrypt credentials"); }
        string cleaned = EmailDesign.CleanHtml("<script>alert(1)</script><img src='https://example.com/a.png' onerror='alert(1)'><form action='https://example.com'><input></form><p>Safe</p>");
        check(!cleaned.Contains("<script") && !cleaned.Contains("onerror") && !cleaned.Contains("<form") && cleaned.Contains("Safe"), "Imported HTML is parsed and sanitized before rendering");
        try { EmailDesign.Blocks("[{\"type\":\"image\",\"url\":\"javascript:alert(1)\"}]"); throw new Exception("Active image accepted"); } catch (FormatException) { check(true, "Block image URLs reject active schemes"); }
        var calendar = EmailDesign.Calendar(new("Meeting\nBEGIN:VEVENT", new string('অ',100), "Dhaka", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), Guid.NewGuid());
        check(calendar.Contains("SUMMARY:Meeting\\nBEGIN:VEVENT") && calendar.Split("\r\n").All(x => System.Text.Encoding.UTF8.GetByteCount(x)<=75), "Calendar event escapes injected lines and folds UTF-8 correctly");
        check(!PublicLinkChecker.PublicAddress(IPAddress.Parse("127.0.0.1")) && !PublicLinkChecker.PublicAddress(IPAddress.Parse("::ffff:10.0.0.1")) && PublicLinkChecker.PublicAddress(IPAddress.Parse("8.8.8.8")), "Link checker blocks loopback and mapped private addresses");
        await LegacyUpgrade(check);
        var clock = new Clock(); var transport = new Transport();
        var host = StudioHost.Build(["--urls","http://127.0.0.1:0","--Logging:LogLevel:Default","Warning"], settings, configure: services => { services.AddSingleton<TimeProvider>(clock); services.AddSingleton<ISmtpTransport>(transport); });
        await host.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer",settings.ApiKey);
        async Task<JsonElement> Read(HttpResponseMessage response) { string content = await response.Content.ReadAsStringAsync(); if(!response.IsSuccessStatusCode)throw new Exception(response.StatusCode+" "+content);return JsonDocument.Parse(content).RootElement.Clone(); }
        async Task<JsonElement> Post(string path, object body) => await Read(await client.PostAsJsonAsync("/api"+path,body));
        async Task Tick() => await new DispatchWorker(host.Services.GetRequiredService<IServiceScopeFactory>(),settings,host.Services.GetRequiredService<ILogger<DispatchWorker>>()).Tick();
        async Task<RecipientJob> Job(Guid id) { using var scope=host.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<Store>().Jobs.AsNoTracking().SingleAsync(x=>x.CampaignId==id); }
        CampaignInput Draft(string name, string list="Tests") => new(name,"Hello {{firstName}}","Preview","Hello","Body","Open","https://example.com","Example address","#176b56",list);
        async Task<Guid> Create(CampaignInput draft) => (await Post("/campaigns",draft)).GetProperty("id").GetGuid();
        try
        {
            check((await client.GetAsync("/api/contacts?page=2147483647")).StatusCode==HttpStatusCode.BadRequest,"Pagination rejects overflow-sized values");
            var profile=await Post("/connections",new SenderInput("Sandbox","localhost",587,"sandbox","private-password","sender@example.com","Studio"));Guid sender=profile.GetProperty("id").GetGuid();
            check(profile.GetProperty("hasPassword").GetBoolean()&&!profile.ToString().Contains("private-password")&&!profile.TryGetProperty("passwordCipher",out _),"Connection API never exposes credentials");
            using(var scope=host.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<Store>();check((await db.Senders.FindAsync(sender))!.PasswordCipher.StartsWith("v1:"),"SMTP password is encrypted in the database");}
            await Post("/connections/"+sender+"/test",new{});
            foreach(var email in new[]{"one@example.com","two@example.com","blocked@blocked.example"}) await Post("/contacts",new ContactInput(email,"Ada",Company:email.StartsWith("one")?"Team A":"Team B",ListName:"Tests",ConsentSource:"Test signup",ConsentConfirmed:true));
            var segment=await Post("/segments",new SegmentInput("Team A","[{\"field\":\"company\",\"operator\":\"equals\",\"value\":\"Team A\"}]"));Guid segmentId=segment.GetProperty("id").GetGuid();
            var preview=await Read(await client.GetAsync("/api/segments/"+segmentId+"/preview"));check(preview.GetProperty("total").GetInt32()==1,"Saved audience rules execute against the database");
            await Post("/exclusions",new ExclusionInput("blocked.example","Domain"));
            Guid targeted=await Create(Draft("Targeted") with {SegmentId=segmentId});var result=await Post("/campaigns/"+targeted+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds()));check(result.GetProperty("recipients").GetInt32()==1,"Scheduling freezes list and segment intersections");
            await Tick();await Tick();
            Guid audience=await Create(Draft("Exclude domain"));result=await Read(await client.GetAsync("/api/campaigns/"+audience+"/audience"));check(result.GetProperty("total").GetInt32()==2,"Domain exclusions apply to campaign eligibility");
            var changed=Draft("Saved once");await Read(await client.PutAsJsonAsync("/api/campaigns/"+audience,changed));check((await client.PutAsJsonAsync("/api/campaigns/"+audience,changed)).StatusCode==HttpStatusCode.Conflict,"Stale draft revisions cannot overwrite a newer save");
            var upload=await Post("/attachments",new AttachmentInput("invoice.txt",Convert.ToBase64String("Invoice data"u8.ToArray()),"one@example.com"));var asset=upload.GetProperty("id").GetGuid();
            var design=Draft("Attachments") with {SegmentId=segmentId,BlocksJson="[{\"type\":\"heading\",\"content\":\"Hello {{firstName}}\"},{\"type\":\"text\",\"content\":\"Line one\\nLine two\"}]",AttachmentIdsJson=JsonSerializer.Serialize(new[]{asset}),CalendarJson=JsonSerializer.Serialize(new CalendarEvent("Meeting","Discussion","Dhaka",clock.Now,clock.Now.AddHours(1)),EmailDesign.Json)};
            Guid attached=await Create(design);await Post("/campaigns/"+attached+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds()));await Tick();await Tick();
            var captured=await Job(attached);using(var stream=new MemoryStream(await client.GetByteArrayAsync("/api/jobs/"+captured.Id+"/message"))){var message=await MimeMessage.LoadAsync(stream);check(message.Attachments.Count()==2&&(message.HtmlBody??"").Contains("Hello Ada")&&(message.HtmlBody??"").Contains("<br>"),"Capture contains personalized blocks, selected attachment, and valid calendar MIME");}
            Guid retry=await Create(Draft("Safe retry") with {SegmentId=segmentId});await Post("/campaigns/"+retry+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds(),"Smtp",true,sender));transport.Outcome=new("Transient","450 temporary");await Tick();var job=await Job(retry);check(job.State=="Deferred"&&job.Attempts==1&&job.RetryCount==1,"Temporary SMTP rejection persists a deferred attempt");int before=transport.Sends;await Tick();check(transport.Sends==before,"Deferred messages never retry early");clock.Advance(3);transport.Outcome=new("Accepted","Sandbox accepted");await Tick();await Tick();job=await Job(retry);check(job.State=="Accepted"&&job.Attempts==2,"Due retry resumes the same job without duplicating recipients");
            var attempts=await Read(await client.GetAsync("/api/jobs/"+job.Id+"/attempts"));check(attempts.GetArrayLength()==2,"Attempt history survives successful retry");
            Guid followup=await Create(Draft("Follow-up") with {HistoryCampaignId=retry,HistoryMode="Sent"});result=await Read(await client.GetAsync("/api/campaigns/"+followup+"/audience"));check(result.GetProperty("total").GetInt32()==1,"Follow-ups select prior SMTP-accepted contacts");
            Guid fresh=await Create(Draft("New only") with {HistoryCampaignId=retry,HistoryMode="NotSent"});result=await Read(await client.GetAsync("/api/campaigns/"+fresh+"/audience"));check(result.GetProperty("total").GetInt32()==1,"New-contact targeting excludes accepted recipients");
            clock.Advance(3);Guid uncertain=await Create(Draft("Unknown hold") with {SegmentId=segmentId});await Post("/campaigns/"+uncertain+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds(),"Smtp",true,sender));transport.Outcome=new("Unknown","Sandbox uncertain");await Tick();check((await Job(uncertain)).State=="Unknown","Uncertain SMTP result is held as Unknown");before=transport.Sends;clock.Advance(100);await Tick();check(transport.Sends==before,"Unknown messages are never retried automatically");check((await client.PostAsJsonAsync("/api/campaigns/"+uncertain+"/retry-failed",new RetryInput(true))).StatusCode==HttpStatusCode.BadRequest,"Manual failed-only retry cannot resend Unknown recipients");
            Guid auth=await Create(Draft("Broken auth") with {SegmentId=segmentId});await Post("/campaigns/"+auth+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds(),"Smtp",true,sender));transport.Outcome=new("ConnectionBlocked","Sandbox authentication failure");await Tick();using(var scope=host.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<Store>();check((await db.Campaigns.FindAsync(auth))!.State=="Paused"&&(await db.Senders.FindAsync(sender))!.VerifiedAt is null,"Authentication failure pauses sending and invalidates connection verification");}
            check((await client.PostAsync("/api/campaigns/"+auth+"/resume",null)).StatusCode==HttpStatusCode.BadRequest,"Broken connection cannot resume without a successful retest");await client.PostAsync("/api/campaigns/"+auth+"/cancel",null);
            await Post("/connections/"+sender+"/test",new{});clock.Advance(3);Guid race=await Create(Draft("Unsubscribe race") with {SegmentId=segmentId});await Post("/campaigns/"+race+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds(),"Smtp",true,sender));job=await Job(race);transport.BeforeGate=async()=>{await Post("/contacts/"+job.ContactId+"/suppress",new SuppressionInput("During authentication"));};transport.Outcome=new("Accepted","Should never send");before=transport.Sends;await Tick();transport.BeforeGate=null;check((await Job(race)).State=="Suppressed"&&transport.Sends==before,"Unsubscribe after authentication blocks dispatch at the final gate");
            var history=await Read(await client.GetAsync("/api/contacts/"+job.ContactId+"/history"));check(history.GetArrayLength()>=2,"Consent events retain subscription and suppression history");
            var moved=await Post("/lists/move",new ListMoveInput(["Tests"],"Merged"));check(moved.GetProperty("moved").GetInt32()==3,"List merge moves contacts without recreating them");
            var list=await Read(await client.GetAsync("/api/contacts?list=Merged"));check(list.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("suppressed").GetBoolean()),"List merge preserves unsubscribed status");
            await Post("/library",new LibraryInput("Signature","Snippet","Business","Example Inc"));check((await Read(await client.GetAsync("/api/library?kind=Snippet"))).GetArrayLength()==1,"Reusable snippets persist in the content library");
            check((await Post("/maintenance/backup",new{})).GetProperty("name").GetString()!.EndsWith(".db"),"Online SQLite backup creates a consistent server-side database snapshot");
            try{using var other=StudioHost.Build([],settings);throw new Exception("Second process acquired workspace");}catch(IOException){check(true,"Workspace lease prevents duplicate local server ownership");}
        }
        finally {await host.StopAsync();await host.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    private static async Task LegacyUpgrade(Action<bool,string> check)
    {
        string dir=Path.Combine(Path.GetTempPath(),"studio-legacy-"+Guid.NewGuid());Directory.CreateDirectory(dir);string path=Path.Combine(dir,"studio.db");
        var options=new DbContextOptionsBuilder<SqliteStore>().UseSqlite("Data Source="+path).Options;
        using(var old=new SqliteStore(options)){await old.GetService<IMigrator>().MigrateAsync(old.Database.GetMigrations().First());await old.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");await old.Database.ExecuteSqlRawAsync("INSERT INTO \"Contacts\" (\"Id\",\"Email\",\"FirstName\",\"LastName\",\"Company\",\"ListName\",\"ConsentSource\",\"CreatedAt\",\"Suppressed\",\"SuppressionReason\") VALUES ('00000000-0000-0000-0000-000000000001','old@example.com','','','','Legacy','Original source',1,1,'Unsubscribed');");}
        var settings=new StudioSettings{DataDirectory=dir};
        using(var upgraded=new SqliteStore(options)){SchemaUpgrade.Apply(upgraded,settings);check(await upgraded.Contacts.AnyAsync(x=>x.Email=="old@example.com"&&x.Suppressed)&&!upgraded.Database.GetPendingMigrations().Any(),"Legacy 0.1 database upgrades without losing suppressed contacts");}
        check(Directory.GetFiles(Path.Combine(dir,"backups"),"*.db").Length==1,"Legacy upgrade saves a consistent recovery snapshot before changing schema");
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);
    }
}
