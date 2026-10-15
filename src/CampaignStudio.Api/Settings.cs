namespace CampaignStudio.Api;

public sealed class StudioSettings
{
    public string ApiKey { get; init; } = "";
    public string TokenSecret { get; init; } = "";
    public string DataDirectory { get; init; } = "data";
    public string? Postgres { get; init; }
    public bool EnableLiveSending { get; init; }
    public string PublicBaseUrl { get; init; } = "";
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = "";
    public string SmtpPassword { get; init; } = "";
    public string FromEmail { get; init; } = "";
    public string FromName { get; init; } = "Campaign Studio";
    public int DelayMilliseconds { get; init; } = 1000;
    public int LeaseSeconds { get; init; } = 300;
    public bool StartWorker { get; init; } = true;
    public bool LiveReady => EnableLiveSending && SmtpHost.Length > 0 && Core.ContactRules.ValidEmail(FromEmail)
        && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var u) && u.Scheme == "https";
    public static StudioSettings FromConfiguration(IConfiguration c) => new()
    {
        ApiKey = c["Studio:ApiKey"] ?? "", TokenSecret = c["Studio:TokenSecret"] ?? "",
        DataDirectory = c["Studio:DataDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "data"),
        Postgres = c["Studio:Postgres"], EnableLiveSending = c.GetValue<bool>("Studio:EnableLiveSending"),
        PublicBaseUrl = c["Studio:PublicBaseUrl"] ?? "", SmtpHost = c["Studio:SmtpHost"] ?? "",
        SmtpPort = c.GetValue("Studio:SmtpPort", 587), SmtpUser = c["Studio:SmtpUser"] ?? "",
        SmtpPassword = c["Studio:SmtpPassword"] ?? "", FromEmail = c["Studio:FromEmail"] ?? "",
        FromName = c["Studio:FromName"] ?? "Campaign Studio", DelayMilliseconds = Math.Max(1000, c.GetValue("Studio:DelayMilliseconds", 1000))
    };
}
