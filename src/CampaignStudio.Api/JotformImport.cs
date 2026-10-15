using System.Text.Json;
using System.Text.RegularExpressions;
using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;
namespace CampaignStudio.Api;

public sealed record JotformInput(string ApiKey,string FormId,string Region="standard",string EmailField="",string FirstNameField="",string OptOutField="",string OptOutValue="Unsubscribe",int Offset=0,string ListName="Main audience",string ConsentSource="",bool ConsentConfirmed=false);
public sealed record FormImportRow(string Email,string FirstName,bool Unsubscribe);
public sealed class JotformClient(HttpClient client)
{
    private static string Host(string region)=>region switch{"standard"=>"api.jotform.com","eu"=>"eu-api.jotform.com","hipaa"=>"hipaa-api.jotform.com",_=>throw new FormatException("Choose a supported Jotform region.")};
    private async Task<JsonElement> Fetch(JotformInput input,string suffix,CancellationToken ct)
    {
        if(input.ApiKey is null||!Regex.IsMatch(input.ApiKey,@"^[a-zA-Z0-9]{20,200}$")||input.FormId is null||!Regex.IsMatch(input.FormId,@"^\d{5,30}$")||input.Offset is <0 or >1000000)throw new FormatException("Check your Jotform API key, form ID and offset.");
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://"+Host(input.Region)+"/form/"+input.FormId+suffix);request.Headers.Add("APIKEY",input.ApiKey);
        try
        {
            using var response=await client.SendAsync(request,ct);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if(doc.RootElement.GetProperty("responseCode").GetInt32()!=200)throw new HttpRequestException();return doc.RootElement.GetProperty("content").Clone();
        }
        catch(Exception)when(!ct.IsCancellationRequested){throw new FormatException("Jotform request failed. Check the key, region, form access and API quota.");}
    }
    public async Task<object[]> Questions(JotformInput input,CancellationToken ct)
    {var content=await Fetch(input,"/questions",ct);return content.EnumerateObject().Select(x=>(object)new{id=x.Name,label=x.Value.TryGetProperty("text",out var text)?text.GetString():x.Name,type=x.Value.TryGetProperty("type",out var type)?type.GetString():""}).ToArray();}
    public async Task<List<FormImportRow>> Rows(JotformInput input,CancellationToken ct)
    {
        if(input.EmailField is null||!Regex.IsMatch(input.EmailField,@"^\d{1,20}$")||input.FirstNameField is null||input.OptOutField is null||input.OptOutValue is null||input.OptOutValue.Length>250)throw new FormatException("Select an email answer field.");
        var content=await Fetch(input,"/submissions?limit=500&offset="+input.Offset+"&orderby=id",ct);return Parse(content,input);
    }
    public static List<FormImportRow> Parse(JsonElement content,JotformInput input)
    {
        string Answer(JsonElement answers,string id)
        {
            if(id.Length==0||!answers.TryGetProperty(id,out var field)||!field.TryGetProperty("answer",out var value))return "";
            if(value.ValueKind==JsonValueKind.String)return value.GetString()??"";
            if(value.ValueKind==JsonValueKind.Object&&value.TryGetProperty("first",out var first))return first.GetString()??"";
            if(value.ValueKind==JsonValueKind.Array)return string.Join(",",value.EnumerateArray().Select(x=>x.ToString()));return value.ToString();
        }
        var rows=new List<FormImportRow>();foreach(var submission in content.EnumerateArray().Take(500))
        {if(!submission.TryGetProperty("answers",out var answers))continue;rows.Add(new(ContactRules.Normalize(Answer(answers,input.EmailField)),Answer(answers,input.FirstNameField),input.OptOutField.Length>0&&Answer(answers,input.OptOutField).Trim().Equals(input.OptOutValue,StringComparison.OrdinalIgnoreCase)));}return rows;
    }
}
public static class JotformImport
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/jotform/questions",async(JotformInput input,JotformClient client,CancellationToken ct)=>Results.Ok(await client.Questions(input,ct)));
        api.MapPost("/jotform/preview",async(JotformInput input,JotformClient client,CancellationToken ct)=>{var rows=await client.Rows(input,ct);return Results.Ok(new{rows,nextOffset=input.Offset+rows.Count,hasMore=rows.Count==500});});
        api.MapPost("/jotform/import",async(JotformInput input,JotformClient client,Store db,CancellationToken ct)=>
        {
            if(!input.ConsentConfirmed||input.ConsentSource is null||input.ConsentSource.Trim().Length is <1 or >250||input.ListName is null||input.ListName.Trim().Length is <1 or >250)throw new FormatException("Record the permission source and confirm importing this Jotform audience.");
            var rows=await client.Rows(input,ct);int added=0,duplicates=0,unsubscribed=0,rejected=0;await using var tx=await db.Database.BeginTransactionAsync(ct);
            var emails=rows.Select(x=>x.Email).Distinct().ToArray();var contacts=new Dictionary<string,Contact>();foreach(var batch in emails.Chunk(256))foreach(var contact in await db.Contacts.Where(x=>batch.Contains(x.Email)).ToListAsync(ct))contacts[contact.Email]=contact;
            foreach(var row in rows)
            {
                if(!ContactRules.ValidEmail(row.Email)||row.FirstName.Length>250){rejected++;continue;}
                if(!contacts.TryGetValue(row.Email,out var contact)){contact=new Contact{Email=row.Email,FirstName=row.FirstName,ListName=input.ListName,ConsentSource=input.ConsentSource};contacts[row.Email]=contact;db.Add(contact);AudienceRules.Log(db,contact.Id,"Subscribed",input.ConsentSource);added++;}else duplicates++;
                if(row.Unsubscribe){contact.Suppressed=true;contact.SuppressionReason="Jotform opt-out";AudienceRules.Log(db,contact.Id,"Unsubscribed","Jotform form "+input.FormId);await db.Jobs.Where(x=>x.ContactId==contact.Id&&(x.State=="Pending"||x.State=="Deferred")).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Suppressed"),ct);unsubscribed++;}
            }
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(new{added,duplicates,unsubscribed,rejected,nextOffset=input.Offset+rows.Count,hasMore=rows.Count==500});
        });
    }
}
