using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CampaignStudio.Api;

public static class StudioVersion
{
    public static string Current => typeof(StudioVersion).Assembly.GetName().Version!.ToString(3);
    public const string Channel = "Preview";
}

public sealed record UpdateResult(string Status, string CurrentVersion, string? LatestVersion, string Message, string? ReleaseUrl = null);

public sealed class UpdateChecker(HttpClient client)
{
    public async Task<UpdateResult> Check(CancellationToken ct)
    {
        using var response = await client.GetAsync("https://api.github.com/repos/mdmohibulalam/email-sender/releases/latest", HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new("unpublished", StudioVersion.Current, null, "No stable release has been published yet. Preview installers are available from verified GitHub Actions builds.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 131072) throw new HttpRequestException("Release response is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var body = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (body.Length + count > 131072) throw new HttpRequestException("Release response is too large.");
            body.Write(buffer, 0, count);
        }
        using var json = JsonDocument.Parse(body.ToArray());
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out var tagValue) || tagValue.ValueKind != JsonValueKind.String)
            throw new HttpRequestException("Invalid release response.");
        string tag = tagValue.GetString()!;
        if (tag.Length > 40 || !Regex.IsMatch(tag, @"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant) || !Version.TryParse(tag.TrimStart('v'), out var version))
            throw new HttpRequestException("Unsupported stable release version.");
        if ((root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) ||
            (root.TryGetProperty("prerelease", out var preview) && preview.ValueKind == JsonValueKind.True))
            throw new HttpRequestException("Expected a published stable release.");
        bool newer = version >= Version.Parse(StudioVersion.Current); // A stable version also supersedes the matching preview.
        string url = "https://github.com/mdmohibulalam/email-sender/releases/tag/" + Uri.EscapeDataString(tag);
        return new(newer ? "available" : "current", StudioVersion.Current, version.ToString(3),
            newer ? "A stable release is available. Review its notes and download the installer from GitHub." : "This preview is newer than the latest published stable release.", url);
    }
}
