using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CampaignStudio.Api;

public sealed class SignupTokens(StudioSettings settings)
{
    public string Create(SignupRequest request)
    {
        string payload=$"signup:{request.Id:N}:{request.ExpiresAt}";
        string signature=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret),Encoding.UTF8.GetBytes(payload)));
        return $"{request.Id:N}.{request.ExpiresAt}.{signature}";
    }
    public bool Valid(string token, SignupRequest request) => token.Length<150&&CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(token),Encoding.ASCII.GetBytes(Create(request)));
}
public static class SignupForms
{
    private static IResult Page(string title,string body) => Results.Content("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>"+WebUtility.HtmlEncode(title)+"</title></head><body style='max-width:520px;margin:60px auto;padding:24px;font:17px/1.6 system-ui'><h1>"+WebUtility.HtmlEncode(title)+"</h1>"+body+"</body></html>","text/html");
    public static void Map(WebApplication app,RouteGroupBuilder api)
    {
        api.MapGet("/forms",async(Store db)=>await db.Forms.OrderBy(x=>x.Name).ToListAsync());
        api.MapPost("/forms",async(SignupFormInput i,Store db)=>
        {
            if(i.Name is null||i.Name.Trim().Length is <1 or >250||i.ListName is null||i.ListName.Trim().Length is <1 or >250||!await db.Senders.AnyAsync(x=>x.Id==i.SenderId))throw new FormatException("Enter a form name, list and sending connection.");
            var form=new SignupForm{Name=i.Name.Trim(),ListName=i.ListName.Trim(),SenderId=i.SenderId,Enabled=i.Enabled};db.Add(form);await db.SaveChangesAsync();return Results.Ok(form);
        });
        api.MapPost("/forms/{id:guid}/toggle",async(Guid id,Store db)=>{var form=await db.Forms.FindAsync(id);if(form is null)return Results.NotFound();form.Enabled=!form.Enabled;await db.SaveChangesAsync();return Results.Ok(form);});
        api.MapGet("/forms/requests",async(Store db)=>await db.Signups.OrderByDescending(x=>x.CreatedAt).Take(200).ToListAsync());
        app.MapGet("/f/{id:guid}",async(Guid id,Store db)=>
        {
            var form=await db.Forms.FindAsync(id);if(form is null||!form.Enabled)return Results.NotFound();
            return Page(form.Name,$"<form method='post' action='/f/{id}'><p><label>Email<br><input type='email' name='email' required maxlength='254'></label></p><p><label>First name<br><input name='firstName' maxlength='250'></label></p><div style='display:none'><label>Leave empty<input name='website' tabindex='-1' autocomplete='off'></label></div><p><label><input type='checkbox' name='consent' required>I want to subscribe and can unsubscribe at any time.</label></p><button>Send confirmation email</button></form>");
        });
        app.MapPost("/f/{id:guid}",async(Guid id,HttpRequest request,Store db,TimeProvider clock)=>
        {
            if(!request.HasFormContentType||request.ContentLength>5000)return Results.BadRequest();var values=await request.ReadFormAsync();
            var form=await db.Forms.FindAsync(id);if(form is null||!form.Enabled)return Results.NotFound();
            string email=ContactRules.Normalize(values["email"].ToString()),first=values["firstName"].ToString();
            if(values["website"].ToString().Length==0&&values["consent"].ToString()=="on"&&ContactRules.ValidEmail(email)&&first.Length<=250)
                await Request(db,form,email,first,clock.GetUtcNow().ToUnixTimeSeconds());
            return Page("Check your inbox","<p>If this request can be processed, you will receive a confirmation link. Your subscription changes only after you confirm.</p>");
        });
        static Guid? Id(string token) { var part=token.Split('.')[0];return Guid.TryParseExact(part,"N",out var id)?id:null; }
        app.MapGet("/subscribe/confirm/{token}",async(string token,Store db,SignupTokens tokens,TimeProvider clock)=>
        {
            Guid? id=Id(token);var signup=id.HasValue?await db.Signups.FindAsync(id.Value):null;
            if(signup is null||!tokens.Valid(token,signup)||signup.ExpiresAt<clock.GetUtcNow().ToUnixTimeSeconds())return Page("Link expired","<p>Request a new confirmation from the signup form.</p>");
            return Page("Confirm your subscription","<p>Confirm that you want to receive this newsletter.</p><form method='post'><button>Confirm subscription</button></form>");
        });
        app.MapPost("/subscribe/confirm/{token}",async(string token,Store db,SignupTokens tokens,TimeProvider clock)=>
        {
            Guid? id=Id(token);if(!id.HasValue)return Results.BadRequest();
            await using var tx=await db.Database.BeginTransactionAsync();
            var signup=await db.Signups.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);if(signup is null||!tokens.Valid(token,signup)||signup.ExpiresAt<clock.GetUtcNow().ToUnixTimeSeconds()||!await db.Forms.AnyAsync(x=>x.Id==signup.FormId&&x.Enabled))return Results.BadRequest();
            int locked=await db.Signups.Where(x=>x.Id==signup.Id&&x.State!="Confirmed"&&x.State!="Expired").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Confirmed"));
            if(locked>0)
            {
                var contact=await db.Contacts.FirstOrDefaultAsync(x=>x.Email==signup.Email);
                if(contact is null){contact=new Contact{Email=signup.Email,FirstName=signup.FirstName,ListName=signup.ListName,ConsentSource="Confirmed signup form "+signup.FormId};db.Add(contact);}
                else {contact.Suppressed=false;contact.SuppressionReason="";contact.ListName=signup.ListName;contact.ConsentSource="Confirmed signup form "+signup.FormId;}
                AudienceRules.Log(db,contact.Id,"Subscribed",contact.ConsentSource);await db.SaveChangesAsync();
            }
            await tx.CommitAsync();return Page("Subscription confirmed","<p>Thank you. You can unsubscribe from any newsletter.</p>");
        });
    }
    public static async Task Request(Store db,SignupForm form,string email,string first,long now)
    {
        var existing=await db.Signups.FirstOrDefaultAsync(x=>x.FormId==form.Id&&x.Email==email);
        if(existing is not null&&existing.CreatedAt>now-86400)return;
        if(await db.Signups.CountAsync(x=>x.State=="Pending")>=10000)return;
        if(existing is null){existing=new SignupRequest{FormId=form.Id,Email=email};db.Add(existing);}
        existing.FirstName=first;existing.ListName=form.ListName;existing.State="Pending";existing.CreatedAt=now;existing.ExpiresAt=now+2*86400;existing.NextAttemptAt=now;existing.Attempts=0;existing.Detail="Awaiting confirmation dispatch";await db.SaveChangesAsync();
    }
    public static async Task ProcessOne(Store db,StudioSettings settings,IServiceProvider services,long now,CancellationToken ct)
    {
        await db.Signups.Where(x=>x.ExpiresAt<now&&(x.State=="Pending"||x.State=="Sent")).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Expired"),ct);
        if(!settings.EnableLiveSending||!settings.PublicLinksReady)return;
        await db.Signups.Where(x=>x.State=="Sending"&&x.NextAttemptAt<now-settings.LeaseSeconds).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Unknown").SetProperty(x=>x.Detail,"Confirmation send interrupted; never automatically resent."),ct);
        var request=await db.Signups.AsNoTracking().Where(x=>x.State=="Pending"&&x.NextAttemptAt<=now&&x.ExpiresAt>=now&&db.Forms.Any(f=>f.Id==x.FormId&&f.Enabled&&db.Senders.Any(s=>s.Id==f.SenderId&&s.VerifiedAt!=null))).OrderBy(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
        if(request is null)return;var form=(await db.Forms.FindAsync(request.FormId))!;
        var sender=(await db.Senders.AsNoTracking().FirstAsync(x=>x.Id==form.SenderId,ct));
        await using(var tx=await db.Database.BeginTransactionAsync(ct))
        {
            if(await db.Senders.Where(x=>x.Id==sender.Id&&x.VerifiedAt!=null&&x.NextSendAt<=now*1000).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextSendAt,now*1000+sender.DelayMilliseconds),ct)==0)return;
            int active = await db.Jobs.CountAsync(j=>j.State=="Sending"&&db.Campaigns.Any(c=>c.Id==j.CampaignId&&c.SenderId==sender.Id),ct)+await db.Signups.CountAsync(r=>r.State=="Sending"&&db.Forms.Any(f=>f.Id==r.FormId&&f.SenderId==sender.Id),ct);
            if(active>=sender.MaxConnections)return;
            if(await db.Signups.Where(x=>x.Id==request.Id&&x.State=="Pending"&&x.NextAttemptAt<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Sending").SetProperty(x=>x.NextAttemptAt,now).SetProperty(x=>x.Attempts,x=>x.Attempts+1),ct)==0)return;
            await tx.CommitAsync(ct);
        }
        var message=new MimeMessage{MessageId=$"signup.{request.Id:N}.{request.CreatedAt}@{sender.FromEmail.Split('@').Last()}",Subject="Confirm your subscription"};message.From.Add(new MailboxAddress(sender.FromName,sender.FromEmail));message.To.Add(MailboxAddress.Parse(request.Email));
        string link=settings.PublicBaseUrl.TrimEnd('/')+"/subscribe/confirm/"+services.GetRequiredService<SignupTokens>().Create(request);
        message.Body=new TextPart("plain"){Text="Confirm your request to subscribe to "+form.Name+":\n"+link+"\n\nIf you did not request this, ignore this email. The link expires in 48 hours."};
        SmtpOutcome result;
        try{string password=sender.PasswordCipher.Length>0?services.GetRequiredService<CredentialVault>().Open(sender.PasswordCipher):"";result=await services.GetRequiredService<ISmtpTransport>().Send(sender,password,message,async()=>await db.Forms.AnyAsync(x=>x.Id==form.Id&&x.Enabled)&&await db.Signups.AnyAsync(x=>x.Id==request.Id&&x.State=="Sending"),ct);}
        catch(Exception){result=new("ConnectionBlocked","Unable to prepare the confirmation sending connection.");}
        string state=result.State=="Accepted"?"Sent":result.State=="Transient"&&request.Attempts<sender.MaxRetries?"Pending":result.State is "Unknown"?"Unknown":result.State is "Stopped" or "InterruptedBeforeSend"?"Pending":"Failed";
        await db.Signups.Where(x=>x.Id==request.Id&&x.State=="Sending").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,state).SetProperty(x=>x.Detail,result.Detail).SetProperty(x=>x.NextAttemptAt,now+Math.Min(3600,settings.RetryBaseSeconds*(1L<<Math.Min(request.Attempts,5)))));
    }
}
