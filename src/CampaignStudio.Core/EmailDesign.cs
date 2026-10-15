using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Ganss.Xss;
namespace CampaignStudio.Core;

public static class EmailDesign
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json) ?? throw new FormatException("Missing design data.");
    public static string CleanHtml(string source)
    {
        if (source.Length > 500_000) throw new FormatException("HTML must be under 500 KB.");
        var sanitizer = new HtmlSanitizer();
        foreach (string tag in new[] { "form", "input", "button", "textarea", "select", "option", "video", "audio", "iframe", "object", "embed" }) sanitizer.AllowedTags.Remove(tag);
        sanitizer.AllowedAttributes.Add("class");
        return sanitizer.Sanitize(source);
    }
    public static List<EmailBlock> Blocks(string json)
    {
        if (json.Length > 100_000) throw new FormatException("Design exceeds 100 KB.");
        List<EmailBlock> blocks;
        try { blocks = Read<List<EmailBlock>>(json); } catch (JsonException) { throw new FormatException("Invalid block design."); }
        if (blocks.Count is < 1 or > 50) throw new FormatException("Use between 1 and 50 blocks.");
        foreach (var b in blocks)
        {
            if (b is null || b.Content is null || b.Url is null || b.Label is null || b.Content.Length > 20000 || b.Url.Length > 2000 || b.Label.Length > 500
                || b.Type is not ("heading" or "text" or "image" or "button" or "divider" or "spacer" or "columns") || b.Height is < 0 or > 200)
                throw new FormatException("Check block type, text length, and spacing.");
            if (b.Type is "button" or "image" && (!EmailRenderer.ValidUrl(b.Url) || b.Type == "image" && !b.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                throw new FormatException("Images need HTTPS; buttons need an HTTP or HTTPS link.");
        }
        return blocks;
    }
    public static string RenderBlocks(Campaign c, string first, string last, string email)
    {
        string E(string s) => WebUtility.HtmlEncode(EmailRenderer.Merge(s, first, last, email)).Replace("\r\n", "\n").Replace("\n", "<br>");
        return string.Join("", Blocks(c.BlocksJson).Select(b => b.Type switch
        {
            "heading" => $"<h2 style='line-height:1.3'>{E(b.Content)}</h2>",
            "text" => $"<p>{E(b.Content)}</p>",
            "image" => $"<p><img src='{WebUtility.HtmlEncode(b.Url)}' alt='{E(b.Label)}' width='536' style='width:100%;max-width:536px;height:auto'></p>",
            "button" => $"<table role='presentation' cellpadding='14'><tr><td bgcolor='{c.Accent}'><a href='{WebUtility.HtmlEncode(b.Url)}' style='color:white;text-decoration:none'>{E(b.Label)}</a></td></tr></table>",
            "divider" => "<hr style='border:0;border-top:1px solid #ddd'>",
            "columns" => $"<table role='presentation' width='100%'><tr><td width='50%' valign='top' style='padding:8px'>{E(b.Content)}</td><td width='50%' valign='top' style='padding:8px'>{E(b.Label)}</td></tr></table>",
            _ => $"<div style='height:{b.Height}px;font-size:1px'>&nbsp;</div>"
        }));
    }
    public static string FinishHtml(Campaign c, string html)
    {
        if (string.IsNullOrWhiteSpace(c.UtmCampaign)) return html;
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var a in doc.QuerySelectorAll("a[href]"))
        {
            var href = a.GetAttribute("href")!;
            if (!EmailRenderer.ValidUrl(href) || href.Contains("/u/", StringComparison.Ordinal)) continue;
            var uri = new UriBuilder(href);
            uri.Query = uri.Query.TrimStart('?') + (uri.Query.Length > 1 ? "&" : "") + "utm_source=campaignstudio&utm_medium=email&utm_campaign=" + Uri.EscapeDataString(c.UtmCampaign);
            a.SetAttribute("href", uri.Uri.AbsoluteUri);
        }
        return doc.DocumentElement.OuterHtml;
    }
    public static string Text(string html) => new HtmlParser().ParseDocument(html).Body?.TextContent ?? "";
    public static string Calendar(CalendarEvent c, Guid campaignId)
    {
        if (c.End <= c.Start || c.Title.Length is < 1 or > 250 || c.Description.Length > 10000 || c.Location.Length > 500) throw new FormatException("Check event title, dates, and length.");
        string Escape(string s) => s.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
        var lines = new[] { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Campaign Studio//EN", "METHOD:PUBLISH", "BEGIN:VEVENT", $"UID:{campaignId:N}@campaign-studio.local", "DTSTAMP:" + DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"), "DTSTART:" + c.Start.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'"), "DTEND:" + c.End.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'"), "SUMMARY:" + Escape(c.Title), "DESCRIPTION:" + Escape(c.Description), "LOCATION:" + Escape(c.Location), "END:VEVENT", "END:VCALENDAR" };
        // Fold by UTF-8 bytes so calendar clients accept long international text.
        var folded = new List<string>();
        foreach (var line in lines)
        {
            var buffer = ""; int bytes = 0;
            foreach (var rune in line.EnumerateRunes()) { int length = rune.Utf8SequenceLength; if (bytes + length > 73) { folded.Add(buffer); buffer = " "; bytes = 1; } buffer += rune; bytes += length; }
            folded.Add(buffer);
        }
        return string.Join("\r\n", folded) + "\r\n";
    }
}
