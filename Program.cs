var builder = WebApplication.CreateBuilder(args);

// Add HttpClient for sending requests to Discord
builder.Services.AddHttpClient();

var app = builder.Build();

// Get Discord webhook URL from configuration
var discordWebhookUrl = builder.Configuration["Discord:WebhookUrl"];

if (string.IsNullOrEmpty(discordWebhookUrl))
{
    throw new Exception("Discord webhook URL is missing. Add it to appsettings.json or as an environment variable.");
}

// Simple relay endpoint
app.MapPost("/plastic-webhook", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    // Read the body sent by Plastic
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest("Empty body received");

    // Forward to Discord
    var client = httpClientFactory.CreateClient();

    var payload = new
    {
        content = body.Length > 1900 
            ? body.Substring(0, 1900) + "..." 
            : body
    };

    var response = await client.PostAsJsonAsync(discordWebhookUrl, payload);

    return response.IsSuccessStatusCode 
        ? Results.Ok("Sent to Discord") 
        : Results.Problem("Failed to send to Discord");
});

app.Run();