using System.Net;
using System.Net.Mail;
using NotificationService.Shell;

namespace NotificationService.Email;

public sealed class SmtpEmailSender(SmtpOptions options) : IEmailSender
{
    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(options.UserName))
        {
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        }

        using var message = new MailMessage(options.FromAddress, recipient, subject, body)
        {
            IsBodyHtml = false
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
