using System.Security.Cryptography;
using System.Text;
using AngleSharp.Html.Parser;
using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
namespace CampaignStudio.Api;
public sealed class EngagementTracking(StudioSettings settings)
{
    public string Signature(Guid job,string purpose)=>Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret),Encoding.UTF8.GetBytes("tracking:v1:"+job.ToString("N")+":"+purpose)));
    private bool Valid(Guid job,string purpose,string signature)=>signature.Length==64&&CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature),Encoding.ASCII.GetBytes(Signature(job,purpose)));
    public string Rewrite(string html,RecipientJob job)
    {
        var doc=new HtmlParser().ParseDocument(html);string root=settings.PublicBaseUrl.TrimEnd('/');
        foreach(var a in doc.QuerySelectorAll("a[href]"))
        {
            string href=a.GetAttribute("href")??"";if(!EmailRenderer.ValidUrl(href)||href.Length>2000||href.StartsWith(root+"/u/",StringComparison.Ordinal))continue;
            a.SetAttribute("href",root+"/t/c/"+job.Id.ToString("N")+"/"+Signature(job.Id,"click:"+href)+"?url="+Uri.EscapeDataString(href));
        }
        var pixel=doc.CreateElement("img");pixel.SetAttribute("src",root+"/t/o/"+job.Id.ToString("N")+"/"+Signature(job.Id,"open")+".gif");pixel.SetAttribute("width","1");pixel.SetAttribute("height","1");pixel.SetAttribute("alt","");doc.Body!.AppendChild(pixel);return doc.DocumentElement.OuterHtml;
    }
    private static async Task<bool> Record(Store db,Guid job,string kind,string url,string key,TimeProvider clock)
    {
        if(!await db.Jobs.AnyAsync(j=>j.Id==job&&(j.State=="Sending"||j.State=="Accepted"||j.State=="Unknown")&&db.Campaigns.Any(c=>c.Id==j.CampaignId&&c.TrackEngagement)))return false;
        long now=clock.GetUtcNow().ToUnixTimeSeconds(),bucket=now/60;
        if(!await db.Engagement.AnyAsync(x=>x.JobId==job&&x.Kind==kind&&x.LinkKey==key&&x.Bucket==bucket))
        {
            var signal=new EngagementSignal{JobId=job,Kind=kind,Url=url,LinkKey=key,Bucket=bucket,At=now};db.Add(signal);
            try{await db.SaveChangesAsync();}catch(DbUpdateException){db.Entry(signal).State=EntityState.Detached;}
        }
        return true;
    }
    public void Map(WebApplication app,RouteGroupBuilder api)
    {
        app.MapGet("/t/c/{job:guid}/{signature}",async(Guid job,string signature,string? url,Store db,TimeProvider clock)=>
        {
            if(url is null||url.Length>2000||!EmailRenderer.ValidUrl(url)||!Valid(job,"click:"+url,signature)||!await Record(db,job,"Click",url,signature,clock))return Results.NotFound();
            return Results.Redirect(url);
        });
        app.MapGet("/t/o/{job:guid}/{signature}.gif",async(Guid job,string signature,Store db,TimeProvider clock,HttpResponse response)=>
        {
            if(!Valid(job,"open",signature)||!await Record(db,job,"Open","","",clock))return Results.NotFound();
            response.Headers.CacheControl="no-store, no-cache, max-age=0";response.Headers.Pragma="no-cache";
            return Results.File(Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"),"image/gif");
        });
        api.MapGet("/campaigns/{id:guid}/engagement",async(Guid id,Store db)=>
        {
            var signals=from signal in db.Engagement join job in db.Jobs on signal.JobId equals job.Id where job.CampaignId==id select signal;
            return Results.Ok(new{enabled=await db.Campaigns.AnyAsync(x=>x.Id==id&&x.TrackEngagement),
                openRecipients=await signals.Where(x=>x.Kind=="Open").Select(x=>x.JobId).Distinct().CountAsync(),
                clickRecipients=await signals.Where(x=>x.Kind=="Click").Select(x=>x.JobId).Distinct().CountAsync(),
                links=await signals.Where(x=>x.Kind=="Click").GroupBy(x=>x.Url).Select(x=>new{url=x.Key,recipients=x.Select(s=>s.JobId).Distinct().Count()}).OrderBy(x=>x.url).Take(100).ToListAsync(),
                note="Image loads and link requests are signals. Proxies, scanners, caching and blocked images prevent reliable human-open/read-time measurement."});
        });
    }
}
