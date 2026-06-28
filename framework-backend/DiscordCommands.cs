using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordCommands
{
    public static void Configure(DiscordSocketClient client)
    {
        client.Ready += async () => await RegisterCommands(client);
        client.SlashCommandExecuted += HandleSlashCommand;
    }

    private static async Task RegisterCommands(DiscordSocketClient client)
    {
        var pingCommand = new SlashCommandBuilder()
            .WithName("ping")
            .WithDescription("Replies with pong");

        var sendMsgCommand = new SlashCommandBuilder()
            .WithName("sendmsg")
            .WithDescription("Sends a message to a specific channel")
            .WithDefaultMemberPermissions(GuildPermission.ManageMessages)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("The channel to send the message to")
                .WithType(ApplicationCommandOptionType.Channel)
                .AddChannelType(ChannelType.News)
                .AddChannelType(ChannelType.Text)
                .WithRequired(true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("message")
                .WithDescription("The message to send")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        try
        {
            await client.Rest.BulkOverwriteGlobalCommands(new[]
            {
                pingCommand.Build(),
                sendMsgCommand.Build()
            });
        }
        catch (HttpException ex)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ex.Errors));
        }
    }

    private static async Task HandleSlashCommand(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "ping":
                await command.RespondAsync("Pong!");
                break;

            case "sendmsg":
                await HandleSendMsg(command);
                break;
        }
    }

    private static async Task HandleSendMsg(SocketSlashCommand command)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var messageOption = command.Data.Options.First(o => o.Name == "message");

        var targetChannel = channelOption.Value as IMessageChannel;
        var messageText = messageOption.Value as string;

        if (targetChannel == null)
        {
            await command.RespondAsync("That channel isn't a text channel I can post in.", ephemeral: true);
            return;
        }

        await targetChannel.SendMessageAsync(messageText);
        await command.RespondAsync($"Sent to {((IChannel)targetChannel).Name}.", ephemeral: true);
    }
}