using CampaignStudio.Api;
using CampaignStudio.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public static class AdvancedChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        const string png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAEElEQVR4AQEFAPr/AP8AAP8FAAH/+lyI0QAAAABJRU5ErkJggg==";
        check(DirectDelivery.Rank(new[]{(20,"mx2.example."),(10,"mx1.example.")},"example.com").SequenceEqual(new[]{"mx1.example","mx2.example"}),"Direct delivery orders MX hosts");
        try{DirectDelivery.Rank(new[]{(0,".")},"example.com");throw new Exception("Null MX accepted");}catch(NoMailServerException){check(true,"Direct delivery respects null MX");}
        check((await new SmtpTransport().Test(new SenderProfile{DeliveryMode="Direct",FromEmail="sender@example.com"},"",CancellationToken.None)).State=="ConnectionBlocked","Direct delivery defaults to disabled");

        string dir=Path.Combine(Path.GetTempPath(),"studio-advanced-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        var settings=new StudioSettings{DataDirectory=dir,ApiKey=new string('a',64),TokenSecret=new string('b',64),StartWorker=false,EnableLiveSending=true,PublicBaseUrl="https://studio.example"};
        var clock=new ExtendedChecks.Clock();var transport=new ExtendedChecks.Transport();
        var host=StudioHost.Build(["--urls","http://127.0.0.1:0","--Logging:LogLevel:Default","Warning","--Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command","None","--Logging:LogLevel:Microsoft.EntityFrameworkCore.Update","None"],settings,configure:s=>{s.AddSingleton<TimeProvider>(clock);s.AddSingleton<ISmtpTransport>(transport);});
        await host.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
        client.DefaultRequestHeaders.Authorization=new("Bearer",settings.ApiKey);
        async Task<JsonElement> Read(HttpResponseMessage response){string text=await response.Content.ReadAsStringAsync();if(!response.IsSuccessStatusCode)throw new Exception(response.StatusCode+" "+text);return JsonDocument.Parse(text).RootElement.Clone();}
        async Task<JsonElement> Post(string path,object body)=>await Read(await client.PostAsJsonAsync("/api"+path,body));
        async Task Tick()=>await new DispatchWorker(host.Services.GetRequiredService<IServiceScopeFactory>(),settings,host.Services.GetRequiredService<ILogger<DispatchWorker>>()).Tick();
        CampaignInput Draft(string name,string list)=>new(name,"Hello {{firstName}}","Preview","Heading","Body","Open","https://example.com","Example address","#176b56",list);
        async Task<Guid> Create(CampaignInput draft)=>(await Post("/campaigns",draft)).GetProperty("id").GetGuid();
        async Task<RecipientJob> Job(Guid id){using var scope=host.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<Store>().Jobs.AsNoTracking().SingleAsync(x=>x.CampaignId==id);}
        async Task Capture(Guid id){await Post("/campaigns/"+id+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds()));for(int i=0;i<4;i++)await Tick();}
        TaskCompletionSource? release=null;var running=new List<Task>();
        try
        {
            byte[] invalid=Convert.FromBase64String(png);invalid[^5]^=1;
            check((await client.PostAsJsonAsync("/api/images",new AttachmentInput("corrupt.png",Convert.ToBase64String(invalid)))).StatusCode==HttpStatusCode.BadRequest,"Image upload rejects corrupt PNG checksums");
            var image=await Post("/images",new AttachmentInput("pixel.png",png));string url=image.GetProperty("url").GetString()!;
            using(var visitor=new HttpClient{BaseAddress=client.BaseAddress})
            {
                check((await visitor.GetAsync(url)).IsSuccessStatusCode,"Signed image preview loads");
                check((await visitor.GetAsync(url[..^1]+(url[^1]=='A'?'B':'A'))).StatusCode==HttpStatusCode.NotFound,"Image preview rejects forged signatures");
            }
            await Post("/contacts",new ContactInput("image@example.com","Image",ListName:"Image",ConsentSource:"Sandbox",ConsentConfirmed:true));
            Guid imageCampaign=await Create(Draft("Image capture","Image") with{BlocksJson=JsonSerializer.Serialize(new[]{new EmailBlock("image",Url:url,Label:"Pixel")},EmailDesign.Json)});
            await Capture(imageCampaign);var imageJob=await Job(imageCampaign);
            using(var raw=new MemoryStream(await client.GetByteArrayAsync("/api/jobs/"+imageJob.Id+"/message")))
            {var mime=await MimeMessage.LoadAsync(raw);check((mime.HtmlBody??"").Contains("cid:")&&mime.BodyParts.Any(x=>x.ContentType.MimeType=="image/png"),"Uploaded images are embedded in MIME");}

            foreach(string email in new[]{"one@example.com","two@example.com","three@example.com"})await Post("/contacts",new ContactInput(email,ListName:"Parallel",ConsentSource:"Sandbox",ConsentConfirmed:true));
            var senderJson=await Post("/connections",new SenderInput("Parallel","localhost",587,"sandbox","test","sender@example.com","Sandbox",DelayMilliseconds:10,MaxConnections:2));
            Guid sender=senderJson.GetProperty("id").GetGuid();await Post("/connections/"+sender+"/test",new{});
            Guid parallel=await Create(Draft("Parallel caps","Parallel"));
            await Post("/campaigns/"+parallel+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds(),"Smtp",true,sender));
            var firstEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var twoEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            release=new(TaskCreationOptions.RunContinuationsAsynchronously);int entered=0;
            transport.BeforeGate=async()=>{int n=Interlocked.Increment(ref entered);if(n==1)firstEntered.TrySetResult();if(n==2)twoEntered.TrySetResult();await release!.Task;};
            Task first=Tick();running.Add(first);await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            clock.Advance(1);Task second=Tick();running.Add(second);await twoEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            clock.Advance(1);await Tick();
            using(var scope=host.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<Store>();
                check(await db.Jobs.CountAsync(x=>x.CampaignId==parallel&&x.State=="Sending")==2&&await db.Jobs.CountAsync(x=>x.CampaignId==parallel&&x.State=="Pending")==1,"Concurrent workers honor per-sender caps");
                check((await db.Campaigns.FindAsync(parallel))!.State=="Running","In-flight recipients prevent premature completion");
            }
            release.TrySetResult();await Task.WhenAll(running);transport.BeforeGate=null;clock.Advance(1);await Tick();await Tick();
            using(var scope=host.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<Store>();
                check(await db.Jobs.CountAsync(x=>x.CampaignId==parallel&&x.State=="Accepted")==3&&await db.Attempts.CountAsync(x=>db.Jobs.Any(j=>j.Id==x.JobId&&j.CampaignId==parallel))==3,"Parallel dispatch records exactly one attempt per recipient");
            }

            await Post("/contacts",new ContactInput("tracked@example.com",ListName:"Tracked",ConsentSource:"Sandbox",ConsentConfirmed:true));
            Guid tracked=await Create(Draft("Signals","Tracked") with{TrackEngagement=true});
            clock.Advance(3);await Post("/campaigns/"+tracked+"/schedule",new ScheduleInput(clock.Now.ToUnixTimeSeconds(),"Smtp",true,sender));await Tick();await Tick();
            var trackedJob=await Job(tracked);var tracking=host.Services.GetRequiredService<EngagementTracking>();
            check((transport.LastMessage!.HtmlBody??"").Contains("/t/c/")&&(transport.LastMessage.HtmlBody??"").Contains("/t/o/"),"Live tracking adds signed links and image signals");
            using(var handler=new HttpClientHandler{AllowAutoRedirect=false})using(var visitor=new HttpClient(handler){BaseAddress=client.BaseAddress})
            {
                string open="/t/o/"+trackedJob.Id.ToString("N")+"/"+tracking.Signature(trackedJob.Id,"open")+".gif";await visitor.GetAsync(open);await visitor.GetAsync(open);
                const string destination="https://example.com/article";
                string click="/t/c/"+trackedJob.Id.ToString("N")+"/"+tracking.Signature(trackedJob.Id,"click:"+destination)+"?url="+Uri.EscapeDataString(destination);
                var response=await visitor.GetAsync(click);
                check(response.StatusCode==HttpStatusCode.Redirect&&response.Headers.Location?.AbsoluteUri==destination,"Signed click links redirect without fetching destinations");
                check((await visitor.GetAsync("/t/c/"+trackedJob.Id+"/invalid?url="+Uri.EscapeDataString(destination))).StatusCode==HttpStatusCode.NotFound,"Forged tracking redirects are rejected");
            }
            var signals=await Read(await client.GetAsync("/api/campaigns/"+tracked+"/engagement"));
            check(signals.GetProperty("openRecipients").GetInt32()==1&&signals.GetProperty("clickRecipients").GetInt32()==1,"Signals report distinct recipients without claiming human reads");

            var original=new MimeMessage{Subject="Imported binary message"};original.From.Add(MailboxAddress.Parse("source@example.com"));original.To.Add(MailboxAddress.Parse("dest@example.com"));
            var body=new BodyBuilder{HtmlBody="<h1>Imported</h1><p><a href='https://example.com/from-eml'>Read more</a></p><img src='cid:logo@example.com'>"};
            var logo=body.LinkedResources.Add("logo.png",Convert.FromBase64String(png),ContentType.Parse("image/png"));logo.ContentId="logo@example.com";
            var attachment=(MimePart)body.Attachments.Add("binary.bin",new byte[]{0,255,42});attachment.ContentTransferEncoding=ContentEncoding.Binary;original.Body=body.ToMessageBody();
            using var serialized=new MemoryStream();await original.WriteToAsync(serialized);
            var imported=await Post("/messages/import",new HtmlImportInput("original.eml","","eml",Convert.ToBase64String(serialized.ToArray())));
            Guid file=imported.GetProperty("attachments")[0].GetProperty("id").GetGuid();
            check(imported.GetProperty("subject").GetString()=="Imported binary message"&&imported.GetProperty("sourceHtml").GetString()!.Contains("/media/"),"EML import preserves subject and inline-image references");
            check((await File.ReadAllBytesAsync(Path.Combine(dir,"attachments",file.ToString("N")))).SequenceEqual(new byte[]{0,255,42}),"EML import preserves binary attachment bytes");
            await Post("/contacts",new ContactInput("import@example.com",ListName:"Imported",ConsentSource:"Sandbox",ConsentConfirmed:true));
            Guid importedCampaign=await Create(Draft("Imported MIME","Imported") with{SourceHtml=imported.GetProperty("sourceHtml").GetString()!,AttachmentIdsJson=JsonSerializer.Serialize(new[]{file})});
            await Capture(importedCampaign);var importedJob=await Job(importedCampaign);
            using(var raw=new MemoryStream(await client.GetByteArrayAsync("/api/jobs/"+importedJob.Id+"/message")))
            {var mime=await MimeMessage.LoadAsync(raw);check(mime.Attachments.Count()==1&&(mime.HtmlBody??"").Contains("cid:")&&(mime.TextBody??"").Contains("https://example.com/from-eml"),"Imported EML re-exports attachments, inline images and text links");}
            check((await client.GetAsync("/api/contacts?sort=company:asc,email:desc")).IsSuccessStatusCode&&(await client.GetAsync("/api/contacts?sort=unsafe:asc")).StatusCode==HttpStatusCode.BadRequest,"Multi-field sorting validates fields and direction");
            using(var scope=host.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<Store>();db.Attempts.Add(new(){JobId=Guid.NewGuid(),Number=1});
                try{await db.SaveChangesAsync();throw new Exception("Orphan accepted");}catch(DbUpdateException){check(true,"Foreign keys reject orphan attempts");}
            }
        }
        finally
        {
            release?.TrySetResult();transport.BeforeGate=null;
            try{await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(5));}catch(Exception){}
            await host.StopAsync();await host.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);
        }
        await Recovery(check);
    }
    private static async Task Recovery(Action<bool,string> check)
    {
        string dir=Path.Combine(Path.GetTempPath(),"studio-recovery-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        var options=new DbContextOptionsBuilder<SqliteStore>().UseSqlite("Data Source="+Path.Combine(dir,"studio.db")+";Foreign Keys=True").Options;
        using(var old=new SqliteStore(options))
        {
            await old.GetService<IMigrator>().MigrateAsync(old.Database.GetMigrations().First());
            await old.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");
            await old.Database.ExecuteSqlRawAsync("INSERT INTO \"Contacts\" (\"Id\",\"Email\",\"FirstName\",\"LastName\",\"Company\",\"ListName\",\"ConsentSource\",\"CreatedAt\",\"Suppressed\",\"SuppressionReason\") VALUES ('00000000-0000-0000-0000-000000000001','old@example.com','','','','Legacy','Original',1,1,'Unsubscribed');");
        }
        var settings=new StudioSettings{DataDirectory=dir};using(var db=new SqliteStore(options))SchemaUpgrade.Apply(db,settings);
        string backup=Directory.GetFiles(Path.Combine(dir,"backups"),"*.db").Single();
        using(var partial=new SqliteStore(options))await partial.Database.ExecuteSqlRawAsync("ALTER TABLE \"Contacts\" ADD COLUMN \"PartialUpgrade\" TEXT NOT NULL DEFAULT ''; UPDATE \"Contacts\" SET \"Suppressed\"=0;");
        string hash;using(var stream=File.OpenRead(backup))hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        await File.WriteAllTextAsync(Path.Combine(dir,"schema-upgrade.json"),JsonSerializer.Serialize(new{Path=backup,Sha256=hash}));
        using(var restored=new SqliteStore(options))
        {
            SchemaUpgrade.Apply(restored,settings);check(await restored.Contacts.AnyAsync(x=>x.Email=="old@example.com"&&x.Suppressed),"Interrupted upgrades restore verified contact state");
            await restored.Database.OpenConnectionAsync();using var command=restored.Database.GetDbConnection().CreateCommand();
            command.CommandText="SELECT count(*) FROM pragma_table_info('Contacts') WHERE name='PartialUpgrade'";
            check(Convert.ToInt32(await command.ExecuteScalarAsync())==0&&!File.Exists(Path.Combine(dir,"schema-upgrade.json")),"Recovery removes partial schema changes and clears its marker");
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);
    }
}
