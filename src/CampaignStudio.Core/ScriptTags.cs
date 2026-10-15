using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
namespace CampaignStudio.Core;

public static partial class ScriptTags
{
    [GeneratedRegex(@"\{\{tag:([a-zA-Z][a-zA-Z0-9_]{0,49})\}\}")] private static partial Regex Pattern();
    public static Dictionary<string,string> Validate(string json)
    {
        if (json is null || json.Length > 100000) throw new FormatException("Programmable tags exceed 100 KB.");
        var tags = EmailDesign.Read<Dictionary<string,string>>(json);
        if (tags.Count > 20 || tags.Any(x => !Regex.IsMatch(x.Key, @"^[a-zA-Z][a-zA-Z0-9_]{0,49}$") || x.Value is null || x.Value.Length > 5000)) throw new FormatException("Use up to 20 named JavaScript expressions, each under 5 KB.");
        return tags;
    }
    public static string Resolve(string json, string text, string first, string last, string email, Dictionary<string,string>? cache = null)
    {
        if (!text.Contains("{{tag:", StringComparison.Ordinal)) return text;
        Dictionary<string,string>? tags = null; var values = cache ?? new Dictionary<string,string>();
        return Pattern().Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            if (values.TryGetValue(name, out var cached)) return cached;
            tags ??= Validate(json);
            if (!tags.TryGetValue(name, out var expression)) throw new FormatException("Missing programmable tag: " + name);
            try
            {
                // Pass only JSON data. CLR, I/O, network and browser APIs are not installed.
                using var engine = new Engine(o => o.LimitMemory(2_000_000).TimeoutInterval(TimeSpan.FromMilliseconds(100)).MaxStatements(2000).LimitRecursion(30));
                engine.Execute("'use strict';const contact=Object.freeze(" + JsonSerializer.Serialize(new { firstName=first,lastName=last,email }) + ");");
                var result = engine.Evaluate("(" + expression + ")");
                if (!result.IsString() && !result.IsNumber() && !result.IsBoolean() && !result.IsNull()) throw new FormatException("Tag must return text, a number, boolean, or null.");
                string value = result.IsNull() ? "" : result.ToString(); if (value.Length > 10000) throw new FormatException("Tag output exceeds 10 KB.");
                values[name] = value; return value;
            }
            catch (Exception ex) when (ex is not FormatException) { throw new FormatException("Programmable tag failed or exceeded execution limits: " + name); }
        });
    }
}
