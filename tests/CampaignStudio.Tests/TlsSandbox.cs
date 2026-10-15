using CampaignStudio.Api;
using CampaignStudio.Core;
using MailKit.Net.Smtp;
using MimeKit;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

public sealed class TlsSandbox : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback,0);
    private readonly CancellationTokenSource stopping = new();
    private readonly X509Certificate2 certificate;
    private readonly Task loop;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public string Mode {get;set;}="Accept";
    public int Messages {get;private set;}
    public TlsSandbox()
    {
        using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");request.CertificateExtensions.Add(san.Build());request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection{new Oid("1.3.6.1.5.5.7.3.1")},false));using var issued=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(2));certificate=X509CertificateLoader.LoadPkcs12(issued.Export(X509ContentType.Pfx),null,OperatingSystem.IsWindows()?X509KeyStorageFlags.UserKeySet:X509KeyStorageFlags.EphemeralKeySet);listener.Start();loop=Loop();
    }
    private async Task Loop()
    {
        try{while(!stopping.IsCancellationRequested){var socket=await listener.AcceptTcpClientAsync(stopping.Token);_=Handle(socket);}}catch(OperationCanceledException){}catch(SocketException)when(stopping.IsCancellationRequested){}
    }
    private async Task Handle(TcpClient socket)
    {
        using(socket)
        try
        {
            Stream stream=socket.GetStream();StreamReader reader=new(stream,Encoding.ASCII,false,1024,true);StreamWriter writer=new(stream,Encoding.ASCII,1024,true){NewLine="\r\n",AutoFlush=true};
            await writer.WriteLineAsync("220 localhost sandbox");
            while(!stopping.IsCancellationRequested)
            {
                string? line=await reader.ReadLineAsync(stopping.Token);if(line is null)return;
                string command=line.Split(' ')[0].ToUpperInvariant();
                switch(command)
                {
                    case "EHLO":await writer.WriteAsync("250-localhost\r\n250-STARTTLS\r\n250 AUTH PLAIN\r\n");await writer.FlushAsync();break;
                    case "HELO":await writer.WriteLineAsync("250 localhost");break;
                    case "STARTTLS":
                        await writer.WriteLineAsync("220 Begin TLS");reader.Dispose();writer.Dispose();var tls=new SslStream(stream,false);await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=System.Security.Authentication.SslProtocols.Tls12|System.Security.Authentication.SslProtocols.Tls13},stopping.Token);stream=tls;reader=new(stream,Encoding.ASCII,false,1024,true);writer=new(stream,Encoding.ASCII,1024,true){NewLine="\r\n",AutoFlush=true};break;
                    case "AUTH":if(line.Split(' ').Length<3){await writer.WriteLineAsync("334 ");await reader.ReadLineAsync(stopping.Token);}await writer.WriteLineAsync(Mode=="AuthFailure"?"535 5.7.8 Authentication rejected":"235 2.7.0 Authenticated");break;
                    case "MAIL":await writer.WriteLineAsync("250 2.1.0 Sender accepted");break;
                    case "RCPT":await writer.WriteLineAsync(Mode=="Missing"?"550 5.1.1 No such recipient":Mode=="Policy"?"550 5.7.1 Policy rejection":"250 2.1.5 Recipient accepted");break;
                    case "DATA":
                        await writer.WriteLineAsync("354 End with a dot");while((line=await reader.ReadLineAsync(stopping.Token)) is not null&&line!="."){}Messages++;
                        if(Mode=="Drop")return;await writer.WriteLineAsync(Mode=="Temporary"?"450 4.2.0 Try later":"250 2.0.0 Accepted");if(Mode=="CloseAfterAccept")return;break;
                    case "QUIT":await writer.WriteLineAsync("221 Goodbye");return;
                    case "RSET":case "NOOP":await writer.WriteLineAsync("250 OK");break;
                    default:await writer.WriteLineAsync("502 Not implemented");break;
                }
            }
        }
        catch(Exception)when(!Environment.HasShutdownStarted){}
    }
    public sealed class Pinned(string thumbprint):SmtpTransport
    {protected override void Configure(SmtpClient client)=>client.ServerCertificateValidationCallback=(_,cert,_,_)=>cert?.GetCertHashString()==thumbprint;}
    public async Task Run(Action<bool,string> check)
    {
        var sender=new SenderProfile{Host="localhost",Port=Port,Username="sandbox",FromEmail="sender@example.com",FromName="Sandbox"};
        check((await new SmtpTransport().Test(sender,"not-real",CancellationToken.None)).State=="ConnectionBlocked","Production SMTP transport rejects an untrusted TLS certificate");
        var transport=new Pinned(certificate.Thumbprint);check((await transport.Test(sender,"not-real",CancellationToken.None)).State=="Verified"&&Messages==0,"TLS sandbox connection test authenticates without sending a message");
        var message=new MimeMessage{Subject="Sandbox",Body=new TextPart("plain"){Text="No relay"}};message.From.Add(MailboxAddress.Parse(sender.FromEmail));message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        async Task<SmtpOutcome> Send()=>await transport.Send(sender,"not-real",message,()=>Task.FromResult(true),CancellationToken.None);
        foreach(var test in new[]{("CloseAfterAccept","Accepted"),("Temporary","Transient"),("Missing","HardBounce"),("Policy","Rejected"),("AuthFailure","ConnectionBlocked"),("Drop","Unknown")}){Mode=test.Item1;check((await Send()).State==test.Item2,"Real TLS sandbox classifies "+test.Item1+" as "+test.Item2);}
        Mode="Accept";int before=Messages;check((await transport.Send(sender,"not-real",message,()=>Task.FromResult(false),CancellationToken.None)).State=="Stopped"&&Messages==before,"Final dispatch gate prevents DATA on a real authenticated TLS connection");
    }
    public async ValueTask DisposeAsync(){stopping.Cancel();listener.Stop();await loop;certificate.Dispose();stopping.Dispose();}
}
