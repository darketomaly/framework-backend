using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordAutoReact
{
    private static readonly ConcurrentDictionary<string, string> ReactionCache = new(StringComparer.OrdinalIgnoreCase);

    public static void Configure(DiscordSocketClient client)
    {
        _ = RefreshCacheAsync();
        client.MessageReceived += HandleMessageReceived;
    }

    public static async Task RefreshCacheAsync()
    {
        var values = await DatabaseManager.QueryAllValues(DatabaseTable.AutoReactChannels);
        ReactionCache.Clear();

        foreach (var pair in values)
        {
            ReactionCache[pair.Key] = pair.Value;
        }
    }

    private static async Task HandleMessageReceived(SocketMessage message)
    {
        if (!ReactionCache.TryGetValue(message.Channel.Id.ToString(), out var storedValue) || string.IsNullOrWhiteSpace(storedValue))
        {
            return;
        }

        switch (storedValue.Trim().ToUpperInvariant())
        {
            case "THUMBS":
                await TryReact(message, EmojiId.ReactionThumbsUp, EmojiId.ReactionThumbsDown);
                break;

            case "LAUGH":
                var hasImage = message.Attachments != null && message.Attachments.Count > 0 && message.Attachments.Any(a => a.Width > 0);
                var hasLink = Regex.IsMatch(message.Content, @"https?:\/\/[^\s]+", RegexOptions.IgnoreCase);

                if (hasImage || hasLink)
                {
                    await TryReact(message, EmojiId.ReactionLaugh);
                }

                break;

            case "NONE":
            default:
                break;
        }
    }

    private static async Task TryReact(SocketMessage message, params string[] reactions)
    {
        try
        {
            foreach (var reaction in reactions)
            {
                await message.AddReactionAsync(Emote.Parse(reaction));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error auto-reacting in announcements: {e}");
        }
    }
}