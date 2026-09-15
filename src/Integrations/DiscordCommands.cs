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
    }

    private static async Task RegisterCommands(DiscordSocketClient client)
    {
        var generateServerKeyCommand = new SlashCommandBuilder()
            .WithName("darkgenerateserverkey")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .WithDescription("Generates a unique key for this server to pass to webhook relays");

        var autoReactAnnouncementsChannelCommand = new SlashCommandBuilder()
            .WithName("darkautoreactannouncementchannel")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("The channel to react to")
                .WithType(ApplicationCommandOptionType.Channel)
                .AddChannelType(ChannelType.Text)
                .AddChannelType(ChannelType.News)
                .WithRequired(true))   
            .WithDescription("Bot will react with a thumbs up and a thumbs down to all messages on the target channel.");

        var autoReactMemesChannelCommand = new SlashCommandBuilder()
            .WithName("darkautoreactmemeschannel")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("The channel to react to")
                .WithType(ApplicationCommandOptionType.Channel)
                .AddChannelType(ChannelType.Text)
                .AddChannelType(ChannelType.News)
                .WithRequired(true))   
            .WithDescription("Bot will react with a laugh to all messages on the target channel.");
        
        var sendMsgCommand = new SlashCommandBuilder()
            .WithName("darksendmsg")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
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
                .WithDescription("The message to send. Use <br> for a line break")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("image")
                .WithDescription("An image to attach")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(false));

        var editMsgCommand = new SlashCommandBuilder()
            .WithName("darkeditmsg")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
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
                .WithRequired(true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("new_message")
                .WithDescription("The new text content. Use <br> for a line break")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("new_image")
                .WithDescription("A new image to replace the existing one")
                .WithType(ApplicationCommandOptionType.Attachment)
                .WithRequired(false));

        try
        {
            await client.Rest.BulkOverwriteGlobalCommands(new[]
            {
                sendMsgCommand.Build(),
                editMsgCommand.Build(),
                generateServerKeyCommand.Build(),
                autoReactAnnouncementsChannelCommand.Build(),
                autoReactMemesChannelCommand.Build()
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
            case "darksendmsg":
                await HandleSendMsg(command);
                break;

            case "darkeditmsg":
                await HandleEditMsg(command, client);
                break;
            
            case "darkgenerateserverkey":
                await HandleGenerateServerKey(command, client);
                break;

            case "darkautoreactannouncementchannel":
                await HandleAutoReactAnnouncementChannel(command);
                break;

            case "darkautoreactmemeschannel":
                await HandleAutoReactMemesChannel(command);
                break;
        }
    }

    // ---------- /darksendmsg ----------

    private static async Task HandleSendMsg(SocketSlashCommand command)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var messageOption = command.Data.Options.FirstOrDefault(o => o.Name == "message");
        var imageOption = command.Data.Options.FirstOrDefault(o => o.Name == "image");

        var targetChannel = channelOption.Value as IMessageChannel;
        var rawText = messageOption?.Value as string ?? "";
        var messageText = rawText.Replace("<br>", "\n");
        var attachment = imageOption?.Value as Attachment;

        if (targetChannel == null)
        {
            await command.RespondAsync("That channel isn't a text channel I can post in.", ephemeral: true);
            return;
        }

        if (string.IsNullOrEmpty(messageText) && attachment == null)
        {
            await command.RespondAsync("You need to provide a message, an image, or both.", ephemeral: true);
            return;
        }

        if (attachment != null)
        {
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

    // ---------- /darkeditmsg ----------

    private static async Task HandleEditMsg(SocketSlashCommand command, DiscordSocketClient client)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var messageIdOption = command.Data.Options.First(o => o.Name == "message_id");
        var newMessageOption = command.Data.Options.FirstOrDefault(o => o.Name == "new_message");
        var newImageOption = command.Data.Options.FirstOrDefault(o => o.Name == "new_image");

        var targetChannel = channelOption.Value as IMessageChannel;
        var rawMessageId = messageIdOption.Value as string;
        var hasNewText = newMessageOption != null;
        var rawNewText = newMessageOption?.Value as string ?? "";
        var newText = rawNewText.Replace("<br>", "\n");
        var newAttachment = newImageOption?.Value as Attachment;

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

        if (!hasNewText && newAttachment == null)
        {
            await command.RespondAsync("You need to provide a new message, a new image, or both.", ephemeral: true);
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

        if (newAttachment != null)
        {
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(newAttachment.Url);
            using var stream = new MemoryStream(bytes);
            var fileAttachment = new FileAttachment(stream, newAttachment.Filename);

            await existingMessage.ModifyAsync(props =>
            {
                if (hasNewText)
                {
                    props.Content = newText;
                }
                props.Attachments = new[] { fileAttachment };
            });
        }
        else
        {
            await existingMessage.ModifyAsync(props => props.Content = newText);
        }

        await command.RespondAsync("Message edited.", ephemeral: true);
    }

    // ---------- /darkautoreactannouncementchannel ----------

    private static async Task HandleAutoReactAnnouncementChannel(SocketSlashCommand command)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var targetChannel = channelOption.Value as IMessageChannel;

        await command.RespondAsync($"Now reacting with thumbs up and thumbs down to all messages on {targetChannel.Name}.", ephemeral: true);
    }

    // ---------- /darkautoreactmemeschannel ----------

    private static async Task HandleAutoReactMemesChannel(SocketSlashCommand command)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var targetChannel = channelOption.Value as IMessageChannel;

        await command.RespondAsync($"Now reacting with a laugh to all media messages on {targetChannel.Name}.", ephemeral: true);
    }

    // ---------- /darkgenerateserverkey ----------
    
    private static async Task HandleGenerateServerKey(SocketSlashCommand command, DiscordSocketClient client)
    {
        var guildId = (ulong)command.GuildId;
        
        var (exitCode, value) = await DatabaseManager.QueryValue(guildId.ToString());

        if (exitCode is DatabaseQueryExitCode.ValueNotFound)
        {
            var randomShort = (short)Random.Shared.Next(0, short.MaxValue);
            value = randomShort.ToString();

            var addValue = DatabaseManager.AddValue(guildId.ToString(), value);

            if (addValue.Result is DatabaseQueryExitCode.AddValueSuccess)
            {
                await command.RespondAsync($"Secret value not found, generated one: `{value}`. Use this on the relay payload.", ephemeral: true);
            }
            else
            {
                await command.RespondAsync($"There was a problem generating the secret value.", ephemeral: true);
            }
        }
        else if (exitCode is DatabaseQueryExitCode.QuerySuccess)
        {
            Console.WriteLine($"Test query was a success: {value}");
            await command.RespondAsync($"Secret value found for guild #{guildId}: `{value}`. Use this on the relay payload.", ephemeral: true);
        }
    }
}