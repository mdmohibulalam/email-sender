using System.Text.Json;

namespace CampaignStudio.Api;

public sealed record WorkspaceAppearance(string Theme = "system", string Palette = "forest", bool ReducedMotion = false)
{
    public bool IsValid => Theme is "system" or "light" or "dark" && Palette is "forest" or "indigo" or "ocean" or "rose";
}

// Kept with the workspace, so native preferences survive its changing loopback port.
public sealed class WorkspacePreferences(StudioSettings settings, ILogger<WorkspacePreferences> logger) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path = Path.Combine(settings.DataDirectory, "appearance.json");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<WorkspaceAppearance> Read(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(path)) return new();
            if (new FileInfo(path).Length > 1024) { logger.LogWarning("Appearance file exceeds its size limit; using defaults."); return new(); }
            try
            {
                var value = JsonSerializer.Deserialize<WorkspaceAppearance>(await File.ReadAllTextAsync(path, ct), Json);
                if (value?.IsValid == true) return value;
                logger.LogWarning("Invalid appearance settings; using defaults.");
            }
            catch (JsonException) { logger.LogWarning("Unreadable appearance JSON; using defaults."); }
            return new();
        }
        finally { gate.Release(); }
    }

    public async Task<WorkspaceAppearance> Save(WorkspaceAppearance value, CancellationToken ct)
    {
        if (!value.IsValid) throw new FormatException("Choose a supported theme and color palette.");
        await gate.WaitAsync(ct);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Json), ct);
            using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite)) stream.Flush(true);
            File.Move(temporary, path, true);
            return value;
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } finally { gate.Release(); } }
    }

    public void Dispose() => gate.Dispose();
}
