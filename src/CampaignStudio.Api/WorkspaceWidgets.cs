using System.Diagnostics;
using System.Text.Json;
using DnsClient;
namespace CampaignStudio.Api;
public sealed record DomainInput(string Domain);
public sealed record ShortcutInput(string Name);
public static class WorkspaceWidgets
{
    private static readonly LookupClient Dns=new(new LookupClientOptions{Timeout=TimeSpan.FromSeconds(4),Retries=1,UseCache=true});
    private static Dictionary<string,string> Shortcuts(StudioSettings s){if(s.ShortcutsJson.Length>20000)throw new FormatException("Shortcut configuration too large.");return JsonSerializer.Deserialize<Dictionary<string,string>>(s.ShortcutsJson)??[];}
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/shortcuts",(StudioSettings s)=>Shortcuts(s).Select(x=>new{name=x.Key,available=OperatingSystem.IsWindows()&&Path.IsPathFullyQualified(x.Value)&&x.Value.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(x.Value)}));
        api.MapPost("/shortcuts/open",(ShortcutInput input,StudioSettings s)=>
        {
            if(input.Name is null||!OperatingSystem.IsWindows()||!Shortcuts(s).TryGetValue(input.Name,out var file)||!Path.IsPathFullyQualified(file)||!file.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(file))throw new FormatException("This Windows shortcut is not configured.");
            using var process=Process.Start(new ProcessStartInfo(file){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(file)!});return Results.Ok(new{opened=process is not null});
        });
        api.MapPost("/domains/info",async(DomainInput input,CancellationToken ct)=>
        {
            if(input.Domain is null||input.Domain.Length>253||Uri.CheckHostName(input.Domain)!=UriHostNameType.Dns||!input.Domain.Contains('.'))throw new FormatException("Enter a domain without a protocol or path.");
            try
            {
                var q=await Task.WhenAll(Dns.QueryAsync(input.Domain,QueryType.A,cancellationToken:ct),Dns.QueryAsync(input.Domain,QueryType.AAAA,cancellationToken:ct),Dns.QueryAsync(input.Domain,QueryType.MX,cancellationToken:ct));
                return Results.Ok(new{domain=input.Domain,ipv4=q[0].Answers.ARecords().Select(x=>x.Address.ToString()),ipv6=q[1].Answers.AaaaRecords().Select(x=>x.Address.ToString()),mx=q[2].Answers.MxRecords().OrderBy(x=>x.Preference).Select(x=>new{priority=x.Preference,host=x.Exchange.Value}),note="DNS records do not verify an individual mailbox or guarantee delivery."});
            }
            catch(Exception)when(!ct.IsCancellationRequested){throw new FormatException("DNS lookup failed or timed out.");}
        });
    }
}
