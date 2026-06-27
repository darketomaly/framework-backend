using framework_backend;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddHttpClient();

        var app = builder.Build();

        // Register our relay
        PlasticDiscordRelay.Configure(app);

        app.Run();
    }
}