using Npgsql;

namespace FieldLedger.Seeder;

internal sealed class MigrationRunner(string connectionString)
{
    private const long AdvisoryLockKey = 0x464C444C45444752;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var migrationsDirectory = ResolveMigrationsDirectory();
        var migrationFiles = Directory
            .EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine($"Migrations directory: {migrationsDirectory}");
        Console.WriteLine($"Discovered {migrationFiles.Length} migration file(s).");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await AcquireLockAsync(connection, cancellationToken);
        Console.WriteLine("Acquired migration advisory lock.");

        try
        {
            await EnsureMigrationsTableAsync(connection, cancellationToken);

            var appliedCount = 0;
            var skippedCount = 0;

            foreach (var path in migrationFiles)
            {
                var filename = Path.GetFileName(path);
                if (await IsAppliedAsync(connection, filename, cancellationToken))
                {
                    Console.WriteLine($"SKIP  {filename}");
                    skippedCount++;
                    continue;
                }

                Console.WriteLine($"APPLY {filename}");
                var sql = await File.ReadAllTextAsync(path, cancellationToken);

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await using (var migrationCommand = new NpgsqlCommand(sql, connection, transaction))
                    {
                        migrationCommand.CommandTimeout = 0;
                        await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await using (var markerCommand = new NpgsqlCommand(
                        "insert into schema_migrations (filename) values (@filename);",
                        connection,
                        transaction))
                    {
                        markerCommand.Parameters.AddWithValue("filename", filename);
                        await markerCommand.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    appliedCount++;
                    Console.WriteLine($"DONE  {filename}");
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }

            Console.WriteLine($"Migration run complete: {appliedCount} applied, {skippedCount} skipped.");
        }
        finally
        {
            await ReleaseLockAsync(connection);
            Console.WriteLine("Released migration advisory lock.");
        }
    }

    private static string ResolveMigrationsDirectory()
    {
        var configuredPath = Environment.GetEnvironmentVariable("MIGRATIONS_PATH");
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(configuredPath);
        }

        candidates.Add("/db/migrations");
        candidates.Add("../../db/migrations");
        candidates.Add("./db/migrations");

        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find a migrations directory. Checked: {string.Join(", ", candidates)}.");
    }

    private static async Task AcquireLockAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select pg_advisory_lock(@lock_key);",
            connection);
        command.Parameters.AddWithValue("lock_key", AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            return;
        }

        await using var command = new NpgsqlCommand(
            "select pg_advisory_unlock(@lock_key);",
            connection);
        command.Parameters.AddWithValue("lock_key", AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task EnsureMigrationsTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            create table if not exists schema_migrations (
              filename text primary key,
              applied_at timestamptz not null default now()
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(
        NpgsqlConnection connection,
        string filename,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select exists (select 1 from schema_migrations where filename = @filename);",
            connection);
        command.Parameters.AddWithValue("filename", filename);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Migration marker query returned no value."));
    }
}
