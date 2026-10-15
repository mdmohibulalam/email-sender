namespace CampaignStudio.Core;

public sealed class Contact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Company { get; set; } = "";
    public string ListName { get; set; } = "Main audience";
    public string ConsentSource { get; set; } = "";
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public bool Suppressed { get; set; }
    public string SuppressionReason { get; set; } = "";
}

public sealed class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled campaign";
    public string BlocksJson { get; set; } = "";
    public string SourceHtml { get; set; } = "";
    public Guid? SegmentId { get; set; }
    public Guid? HistoryCampaignId { get; set; }
    public string HistoryMode { get; set; } = "All";
    public string AttachmentIdsJson { get; set; } = "[]";
    public string CalendarJson { get; set; } = "";
    public string UtmCampaign { get; set; } = "";
    public bool TrackEngagement { get; set; }
    public string TagsJson { get; set; } = "{}";
    public int Revision { get; set; }
    public Guid? SenderId { get; set; }
    public string SenderEmail { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string ReplyTo { get; set; } = "";
    public string ReturnPath { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Preheader { get; set; } = "";
    public string Heading { get; set; } = "Hello {{firstName}}";
    public string Body { get; set; } = "Share your news with your audience.";
    public string ButtonLabel { get; set; } = "Visit our website";
    public string ButtonUrl { get; set; } = "https://example.com";
    public string Footer { get; set; } = "Your company · Your postal address";
    public string Accent { get; set; } = "#176b56";
    public string ListName { get; set; } = "Main audience";
    public string State { get; set; } = "Draft";
    public string Mode { get; set; } = "Capture";
    public long? ScheduledAt { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public sealed class RecipientJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Guid ContactId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string State { get; set; } = "Pending";
    public string MessageId { get; set; } = "";
    public long? StartedAt { get; set; }
    public long? FinishedAt { get; set; }
    public string Detail { get; set; } = "";
    public int Attempts { get; set; }
    public int RetryCount { get; set; }
    public long? NextAttemptAt { get; set; }
}

public sealed record ContactInput(string Email, string FirstName = "", string LastName = "",
    string Company = "", string ListName = "Main audience", string ConsentSource = "", bool ConsentConfirmed = false);
public sealed record ImportInput(string Csv, int EmailColumn = 0, int FirstNameColumn = 1,
    int LastNameColumn = 2, int CompanyColumn = 3, bool HasHeader = true,
    string ListName = "Main audience", string ConsentSource = "", bool ConsentConfirmed = false);
public sealed record ImportIssue(int Row, string Email, string Reason);
public sealed record ScheduleInput(long ScheduledAt, string Mode = "Capture", bool ConfirmLiveSend = false, Guid? SenderId = null);
public sealed record CampaignInput(string Name, string Subject, string Preheader, string Heading, string Body,
    string ButtonLabel, string ButtonUrl, string Footer, string Accent, string ListName,
    string BlocksJson = "", string SourceHtml = "", Guid? SegmentId = null, Guid? HistoryCampaignId = null,
    string HistoryMode = "All", string AttachmentIdsJson = "[]", string CalendarJson = "", string UtmCampaign = "", int ExpectedRevision = 0, string TagsJson = "{}", bool TrackEngagement = false);
public sealed record SuppressionInput(string Reason = "Manually excluded");

public static class ContactRules
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
    public static bool ValidEmail(string email) => email.Length <= 254 && !email.Any(char.IsWhiteSpace)
        && System.Net.Mail.MailAddress.TryCreate(email, out var parsed) && parsed.Address == email
        && email.Contains('@') && email[(email.LastIndexOf('@') + 1)..].Contains('.');
    public static string CsvCell(string value)
    {
        // CSV opened in a spreadsheet must not execute contact data as formulas.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
