using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordAutoReact
{
    private const ulong ChannelIdAnnouncements = 1518333254480953451;
    private const ulong ChannelIdMemes = 1518347521947340921;

    public static void Configure(DiscordSocketClient client)
    {
        client.MessageReceived += HandleMessageReceived;
    }

    private static async Task HandleMessageReceived(SocketMessage message)
    {
        switch (message.Channel.Id)
        {
            case  ChannelIdAnnouncements:

                await TryReact(message,EmojiId.ReactionThumbsUp, EmojiId.ReactionThumbsDown);
                break;
            
            case ChannelIdMemes:
                
                await TryReact(message,EmojiId.ReactionLaugh);
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