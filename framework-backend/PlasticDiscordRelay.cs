using System.Text.Json;
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
            
            try
            {
                using var doc = JsonDocument.Parse(body);
                var embedJson = doc.RootElement.GetProperty("embeds")[0];
                var footer = embedJson.GetProperty("footer");

                var embedBuilder = new EmbedBuilder()
                    .WithTitle(embedJson.GetProperty("title").GetString())
                    .WithDescription(embedJson.GetProperty("description").GetString())
                    .WithFooter(footer.GetProperty("text").GetString(), footer.GetProperty("icon_url").GetString())
                    .WithColor(2303786);

                var embed = embedBuilder.Build();

                var channel = await client.GetChannelAsync(ChannelIdJira) as IMessageChannel;
                
                if (channel == null)
                {
                    Console.WriteLine("Jira channel not found");
                    return Results.Problem("Channel not found");
                }
            
                await channel.SendMessageAsync(embed: embed);
                return Results.Ok();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        });
    }
}