using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace framework_backend;

public static class DiscordCommands
{
    public static void Configure(DiscordSocketClient client)
    {
        client.Ready += async () => await RegisterCommands(client);
        client.SlashCommandExecuted += HandleSlashCommand;
    }

    private static async Task RegisterCommands(DiscordSocketClient client)
    {
        var pingCommand = new SlashCommandBuilder()
            .WithName("ping")
            .WithDescription("Replies with pong");

        try
        {
            await client.Rest.CreateGlobalCommand(pingCommand.Build());
        }
        catch (HttpException ex)
        {
            Console.WriteLine(JsonConvert.SerializeObject(ex.Errors, Formatting.Indented));
        }
    }

    private static async Task HandleSlashCommand(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "ping":
                await command.RespondAsync("Pong!");
                break;
        }
    }
}