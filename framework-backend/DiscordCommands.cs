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

        try
        {
            await client.Rest.BulkOverwriteGlobalCommands(new[]
            {
                sendMsgCommand.Build()
            });
        }
        catch (HttpException ex)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ex.Errors));
        }
    }

    private static async Task HandleSlashCommand(SocketSlashCommand command, DiscordSocketClient client)
    {
        if (command.Data.Name == "sendmsg")
        {
            await HandleSendMsgCommand(command);
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

        // Encode the channel id + optional attachment url/filename into the modal CustomId
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

    // ---------- Modal submission ----------

    private static async Task HandleModalSubmit(SocketModal modal, DiscordSocketClient client)
    {
        var parts = modal.Data.CustomId.Split('|');

        if (parts[0] != "sendmsg_modal")
        {
            return;
        }

        var channelId = ulong.Parse(parts[1]);
        var attachmentUrl = parts.Length > 2 ? parts[2] : "";
        var attachmentFilename = parts.Length > 3 ? parts[3] : "";

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
}