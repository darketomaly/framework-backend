using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordMessageApi
{
    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        // GET /api/discord/channels - Lists guilds and their text channels the bot can access
        app.MapGet("/api/discord/channels", () =>
        {
            if (client.ConnectionState != ConnectionState.Connected)
            {
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

            return Results.Ok(guilds);
        });

        // POST /api/discord/send - Send a message to a specific channel
        app.MapPost("/api/discord/send", async (SendMessageRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.ChannelId) || !ulong.TryParse(req.ChannelId, out var channelId))
            {
                return Results.BadRequest(new { error = "Valid channel ID is required" });
            }

            if (string.IsNullOrWhiteSpace(req.Content))
            {
                return Results.BadRequest(new { error = "Message content cannot be empty" });
            }

            var channel = await client.GetChannelAsync(channelId) as IMessageChannel;
            if (channel == null)
            {
                return Results.NotFound(new { error = "Channel not found or bot does not have access to it" });
            }

            try
            {
                var message = await channel.SendMessageAsync(req.Content);
                return Results.Ok(new
                {
                    success = true,
                    messageId = message.Id.ToString(),
                    channelId = channel.Id.ToString()
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });
    }

    public record SendMessageRequest(string? ChannelId, string? Content);
}
