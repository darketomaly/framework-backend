using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordRelay
{
    private const ulong ChannelIdPlastic = 1517147424982958281;
    private const ulong ChannelIdJira = 1518976935277891594;

    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        JiraMap(app, client);
        PlasticMap(app, client);
    }
    
    private static void JiraMap(WebApplication app, DiscordSocketClient client)
    {
        // Jira

        app.MapPost("/jira-discord-webhook", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            try
            {
                using var doc = JsonDocument.Parse(body);
                var embedJson = doc.RootElement.GetProperty("embeds")[0];
                var footerJson = embedJson.GetProperty("footer");

                string iconUrl = footerJson.GetProperty("icon_url").GetString()?.Trim() ?? "";

                var embed = new EmbedBuilder()
                    .WithTitle(embedJson.GetProperty("title").GetString())
                    .WithDescription(embedJson.GetProperty("description").GetString())
                    .WithColor(2303786)
                    .WithFooter(footer =>
                    {
                        footer.Text = footerJson.GetProperty("text").GetString();
                        if (!string.IsNullOrEmpty(iconUrl))
                            footer.IconUrl = iconUrl;
                    })
                    .Build();

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
                Console.WriteLine($"Error processing Jira webhook: {e}");
                return Results.Problem("Failed to process embed");
            }
        });
    }

    private static void PlasticMap(WebApplication app, DiscordSocketClient client)
    {
        // Plastic
        app.MapPost("/plastic-discord-webhook", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();

            try
            {
                // -------
                using var doc = JsonDocument.Parse(body);
                var json = doc.RootElement;
                var user = json.GetProperty("PLASTIC_USER").GetString();
                var content = json.GetProperty("content").GetString();
                var comment = json.GetProperty("PLASTIC_COMMENT").GetString();
                var branch = json.GetProperty("PLASTIC_BRANCH_NAME").GetString();

                var embed = new EmbedBuilder();
                
                // To do
                // Switch statement, depending on type of webhook (checkin, merge, branch created etc) is the title/description

                if (content.StartsWith("New checkin"))
                {
                    embed.WithTitle(branch);
                    embed.WithDescription(comment);
                }

                // --- 
                
                embed.WithFooter(user);
                embed.WithColor(2303786);
                
                
                // -------

                var channel = await client.GetChannelAsync(ChannelIdPlastic) as IMessageChannel;

                if (channel == null)
                {
                    Console.WriteLine("Jira channel not found");
                    return Results.Problem("Channel not found");
                }

                await channel.SendMessageAsync(embed: embed.Build());
                return Results.Ok();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing Jira webhook: {e}");
                return Results.Problem("Failed to process embed");
            }
        });
    }
}