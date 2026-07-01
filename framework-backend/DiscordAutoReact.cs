using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordAutoReact
{
    private const ulong ChannelIdAnnouncements = 1518333254480953451;

    private static readonly Emote ThumbsUpEmote = Emote.Parse("<:thumbs_up:1521877716453032047>");
    private static readonly Emote ThumbsDownEmote = Emote.Parse("<:thumbs_down:1521877749856337940>");

    public static void Configure(DiscordSocketClient client)
    {
        client.MessageReceived += HandleMessageReceived;
    }

    private static async Task HandleMessageReceived(SocketMessage message)
    {
        //Console.WriteLine(message.ToString());
        
        if (message.Channel.Id != ChannelIdAnnouncements)
        {
            return;
        }
        
        try
        {
            await message.AddReactionAsync(ThumbsUpEmote);
            await message.AddReactionAsync(ThumbsDownEmote);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error auto-reacting in announcements: {e}");
        }
    }
}