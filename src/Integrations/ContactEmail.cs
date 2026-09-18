using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace framework_backend;

public static class ContactEmail
{
    public static void Configure(WebApplication app)
    {
        app.MapPost("/contact", async (
            HttpRequest request,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger) =>
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

            var apiKey = Environment.GetEnvironmentVariable("SMTP2GO_API_KEY");
            var sender = Environment.GetEnvironmentVariable("CONTACT_EMAIL_FROM");
            var recipient = Environment.GetEnvironmentVariable("CONTACT_EMAIL_TO");

            if (string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(sender) ||
                string.IsNullOrWhiteSpace(recipient))
            {
                logger.LogError("SMTP2GO API contact email configuration is incomplete.");
                return Results.Problem("Contact email is not configured.", statusCode: 503);
            }

            var displayName = string.IsNullOrWhiteSpace(name) ? "Website visitor" : name;
            var emailRequest = new Smtp2GoEmail(
                sender,
                [recipient],
                $"New message from {displayName}",
                BuildHtml(displayName, email, messageText),
                $"Name: {displayName}\nEmail: {email}\n\n{messageText}",
                [new Smtp2GoHeader("Reply-To", email)],
                true);

            try
            {
                var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("X-Smtp2go-Api-Key", apiKey);
                using var response = await client.PostAsJsonAsync(
                    "https://api.smtp2go.com/v3/email/send",
                    emailRequest);

                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError(
                        "SMTP2GO API rejected a contact email with status {StatusCode}: {Details}",
                        response.StatusCode,
                        responseBody);
                    return Results.Problem("Unable to send the message.", statusCode: 502);
                }

                var apiResponse = System.Text.Json.JsonSerializer.Deserialize<Smtp2GoResponse>(responseBody);
                if (apiResponse?.Data?.Failed > 0)
                {
                    logger.LogError("SMTP2GO failed to send a contact email: {Details}", responseBody);
                    return Results.Problem("Unable to send the message.", statusCode: 502);
                }
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(exception, "Could not reach the SMTP2GO API.");
                return Results.Problem("The email service could not be reached.", statusCode: 502);
            }

            return Results.Ok(new { message = "Message sent." });
        });
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

    private sealed record Smtp2GoEmail(
        [property: JsonPropertyName("sender")] string Sender,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html_body")] string HtmlBody,
        [property: JsonPropertyName("text_body")] string TextBody,
        [property: JsonPropertyName("custom_headers")] Smtp2GoHeader[] CustomHeaders,
        [property: JsonPropertyName("fastaccept")] bool FastAccept);

    private sealed record Smtp2GoHeader(
        [property: JsonPropertyName("header")] string Header,
        [property: JsonPropertyName("value")] string Value);

    private sealed record Smtp2GoResponse(
        [property: JsonPropertyName("data")] Smtp2GoResponseData? Data);

    private sealed record Smtp2GoResponseData(
        [property: JsonPropertyName("failed")] int Failed);
}
