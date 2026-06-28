using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordCommands
{
    public static void Configure(DiscordSocketClient client)
    {
        client.Ready += async () => await RegisterCommands(client);
        client.SlashCommandExecuted += command => HandleSlashCommand(command, client);
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

    private static async Task HandleSlashCommand(SocketSlashCommand command, DiscordSocketClient client)
    {
        switch (command.Data.Name)
        {
            case "ping":
                await command.RespondAsync("Pong!");
                break;

            case "sendmsg":
                await HandleSendMsgCommand(command);
                break;

            case "editmsg":
                await HandleEditMsgCommand(command, client);
                break;
        }
    }

    // ---------- /sendmsg ----------

    private static async Task HandleSendMsgCommand(SocketSlashCommand command)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var imageOption = command.Data.Options.FirstOrDefault(o => o.Name == "image");

        var targetChannel = channelOption.Value as IMessageChannel;
        var attachment = imageOption?.Value as Attachment;

        if (targetChannel == null)
        {
            await command.RespondAsync("That channel isn't a text channel I can post in.", ephemeral: true);
            return;
        }

        // Encode channel id + optional attachment url/filename into the modal CustomId
        // Format: sendmsg_modal|channelId|attachmentUrl|attachmentFilename
        var customId = $"sendmsg_modal|{targetChannel.Id}|{attachment?.Url ?? ""}|{attachment?.Filename ?? ""}";

        var modal = new ModalBuilder()
            .WithTitle("Send a message")
            .WithCustomId(customId)
            .AddTextInput("Message", "message_body", TextInputStyle.Paragraph,
                placeholder: "Type your message here...", required: true, maxLength: 2000)
            .Build();

        await command.RespondWithModalAsync(modal);
    }

    // ---------- /editmsg ----------

    private static async Task HandleEditMsgCommand(SocketSlashCommand command, DiscordSocketClient client)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var messageIdOption = command.Data.Options.First(o => o.Name == "message_id");

        var targetChannel = channelOption.Value as IMessageChannel;
        var rawMessageId = messageIdOption.Value as string;

        if (targetChannel == null)
        {
            await command.RespondAsync("That channel isn't a text channel I can edit messages in.", ephemeral: true);
            return;
        }

        if (!ulong.TryParse(rawMessageId, out var messageId))
        {
            await command.RespondAsync("That doesn't look like a valid message ID.", ephemeral: true);
            return;
        }

        var existingMessage = await targetChannel.GetMessageAsync(messageId) as IUserMessage;

        if (existingMessage == null)
        {
            await command.RespondAsync("Couldn't find that message in that channel.", ephemeral: true);
            return;
        }

        if (existingMessage.Author.Id != client.CurrentUser.Id)
        {
            await command.RespondAsync("I can only edit messages that I sent.", ephemeral: true);
            return;
        }

        var customId = $"editmsg_modal|{targetChannel.Id}|{messageId}";

        var modal = new ModalBuilder()
            .WithTitle("Edit message")
            .WithCustomId(customId)
            .AddTextInput("New message", "message_body", TextInputStyle.Paragraph,
                value: existingMessage.Content, required: true, maxLength: 2000)
            .Build();

        await command.RespondWithModalAsync(modal);
    }

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