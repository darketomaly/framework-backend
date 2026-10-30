using System.Net.Http.Headers;
using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordMessageApi
{
    private const ulong ADMINISTRATOR = 0x8;

    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        // Support both /discord/send and /api/discord/send with multipart/form-data or JSON
        app.MapPost("/discord/send", (Delegate)((HttpContext context) => HandleSendMessage(context, client))).RequireCors("Website");
        app.MapPost("/api/discord/send", (Delegate)((HttpContext context) => HandleSendMessage(context, client))).RequireCors("Website");

        // Message retrieval and editing endpoints
        app.MapGet("/discord/message", (Delegate)((HttpContext context) => HandleGetMessage(context, client))).RequireCors("Website");
        app.MapGet("/api/discord/message", (Delegate)((HttpContext context) => HandleGetMessage(context, client))).RequireCors("Website");

        app.MapPost("/discord/edit", (Delegate)((HttpContext context) => HandleEditMessage(context, client))).RequireCors("Website");
        app.MapPost("/api/discord/edit", (Delegate)((HttpContext context) => HandleEditMessage(context, client))).RequireCors("Website");
    }

    private static async Task<IResult> HandleSendMessage(HttpContext context, DiscordSocketClient client)
    {
        string? channelIdStr = null;
        string? content = null;
        IFormFile? file = null;

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();
            channelIdStr = form["channelId"];
            content = form["content"];
            file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        }
        else
        {
            var bodyReq = await context.Request.ReadFromJsonAsync<SendMessageRequest>();
            if (bodyReq != null)
            {
                channelIdStr = bodyReq.ChannelId;
                content = bodyReq.Content;
            }
        }

        Console.WriteLine($"[DiscordMessageApi] Received POST request to send message. Target ChannelId: {channelIdStr}, HasFile: {file != null}");

        if (string.IsNullOrWhiteSpace(channelIdStr) || !ulong.TryParse(channelIdStr, out var channelId))
        {
            Console.WriteLine("[DiscordMessageApi] Invalid or empty ChannelId provided.");
            return Results.BadRequest(new { error = "Valid channel ID is required" });
        }

        if (string.IsNullOrWhiteSpace(content) && file == null)
        {
            Console.WriteLine("[DiscordMessageApi] Neither message content nor file attachment provided.");
            return Results.BadRequest(new { error = "Message content or file attachment is required" });
        }

        // Verify caller's Discord authorization token
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new { error = "Authorization required. Please log in with Discord." }, statusCode: 401);
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var targetChannel = client.GetChannel(channelId) as SocketTextChannel;
        if (targetChannel == null)
        {
            return Results.NotFound(new { error = "Channel not found or bot does not have access to it." });
        }

        var targetGuild = targetChannel.Guild;

        var authResult = await ValidateUserAdminAsync(token, targetGuild);
        if (authResult != null)
        {
            return authResult;
        }

        // Check bot permissions
        var botUser = targetGuild.CurrentUser;
        if (botUser != null)
        {
            var botPerms = botUser.GetPermissions(targetChannel);
            if (!botPerms.ViewChannel || !botPerms.SendMessages)
            {
                return Results.Json(new { error = "Bot lacks permission to send messages to this channel." }, statusCode: 403);
            }
        }

        try
        {
            IUserMessage message;
            if (file != null && file.Length > 0)
            {
                using var fileStream = file.OpenReadStream();
                var safeFileName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(safeFileName))
                {
                    safeFileName = "attachment.png";
                }

                message = await targetChannel.SendFileAsync(
                    stream: fileStream,
                    filename: safeFileName,
                    text: content ?? string.Empty
                );
            }
            else
            {
                message = await targetChannel.SendMessageAsync(content ?? string.Empty);
            }

            Console.WriteLine($"[DiscordMessageApi] Successfully sent message {message.Id} to channel {channelId}.");

            return Results.Ok(new
            {
                success = true,
                messageId = message.Id.ToString(),
                channelId = targetChannel.Id.ToString()
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DiscordMessageApi] Failed to send message to channel {channelId}: {ex}");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    private static async Task<IResult> HandleGetMessage(HttpContext context, DiscordSocketClient client)
    {
        var channelIdStr = context.Request.Query["channelId"].ToString();
        var messageIdStr = context.Request.Query["messageId"].ToString();

        if (string.IsNullOrWhiteSpace(channelIdStr) || !ulong.TryParse(channelIdStr, out var channelId))
        {
            return Results.BadRequest(new { error = "Valid channelId is required." });
        }

        if (string.IsNullOrWhiteSpace(messageIdStr) || !ulong.TryParse(messageIdStr, out var messageId))
        {
            return Results.BadRequest(new { error = "Valid messageId is required." });
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new { error = "Authorization required. Please log in with Discord." }, statusCode: 401);
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var targetChannel = client.GetChannel(channelId) as SocketTextChannel;
        if (targetChannel == null)
        {
            return Results.NotFound(new { error = "Channel not found or bot does not have access to it." });
        }

        var authResult = await ValidateUserAdminAsync(token, targetChannel.Guild);
        if (authResult != null)
        {
            return authResult;
        }

        try
        {
            var msg = await targetChannel.GetMessageAsync(messageId) as IUserMessage;
            if (msg == null)
            {
                return Results.NotFound(new { error = "Message not found in that channel." });
            }

            var isBotMessage = msg.Author.Id == client.CurrentUser.Id;
            var attachments = msg.Attachments.Select(a => new
            {
                id = a.Id.ToString(),
                filename = a.Filename,
                url = a.Url,
                proxyUrl = a.ProxyUrl,
                size = a.Size,
                contentType = a.ContentType,
                width = a.Width,
                height = a.Height
            }).ToList();

            return Results.Ok(new
            {
                id = msg.Id.ToString(),
                channelId = targetChannel.Id.ToString(),
                content = msg.Content ?? string.Empty,
                isBotMessage,
                author = new
                {
                    id = msg.Author.Id.ToString(),
                    username = msg.Author.Username,
                    isBot = msg.Author.IsBot
                },
                timestamp = msg.Timestamp,
                editedTimestamp = msg.EditedTimestamp,
                attachments
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DiscordMessageApi] Failed to load message {messageId} in {channelId}: {ex}");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    private static async Task<IResult> HandleEditMessage(HttpContext context, DiscordSocketClient client)
    {
        string? channelIdStr = null;
        string? messageIdStr = null;
        string? content = null;
        bool removeAttachment = false;
        IFormFile? file = null;

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();
            channelIdStr = form["channelId"];
            messageIdStr = form["messageId"];
            content = form["content"];
            if (bool.TryParse(form["removeAttachment"], out var removeFlag))
            {
                removeAttachment = removeFlag;
            }
            file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        }
        else
        {
            var bodyReq = await context.Request.ReadFromJsonAsync<EditMessageRequest>();
            if (bodyReq != null)
            {
                channelIdStr = bodyReq.ChannelId;
                messageIdStr = bodyReq.MessageId;
                content = bodyReq.Content;
                removeAttachment = bodyReq.RemoveAttachment ?? false;
            }
        }

        if (string.IsNullOrWhiteSpace(channelIdStr) || !ulong.TryParse(channelIdStr, out var channelId))
        {
            return Results.BadRequest(new { error = "Valid channelId is required." });
        }

        if (string.IsNullOrWhiteSpace(messageIdStr) || !ulong.TryParse(messageIdStr, out var messageId))
        {
            return Results.BadRequest(new { error = "Valid messageId is required." });
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new { error = "Authorization required. Please log in with Discord." }, statusCode: 401);
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var targetChannel = client.GetChannel(channelId) as SocketTextChannel;
        if (targetChannel == null)
        {
            return Results.NotFound(new { error = "Channel not found or bot does not have access to it." });
        }

        var authResult = await ValidateUserAdminAsync(token, targetChannel.Guild);
        if (authResult != null)
        {
            return authResult;
        }

        try
        {
            var existingMessage = await targetChannel.GetMessageAsync(messageId) as IUserMessage;
            if (existingMessage == null)
            {
                return Results.NotFound(new { error = "Couldn't find that message in that channel." });
            }

            if (existingMessage.Author.Id != client.CurrentUser.Id)
            {
                return Results.BadRequest(new { error = "The bot can only edit messages that it sent." });
            }

            var hasNewText = content != null;

            if (file != null && file.Length > 0)
            {
                using var fileStream = file.OpenReadStream();
                var safeFileName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(safeFileName))
                {
                    safeFileName = "attachment.png";
                }

                var fileAttachment = new FileAttachment(fileStream, safeFileName);
                await existingMessage.ModifyAsync(props =>
                {
                    if (hasNewText)
                    {
                        props.Content = content;
                    }
                    props.Attachments = new[] { fileAttachment };
                });
            }
            else if (removeAttachment)
            {
                await existingMessage.ModifyAsync(props =>
                {
                    if (hasNewText)
                    {
                        props.Content = content;
                    }
                    props.Attachments = Array.Empty<FileAttachment>();
                });
            }
            else
            {
                if (!hasNewText)
                {
                    return Results.BadRequest(new { error = "You must provide new text, a new image, or choose to remove the attachment to edit." });
                }

                await existingMessage.ModifyAsync(props =>
                {
                    props.Content = content;
                });
            }

            Console.WriteLine($"[DiscordMessageApi] Successfully edited message {messageId} in channel {channelId}.");

            return Results.Ok(new
            {
                success = true,
                messageId = messageId.ToString(),
                channelId = targetChannel.Id.ToString()
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DiscordMessageApi] Failed to edit message {messageId} in channel {channelId}: {ex}");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    private static async Task<IResult?> ValidateUserAdminAsync(string token, SocketGuild targetGuild)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var guildsResponse = await httpClient.GetAsync("https://discord.com/api/v10/users/@me/guilds");
        if (!guildsResponse.IsSuccessStatusCode)
        {
            return Results.Json(new { error = "Failed to verify user permissions with Discord." }, statusCode: 401);
        }

        var guildsJson = await guildsResponse.Content.ReadAsStringAsync();
        using var guildsDoc = JsonDocument.Parse(guildsJson);
        var isAuthorizedAdmin = false;

        foreach (var element in guildsDoc.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("id", out var gIdProp) && gIdProp.GetString() == targetGuild.Id.ToString())
            {
                var isOwner = element.TryGetProperty("owner", out var ownerProp) && ownerProp.GetBoolean();
                ulong userPerms = 0;
                if (element.TryGetProperty("permissions", out var permsProp))
                {
                    if (permsProp.ValueKind == JsonValueKind.String)
                        ulong.TryParse(permsProp.GetString(), out userPerms);
                    else if (permsProp.ValueKind == JsonValueKind.Number)
                        userPerms = permsProp.GetUInt64();
                }

                if (isOwner || (userPerms & ADMINISTRATOR) == ADMINISTRATOR)
                {
                    isAuthorizedAdmin = true;
                }
                break;
            }
        }

        if (!isAuthorizedAdmin)
        {
            return Results.Json(new { error = "Forbidden: You must be an administrator or owner of this server." }, statusCode: 403);
        }

        return null;
    }

    public record SendMessageRequest(string? ChannelId, string? Content);
    public record EditMessageRequest(string? ChannelId, string? MessageId, string? Content, bool? RemoveAttachment = false);
}
