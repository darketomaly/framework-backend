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
                .AddChannelType(ChannelType.Text)
                .AddChannelType(ChannelType.News)
                .WithRequired(true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("message")
                .WithDescription("The message to send")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("image")
                .WithDescription("An image to attach")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(false));

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
        var imageOption = command.Data.Options.FirstOrDefault(o => o.Name == "image");

        var targetChannel = channelOption.Value as IMessageChannel;
        var messageText = messageOption.Value as string;
        var attachment = imageOption?.Value as Discord.Attachment;

        if (targetChannel == null)
        {
            await command.RespondAsync("That channel isn't a text channel I can post in.", ephemeral: true);
            return;
        }

        if (attachment != null)
        {
            // Download the attachment bytes, then re-upload to the target channel
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(attachment.Url);
            using var stream = new MemoryStream(bytes);

            await targetChannel.SendFileAsync(stream, attachment.Filename, messageText);
        }
        else
        {
            await targetChannel.SendMessageAsync(messageText);
        }

        await command.RespondAsync($"Sent to {((IChannel)targetChannel).Name}.", ephemeral: true);
    }
}