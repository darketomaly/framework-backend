using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class PlasticDiscordRelay
{
    private const ulong ChannelIdPlastic = 1517147424982958281;
    private const ulong ChannelIdJira = 1518976935277891594;

    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        // Plastic
        
        app.MapPost("/plastic-discord-webhook", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            // Send message using the bot
            var channel = await client.GetChannelAsync(ChannelIdPlastic) as IMessageChannel;

            if (channel != null)
            {
                await channel.SendMessageAsync(body);
            }

            return Results.Ok();
        });
        
        // Jira
        
        app.MapPost("/jira-discord-webhook", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            // Send message using the bot
            var channel = await client.GetChannelAsync(ChannelIdJira) as IMessageChannel; //

            if (channel != null)
            {
                await channel.SendMessageAsync(body);
            }

            return Results.Ok();
        });
    }
}