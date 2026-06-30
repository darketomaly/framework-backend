using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordAutoReact
{
    private const ulong ChannelIdAnnouncements = 1518333254480953451;

    private static readonly Emote ThumbsUpEmote = Emote.Parse("<:darkthumbsup:1521505142090760244>");
    private static readonly Emote ThumbsDownEmote = Emote.Parse("<:darkthumbsdown:1521505179541704784>");

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
            await message.AddReactionAsync(ThumbsUpEmote);
            await message.AddReactionAsync(ThumbsDownEmote);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error auto-reacting in announcements: {e}");
        }
    }
}