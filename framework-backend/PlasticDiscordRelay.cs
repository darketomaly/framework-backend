using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class PlasticDiscordRelay
{
    private static readonly ulong TargetChannelId = 1517147424982958281; 

    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        app.MapPost("/plastic-discord-webhook", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            // Send message using the bot
            var channel = await client.GetChannelAsync(TargetChannelId) as IMessageChannel;

            if (channel != null)
            {
                await channel.SendMessageAsync(body);
            }

            return Results.Ok();
        });
    }
}