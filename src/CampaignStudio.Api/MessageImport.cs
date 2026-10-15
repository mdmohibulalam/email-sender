using System.Net;
using System.Security.Cryptography;
using System.Text;
using AngleSharp.Html.Parser;
using CampaignStudio.Core;
using MimeKit;
namespace CampaignStudio.Api;
public static class MessageImport
{
    private sealed record Imported(Attachment Asset,byte[] Bytes,string? Cid);
    private static bool Raster(string type,byte[] b)=>type switch
    {
        "image/png"=>b.Length>8&&b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),
        "image/jpeg"=>b.Length>3&&b[0]==255&&b[1]==216&&b[2]==255,
        "image/gif"=>b.Length>6&&Encoding.ASCII.GetString(b,0,6) is "GIF87a" or "GIF89a",
        "image/webp"=>b.Length>12&&Encoding.ASCII.GetString(b,0,4)=="RIFF"&&Encoding.ASCII.GetString(b,8,4)=="WEBP",_=>false
    };
    public static async Task<object> Import(HtmlImportInput input,Store db,StudioSettings settings,ImageAssets images,CancellationToken ct)
    {
        if(input.Name is null||input.Name.Length>250||input.Content is null||input.Content.Length>2_000_000||input.Base64 is null||input.Base64.Length>2_700_000||input.Format is not ("html" or "eml"))throw new FormatException("Import HTML under 500 KB or EML under 2 MB.");
        if(input.Format=="html")return new{name=input.Name,subject="",sourceHtml=input.Content,safePreview=EmailDesign.CleanHtml(input.Content),attachments=Array.Empty<object>()};
        byte[] raw=input.Base64.Length>0?Convert.FromBase64String(input.Base64):Encoding.UTF8.GetBytes(input.Content);if(raw.Length>2_000_000)throw new FormatException("EML exceeds 2 MB.");
        using var stream=new MemoryStream(raw);var message=await MimeMessage.LoadAsync(stream,ct);
        string html=message.HtmlBody??"<p>"+WebUtility.HtmlEncode(message.TextBody??"").Replace("\n","<br>")+"</p>";
        var doc=new HtmlParser().ParseDocument(html);
        var referenced=doc.QuerySelectorAll("img[src]").Select(x=>x.GetAttribute("src")??"").Where(x=>x.StartsWith("cid:",StringComparison.OrdinalIgnoreCase)).Select(x=>Uri.UnescapeDataString(x[4..]).Trim('<','>')).ToHashSet(StringComparer.Ordinal);
        var files=new List<Imported>();long total=0;
        foreach(var part in message.BodyParts.OfType<MimePart>())
        {
            bool inline=part.ContentId is not null&&referenced.Contains(part.ContentId);if(!inline&&!part.IsAttachment)continue;
            if(part.Content is null)throw new FormatException("Imported MIME file is empty.");
            using var decoded=new MemoryStream();await part.Content.DecodeToAsync(decoded,ct);byte[] bytes=decoded.ToArray();total+=bytes.Length;
            if(bytes.Length>5_000_000||total>20_000_000||files.Count>=20)throw new FormatException("Imported MIME files exceed their limits.");
            string type=part.ContentType.MimeType.ToLowerInvariant();if(inline&&!Raster(type,bytes))throw new FormatException("Embedded image format is unsupported.");
            if(inline&&type=="image/png")PngValidator.Check(bytes);
            string name=Path.GetFileName(part.FileName??(inline?"embedded-image":"attachment.bin")).Replace('\\','_').Replace('/','_').Replace('\r','_').Replace('\n','_');if(name.Length>200)name=name[..200];if(string.IsNullOrWhiteSpace(name))name="attachment.bin";
            var asset=new Attachment{Name=name,ContentType=inline?type:"application/octet-stream",Size=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes))};files.Add(new(asset,bytes,inline?part.ContentId:null));
        }
        foreach(var part in message.Attachments.OfType<MessagePart>())
        {
            if(part.Message is null)continue;using var decoded=new MemoryStream();await part.Message.WriteToAsync(decoded,ct);byte[] bytes=decoded.ToArray();total+=bytes.Length;
            if(bytes.Length>5_000_000||total>20_000_000||files.Count>=20)throw new FormatException("Imported attachments exceed their limits.");
            files.Add(new(new Attachment{Name="forwarded-message.eml",Size=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes))},bytes,null));
        }
        foreach(var img in doc.QuerySelectorAll("img[src]"))
        {
            string src=img.GetAttribute("src")??"";if(!src.StartsWith("cid:",StringComparison.OrdinalIgnoreCase))continue;
            string cid=Uri.UnescapeDataString(src[4..]).Trim('<','>');var file=files.FirstOrDefault(x=>x.Cid==cid)??throw new FormatException("A referenced embedded image is missing.");img.SetAttribute("src",images.Url(file.Asset.Id));
        }
        html=doc.DocumentElement.OuterHtml;EmailDesign.CleanHtml(html);string folder=Path.Combine(settings.DataDirectory,"attachments");Directory.CreateDirectory(folder);var written=new List<string>();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach(var file in files){string path=Path.Combine(folder,file.Asset.Id.ToString("N"));await File.WriteAllBytesAsync(path,file.Bytes,ct);written.Add(path);db.Attachments.Add(file.Asset);}
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        catch{foreach(var path in written)File.Delete(path);throw;}
        return new{name=input.Name,subject=message.Subject??"",sourceHtml=html,safePreview=EmailDesign.CleanHtml(html),attachments=files.Where(x=>x.Cid is null).Select(x=>new{x.Asset.Id,x.Asset.Name})};
    }
}
