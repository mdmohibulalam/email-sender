using System.Net;
using System.Net.Sockets;
using AngleSharp.Html.Parser;
namespace CampaignStudio.Api;

public static class PublicLinkChecker
{
    public static bool PublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
        var b = address.GetAddressBytes();
        if (b.Length == 16) return (b[0] & 0xe0) == 0x20; // Global IPv6 unicast only.
        return b[0] is not (0 or 10 or 127) && b[0] < 224 && !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && b[1] == 168) && !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 198 && b[1] is 18 or 19);
    }
    public static async Task<object[]> Check(string html, CancellationToken ct)
    {
        var doc = new HtmlParser().ParseDocument(html);
        var links = doc.QuerySelectorAll("a[href],img[src]").Select(x => x.GetAttribute(x.LocalName == "img" ? "src" : "href") ?? "").Distinct().Take(50);
        var results = new List<object>();
        foreach (var link in links)
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !uri.IsDefaultPort || uri.UserInfo.Length > 0) { results.Add(new { url = link, status = "Skipped", detail = "Only public HTTP/HTTPS links on standard ports are checked." }); continue; }
            try
            {
                using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5), ConnectCallback = async (context, token) =>
                {
                    var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
                    if (addresses.Length == 0 || addresses.Any(x => !PublicAddress(x))) throw new HttpRequestException("Private or reserved target blocked.");
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    try { await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, token); return new NetworkStream(socket, true); } catch { socket.Dispose(); throw; }
                } };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(7) };
                using var request = new HttpRequestMessage(HttpMethod.Head, uri); using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                results.Add(new { url = link, status = ((int)response.StatusCode).ToString(), detail = (int)response.StatusCode is >= 300 and < 400 ? "Redirect; destination was not followed." : response.IsSuccessStatusCode ? "Reachable at check time." : "Review response; HEAD requests may be rejected by some websites." });
            }
            catch (Exception) when (!ct.IsCancellationRequested) { results.Add(new { url = link, status = "Unverified", detail = "Blocked, timed out, or unavailable." }); }
        }
        return results.ToArray();
    }
}
