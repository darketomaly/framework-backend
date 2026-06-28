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
        client.ModalSubmitted += modal => HandleModalSubmit(modal, client);
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
                .WithName("image")
                .WithDescription("An image to attach")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(false));

        var editMsgCommand = new SlashCommandBuilder()
            .WithName("editmsg")
            .WithDescription("Edits a message previously sent by the bot")
            .WithDefaultMemberPermissions(GuildPermission.ManageMessages)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("The channel the message is in")
                .WithType(ApplicationCommandOptionType.Channel)
                .AddChannelType(ChannelType.Text)
                .AddChannelType(ChannelType.News)
                .WithRequired(true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("message_id")
                .WithDescription("The ID of the message to edit")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        try
        {
            await client.Rest.BulkOverwriteGlobalCommands(new[]
            {
                pingCommand.Build(),
                sendMsgCommand.Build(),
                editMsgCommand.Build()
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
        }
    }

    // ---------- /sendmsg ----------

    // ---------- Modal submissions ----------

    private static async Task HandleModalSubmit(SocketModal modal, DiscordSocketClient client)
    {
        var parts = modal.Data.CustomId.Split('|');
        var modalType = parts[0];

        switch (modalType)
        {
            case "sendmsg_modal":
                await HandleSendMsgModal(modal, parts, client);
                break;

            case "editmsg_modal":
                await HandleEditMsgModal(modal, parts, client);
                break;
        }
    }

    private static async Task HandleSendMsgModal(SocketModal modal, string[] parts, DiscordSocketClient client)
    {
        var channelId = ulong.Parse(parts[1]);
        var attachmentUrl = parts[2];
        var attachmentFilename = parts[3];

        var messageText = modal.Data.Components.First(c => c.CustomId == "message_body").Value;

        var targetChannel = await client.GetChannelAsync(channelId) as IMessageChannel;

        if (targetChannel == null)
        {
            await modal.RespondAsync("Couldn't find that channel anymore.", ephemeral: true);
            return;
        }

        if (!string.IsNullOrEmpty(attachmentUrl))
        {
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(attachmentUrl);
            using var stream = new MemoryStream(bytes);

            await targetChannel.SendFileAsync(stream, attachmentFilename, messageText);
        }
        else
        {
            await targetChannel.SendMessageAsync(messageText);
        }

        await modal.RespondAsync($"Sent to {((IChannel)targetChannel).Name}.", ephemeral: true);
    }

    private static async Task HandleEditMsgModal(SocketModal modal, string[] parts, DiscordSocketClient client)
    {
        var channelId = ulong.Parse(parts[1]);
        var messageId = ulong.Parse(parts[2]);

        var newText = modal.Data.Components.First(c => c.CustomId == "message_body").Value;

        var targetChannel = await client.GetChannelAsync(channelId) as IMessageChannel;

        if (targetChannel == null)
        {
            await modal.RespondAsync("Couldn't find that channel anymore.", ephemeral: true);
            return;
        }

        var existingMessage = await targetChannel.GetMessageAsync(messageId) as IUserMessage;

        if (existingMessage == null)
        {
            await modal.RespondAsync("Couldn't find that message anymore.", ephemeral: true);
            return;
        }

        await existingMessage.ModifyAsync(props => props.Content = newText);
        await modal.RespondAsync("Message edited.", ephemeral: true);
    }
}