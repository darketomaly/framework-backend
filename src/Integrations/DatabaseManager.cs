using Npgsql;

namespace framework_backend;

public enum DatabaseQueryExitCode
{
    Success = 0,
    NotFound = 1,
    ConnectionFailed = 2,
    QueryFailed = 3
}

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

            string databaseConnectionString;

            if (Uri.TryCreate(databaseUrl, UriKind.Absolute, out var databaseUri) &&
                databaseUri.Scheme is "postgres" or "postgresql")
            {
                if (string.IsNullOrWhiteSpace(databaseUri.UserInfo))
                {
                    Console.WriteLine("PostgreSQL connection skipped: DATABASE_URL has no credentials");
                    return null;
                }

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
            else
            {
                databaseConnectionString = new NpgsqlConnectionStringBuilder(databaseUrl).ConnectionString;
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
        catch (InvalidOperationException exception)
        {
            Console.WriteLine($"PostgreSQL connection configuration is invalid: {exception.Message}");
        }

        return null;
    }

    public static async Task<(DatabaseQueryExitCode ExitCode, string? Value)> QueryValue(string key)
    {
        await using var database = await Connect();

        if (database is null)
        {
            return (DatabaseQueryExitCode.ConnectionFailed, null);
        }

        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT value FROM secret_keys WHERE secret_key = @key LIMIT 1",
                database);
            command.Parameters.AddWithValue("key", key);

            var result = await command.ExecuteScalarAsync();

            if (result is null || result is DBNull)
            {
                Console.WriteLine($"PostgreSQL value for key '{key}': <not found>");
                return (DatabaseQueryExitCode.NotFound, null);
            }

            var value = result.ToString();
            Console.WriteLine($"PostgreSQL value for key '{key}': {value}");
            return (DatabaseQueryExitCode.Success, value);
        }
        catch (NpgsqlException exception)
        {
            Console.WriteLine($"PostgreSQL query failed: {exception.Message}");
            return (DatabaseQueryExitCode.QueryFailed, null);
        }
    }

    public static async Task AddValue(string key, string value)
    {
        // To do
        // Add entry into the database from the discord command
    }
}