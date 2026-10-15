using System.Text.Json.Serialization;
namespace CampaignStudio.Core;

public sealed class SenderProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    [JsonIgnore] public string PasswordCipher { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "";
    public string ReplyTo { get; set; } = "";
    public string ReturnPath { get; set; } = "";
    public int DelayMilliseconds { get; set; } = 1000;
    public int MaxRetries { get; set; } = 3;
    public long? VerifiedAt { get; set; }
    public long NextSendAt { get; set; }
    public string TestResult { get; set; } = "Not tested";
    public int Revision { get; set; }
}
public sealed record SenderInput(string Name, string Host, int Port, string Username, string Password,
    string FromEmail, string FromName, string ReplyTo = "", string ReturnPath = "", int DelayMilliseconds = 1000,
    int MaxRetries = 3, bool ClearPassword = false, int ExpectedRevision = 0);
public sealed class SendAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public int Number { get; set; }
    public long StartedAt { get; set; }
    public long? FinishedAt { get; set; }
    public string Outcome { get; set; } = "Sending";
    public string Detail { get; set; } = "";
}
public sealed class Segment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string RulesJson { get; set; } = "[]";
    public string Match { get; set; } = "All";
}
public sealed record SegmentRule(string Field, string Operator, string Value);
public sealed record SegmentInput(string Name, string RulesJson, string Match = "All");
public sealed record ListMoveInput(string[] Sources, string Destination, Guid? SegmentId = null);
public sealed class Exclusion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Value { get; set; } = "";
    public string Kind { get; set; } = "Email";
}
public sealed record ExclusionInput(string Value, string Kind);
public sealed class ConsentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContactId { get; set; }
    public string Action { get; set; } = "";
    public string Source { get; set; } = "";
    public long At { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
public sealed class LibraryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Snippet";
    public string Category { get; set; } = "General";
    public string Content { get; set; } = "";
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
public sealed record LibraryInput(string Name, string Kind, string Category, string Content);
public sealed class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string RecipientEmail { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}
public sealed record AttachmentInput(string Name, string Base64, string RecipientEmail = "");
public sealed record CalendarEvent(string Title, string Description, string Location, DateTimeOffset Start, DateTimeOffset End);
public sealed record EmailBlock(string Type, string Content = "", string Url = "", string Label = "", int Height = 24);
public sealed record HtmlImportInput(string Name, string Content, string Format = "html");
public sealed record RetryInput(bool ConfirmRetry = false);
