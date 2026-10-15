using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
using MimeKit;
namespace CampaignStudio.Api;
public sealed class ImageAssets(StudioSettings settings)
{
    private static bool Raster(string mime)=>mime is "image/png" or "image/jpeg" or "image/gif" or "image/webp";
    private string Token(Guid id)=>Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret),Encoding.ASCII.GetBytes("image:"+id.ToString("N"))));
    public string Url(Guid id)=>"/media/"+id.ToString("N")+".png?token="+Token(id);
    private bool Valid(Guid id,string token)=>token.Length==64&&CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(token),Encoding.ASCII.GetBytes(Token(id)));
    public void Map(WebApplication app,RouteGroupBuilder api)
    {
        api.MapGet("/images",async(Store db)=>(await db.Attachments.Where(x=>x.ContentType=="image/png").OrderBy(x=>x.Name).ToListAsync()).Select(x=>new{x.Id,x.Name,x.Size,url=Url(x.Id)}));
        api.MapPost("/images",async(AttachmentInput i,Store db)=>
        {
            if(i.Name is null||i.Name.Length is <1 or >200||i.Name.IndexOfAny(['\r','\n','/','\\'])>=0||i.Base64 is null||i.Base64.Length>6_700_000)throw new FormatException("Choose a named PNG under 5 MB.");
            byte[] bytes=Convert.FromBase64String(i.Base64);if(bytes.Length>5_000_000)throw new FormatException("Image exceeds 5 MB.");PngValidator.Check(bytes);
            var asset=new Attachment{Name=i.Name,ContentType="image/png",Size=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes))};
            string folder=Path.Combine(settings.DataDirectory,"attachments");Directory.CreateDirectory(folder);string path=Path.Combine(folder,asset.Id.ToString("N"));
            await File.WriteAllBytesAsync(path+".tmp",bytes);File.Move(path+".tmp",path);
            try{db.Add(asset);await db.SaveChangesAsync();}catch{File.Delete(path);throw;}
            return Results.Ok(new{asset.Id,asset.Name,asset.Size,url=Url(asset.Id)});
        });
        app.MapGet("/media/{name}.png",async(string name,string? token,Store db)=>
        {
            if(!Guid.TryParseExact(name,"N",out var id)||!Valid(id,token??""))return Results.NotFound();
            var asset=await db.Attachments.FindAsync(id);if(asset is null||!Raster(asset.ContentType))return Results.NotFound();
            string path=Path.Combine(settings.DataDirectory,"attachments",id.ToString("N"));return File.Exists(path)?Results.File(path,asset.ContentType):Results.NotFound();
        });
    }
    public async Task<string> Embed(string html,BodyBuilder body,Store db,CancellationToken ct)
    {
        var links=Regex.Matches(html,@"\/media\/([a-f0-9]{32})\.png\?token=([A-F0-9]{64})").Cast<Match>().DistinctBy(x=>x.Value).ToArray();
        foreach(var link in links)
        {
            var id=Guid.ParseExact(link.Groups[1].Value,"N");if(!Valid(id,link.Groups[2].Value))throw new FormatException("Invalid image link.");
            var asset=await db.Attachments.FindAsync([id],ct)??throw new FormatException("Image missing.");
            byte[] bytes=await File.ReadAllBytesAsync(Path.Combine(settings.DataDirectory,"attachments",id.ToString("N")),ct);
            if(!Raster(asset.ContentType)||asset.Sha256!=Convert.ToHexString(SHA256.HashData(bytes)))throw new FormatException("Image integrity check failed.");
            var part=body.LinkedResources.Add(asset.Name,bytes,ContentType.Parse(asset.ContentType));part.ContentId=id.ToString("N")+"@campaign-studio.local";html=html.Replace(link.Value,"cid:"+part.ContentId,StringComparison.Ordinal);
        }
        return html;
    }
}
