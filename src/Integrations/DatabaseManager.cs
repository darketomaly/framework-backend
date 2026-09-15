using Npgsql;

namespace framework_backend;

public static class DatabaseManager
{
    private static async Task<NpgsqlConnection?> Connect()
    {
        try
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                Console.WriteLine("PostgreSQL connection skipped: DATABASE_URL is not configured");
                return null;
            }

            var databaseConnectionString = databaseUrl;

            if (Uri.TryCreate(databaseUrl, UriKind.Absolute, out var databaseUri) &&
                !string.IsNullOrWhiteSpace(databaseUri.UserInfo))
            {
                var userInfo = databaseUri.UserInfo.Split(':', 2);

                if (userInfo.Length != 2)
                {
                    Console.WriteLine("PostgreSQL connection skipped: invalid DATABASE_URL credentials");
                    return null;
                }

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

            var database = new NpgsqlConnection(databaseConnectionString);
            await database.OpenAsync();
            return database;
        }
        catch (NpgsqlException exception)
        {
            Console.WriteLine($"PostgreSQL connection failed: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine($"PostgreSQL connection configuration is invalid: {exception.Message}");
        }

        return null;
    }

    public static async Task QueryValue(string key)
    {
        await using var database = await Connect();

        if (database is null)
        {
            return;
        }

        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT value FROM secret_keys WHERE secret_key = @key LIMIT 1",
                database);
            command.Parameters.AddWithValue("key", key);

            var result = await command.ExecuteScalarAsync();
            Console.WriteLine($"Testing! PostgreSQL value for key '{key}': {result ?? "<not found>"}");
        }
        catch (NpgsqlException exception)
        {
            Console.WriteLine($"PostgreSQL query failed: {exception.Message}");
        }
    }
}