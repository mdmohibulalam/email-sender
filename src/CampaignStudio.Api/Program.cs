using CampaignStudio.Api;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Text.Json;

var app = StudioHost.Build(args);
string? readyFile = app.Configuration["Studio:ReadyFile"];
bool wroteReady = false;
try
{
    if (!string.IsNullOrWhiteSpace(readyFile))
    {
        string data = Path.GetFullPath(app.Services.GetRequiredService<StudioSettings>().DataDirectory);
        if (Path.GetDirectoryName(Path.GetFullPath(readyFile)) != data) throw new InvalidOperationException("Ready file must be directly inside the data directory.");
    }
    await app.StartAsync();
    if (!string.IsNullOrWhiteSpace(readyFile))
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await File.WriteAllTextAsync(readyFile + ".tmp", JsonSerializer.Serialize(new { url=address, pid=Environment.ProcessId, version=StudioVersion.Current }));
        File.Move(readyFile + ".tmp", readyFile, true); wroteReady = true;
    }
    await app.WaitForShutdownAsync();
}
finally { if (wroteReady) File.Delete(readyFile!); await app.DisposeAsync(); }
