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

            // Parse the Discord-style embed JSON you receive
            using var doc = JsonDocument.Parse(body);
            var embedData = doc.RootElement.GetProperty("embeds")[0];

            var embedBuilder = new EmbedBuilder()
                .WithTitle(embedData.GetProperty("title").GetString())
                .WithDescription(embedData.GetProperty("description").GetString())
                .WithColor(uint.Parse(embedData.GetProperty("color").GetString()!)) // convert string to uint
                .WithFooter(footer =>
                {
                    var footerData = embedData.GetProperty("footer");
                    footer.Text = footerData.GetProperty("text").GetString();
                    footer.IconUrl = footerData.GetProperty("icon_url").GetString();
                });

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