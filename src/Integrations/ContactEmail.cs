using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace framework_backend;

public static class ContactEmail
{
    public static void Configure(WebApplication app)
    {
        app.MapPost("/contact", async (HttpRequest request, ILogger<Program> logger) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "The request must use a form content type." });
            }

            var form = await request.ReadFormAsync();
            if (!string.IsNullOrWhiteSpace(form["website"]))
            {
                return Results.Ok(new { message = "Message sent." });
            }

            var email = form["email"].ToString().Trim();
            var name = form["name"].ToString().Trim();
            var messageText = form["message"].ToString().Trim();

            if (!IsValidEmail(email) ||
                string.IsNullOrWhiteSpace(messageText) ||
                messageText.Length > 10_000 ||
                name.Length > 200)
            {
                return Results.BadRequest(new { error = "Please provide a valid email address and message." });
            }

            var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
            var smtpPortValue = Environment.GetEnvironmentVariable("SMTP_PORT");
            var smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME");
            var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
            var sender = Environment.GetEnvironmentVariable("CONTACT_EMAIL_FROM");
            var recipient = Environment.GetEnvironmentVariable("CONTACT_EMAIL_TO");

            if (!int.TryParse(smtpPortValue, out var smtpPort) ||
                string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(smtpUsername) ||
                string.IsNullOrWhiteSpace(smtpPassword) ||
                string.IsNullOrWhiteSpace(sender) ||
                string.IsNullOrWhiteSpace(recipient))
            {
                logger.LogError("SMTP contact email configuration is incomplete.");
                return Results.Problem("Contact email is not configured.", statusCode: 503);
            }

            var displayName = string.IsNullOrWhiteSpace(name) ? "Website visitor" : name;
            var emailMessage = BuildMessage(sender, recipient, displayName, email, messageText);

            try
            {
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(smtpUsername, smtpPassword);
                await smtp.SendAsync(emailMessage);
                await smtp.DisconnectAsync(true);
            }
            catch (SmtpCommandException exception)
            {
                logger.LogError(exception, "SMTP2GO rejected a contact email.");
                return Results.Problem("Unable to send the message.", statusCode: 502);
            }
            catch (SmtpProtocolException exception)
            {
                logger.LogError(exception, "SMTP2GO returned an invalid response.");
                return Results.Problem("Unable to send the message.", statusCode: 502);
            }
            catch (MailKit.Security.AuthenticationException exception)
            {
                logger.LogError(exception, "SMTP2GO authentication failed.");
                return Results.Problem("Unable to send the message.", statusCode: 502);
            }

            return Results.Ok(new { message = "Message sent." });
        });
    }

    private static MimeMessage BuildMessage(
        string sender,
        string recipient,
        string name,
        string email,
        string messageText)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(sender));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.ReplyTo.Add(new MailboxAddress(name, email));
        message.Subject = $"New message from {name}";

        var plainText = $"Name: {name}\nEmail: {email}\n\n{messageText}";
        var builder = new BodyBuilder
        {
            TextBody = plainText,
            HtmlBody = BuildHtml(name, email, messageText)
        };

        message.Body = builder.ToMessageBody();
        return message;
    }

    private static bool IsValidEmail(string email) =>
        email.Length <= 320 &&
        new EmailAddressAttribute().IsValid(email);

    private static string BuildHtml(string name, string email, string messageText)
    {
        var logoUrl = Environment.GetEnvironmentVariable("CONTACT_EMAIL_LOGO_URL");
        var logo = string.IsNullOrWhiteSpace(logoUrl)
            ? string.Empty
            : $"<img src=\"{Escape(logoUrl)}\" alt=\"Darketomaly\" width=\"72\" style=\"display:block;border-radius:12px;margin-bottom:24px;\">";

        return $"""
            <!doctype html>
            <html>
              <body style="margin:0;background:#050708;color:#eaf0f2;font-family:Arial,sans-serif;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#050708;padding:32px 12px;">
                  <tr><td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#101416;border:1px solid #38434a;border-radius:14px;padding:32px;">
                      <tr><td>
                        {logo}
                        <div style="color:#8fa0a8;font-size:12px;letter-spacing:2px;text-transform:uppercase;">darketomaly.com</div>
                        <h1 style="margin:10px 0 24px;color:#ffffff;font-size:26px;">New contact message</h1>
                        <p style="margin:0 0 8px;color:#b8c1c8;"><strong style="color:#ffffff;">Name:</strong> {Escape(name)}</p>
                        <p style="margin:0 0 24px;color:#b8c1c8;"><strong style="color:#ffffff;">Email:</strong> <a href="mailto:{Escape(email)}" style="color:#b8c1c8;">{Escape(email)}</a></p>
                        <div style="border-left:3px solid #b8c1c8;padding:4px 0 4px 16px;color:#eaf0f2;line-height:1.6;white-space:pre-wrap;">{Escape(messageText)}</div>
                      </td></tr>
                    </table>
                  </td></tr>
                </table>
              </body>
            </html>
            """;
    }

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);
}
