using System.Linq.Expressions;
using CampaignStudio.Core;
namespace CampaignStudio.Api;
public static class ContactSorting
{
    public static IOrderedQueryable<Contact> Apply(IQueryable<Contact> query,string? sort)
    {
        if(sort?.Length>100)throw new FormatException("Choose up to three sort fields.");
        var parts=(string.IsNullOrWhiteSpace(sort)?"email:asc":sort).Split(',');if(parts.Length>3)throw new FormatException("Choose up to three sort fields.");
        IOrderedQueryable<Contact>? ordered=null;
        foreach(var part in parts)
        {
            var pair=part.Split(':');if(pair.Length!=2||pair[1] is not ("asc" or "desc"))throw new FormatException("Invalid sort direction.");
            Expression<Func<Contact,string>> key=pair[0] switch{"email"=>x=>x.Email,"firstName"=>x=>x.FirstName,"lastName"=>x=>x.LastName,"company"=>x=>x.Company,"listName"=>x=>x.ListName,_=>throw new FormatException("Invalid sort field.")};
            ordered=ordered is null?(pair[1]=="desc"?query.OrderByDescending(key):query.OrderBy(key)):(pair[1]=="desc"?ordered.ThenByDescending(key):ordered.ThenBy(key));
        }
        return ordered!.ThenBy(x=>x.Id);
    }
}
