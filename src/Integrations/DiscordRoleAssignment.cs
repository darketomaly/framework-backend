using System.Text.RegularExpressions;
using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordRoleAssignment
{
    public static void Configure(DiscordSocketClient client)
    {
        client.ReactionAdded += (message, channel, reaction) =>
            HandleReactionAsync(client, message, channel, reaction, assign: true);
        client.ReactionRemoved += (message, channel, reaction) =>
            HandleReactionAsync(client, message, channel, reaction, assign: false);
    }

    private static async Task HandleReactionAsync(
        DiscordSocketClient client,
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction,
        bool assign)
    {
        var emoji = reaction.Emote.ToString();

        if (!EmojiId.RoleEmojis.Contains(emoji))
        {
            return;
        }

        if (cachedMessage.HasValue && cachedMessage.Value.Author.Id != client.CurrentUser.Id)
        {
            return;
        }

        var message = await cachedMessage.GetOrDownloadAsync();

        if (message is null || message.Author.Id != client.CurrentUser.Id)
        {
            return;
        }

        var roleId = FindRoleId(message.Content, emoji);

        if (roleId is null)
        {
            return;
        }

        var channel = await cachedChannel.GetOrDownloadAsync();

        if (channel is not SocketGuildChannel guildChannel)
        {
            return;
        }

        var guildUser = guildChannel.Guild.GetUser(reaction.UserId);

        if (guildUser is null || guildUser.Id == client.CurrentUser.Id)
        {
            return;
        }

        var role = guildChannel.Guild.GetRole(roleId.Value);

        if (role is null)
        {
            return;
        }

        try
        {
            if (assign)
            {
                await guildUser.AddRoleAsync(role);
            }
            else
            {
                await guildUser.RemoveRoleAsync(role);
            }
        }
        catch (HttpException exception)
        {
            Console.WriteLine(
                $"Unable to {(assign ? "assign" : "remove")} role {role.Id} for user {guildUser.Id}: {exception.Message}");
        }
    }

    private static ulong? FindRoleId(string content, string emoji)
    {
        var pattern = $@"(?m)^{Regex.Escape(emoji)}\s+<@&(?<roleId>\d+)>\s*$";
        var match = Regex.Match(content, pattern);

        return match.Success && ulong.TryParse(match.Groups["roleId"].Value, out var roleId)
            ? roleId
            : null;
    }
}