using CampaignStudio.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CampaignStudio.Desktop;

public sealed partial class MainWindow : Window
{
    private WebApplication? server;
    private string origin = "";
    private string key = "";
    private bool initialized;
    private readonly bool smoke = Environment.GetCommandLineArgs().Contains("--smoke-test");
    public MainWindow()
    {
        InitializeComponent(); Title = "Campaign Studio — Preview 0.2";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1360, 920));
        Workspace.Loaded += async (_, _) => await InitializeWorkspace();
        Closed += async (_, _) => { if (server is not null) { await server.StopAsync(); await server.DisposeAsync(); } };
    }
    private async Task InitializeWorkspace()
    {
        if (initialized) return; initialized = true;
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CampaignStudio");
            string? dataOverride = Environment.GetEnvironmentVariable("CAMPAIGN_STUDIO_DATA_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(dataOverride) && Path.IsPathFullyQualified(dataOverride)) folder = dataOverride;
            Directory.CreateDirectory(folder);
            string? cloud = Environment.GetEnvironmentVariable("CAMPAIGN_STUDIO_CLOUD_URL");
            key = Environment.GetEnvironmentVariable("CAMPAIGN_STUDIO_CLOUD_KEY") ?? "";
            if (!string.IsNullOrWhiteSpace(cloud))
            {
                var uri = new Uri(cloud);
                if (uri.Scheme != "https" || uri.AbsolutePath != "/" || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
                    throw new InvalidOperationException("Cloud URL must be an HTTPS origin, for example https://mail.example.com.");
                if (key.Length < 32) throw new InvalidOperationException("Set CAMPAIGN_STUDIO_CLOUD_KEY to the workspace access key.");
                origin = uri.GetLeftPart(UriPartial.Authority);
            }
            else
            {
                key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                string secretFile = Path.Combine(folder, "unsubscribe-secret.bin");
                byte[] secret;
                if (File.Exists(secretFile)) secret = ProtectedData.Unprotect(await File.ReadAllBytesAsync(secretFile), null, DataProtectionScope.CurrentUser);
                else
                {
                    secret = Encoding.UTF8.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
                    await File.WriteAllBytesAsync(secretFile, ProtectedData.Protect(secret, null, DataProtectionScope.CurrentUser));
                }
                server = StudioHost.Build(["--urls", "http://127.0.0.1:0"], new StudioSettings
                { ApiKey = key, TokenSecret = Encoding.UTF8.GetString(secret), DataDirectory = folder,
                    EnableLiveSending = string.Equals(Environment.GetEnvironmentVariable("Studio__EnableLiveSending"),"true",StringComparison.OrdinalIgnoreCase),
                    EnableDirectSending = string.Equals(Environment.GetEnvironmentVariable("Studio__EnableDirectSending"),"true",StringComparison.OrdinalIgnoreCase),
                    PublicBaseUrl = Environment.GetEnvironmentVariable("Studio__PublicBaseUrl") ?? "",
                    ShortcutsJson = Environment.GetEnvironmentVariable("Studio__ShortcutsJson") ?? "{}",
                    ExternalDesignerExecutable = Environment.GetEnvironmentVariable("Studio__ExternalDesignerExecutable") ?? "",
                    ExternalDesignerOutput = Environment.GetEnvironmentVariable("Studio__ExternalDesignerOutput") ?? "" }, Path.Combine(AppContext.BaseDirectory, "wwwroot"));
                await server.StartAsync();
                origin = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
            }
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(folder, "WebView"), null);
            await Workspace.EnsureCoreWebView2Async(environment);
            Workspace.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Workspace.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Workspace.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
            Workspace.CoreWebView2.NavigationStarting += (_, args) =>
            {
                // Preview frames have no scripts or native bridges. Only the trusted workspace can navigate the top-level view.
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Authority) != origin) args.Cancel = true;
            };
            await Workspace.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("if(window.top===window && location.origin===" + JsonSerializer.Serialize(origin) + "){window.__studioDesktopKey=" + JsonSerializer.Serialize(key) + ";}");
            Workspace.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess) { Status.Text = "Could not open the workspace. Check the cloud connection."; if (smoke) Environment.Exit(1); return; }
                Status.Visibility = Visibility.Collapsed;
                if (smoke)
                {
                    for (int attempt=0;attempt<80;attempt++)
                    {
                        string ready=await Workspace.ExecuteScriptAsync("document.querySelectorAll('#nav [data-nav]').length >= 10 && !!document.querySelector('h1')");
                        if(ready=="true"){Environment.Exit(0);return;}
                        await Task.Delay(250);
                    }
                    Environment.Exit(2);
                }
            };
            Workspace.CoreWebView2.DownloadStarting += (_, args) =>
            {
                // Keep exports outside the application folder and prevent overwriting without a prompt.
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
                string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", Path.GetFileName(args.ResultFilePath));
                if (File.Exists(target)) target = Path.Combine(Path.GetDirectoryName(target)!, Path.GetFileNameWithoutExtension(target) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Path.GetExtension(target));
                args.ResultFilePath = target;
            };
            Workspace.Source = new Uri(origin);
        }
        catch (Exception ex) { Status.Text = "Unable to open Campaign Studio: " + ex.Message; if (smoke) Environment.Exit(1); }
    }
}
