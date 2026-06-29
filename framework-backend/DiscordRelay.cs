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

    private static string GetJsonPropertyString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out var queriedElement);

        if (queriedElement.ValueKind is JsonValueKind.String)
        {
            var parsedStr = queriedElement.GetString();
            return string.IsNullOrEmpty(parsedStr) ? string.Empty : parsedStr;
        }
        else
        {
            return string.Empty;
        }
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
                
                var content = GetJsonPropertyString(json, "content");
                var email = GetJsonPropertyString(json, "PLASTIC_USER");
                var branch = GetJsonPropertyString(json, "PLASTIC_FULL_BRANCH_NAME");
                var comment = GetJsonPropertyString(json, "PLASTIC_COMMENT");
                var label = GetJsonPropertyString(json, "PLASTIC_LABEL_NAME");
                var changesetId = GetJsonPropertyString(json, "PLASTIC_CHANGESET_ID");
                var changesetNumber = GetJsonPropertyString(json, "PLASTIC_CHANGESET_NUMBER");

                var userName = email switch
                {
                    "create@darketomaly.hk" => "Darketomaly",
                    _ => email
                };

                var embed = new EmbedBuilder();
                var description = string.Empty;
                
                if (content.StartsWith("New checkin"))
                {
                    embed.WithTitle($"<:dark_dev_checkin:1521226679408922704> New checkin to {branch}");
                    
                    if (!string.IsNullOrEmpty(comment))
                    {
                        var emoji = string.Empty;
                        
                        if (comment.StartsWith("Merge from"))
                        {
                            emoji = ":arrows_clockwise:";
                        }
                        else if (comment.StartsWith("Subtractive merge"))
                        {
                            emoji = ":leftwards_arrow_with_hook:";
                        }
                        else if (comment.StartsWith("Cherry pick"))
                        {
                            emoji = ":cherries:";
                        }
                        
                        description = $"{emoji} {comment}";
                    }
                }
                else if (content.StartsWith("New branch"))
                {
                    embed.WithTitle($":twisted_rightwards_arrows: New branch {branch} created");
                    description = comment;
                }
                else if (content.StartsWith("New label"))
                {
                    embed.WithTitle($":label: New label {label} created");
                    description = comment;
                }
                else
                {
                    embed.WithTitle($"Unknown");
                    description = $"Please define what type of webhook this is: \n\n{body}";
                    
                    // To do
                    // New repo
                }

                // --- 

                if (!string.IsNullOrEmpty(changesetId))
                {
                    description = $"`{changesetId}`\n{description}";
                }
                else if (!string.IsNullOrEmpty(changesetNumber))
                {
                    description = $"`{changesetNumber}`\n{description}";
                }
                
                embed.WithDescription(description);
                embed.WithFooter(userName, GravatarHelper.GetGravatarUrl(email));
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