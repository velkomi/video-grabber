using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace VideoGrabber.Platform.Api.Support;

public sealed class SupportMailTransport(SupportOptions options, IConfiguration configuration) : ISupportNotificationTransport
{
    public string Channel => "email";
    public bool Ready => options.Ready && options.MailConfigured;

    public async Task<SupportSendResult> SendAsync(SupportNotification item, CancellationToken ct)
    {
        if (!Ready) return new("confirmed_failure", "smtp_not_configured");
        var host = configuration["VG_SUPPORT_SMTP_HOST"]!;
        var port = int.TryParse(configuration["VG_SUPPORT_SMTP_PORT"], out var parsed) ? parsed : 587;
        var recipient = configuration["VG_SUPPORT_EMAIL_OWNER"] ?? "velkoshkin@gmail.com";
        if (!SupportOptions.ValidEmail(recipient) || port is not (465 or 587))
            return new("confirmed_failure", "smtp_configuration_invalid");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("VideoGrabber", configuration["VG_SUPPORT_SMTP_FROM"]!));
        message.To.Add(new MailboxAddress("", recipient));
        if (SupportOptions.ValidEmail(item.Payload.Contact)) message.ReplyTo.Add(new MailboxAddress("", item.Payload.Contact));
        message.Subject = SupportNotificationText.Subject(item);
        message.MessageId = item.TicketId.ToString("N") + "@" + MailboxAddress.Parse(configuration["VG_SUPPORT_SMTP_FROM"]!).Domain;
        message.Body = new TextPart("plain") { Text = SupportNotificationText.Format(item, false) };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new SmtpClient { Timeout = 15000 };
        var sendStarted = false;
        try
        {
            await client.ConnectAsync(host, port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, deadline.Token);
            await client.AuthenticateAsync(configuration["VG_SUPPORT_SMTP_USERNAME"]!, configuration["VG_SUPPORT_SMTP_PASSWORD"]!, deadline.Token);
            sendStarted = true;
            await client.SendAsync(message, deadline.Token);
            return SupportSendResult.Delivered;
        }
        catch (SslHandshakeException) { return new("confirmed_failure", "smtp_tls_rejected"); }
        catch (MailKit.Security.AuthenticationException) { return new("confirmed_failure", "smtp_auth_rejected"); }
        catch (SmtpCommandException ex)
        { return new("confirmed_failure", "smtp_rejected", (int)ex.StatusCode is >= 400 and < 500); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Net.Sockets.SocketException
            or SmtpProtocolException or MailKit.ServiceNotConnectedException or MailKit.ServiceNotAuthenticatedException)
        { return sendStarted ? new("unknown", "smtp_ack_unknown") : new("confirmed_failure", "smtp_connect_failed", true); }
        finally
        {
            if (client.IsConnected)
                try { await client.DisconnectAsync(true, deadline.Token); } catch (Exception) { /* The send result is already known. */ }
        }
    }
}
