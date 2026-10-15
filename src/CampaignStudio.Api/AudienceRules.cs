using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
namespace CampaignStudio.Api;

public static class AudienceRules
{
    public static List<SegmentRule> Validate(string json, string match)
    {
        if (json.Length > 10000 || match is not ("All" or "Any")) throw new FormatException("Choose All or Any and up to 12 rules.");
        List<SegmentRule> rules;
        try { rules = EmailDesign.Read<List<SegmentRule>>(json); } catch (System.Text.Json.JsonException) { throw new FormatException("Invalid audience rules."); }
        if (rules.Count is < 1 or > 12 || rules.Any(x => x is null || x.Value is null || x.Value.Length > 250 || x.Field is not ("email" or "firstName" or "lastName" or "company" or "listName") || x.Operator is not ("equals" or "contains" or "endsWith" or "notEquals"))) throw new FormatException("Check audience rule fields and operators.");
        return rules;
    }
    public static IQueryable<Contact> Apply(IQueryable<Contact> query, Segment segment)
    {
        var rules = Validate(segment.RulesJson, segment.Match);
        IQueryable<Contact> One(IQueryable<Contact> source, SegmentRule rule)
        {
            string value = rule.Value.ToLowerInvariant();
            var selected = source.Select(c => new { Contact = c, Value = rule.Field == "email" ? c.Email : rule.Field == "firstName" ? c.FirstName : rule.Field == "lastName" ? c.LastName : rule.Field == "company" ? c.Company : c.ListName });
            return (rule.Operator switch
            {
                "equals" => selected.Where(x => x.Value.ToLower() == value),
                "notEquals" => selected.Where(x => x.Value.ToLower() != value),
                "endsWith" => selected.Where(x => x.Value.ToLower().EndsWith(value)),
                _ => selected.Where(x => x.Value.ToLower().Contains(value))
            }).Select(x => x.Contact);
        }
        if (segment.Match == "All") { foreach (var rule in rules) query = One(query, rule); return query; }
        var combined = query.Where(x => false); foreach (var rule in rules) combined = combined.Union(One(query, rule)); return combined;
    }
    public static IQueryable<Contact> Eligible(Store db, string list) => db.Contacts.Where(c => c.ListName == list && !c.Suppressed && !db.Exclusions.Any(x =>
        x.Kind == "Email" && x.Value == c.Email || x.Kind == "Domain" && (c.Email.EndsWith("@" + x.Value) || c.Email.EndsWith("." + x.Value))));
    public static async Task<IQueryable<Contact>> CampaignAudience(Store db, Campaign c)
    {
        var query = Eligible(db, c.ListName);
        if (c.SegmentId is Guid id)
        {
            var segment = await db.Segments.FindAsync(id) ?? throw new FormatException("Selected segment no longer exists."); query = Apply(query, segment);
        }
        if (c.HistoryCampaignId is Guid previous && c.HistoryMode != "All")
        {
            if (!await db.Campaigns.AnyAsync(x => x.Id == previous)) throw new FormatException("Previous campaign no longer exists.");
            query = c.HistoryMode == "Sent" ? query.Where(x => db.Jobs.Any(j => j.CampaignId == previous && j.ContactId == x.Id && j.State == "Accepted"))
                : query.Where(x => !db.Jobs.Any(j => j.CampaignId == previous && j.ContactId == x.Id && (j.State == "Accepted" || j.State == "Unknown")));
        }
        return query;
    }
    public static Task<bool> CanSend(Store db, Guid campaignId, Guid contactId) => db.Contacts.AnyAsync(c => c.Id == contactId && !c.Suppressed && !db.Exclusions.Any(x =>
        x.Kind == "Email" && x.Value == c.Email || x.Kind == "Domain" && (c.Email.EndsWith("@" + x.Value) || c.Email.EndsWith("." + x.Value))) && db.Campaigns.Any(x => x.Id == campaignId && (x.State == "Scheduled" || x.State == "Running")));
    public static void Log(Store db, Guid contactId, string action, string source) => db.ConsentEvents.Add(new() { ContactId = contactId, Action = action, Source = source });
}
