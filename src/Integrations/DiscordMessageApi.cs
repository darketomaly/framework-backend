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
        // Support both /discord/send and /api/discord/send
        app.MapPost("/discord/send", (HttpContext context, SendMessageRequest req) => HandleSendMessage(context, req, client)).RequireCors("Website");
        app.MapPost("/api/discord/send", (HttpContext context, SendMessageRequest req) => HandleSendMessage(context, req, client)).RequireCors("Website");
    }

    private static async Task<IResult> HandleSendMessage(HttpContext context, SendMessageRequest req, DiscordSocketClient client)
    {
        Console.WriteLine($"[DiscordMessageApi] Received POST request to send message. Target ChannelId: {req.ChannelId}");

        if (string.IsNullOrWhiteSpace(req.ChannelId) || !ulong.TryParse(req.ChannelId, out var channelId))
        {
            Console.WriteLine("[DiscordMessageApi] Invalid or empty ChannelId provided.");
            return Results.BadRequest(new { error = "Valid channel ID is required" });
        }

        if (string.IsNullOrWhiteSpace(req.Content))
        {
            Console.WriteLine("[DiscordMessageApi] Empty message content provided.");
            return Results.BadRequest(new { error = "Message content cannot be empty" });
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

        // Check user admin permissions in the target guild via Discord OAuth API
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
            return Results.Json(new { error = "Forbidden: You must be an administrator or owner of this server to post messages." }, statusCode: 403);
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
            var message = await targetChannel.SendMessageAsync(req.Content);
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

    public record SendMessageRequest(string? ChannelId, string? Content);
}
