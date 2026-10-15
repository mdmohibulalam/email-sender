using CampaignStudio.Core;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
namespace CampaignStudio.Api;

public sealed record SmtpOutcome(string State, string Detail);
public interface ISmtpTransport
{
    Task<SmtpOutcome> Test(SenderProfile sender, string password, CancellationToken ct);
    Task<SmtpOutcome> Send(SenderProfile sender, string password, MimeMessage message, Func<Task<bool>> maySend, CancellationToken ct);
}
public class SmtpTransport : ISmtpTransport
{
    protected virtual void Configure(SmtpClient client) { }
    private async Task Connect(SmtpClient client, SenderProfile sender, string password, CancellationToken ct)
    {
        Configure(client); client.Timeout = 30000;
        await client.ConnectAsync(sender.Host, sender.Port, sender.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        if (sender.Username.Length > 0) await client.AuthenticateAsync(sender.Username, password, ct);
    }
    public async Task<SmtpOutcome> Test(SenderProfile sender, string password, CancellationToken ct)
    {
        try { using var client = new SmtpClient(); await Connect(client, sender, password, ct); try { await client.DisconnectAsync(true, ct); } catch (Exception) { } return new("Verified", "TLS and authentication verified. No email was sent."); }
        catch (Exception) { return new("ConnectionBlocked", "TLS or authentication failed. Check hostname, port, certificate, and credentials. No email was sent."); }
    }
    public async Task<SmtpOutcome> Send(SenderProfile sender, string password, MimeMessage message, Func<Task<bool>> maySend, CancellationToken ct)
    {
        bool started = false;
        try
        {
            using var client = new SmtpClient(); await Connect(client, sender, password, ct);
            if (!await maySend()) return new("Stopped", "Campaign paused or recipient excluded before dispatch.");
            started = true;
            if (sender.ReturnPath.Length > 0) await client.SendAsync(message, MailboxAddress.Parse(sender.ReturnPath), message.To.Mailboxes, ct);
            else await client.SendAsync(message, ct);
            try { await client.DisconnectAsync(true, ct); } catch (Exception) { }
            return new("Accepted", "SMTP accepted the message. Delivery is not confirmed.");
        }
        catch (MailKit.Security.AuthenticationException) { return new("ConnectionBlocked", "SMTP rejected authentication. Retest this connection."); }
        catch (SslHandshakeException) { return new("ConnectionBlocked", "Required TLS could not be verified. Retest this connection."); }
        catch (SmtpCommandException ex)
        {
            int code = (int)ex.StatusCode;
            if (code is 530 or 535 || ex.ErrorCode == SmtpErrorCode.SenderNotAccepted) return new("ConnectionBlocked", "SMTP rejected the sender or authentication. Retest this connection.");
            if (code is >= 400 and < 500) return new("Transient", "SMTP temporarily rejected the message. A bounded retry is scheduled.");
            if (ex.ErrorCode == SmtpErrorCode.RecipientNotAccepted && System.Text.RegularExpressions.Regex.IsMatch(ex.Message, @"\b5\.1\.1\b")) return new("HardBounce", "SMTP reported that this recipient does not exist (5.1.1).");
            return new("Rejected", "SMTP rejected this message. Review the provider response before retrying.");
        }
        catch (OperationCanceledException) { return new(started ? "Unknown" : "InterruptedBeforeSend", started ? "Interrupted SMTP result. Never automatically retried." : "Stopped before dispatch."); }
        catch (Exception ex) when (!started && ex is IOException or System.Net.Sockets.SocketException or TimeoutException) { return new("Transient", "Connection unavailable before dispatch. A bounded retry is scheduled."); }
        catch (Exception) { return new(started ? "Unknown" : "ConnectionBlocked", started ? "SMTP result uncertain. Check the provider before retrying." : "Unable to prepare the sending connection. Retest it."); }
    }
}
