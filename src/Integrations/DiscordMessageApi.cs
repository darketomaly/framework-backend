using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordMessageApi
{
    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        // Support both /discord/channels and /api/discord/channels
        app.MapGet("/discord/channels", () => HandleGetChannels(client)).RequireCors("Website");
        app.MapGet("/api/discord/channels", () => HandleGetChannels(client)).RequireCors("Website");

        // Support both /discord/send and /api/discord/send
        app.MapPost("/discord/send", (SendMessageRequest req) => HandleSendMessage(req, client)).RequireCors("Website");
        app.MapPost("/api/discord/send", (SendMessageRequest req) => HandleSendMessage(req, client)).RequireCors("Website");
    }

    private static IResult HandleGetChannels(DiscordSocketClient client)
    {
        Console.WriteLine($"[DiscordMessageApi] Received GET request for discord channels. Client connection state: {client.ConnectionState}");

        if (client.ConnectionState != ConnectionState.Connected)
        {
            Console.WriteLine("[DiscordMessageApi] Discord client is not yet connected to gateway.");
            return Results.Json(new { error = "Bot is not connected yet" }, statusCode: 503);
        }

        var guilds = client.Guilds.Select(guild => new
        {
            id = guild.Id.ToString(),
            name = guild.Name,
            iconUrl = guild.IconUrl,
            channels = guild.TextChannels
                .OrderBy(c => c.Position)
                .Select(c => new
                {
                    id = c.Id.ToString(),
                    name = c.Name,
                    category = c.Category?.Name
                })
                .ToList()
        }).ToList();

        var totalChannels = guilds.Sum(g => g.channels.Count);
        Console.WriteLine($"[DiscordMessageApi] Successfully fetched {guilds.Count} guilds with {totalChannels} total text channels.");

        return Results.Ok(guilds);
    }

    private static async Task<IResult> HandleSendMessage(SendMessageRequest req, DiscordSocketClient client)
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

        var channel = await client.GetChannelAsync(channelId) as IMessageChannel;
        if (channel == null)
        {
            Console.WriteLine($"[DiscordMessageApi] Channel {channelId} could not be resolved or bot lacks access.");
            return Results.NotFound(new { error = "Channel not found or bot does not have access to it" });
        }

        try
        {
            var message = await channel.SendMessageAsync(req.Content);
            Console.WriteLine($"[DiscordMessageApi] Successfully sent message {message.Id} to channel {channelId}.");

            return Results.Ok(new
            {
                success = true,
                messageId = message.Id.ToString(),
                channelId = channel.Id.ToString()
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
