using CampaignStudio.Api;
using CampaignStudio.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net.Http.Json;
using System.Text.Json;

public static class PostgresChecks
{
    public static async Task Run(string connectionString,Action<bool,string> check)
    {
        string schema="studio_test_"+Guid.NewGuid().ToString("N"),dir=Path.Combine(Path.GetTempPath(),schema);Directory.CreateDirectory(dir);
        await using var admin=new NpgsqlConnection(connectionString);await admin.OpenAsync();await using(var command=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await command.ExecuteNonQueryAsync();
        var builder=new NpgsqlConnectionStringBuilder(connectionString){SearchPath=schema};
        var settings=new StudioSettings{DataDirectory=dir,Postgres=builder.ConnectionString,ApiKey=new string('p',64),TokenSecret=new string('q',64),StartWorker=false};
        var host=StudioHost.Build(["--urls","http://127.0.0.1:0","--Logging:LogLevel:Default","Warning"],settings);
        try
        {
            await host.StartAsync();using var client=new HttpClient{BaseAddress=new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};client.DefaultRequestHeaders.Authorization=new("Bearer",settings.ApiKey);
            async Task<JsonElement> Read(HttpResponseMessage r){r.EnsureSuccessStatusCode();return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();}
            var imported=await Read(await client.PostAsJsonAsync("/api/contacts/import",new ImportInput("Email,First\na@example.com,Ada\nb@example.com,Bob\na@example.com,Duplicate",ConsentSource:"PostgreSQL sandbox",ConsentConfirmed:true)));
            check(imported.GetProperty("added").GetInt32()==2&&imported.GetProperty("duplicates").GetInt32()==1,"PostgreSQL import deduplicates contacts");
            var draft=new CampaignInput("PostgreSQL","Hello","Preview","Hello {{firstName}}","Body","Open","https://example.com","Address","#176b56","Main audience");
            var campaign=await Read(await client.PostAsJsonAsync("/api/campaigns",draft));Guid id=campaign.GetProperty("id").GetGuid();await Read(await client.PostAsJsonAsync("/api/campaigns/"+id+"/schedule",new ScheduleInput(DateTimeOffset.UtcNow.ToUnixTimeSeconds())));
            var worker=new DispatchWorker(host.Services.GetRequiredService<IServiceScopeFactory>(),settings,host.Services.GetRequiredService<ILogger<DispatchWorker>>());await worker.Tick();await worker.Tick();await worker.Tick();
            using(var scope=host.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<Store>();check(await db.Jobs.CountAsync(x=>x.CampaignId==id&&x.State=="Captured")==2,"PostgreSQL queue captures each frozen recipient exactly once");check(await db.Attempts.CountAsync()==2,"PostgreSQL records dispatch attempts");check(!db.Database.HasPendingModelChanges()&&!db.Database.GetPendingMigrations().Any(),"PostgreSQL schema matches all checked-in migrations");}
            await host.StopAsync();await host.DisposeAsync();host=StudioHost.Build(["--urls","http://127.0.0.1:0","--Logging:LogLevel:Default","Warning"],settings);await host.StartAsync();using(var scope=host.Services.CreateScope())check(await scope.ServiceProvider.GetRequiredService<Store>().Contacts.CountAsync()==2,"PostgreSQL contacts survive host restart");
        }
        finally {await host.StopAsync();await host.DisposeAsync();NpgsqlConnection.ClearAllPools();await using var command=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await command.ExecuteNonQueryAsync();Directory.Delete(dir,true);}
    }
}
