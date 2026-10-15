using System.Diagnostics;
using System.Security.Cryptography;
namespace CampaignStudio.Api;
public sealed class ExternalDesigner(StudioSettings settings):IDisposable
{
    private readonly object gate=new();private Process? process;private string oldHash="";
    public bool Available=>OperatingSystem.IsWindows()&&Path.IsPathFullyQualified(settings.ExternalDesignerExecutable)&&settings.ExternalDesignerExecutable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(settings.ExternalDesignerExecutable)&&Path.IsPathFullyQualified(settings.ExternalDesignerOutput);
    private string Hash(){if(!File.Exists(settings.ExternalDesignerOutput)||new FileInfo(settings.ExternalDesignerOutput).Length>500000)return "";using var file=File.OpenRead(settings.ExternalDesignerOutput);return Convert.ToHexString(SHA256.HashData(file));}
    public object Status(){lock(gate){bool running=process is not null&&!process.HasExited;return new{available=Available,running,canImport=process is not null&&!running&&Hash() is string h&&h.Length>0&&h!=oldHash};}}
    public void Start(){lock(gate){if(!Available)throw new FormatException("Configure a Windows designer executable and HTML export path.");if(process is not null&&!process.HasExited)throw new FormatException("The designer is already open.");oldHash=Hash();process?.Dispose();process=Process.Start(new ProcessStartInfo(settings.ExternalDesignerExecutable){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(settings.ExternalDesignerExecutable)!});}}
    public string Import(){lock(gate){if(process is null||!process.HasExited||Hash() is not string h||h.Length==0||h==oldHash)throw new FormatException("Export updated HTML and close the designer before importing.");string html=File.ReadAllText(settings.ExternalDesignerOutput);Core.EmailDesign.CleanHtml(html);return html;}}
    public void Dispose(){lock(gate)process?.Dispose();}
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/external-editor",(ExternalDesigner d)=>d.Status());
        api.MapPost("/external-editor/start",(ExternalDesigner d)=>{d.Start();return Results.Ok(new{started=true});});
        api.MapPost("/external-editor/import",(ExternalDesigner d)=>Results.Ok(new{sourceHtml=d.Import()}));
    }
}
