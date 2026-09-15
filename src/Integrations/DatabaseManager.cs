using Npgsql;

namespace framework_backend;

public enum DatabaseQueryExitCode
{
    QuerySuccess = 0,
    ValueNotFound = 1,
    ConnectionFailed = 2,
    QueryFailed = 3,
    AddValueSuccess = 4,
    AddValueFailed = 5,
}

public static class DatabaseTable
{
    public static string SecretKeys => GetRequiredTableName("SECRET_KEYS_TABLE");
    public static string AutoReactChannels => GetRequiredTableName("AUTO_REACT_CHANNELS_TABLE");

    private static string GetRequiredTableName(string environmentVariable)
    {
        return Environment.GetEnvironmentVariable(environmentVariable);;
    }
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

    public static async Task<(DatabaseQueryExitCode ExitCode, string? Value)> QueryValue(string key, string tableName)
    {
        await using var database = await Connect();

        if (database is null)
        {
            return (DatabaseQueryExitCode.ConnectionFailed, null);
        }

        try
        {
            await using var command = new NpgsqlCommand(
                $"SELECT value FROM {tableName} WHERE key = @key LIMIT 1",
                database);
            command.Parameters.AddWithValue("key", key);

            var result = await command.ExecuteScalarAsync();

            if (result is null || result is DBNull)
            {
                Console.WriteLine($"PostgreSQL value for key '{key}': <not found>");
                return (DatabaseQueryExitCode.ValueNotFound, null);
            }

            var value = result.ToString();
            Console.WriteLine($"PostgreSQL value for key '{key}': {value}");
            return (DatabaseQueryExitCode.QuerySuccess, value);
        }
        catch (NpgsqlException exception)
        {
            Console.WriteLine($"PostgreSQL query failed: {exception.Message}");
            return (DatabaseQueryExitCode.QueryFailed, null);
        }
    }

    public static async Task<DatabaseQueryExitCode> AddValue(string key, string value, string tableName)
    {
        await using var database = await Connect();

        if (database is null)
        {
            return DatabaseQueryExitCode.ConnectionFailed;
        }

        try
        {
            await using var keyCheckCommand = new NpgsqlCommand(
                $"SELECT 1 FROM {tableName} WHERE key = @key LIMIT 1",
                database);
            keyCheckCommand.Parameters.AddWithValue("key", key);

            var keyExists = await keyCheckCommand.ExecuteScalarAsync();

            if (keyExists is not null && keyExists is not DBNull)
            {
                await using var updateCommand = new NpgsqlCommand(
                    $"UPDATE {tableName} SET value = @value WHERE key = @key",
                    database);
                updateCommand.Parameters.AddWithValue("key", key);
                updateCommand.Parameters.AddWithValue("value", value);

                await updateCommand.ExecuteNonQueryAsync();

                Console.WriteLine($"PostgreSQL value updated for key '{key}'");
                return DatabaseQueryExitCode.AddValueSuccess;
            }

            await using var insertCommand = new NpgsqlCommand(
                $"INSERT INTO {tableName} (key, value) VALUES (@key, @value)",
                database);
            insertCommand.Parameters.AddWithValue("key", key);
            insertCommand.Parameters.AddWithValue("value", value);

            await insertCommand.ExecuteNonQueryAsync();

            Console.WriteLine($"PostgreSQL value added for key '{key}'");
            return DatabaseQueryExitCode.AddValueSuccess;
        }
        catch (NpgsqlException exception)
        {
            Console.WriteLine($"PostgreSQL add/update failed: {exception.Message}");
            return DatabaseQueryExitCode.AddValueFailed;
        }
    }
}