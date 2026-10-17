using CampaignStudio.Api;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public static class WorkspaceChecks
{
    private sealed class ReplyHandler : HttpMessageHandler
    {
        public Func<HttpResponseMessage> Reply = () => new(HttpStatusCode.NotFound);
        public string? Url, Authorization, UserAgent;
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++; Url=request.RequestUri!.AbsoluteUri; Authorization=request.Headers.Authorization?.ToString(); UserAgent=request.Headers.UserAgent.ToString();
            return Task.FromResult(Reply());
        }
    }
    private static HttpResponseMessage Response(string json, HttpStatusCode code=HttpStatusCode.OK) => new(code){Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json")};
    public static async Task Run(Action<bool,string> check)
    {
        string dir=Path.Combine(Path.GetTempPath(),"studio-preferences-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        var settings=new StudioSettings{DataDirectory=dir,ApiKey=new string('k',64),TokenSecret=new string('t',64),StartWorker=false};
        using var handler=new ReplyHandler();using var upstream=new HttpClient(handler);
        var checker=new UpdateChecker(upstream);
        var host=StudioHost.Build(["--urls","http://127.0.0.1:0","--Logging:LogLevel:Default","Warning"],settings,configure:s=>s.AddSingleton(checker));
        try
        {
            await host.StartAsync();string address=host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client=new HttpClient{BaseAddress=new Uri(address)};
            check((await client.GetAsync("/api/preferences")).StatusCode==HttpStatusCode.Unauthorized&&(await client.GetAsync("/api/updates")).StatusCode==HttpStatusCode.Unauthorized&&handler.Requests==0,"Preferences and update checks require workspace access");
            client.DefaultRequestHeaders.Authorization=new("Bearer",settings.ApiKey);
            var defaults=await client.GetFromJsonAsync<WorkspaceAppearance>("/api/preferences");
            check(defaults==new WorkspaceAppearance(),"First workspace launch uses system theme and Forest palette");
            var expected=new WorkspaceAppearance("dark","indigo",true);
            check((await client.PutAsJsonAsync("/api/preferences",expected)).IsSuccessStatusCode,"Appearance preferences save without changing the database schema");
            check((await client.PutAsJsonAsync("/api/preferences",expected with{Palette="../invalid"})).StatusCode==HttpStatusCode.BadRequest&&(await client.GetFromJsonAsync<WorkspaceAppearance>("/api/preferences"))==expected,"Invalid palettes cannot overwrite saved preferences");
            string saved=await File.ReadAllTextAsync(Path.Combine(dir,"appearance.json"));
            check(!saved.Contains(settings.ApiKey)&&!saved.Contains(settings.TokenSecret),"Appearance storage contains no workspace credentials");
            var current=Version.Parse(StudioVersion.Current);string next=$"{current.Major+1}.0.0";
            handler.Reply=()=>Response(JsonSerializer.Serialize(new{tag_name="v"+next,draft=false,prerelease=false,html_url="javascript:alert(1)"}));
            var release=await client.GetFromJsonAsync<UpdateResult>("/api/updates");
            check(release?.Status=="available"&&release.ReleaseUrl=="https://github.com/mdmohibulalam/email-sender/releases/tag/v"+next,"Available updates use a trusted repository release URL");
            check(handler.Url=="https://api.github.com/repos/mdmohibulalam/email-sender/releases/latest"&&handler.Authorization is null,"Update checks use the fixed release endpoint without forwarding the workspace key");
            handler.Reply=()=>new(HttpStatusCode.NotFound);
            check((await client.GetFromJsonAsync<UpdateResult>("/api/updates"))?.Status=="unpublished","No published release is reported honestly");
            handler.Reply=()=>Response("{}",HttpStatusCode.TooManyRequests);
            var failed=await client.GetAsync("/api/updates");
            check(failed.StatusCode==HttpStatusCode.ServiceUnavailable&&(await failed.Content.ReadAsStringAsync()).Contains("Could not check"),"Unavailable update service returns a retryable error instead of up-to-date");
            handler.Reply=()=>Response("{\"tag_name\":\"v1.0.0-preview\",\"prerelease\":true}");
            check((await client.GetAsync("/api/updates")).StatusCode==HttpStatusCode.ServiceUnavailable,"Stable update checks reject prerelease metadata");
            handler.Reply=()=>Response(new string('x',131073));
            check((await client.GetAsync("/api/updates")).StatusCode==HttpStatusCode.ServiceUnavailable,"Update responses have a bounded size");
            handler.Reply=()=>Response(JsonSerializer.Serialize(new{tag_name="v"+StudioVersion.Current}));
            check((await client.GetFromJsonAsync<UpdateResult>("/api/updates"))?.Status=="available","A matching stable release supersedes the installed preview");
            await host.StopAsync();await host.DisposeAsync();
            host=StudioHost.Build(["--urls","http://127.0.0.1:0","--Logging:LogLevel:Default","Warning"],settings);await host.StartAsync();
            address=host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var restarted=new HttpClient{BaseAddress=new Uri(address)};restarted.DefaultRequestHeaders.Authorization=new("Bearer",settings.ApiKey);
            check((await restarted.GetFromJsonAsync<WorkspaceAppearance>("/api/preferences"))==expected,"Appearance survives a native-style restart on a new loopback port");
            await File.WriteAllTextAsync(Path.Combine(dir,"appearance.json"),"{broken");
            check((await restarted.GetFromJsonAsync<WorkspaceAppearance>("/api/preferences"))==new WorkspaceAppearance(),"Corrupt appearance JSON falls back without blocking the workspace");
        }
        finally{await host.StopAsync();await host.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
