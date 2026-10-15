using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MailKit.Net.Pop3;
using MailKit.Security;

namespace CampaignStudio.Api;

public sealed record InboxMessage(string Uid,MimeMessage Message);
public interface IInboxReader
{
    Task<bool> Test(InboxProfile profile,string password,CancellationToken ct);
    Task<List<InboxMessage>> Read(InboxProfile profile,string password,string[] processed,CancellationToken ct);
}
public sealed class Pop3Reader : IInboxReader
{
    private static async Task Connect(Pop3Client client,InboxProfile profile,string password,CancellationToken ct)
    {client.Timeout=20000;await client.ConnectAsync(profile.Host,profile.Port,profile.Port==995?SecureSocketOptions.SslOnConnect:SecureSocketOptions.StartTls,ct);await client.AuthenticateAsync(profile.Username,password,ct);}
    public async Task<bool> Test(InboxProfile p,string password,CancellationToken ct)
    {try{using var client=new Pop3Client();await Connect(client,p,password,ct);if(!client.Capabilities.HasFlag(Pop3Capabilities.UIDL))return false;await client.DisconnectAsync(true,ct);return true;}catch(Exception){return false;}}
    public async Task<List<InboxMessage>> Read(InboxProfile profile,string password,string[] processed,CancellationToken ct)
    {
        using var client=new Pop3Client();await Connect(client,profile,password,ct);
        if(!client.Capabilities.HasFlag(Pop3Capabilities.UIDL))throw new InvalidOperationException("Stable POP3 UIDL identifiers are required.");
        var uids=await client.GetMessageUidsAsync(ct);var known=processed.ToHashSet(StringComparer.Ordinal);var result=new List<InboxMessage>();long total=0;
        for(int i=0;i<uids.Count&&result.Count<100;i++)
        {if(known.Contains(uids[i]))continue;int size=await client.GetMessageSizeAsync(i,ct);if(size>1_000_000)continue;total+=size;if(total>10_000_000)break;result.Add(new(uids[i],await client.GetMessageAsync(i,ct)));}
        await client.DisconnectAsync(true,ct);return result;
    }
}
public static class Inboxes
{
    private static object Public(InboxProfile p)=>new{p.Id,p.Name,p.Host,p.Port,p.Username,p.SignupFormId,p.AutoPoll,p.VerifiedAt,p.NextPollAt,p.Detail,p.Revision,hasPassword=p.PasswordCipher.Length>0};
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/inboxes",async(Store db)=>(await db.Inboxes.OrderBy(x=>x.Name).ToListAsync()).Select(Public));
        api.MapPost("/inboxes",async(InboxInput i,Store db,CredentialVault vault)=>{var p=new InboxProfile();await Apply(p,i,db,vault);db.Add(p);await db.SaveChangesAsync();return Results.Ok(Public(p));});
        api.MapPut("/inboxes/{id:guid}",async(Guid id,InboxInput i,Store db,CredentialVault vault)=>
        {
            await using var tx=await db.Database.BeginTransactionAsync();if(await db.Inboxes.Where(x=>x.Id==id&&x.Revision==i.ExpectedRevision).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Revision,x=>x.Revision+1))==0)return Results.Conflict(new{error="Inbox changed. Reload before editing."});var p=(await db.Inboxes.FindAsync(id))!;await Apply(p,i,db,vault);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(Public(p));
        });
        api.MapPost("/inboxes/{id:guid}/test",async(Guid id,Store db,CredentialVault vault,IInboxReader reader,TimeProvider clock,CancellationToken ct)=>
        {
            var p=await db.Inboxes.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id,ct);if(p is null)return Results.NotFound();bool valid=await reader.Test(p,vault.Open(p.PasswordCipher),ct);long? at=valid?clock.GetUtcNow().ToUnixTimeSeconds():null;string detail=valid?"TLS, authentication and UIDL verified. No messages changed.":"POP3 TLS, authentication, or UIDL verification failed.";
            int changed=await db.Inboxes.Where(x=>x.Id==id&&x.Revision==p.Revision).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.VerifiedAt,at).SetProperty(x=>x.Detail,detail),ct);return changed>0?Results.Ok(new{verified=valid,detail}):Results.Conflict(new{error="Inbox changed during testing. Test again."});
        });
        api.MapPost("/inboxes/{id:guid}/poll",async(Guid id,Store db,CredentialVault vault,IInboxReader reader,TimeProvider clock,CancellationToken ct)=>Results.Ok(await Poll(db,id,vault,reader,clock.GetUtcNow().ToUnixTimeSeconds(),ct)));
        api.MapGet("/inboxes/{id:guid}/activity",async(Guid id,Store db)=>await db.InboxReceipts.Where(x=>x.InboxId==id).OrderByDescending(x=>x.At).Take(100).ToListAsync());
    }
    private static async Task Apply(InboxProfile p,InboxInput i,Store db,CredentialVault vault)
    {
        if(i.Name is null||i.Name.Length is <1 or >250||i.Host is null||i.Host.Length>253||Uri.CheckHostName(i.Host)==UriHostNameType.Unknown||i.Port is <1 or >65535||i.Username is null||i.Username.Length is <1 or >500||i.Password is null||i.Password.Length>2000||new[]{i.Name,i.Host,i.Username,i.Password}.Any(x=>x.Contains('\r')||x.Contains('\n')))throw new FormatException("Check POP3 name, host, port and credentials.");
        if(i.SignupFormId.HasValue&&!await db.Forms.AnyAsync(x=>x.Id==i.SignupFormId))throw new FormatException("Selected signup form was not found.");
        p.Name=i.Name;p.Host=i.Host;p.Port=i.Port;p.Username=i.Username;p.SignupFormId=i.SignupFormId;p.AutoPoll=i.AutoPoll;p.VerifiedAt=null;p.Detail="Changed; test before polling.";if(i.Password.Length>0)p.PasswordCipher=vault.Seal(i.Password);if(p.PasswordCipher.Length==0)throw new FormatException("Enter the mailbox password.");
    }
    public static async Task<object> Poll(Store db,Guid id,CredentialVault vault,IInboxReader reader,long now,CancellationToken ct)
    {
        var p=await db.Inboxes.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id,ct)??throw new FormatException("Inbox not found.");
        if(p.VerifiedAt is null)throw new FormatException("Test this inbox before polling.");
        if(await db.Inboxes.Where(x=>x.Id==id&&x.VerifiedAt!=null&&x.NextPollAt<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextPollAt,now+60),ct)==0)throw new FormatException("This inbox was polled recently. Wait one minute.");
        List<InboxMessage> messages;
        try{var processed=await db.InboxReceipts.Where(x=>x.InboxId==id).Select(x=>x.Uid).ToArrayAsync(ct);messages=await reader.Read(p,vault.Open(p.PasswordCipher),processed,ct);}
        catch(Exception)when(!ct.IsCancellationRequested){await db.Inboxes.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.VerifiedAt,(long?)null).SetProperty(x=>x.Detail,"Polling failed. Check TLS and mailbox credentials, then test again."),ct);throw new FormatException("Mailbox polling failed. No remote messages were deleted.");}
        int processedCount=0;
        foreach(var entry in messages)
        {
            if(entry.Uid.Length>500)continue;
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            if(await db.InboxReceipts.AnyAsync(x=>x.InboxId==id&&x.Uid==entry.Uid,ct))continue;
            string action="Ignored";string subject=(entry.Message.Subject??"").Trim();var from=entry.Message.From.Mailboxes.ToArray();
            if(from.Length==1&&ContactRules.ValidEmail(from[0].Address)&&subject.Equals("Unsubscribe",StringComparison.OrdinalIgnoreCase))
            {
                var contact=await db.Contacts.FirstOrDefaultAsync(x=>x.Email==ContactRules.Normalize(from[0].Address),ct);
                if(contact is not null){await Suppress(db,contact,"Mailbox unsubscribe",ct);action="Unsubscribed";}
            }
            else if(from.Length==1&&subject.Equals("Subscribe",StringComparison.OrdinalIgnoreCase)&&p.SignupFormId is Guid formId&&ContactRules.ValidEmail(from[0].Address))
            {
                var form=await db.Forms.FindAsync([formId],ct);if(form?.Enabled==true){await SignupForms.Request(db,form,ContactRules.Normalize(from[0].Address),"",now);action="Confirmation requested";}
            }
            else
            {
                var statuses=entry.Message.BodyParts.OfType<MessageDeliveryStatus>().SelectMany(x=>x.StatusGroups).ToArray();
                var ids=entry.Message.References.Concat(entry.Message.BodyParts.OfType<MessagePart>().Where(x=>x.Message is not null).Select(x=>x.Message!.MessageId)).Append(entry.Message.InReplyTo).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
                foreach(var group in statuses)
                {
                    if(group["Status"]?.Trim()!="5.1.1"||group["Action"]?.Trim().ToLowerInvariant()!="failed")continue;
                    string email=ContactRules.Normalize((group["Final-Recipient"]??"").Split(';').Last().Trim());
                    var job=await db.Jobs.AsNoTracking().FirstOrDefaultAsync(x=>x.Email==email&&ids.Contains(x.MessageId)&&(x.State=="Accepted"||x.State=="Unknown"),ct);
                    if(job is not null){var contact=await db.Contacts.FindAsync([job.ContactId],ct);if(contact is not null){await Suppress(db,contact,"Matched delivery-status hard bounce (5.1.1)",ct);action="Hard bounce suppressed";}}
                }
            }
            db.InboxReceipts.Add(new(){InboxId=id,Uid=entry.Uid,Action=action,At=now});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);processedCount++;
        }
        await db.Inboxes.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Detail,"Processed "+processedCount+" new messages. Remote messages retained."),ct);return new{processed=processedCount};
    }
    private static async Task Suppress(Store db,Contact contact,string reason,CancellationToken ct)
    {contact.Suppressed=true;contact.SuppressionReason=reason;AudienceRules.Log(db,contact.Id,"Unsubscribed",reason);await db.SaveChangesAsync(ct);await db.Jobs.Where(x=>x.ContactId==contact.Id&&(x.State=="Pending"||x.State=="Deferred")).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Suppressed"),ct);}
    public static async Task ProcessOne(Store db,IServiceProvider services,long now,CancellationToken ct)
    {
        var id=await db.Inboxes.Where(x=>x.AutoPoll&&x.VerifiedAt!=null&&x.NextPollAt<=now).OrderBy(x=>x.NextPollAt).Select(x=>(Guid?)x.Id).FirstOrDefaultAsync(ct);
        if(id.HasValue)await Poll(db,id.Value,services.GetRequiredService<CredentialVault>(),services.GetRequiredService<IInboxReader>(),now,ct);
    }
}
