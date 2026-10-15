using DnsClient;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Net;
using System.Net.Sockets;
namespace CampaignStudio.Api;
public sealed class NoMailServerException:Exception;
public static class DirectDelivery
{
    private static readonly LookupClient Resolver=new(new LookupClientOptions{Timeout=TimeSpan.FromSeconds(5),Retries=1,UseCache=true});
    public static string[] Rank(IEnumerable<(int Preference,string Host)> records,string domain)
    {
        var mx=records.OrderBy(x=>x.Preference).ToArray();if(mx.Any(x=>x.Host=="."))throw new NoMailServerException();
        return mx.Length==0?[domain]:mx.Select(x=>x.Host.TrimEnd('.')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static async Task Connect(SmtpClient client,string domain,CancellationToken ct)
    {
        IDnsQueryResponse response;
        try{response=await Resolver.QueryAsync(domain,QueryType.MX,cancellationToken:ct);}catch(Exception)when(!ct.IsCancellationRequested){throw new IOException("MX lookup unavailable.");}
        if(response.HasError)throw new IOException("MX lookup failed.");
        var hosts=Rank(response.Answers.MxRecords().Select(x=>((int)x.Preference,x.Exchange.Value)),domain);
        foreach(var host in hosts.Take(10))
        {
            Socket? socket=null;
            try
            {
                var addresses=await Dns.GetHostAddressesAsync(host,ct);
                if(addresses.Length==0||addresses.Any(x=>!PublicLinkChecker.PublicAddress(x)))throw new IOException("MX target is not public.");
                socket=new Socket(SocketType.Stream,ProtocolType.Tcp);
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await socket.ConnectAsync(addresses,25,timeout.Token);await client.ConnectAsync(socket,host,25,SecureSocketOptions.StartTls,timeout.Token);return;
            }
            catch(Exception ex)when(!ct.IsCancellationRequested&&ex is IOException or SocketException or TimeoutException or OperationCanceledException or SslHandshakeException){socket?.Dispose();}
        }
        throw new IOException("No public MX could be reached with required TLS.");
    }
}
