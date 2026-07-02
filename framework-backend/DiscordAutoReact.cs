using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordAutoReact
{
    private const ulong ChannelIdAnnouncements = 1518333254480953451;

    public static void Configure(DiscordSocketClient client)
    {
        client.MessageReceived += HandleMessageReceived;
    }

    private static async Task HandleMessageReceived(SocketMessage message)
    {
        if (message.Channel.Id != ChannelIdAnnouncements)
        {
            return;
        }
        
        try
        {
            await message.AddReactionAsync(Emote.Parse(EmojiId.ReactionThumbsUp));
            await message.AddReactionAsync(Emote.Parse(EmojiId.ReactionThumbsDown));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error auto-reacting in announcements: {e}");
        }
    }
}