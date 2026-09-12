namespace framework_backend;

public static class Redirects
{
    public static void Configure(WebApplication app)
    {
        app.MapGet("/install-discord-bot", () =>
        {
            var discordOAuthUrl = Environment.GetEnvironmentVariable("DISCORD_OAUTH_URL");
            
            // To do
            // Add logging or analytics here before redirecting

            return Results.Redirect(discordOAuthUrl);
        });
    }
}