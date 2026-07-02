using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class EmojiId
{
    public const string PlasticSubtractiveMerge = "<:plastic_subtractive_merge:1521878716102479922>";
    public const string PlasticMergeFrom = "<:plastic_merge_from:1521877966945259722>";
    public const string PlasticLabel = "<:plastic_label:1521877265779265617>";
    public const string PlasticNewBranch = "<:plastic_new_branch:1521877222456164522>";
    public const string PlasticCherryPick = "<:plastic_cherry_pick:1521877174603219044>";
    public const string PlasticCheckin = "<:plastic_checkin:1521865803602067529>";
    
    public const string TaskRevisit = "<:task_revisit:1521878670166327426>";
    public const string TaskCreated = "<:task_created:1521877022945841242>";
    public const string TaskReadyForReview = "<:task_ready_for_review:1521876982655221790>";
    public const string TaskRejected = "<:task_rejected:1521876911318634536>";
    public const string TaskApproved = "<:task_approved:1521876764002091078>";
    
    public const string SprintCompleted = "<:sprint_completed:1521876730195873983>";
    public const string SprintStart = "<:sprint_start:1521876688458481816>";
    
    public const string ReactionThumbsUp = "<:thumbs_up:1521877716453032047>";
    public const string ReactionThumbsDown = "<:thumbs_down:1521877749856337940>";
}

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
                var json = doc.RootElement;
                
                // -- Get title and description given the json --

                var evt = GetJsonPropertyString(json, "event");
                var initiator = GetJsonPropertyString(json, "initiator_display_name");
                var issueKey = GetJsonPropertyString(json, "issue_key");
                var issueName = GetJsonPropertyString(json, "issue_name");
                var initiatorIcon = GetJsonPropertyString(json, "initiator_icon");
                var versionReleased = GetJsonPropertyString(json, "version_released");
                var sprintName = GetJsonPropertyString(json, "sprint_name");
                var rejectionReason = GetJsonPropertyString(json, "rejection_reason");

                var title = string.Empty;
                var description = string.Empty;

                switch (evt)
                {
                    case "discord-issue-created":
                        title = $"{EmojiId.TaskCreated} {initiator} created {issueKey}";
                        description = $"{issueName}";
                        break;
                        
                    case "discord-issue-start":
                        title = $"{initiator} started working on {issueKey}";
                        description = $"{issueName}";
                        break;
                        
                    case "discord-issue-ready-for-review":
                        title = $"{EmojiId.TaskReadyForReview} {initiator} has marked {issueKey} ready for review";
                        description = $"{issueName}";
                        break;
                        
                    case "discord-issue-rejected":
                        title = $"{EmojiId.TaskRejected} {initiator} has rejected {issueKey}";
                        description = $"{description}\n\n**Rejection reason:**\n```{rejectionReason}```";
                        break;
                        
                    case "discord-issue-revision":
                        title = $"{EmojiId.TaskRevisit} {initiator} is revisiting {issueKey}";
                        description = $"{description}";
                        break;
                        
                    case "discord-issue-approved":
                        title = $"{EmojiId.TaskApproved} {issueKey} has been approved by {initiator}";
                        description = $"{issueName}";
                        break;
                        
                    case "discord-sprint-start":
                        title = $"{EmojiId.SprintStart} {sprintName} has started";
                        break;
                        
                    case "discord-sprint-completed":
                        title = $"{EmojiId.SprintCompleted} {sprintName} has been completed";
                        break;
                        
                    case "discord-version-released":
                        title = $"{EmojiId.SprintCompleted} {versionReleased} has been released";
                        break;
                }
                
                // -- Build the embed given the title and description --
                
                var embed = new EmbedBuilder();
                
                embed.WithTitle(title);
                embed.WithDescription(description);
                embed.WithFooter(initiator, initiatorIcon);
                embed.WithColor(2303786);
                
                // -- Send message --
                
                var channel = await client.GetChannelAsync(ChannelIdJira) as IMessageChannel;

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
                    embed.WithTitle($"{EmojiId.PlasticCheckin} New checkin to {branch}");
                    
                    if (!string.IsNullOrEmpty(comment))
                    {
                        var emoji = string.Empty;
                        
                        if (comment.StartsWith("Merge from"))
                        {
                            emoji = EmojiId.PlasticMergeFrom;
                        }
                        else if (comment.StartsWith("Subtractive merge"))
                        {
                            emoji = EmojiId.PlasticSubtractiveMerge;
                        }
                        else if (comment.StartsWith("Cherry pick"))
                        {
                            emoji = EmojiId.PlasticCherryPick;
                        }
                        
                        description = $"{emoji} {comment}";
                    }
                }
                else if (content.StartsWith("New branch"))
                {
                    embed.WithTitle($"{EmojiId.PlasticNewBranch} New branch {branch} created");
                    description = comment;
                }
                else if (content.StartsWith("New label"))
                {
                    embed.WithTitle($"{EmojiId.PlasticLabel} New label {label} created");
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