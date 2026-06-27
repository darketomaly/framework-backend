using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class PlasticDiscordRelay
{
    private const ulong ChannelIdPlastic = 1517147424982958281;
    private const ulong ChannelIdJira    = 1518976935277891594;
    

    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        // Plastic
        app.MapPost("/plastic-discord-webhook", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            var channel = await client.GetChannelAsync(ChannelIdPlastic) as IMessageChannel;

            if (channel == null)
            {
                Console.WriteLine("Plastic channel not found or bot has no access.");
                return Results.Problem("Channel not found");
            }

            await channel.SendMessageAsync(body);
            return Results.Ok();
        });
        
        // Jira
        
        app.MapPost("/jira-discord-webhook", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            var embedBuilder = new EmbedBuilder()
                .WithTitle("hello");

            var embed = embedBuilder.Build();

            var channel = await client.GetChannelAsync(ChannelIdJira) as IMessageChannel;
            if (channel == null)
            {
                Console.WriteLine("Jira channel not found");
                return Results.Problem("Channel not found");
            }

            await channel.SendMessageAsync(embed: embed);
            return Results.Ok();
        });
    }
}