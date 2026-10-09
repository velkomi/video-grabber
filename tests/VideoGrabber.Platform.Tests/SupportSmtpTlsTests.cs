using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using VideoGrabber.Platform.Api.Support;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class SupportSmtpTlsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task Invalid_smtp_port_is_rejected_before_network_access(int port)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["VG_SUPPORT_ENABLED"]="true",["VG_SUPPORT_SMTP_HOST"]="127.0.0.1",["VG_SUPPORT_SMTP_PORT"]=port.ToString(),
          ["VG_SUPPORT_SMTP_USERNAME"]="synthetic-user",["VG_SUPPORT_SMTP_PASSWORD"]="synthetic-password",
          ["VG_SUPPORT_SMTP_FROM"]="sender@example.test" }).Build();
        var result=await new SupportMailTransport(new SupportOptions(config),config).SendAsync(
            new SupportNotification(Guid.NewGuid(),Guid.NewGuid(),1,"other","form",DateTimeOffset.UtcNow,
                new SupportPayload("client@example.test","Synthetic message")),CancellationToken.None);
        Assert.Equal("smtp_configuration_invalid",result.Code);
    }

    [Fact]
    public async Task Smtp_untrusted_certificate_is_rejected_before_credentials_or_message()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var smtpPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        var commands = new List<string>();
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(deadline.Token);
            var stream = peer.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost synthetic SMTP");
            commands.Add((await reader.ReadLineAsync(deadline.Token))!);
            await writer.WriteAsync("250-localhost\r\n250-STARTTLS\r\n250 AUTH PLAIN\r\n");
            commands.Add((await reader.ReadLineAsync(deadline.Token))!);
            await writer.WriteLineAsync("220 Start TLS");
            using var tls = new SslStream(stream, leaveInnerStreamOpen: true);
            try { await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, deadline.Token); }
            catch (Exception ex) when (ex is AuthenticationException or IOException or OperationCanceledException) { }
        }, deadline.Token);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VG_SUPPORT_ENABLED"] = "true", ["VG_SUPPORT_SMTP_HOST"] = "127.0.0.1",
                ["VG_SUPPORT_SMTP_PORT"] = smtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture), ["VG_SUPPORT_SMTP_USERNAME"] = "synthetic-user",
                ["VG_SUPPORT_SMTP_PASSWORD"] = "synthetic-not-a-real-password", ["VG_SUPPORT_SMTP_FROM"] = "sender@example.test"
            }).Build();
            var transport = new SupportMailTransport(new SupportOptions(configuration), configuration);
            var result = await transport.SendAsync(new SupportNotification(Guid.NewGuid(), Guid.NewGuid(), 1,
                "other", "form", DateTimeOffset.UtcNow, new SupportPayload("client@example.test", "Synthetic support message.")), deadline.Token);
            Assert.Equal("confirmed_failure", result.State);
            Assert.Equal("smtp_tls_rejected", result.Code);
            await server;
            Assert.Contains(commands, command => command.StartsWith("EHLO", StringComparison.Ordinal));
            Assert.DoesNotContain(commands, command => command.StartsWith("AUTH", StringComparison.Ordinal));
            Assert.DoesNotContain(commands, command => command.StartsWith("MAIL", StringComparison.Ordinal));
        }
        finally { deadline.Cancel(); listener.Stop(); }
    }
}
