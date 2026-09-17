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
            .WithName("darkautoreactthumbs")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("The channel to react to")
                .WithType(ApplicationCommandOptionType.Channel)
                .AddChannelType(ChannelType.Text)
                .AddChannelType(ChannelType.News)
                .WithRequired(true))   
            .WithDescription("Toggles thumbs-up and thumbs-down reactions for messages in the target channel.");

        var autoReactMemesChannelCommand = new SlashCommandBuilder()
            .WithName("darkautoreactlaugh")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("The channel to react to")
                .WithType(ApplicationCommandOptionType.Channel)
                .AddChannelType(ChannelType.Text)
                .AddChannelType(ChannelType.News)
                .WithRequired(true))   
            .WithDescription("Toggls bot auto-react with a laugh to all messages on the target channel.");
        
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

        var sendReactForRoleMsgCommand = new SlashCommandBuilder()
            .WithName("darksendreactforrolemsg")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .WithDescription("Sends a message for role reactions")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("Channel to send the message to.")
                .WithType(ApplicationCommandOptionType.Channel)
                .WithRequired(true)
            )
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("message_to_replace")
                .WithDescription("Provide a message ID to replace an existing message")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("role_one")
                .WithDescription("First role to assign when reacted.")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(false)
            )
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("role_two")
                .WithDescription("Second role to assign when reacted.")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(false)
            )
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("role_three")
                .WithDescription("Third role to assign when reacted.")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(false)
            );

        try
        {
            await client.Rest.BulkOverwriteGlobalCommands(new[]
            {
                sendMsgCommand.Build(),
                editMsgCommand.Build(),
                generateServerKeyCommand.Build(),
                autoReactAnnouncementsChannelCommand.Build(),
                autoReactMemesChannelCommand.Build(),
                sendReactForRoleMsgCommand.Build()
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

            case "darkautoreactthumbs":
                await HandleAutoReactThumbs(command);
                break;

            case "darkautoreactlaugh":
                await HandleAutoReactLaugh(command);
                break;

            case "darksendreactforrolemsg":
                await HandleSendReactForRoleMsg(command);
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

    private static async Task HandleAutoReactThumbs(SocketSlashCommand command)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var targetChannel = channelOption.Value as IMessageChannel;
        var clearedValue = false;
        var targetValue = "THUMBS";
        
        var currentValue = DatabaseManager.QueryValue(targetChannel.Id.ToString(), DatabaseTable.AutoReactChannels);

        if (currentValue.Result.ExitCode == DatabaseQueryExitCode.QuerySuccess)
        {
            if (currentValue.Result.Value == "THUMBS")
            {
                // There's an entry and command was executed
                // Clearing entry

                clearedValue = true;
                targetValue = "NONE";
            }
        }

        var addEntry = await DatabaseManager.AddValue(targetChannel.Id.ToString(), targetValue, DatabaseTable.AutoReactChannels);

        if (addEntry == DatabaseQueryExitCode.AddValueSuccess)
        {
            await DiscordAutoReact.RefreshCacheAsync();

            if (clearedValue)
            {
                await command.RespondAsync($"Bot was reacting with thumbs up and thumbs down, now it's toggled off.", ephemeral: true);
            }
            else
            {
                await command.RespondAsync($"Now reacting with thumbs up and thumbs down to all messages on {targetChannel.Name}.", ephemeral: true);
            }
        }
        else if (addEntry == DatabaseQueryExitCode.AddValueFailed)
        {
            await command.RespondAsync($"Something went wrong. Not setting auto-reaction on this channel.", ephemeral: true);
        }
    }

    // ---------- /darkautoreactmemeschannel ----------

    private static async Task HandleAutoReactLaugh(SocketSlashCommand command)
    {
        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var targetChannel = channelOption.Value as IMessageChannel;
        var clearedValue = false;
        var targetValue = "LAUGH";

        var currentValue = DatabaseManager.QueryValue(targetChannel.Id.ToString(), DatabaseTable.AutoReactChannels);

        if (currentValue.Result.ExitCode == DatabaseQueryExitCode.QuerySuccess)
        {
            if (currentValue.Result.Value == "LAUGH")
            {
                // There's an entry and command was executed
                // Clearing entry

                clearedValue = true;
                targetValue = "NONE";
            }
        }
        
        var addEntry = await DatabaseManager.AddValue(targetChannel.Id.ToString(), targetValue, DatabaseTable.AutoReactChannels);

        if (addEntry == DatabaseQueryExitCode.AddValueSuccess)
        {
            await DiscordAutoReact.RefreshCacheAsync();

            if (clearedValue)
            {
                await command.RespondAsync($"Bot was reacting with thumbs up and thumbs down, now it's toggled off.", ephemeral: true);
            }
            else
            {
                await command.RespondAsync($"Now reacting with a laugh to all media messages on {targetChannel.Name}.", ephemeral: true);
            }
        }
        else if (addEntry == DatabaseQueryExitCode.AddValueFailed)
        {
            await command.RespondAsync($"Something went wrong. Not setting auto-reaction on this channel.", ephemeral: true);
        }
    }

    // ---------- /darkgenerateserverkey ----------
    
    private static async Task HandleGenerateServerKey(SocketSlashCommand command, DiscordSocketClient client)
    {
        var guildId = (ulong)command.GuildId;
        
        var (exitCode, value) = await DatabaseManager.QueryValue(guildId.ToString(), DatabaseTable.SecretKeys);

        if (exitCode is DatabaseQueryExitCode.ValueNotFound)
        {
            var randomShort = (short)Random.Shared.Next(0, short.MaxValue);
            value = randomShort.ToString();

            var addValue = DatabaseManager.AddValue(guildId.ToString(), value, DatabaseTable.SecretKeys);

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

    // ---------- /darksendreactforrolemsg ----------

    private static async Task HandleSendReactForRoleMsg(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);

        var channelOption = command.Data.Options.First(o => o.Name == "channel");
        var msgIdToReplaceDataOption = command.Data.Options.FirstOrDefault(o => o.Name == "message_to_replace");
        var role1 = command.Data.Options.FirstOrDefault(o => o.Name == "role_one")?.Value as IRole;
        var role2 = command.Data.Options.FirstOrDefault(o => o.Name == "role_two")?.Value as IRole;
        var role3 = command.Data.Options.FirstOrDefault(o => o.Name == "role_three")?.Value as IRole;
        var message = "React to this message to assign yourself a role.\n\n";
        
        var targetChannel = channelOption.Value as IMessageChannel;
        
        if (targetChannel == null)
        {
            await command.FollowupAsync("That channel isn't a text channel I can post in.", ephemeral: true);
            return;
        }
        
        // --- Build and send message ---
        
        var rawMessageId = msgIdToReplaceDataOption?.Value as string;

        if (role1 != null)
        {
            message += $"{EmojiId.One} <@&{role1.Id}>";
        }
        
        if (role2 != null)
        {
            message += $"\n{EmojiId.Two} <@&{role2.Id}>";
        }
        
        if (role3 != null)
        {
            message += $"\n{EmojiId.Three} <@&{role3.Id}>";
        }

        IUserMessage? sentMessage;
        
        if (string.IsNullOrEmpty(rawMessageId))
        {
            // Send new message
            
            sentMessage = await targetChannel.SendMessageAsync(message);
        }
        else
        {
            // Edit message with given id
            
            if (!ulong.TryParse(rawMessageId, out var messageId))
            {
                await command.FollowupAsync("That doesn't look like a valid message ID.", ephemeral: true);
                return;
            }
            
            sentMessage = await targetChannel.GetMessageAsync(messageId) as IUserMessage;

            if (sentMessage == null)
            {
                await command.FollowupAsync("Couldn't find that message in that channel.", ephemeral: true);
                return;
            }

            await sentMessage.ModifyAsync(props => props.Content = message);
        }
        
        // --- Add or remove reactions ---

        if (role1 != null)
        {
            await sentMessage.AddReactionAsync(Emote.Parse(EmojiId.One));
        }
        else
        {
            if (!string.IsNullOrEmpty(rawMessageId))
            {
                await sentMessage.RemoveAllReactionsForEmoteAsync(Emote.Parse(EmojiId.One));
            }
        }

        if (role2 != null)
        {
            await sentMessage.AddReactionAsync(Emote.Parse(EmojiId.Two));
        }
        else
        {
            if (!string.IsNullOrEmpty(rawMessageId))
            {
                await sentMessage.RemoveAllReactionsForEmoteAsync(Emote.Parse(EmojiId.Two));
            }
        }

        if (role3 != null)
        {
            await sentMessage.AddReactionAsync(Emote.Parse(EmojiId.Three));
        }
        else
        {
            if (!string.IsNullOrEmpty(rawMessageId))
            {
                await sentMessage.RemoveAllReactionsForEmoteAsync(Emote.Parse(EmojiId.Three));
            }
        }

        await command.FollowupAsync("React-for-role message sent.", ephemeral: true);
    }
}