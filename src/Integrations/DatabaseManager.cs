using Npgsql;

namespace framework_backend;

public static class DatabaseManager
{
    private static async Task Connect()
    {
        try
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                Console.WriteLine("PostgreSQL test query skipped: DATABASE_URL is not configured");
            }
            else
            {
                var databaseConnectionString = databaseUrl;

                if (Uri.TryCreate(databaseUrl, UriKind.Absolute, out var databaseUri) &&
                    !string.IsNullOrWhiteSpace(databaseUri.UserInfo))
                {
                    var userInfo = databaseUri.UserInfo.Split(':', 2);

                    if (userInfo.Length != 2)
                    {
                        Console.WriteLine("PostgreSQL test query skipped: invalid DATABASE_URL credentials");
                    }
                    else
                    {
                        var connectionBuilder = new NpgsqlConnectionStringBuilder
                        {
                            Host = databaseUri.Host,
                            Port = databaseUri.Port > 0 ? databaseUri.Port : 5432,
                            Database = databaseUri.AbsolutePath.Trim('/'),
                            Username = Uri.UnescapeDataString(userInfo[0]),
                            Password = Uri.UnescapeDataString(userInfo[1]),
                            SslMode = SslMode.Require
                        };

                        databaseConnectionString = connectionBuilder.ConnectionString;
                    }
                }

                await using var database = new NpgsqlConnection(databaseConnectionString);
                await database.OpenAsync();

                await using var command = new NpgsqlCommand(
                    "SELECT value FROM secret_keys WHERE secret_key = @key LIMIT 1",
                    database);
                command.Parameters.AddWithValue("key", "test");

                var result = await command.ExecuteScalarAsync();
                Console.WriteLine($"PostgreSQL test: {result}");
            }
        }
        catch (NpgsqlException exception)
        {
            Console.WriteLine($"PostgreSQL test query failed; continuing normally: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine($"PostgreSQL test query configuration is invalid; continuing normally: {exception.Message}");
        }
    }

    public static async Task QueryValue(string key)
    {
        // Connect
        await Connect();
        
        // Query the key and print its value here
    }
}