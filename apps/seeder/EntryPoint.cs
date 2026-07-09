namespace FieldLedger.Seeder;

internal static class EntryPoint
{
    public static async Task<int> RunAsync(string[] args)
    {
        CliOptions options;

        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            Console.Error.WriteLine("Run with --help for usage.");
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var connectionString = Environment.GetEnvironmentVariable("DATABASE_ADMIN_URL");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("ERROR: DATABASE_ADMIN_URL is required.");
            return 2;
        }

        using var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        try
        {
            switch (options.Command)
            {
                case SeederCommand.Migrate:
                    await new MigrationRunner(connectionString)
                        .RunAsync(cancellationSource.Token);
                    break;
                case SeederCommand.Seed:
                    await new DemoSeeder(connectionString)
                        .RunAsync(options.SeedVersion, options.Force, cancellationSource.Token);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported command: {options.Command}.");
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            Console.Error.WriteLine("ERROR: Operation cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            FieldLedger database migration runner and demo-data seeder

            Usage:
              dotnet run --project apps/seeder -- migrate
              dotnet run --project apps/seeder -- seed [options]
              dotnet run --project apps/seeder -- [options]
              docker compose run --rm seeder [options]

            Commands:
              migrate                 Apply pending SQL migrations in filename order.
              seed                    Seed demo data (the default command).

            Seed options:
              --seed-version <value>  Seed marker key. Defaults to SEED_VERSION or
                                      fieldledger-demo-v1.
              --force                 Rerun even when the seed marker already exists.
              --help, -h              Show help.

            Environment:
              DATABASE_ADMIN_URL      Required Npgsql connection string.
              MIGRATIONS_PATH         Migration directory; defaults to /db/migrations
                                      with local-repository fallbacks.
              SEED_VERSION            Default seed marker key.
            """);
    }
}
