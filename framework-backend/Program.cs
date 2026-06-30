using Discord;
using Discord.WebSocket;
using framework_backend;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddHttpClient();

        var app = builder.Build();

        // Create the Discord client
        var discordClient = CreateDiscordClient();

        // Start the bot
        _ = StartDiscordBot(discordClient);

        // Listen to plastic/jira relay
        DiscordRelay.Configure(app, discordClient);
        DiscordCommands.Configure(discordClient);
        DiscordAutoReact.Configure(discordClient);

        app.Run();
    }

    private static DiscordSocketClient CreateDiscordClient()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.GuildMessages | 
                             GatewayIntents.MessageContent | 
                             GatewayIntents.GuildEmojis |
                             GatewayIntents.GuildMessageReactions
        };

        var client = new DiscordSocketClient(config);

        client.Log += msg =>
        {
            Console.WriteLine(msg);
            return Task.CompletedTask;
        };

        return client;
    }

    private static async Task StartDiscordBot(DiscordSocketClient client)
    {
        string token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        await Task.Delay(-1);
    }
}