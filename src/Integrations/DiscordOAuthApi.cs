using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;

namespace framework_backend;

public static class DiscordOAuthApi
{
    private const string DiscordApiBase = "https://discord.com/api/v10";

    public static void Configure(WebApplication app, DiscordSocketClient client)
    {
        // 1. GET /discord/auth/url - Return Discord OAuth2 authorize URL
        app.MapGet("/discord/auth/url", (HttpRequest request) =>
        {
            Console.WriteLine("[DiscordOAuthApi] Received request for OAuth authorize URL.");
            var clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(clientId))
            {
                Console.WriteLine("[DiscordOAuthApi] Missing DISCORD_CLIENT_ID on server.");
                return Results.BadRequest(new { error = "DISCORD_CLIENT_ID environment variable is not configured on the backend." });
            }

            var redirectUri = request.Query["redirect_uri"].ToString();
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                return Results.BadRequest(new { error = "Missing redirect_uri query parameter." });
            }

            var state = request.Query["state"].ToString();
            var scopes = "identify guilds";

            var url = $"https://discord.com/oauth2/authorize?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&response_type=code" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&scope={Uri.EscapeDataString(scopes)}" +
                      (string.IsNullOrWhiteSpace(state) ? "" : $"&state={Uri.EscapeDataString(state)}");

            return Results.Ok(new { url });
        }).RequireCors("Website");

        // 2. POST /discord/auth/token - Exchange authorization code for user access token
        app.MapPost("/discord/auth/token", async (TokenExchangeRequest req, IHttpClientFactory httpClientFactory) =>
        {
            var clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET");

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return Results.BadRequest(new { error = "OAuth2 credentials are not configured on the backend." });
            }

            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.RedirectUri))
            {
                return Results.BadRequest(new { error = "Code and RedirectUri are required." });
            }

            var httpClient = httpClientFactory.CreateClient();

            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = req.Code,
                ["redirect_uri"] = req.RedirectUri
            };

            var tokenResponse = await httpClient.PostAsync($"{DiscordApiBase}/oauth2/token", new FormUrlEncodedContent(form));
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DiscordOAuthApi] Token exchange failed: {tokenJson}");
                return Results.Json(new { error = "Failed to exchange token with Discord", details = tokenJson }, statusCode: (int)tokenResponse.StatusCode);
            }

            using var doc = JsonDocument.Parse(tokenJson);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var tokenType = doc.RootElement.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";

            // Fetch user info with the token
            var userReq = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}/users/@me");
            userReq.Headers.Authorization = new AuthenticationHeaderValue(tokenType ?? "Bearer", accessToken);

            var userRes = await httpClient.SendAsync(userReq);
            var userJson = await userRes.Content.ReadAsStringAsync();

            return Results.Ok(new
            {
                access_token = accessToken,
                token_type = tokenType,
                user = JsonDocument.Parse(userJson).RootElement
            });
        }).RequireCors("Website");

        // 3. GET /discord/user-channels - Returns guilds & text channels the authenticated user has access to
        app.MapGet("/discord/user-channels", async (HttpRequest request, IHttpClientFactory httpClientFactory) =>
        {
            if (client.ConnectionState != ConnectionState.Connected)
            {
                return Results.Json(new { error = "Bot is not connected yet" }, statusCode: 503);
            }

            var authHeader = request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Unauthorized();
            }

            var userToken = authHeader.Substring("Bearer ".Length).Trim();
            var httpClient = httpClientFactory.CreateClient();

            // Fetch user guilds
            var userGuildsReq = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}/users/@me/guilds");
            userGuildsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

            var userGuildsRes = await httpClient.SendAsync(userGuildsReq);
            if (!userGuildsRes.IsSuccessStatusCode)
            {
                var err = await userGuildsRes.Content.ReadAsStringAsync();
                return Results.Json(new { error = "Failed to fetch user guilds from Discord", details = err }, statusCode: (int)userGuildsRes.StatusCode);
            }

            var userGuildsJson = await userGuildsRes.Content.ReadAsStringAsync();
            using var userGuildsDoc = JsonDocument.Parse(userGuildsJson);

            // Filter to servers where the user has Administrator or ManageMessages permission,
            // or where the user is the owner, AND where the bot is also present!
            const ulong ADMINISTRATOR = 0x8;
            const ulong MANAGE_MESSAGES = 0x2000;
            const ulong SEND_MESSAGES = 0x800;

            var matchingGuilds = new List<object>();

            foreach (var userGuild in userGuildsDoc.RootElement.EnumerateArray())
            {
                var guildIdStr = userGuild.GetProperty("id").GetString();
                if (!ulong.TryParse(guildIdStr, out var guildId)) continue;

                var isOwner = userGuild.TryGetProperty("owner", out var ownerProp) && ownerProp.GetBoolean();
                ulong permissions = 0;
                if (userGuild.TryGetProperty("permissions", out var permProp))
                {
                    if (ulong.TryParse(permProp.GetString(), out var parsedPerm))
                    {
                        permissions = parsedPerm;
                    }
                }

                var hasPermission = isOwner ||
                                    (permissions & ADMINISTRATOR) == ADMINISTRATOR ||
                                    (permissions & MANAGE_MESSAGES) == MANAGE_MESSAGES ||
                                    (permissions & SEND_MESSAGES) == SEND_MESSAGES;

                if (!hasPermission) continue;

                // Check if bot is also in this guild
                var botGuild = client.GetGuild(guildId);
                if (botGuild == null) continue;

                var channels = botGuild.TextChannels
                    .OrderBy(c => c.Position)
                    .Select(c => new
                    {
                        id = c.Id.ToString(),
                        name = c.Name,
                        category = c.Category?.Name
                    })
                    .ToList();

                matchingGuilds.Add(new
                {
                    id = botGuild.Id.ToString(),
                    name = botGuild.Name,
                    iconUrl = botGuild.IconUrl,
                    channels = channels
                });
            }

            return Results.Ok(matchingGuilds);
        }).RequireCors("Website");
    }

    public record TokenExchangeRequest(string? Code, string? RedirectUri);
}
