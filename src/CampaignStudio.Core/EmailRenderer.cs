using System.Net;
using System.Text.RegularExpressions;

namespace CampaignStudio.Core;

public static partial class EmailRenderer
{
    public static string Merge(string text, string firstName, string lastName, string email) => text
        .Replace("{{firstName}}", string.IsNullOrWhiteSpace(firstName) ? "there" : firstName)
        .Replace("{{lastName}}", lastName).Replace("{{email}}", email);
    public static bool ValidUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo);
    [GeneratedRegex("^#[a-fA-F0-9]{6}$")] private static partial Regex ColorPattern();
    public static bool ValidAccent(string value) => ColorPattern().IsMatch(value);
    public static string Html(Campaign c, string first = "Alex", string last = "", string email = "alex@example.com", string unsubscribe = "")
    {
        string E(string text) => WebUtility.HtmlEncode(Merge(ScriptTags.Resolve(c.TagsJson, text, first, last, email), first, last, email));
        string color = ValidAccent(c.Accent) ? c.Accent : "#176b56";
        string link = ValidUrl(c.ButtonUrl) ? WebUtility.HtmlEncode(c.ButtonUrl) : "https://example.com";
        var button = string.IsNullOrWhiteSpace(c.ButtonLabel) ? "" : $"<p style='margin:28px 0'><a href='{link}' style='display:inline-block;padding:14px 22px;border-radius:6px;background:{color};color:white;text-decoration:none;font-weight:bold'>{E(c.ButtonLabel)}</a></p>";
        var unsub = unsubscribe.Length == 0 ? "<span>Unsubscribe link is added when scheduled.</span>" : $"<a href='{WebUtility.HtmlEncode(unsubscribe)}' style='color:#65716c'>Unsubscribe</a>";
        if (!string.IsNullOrWhiteSpace(c.SourceHtml))
        {
            string sourceWithTags = ScriptTags.Resolve(c.TagsJson, c.SourceHtml, WebUtility.HtmlEncode(first), WebUtility.HtmlEncode(last), WebUtility.HtmlEncode(email));
            string rendered = Merge(sourceWithTags, WebUtility.HtmlEncode(first), WebUtility.HtmlEncode(last), WebUtility.HtmlEncode(email));
            return EmailDesign.FinishHtml(c, EmailDesign.CleanHtml(rendered) + "<p>" + E(c.Footer) + "<br>" + unsub + "</p>");
        }
        string content = string.IsNullOrWhiteSpace(c.BlocksJson)
            ? "<h1 style='font-size:28px;line-height:1.3'>" + E(c.Heading) + "</h1><p>" + E(c.Body).Replace("\n", "<br>") + "</p>" + button
            : EmailDesign.RenderBlocks(c, first, last, email);
        return EmailDesign.FinishHtml(c, $"""
        <!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;background:#f3f5f4;font:16px/1.7 Arial,sans-serif;color:#23312c">
        <div style="display:none;max-height:0;overflow:hidden">{E(c.Preheader)}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:32px 12px">
        <table role="presentation" width="600" style="width:100%;max-width:600px;background:white;border-radius:10px" cellpadding="0" cellspacing="0"><tr><td style="padding:40px 32px">
        <div style="height:4px;background:{color};margin-bottom:28px"></div>{content}
        <hr style="border:0;border-top:1px solid #e4e9e6;margin:32px 0"><p style="font-size:12px;color:#65716c;white-space:pre-line">{E(c.Footer)}<br>{unsub}</p>
        </td></tr></table></td></tr></table></body></html>
        """);
    }
}
