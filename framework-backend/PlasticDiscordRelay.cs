namespace framework_backend;

public static class PlasticDiscordRelay
{
    private static readonly string DiscordWebhookUrl = 
        "https://discord.com/api/webhooks/1520380727416455228/-DNNo2-xCfYlRt8zow4lv99-SJgL7ggmcas6qqO1kIjF3awpPWflP3CE0tjX5yxXRa84";

    public static void Configure(WebApplication app)
    {
        app.MapPost("/plastic-discord-webhook", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(DiscordWebhookUrl, new { content = body });

            return Results.Ok();
        });
    }
}