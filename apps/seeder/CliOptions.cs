namespace FieldLedger.Seeder;

internal enum SeederCommand
{
    Seed,
    Migrate
}

internal sealed record CliOptions(
    SeederCommand Command,
    string SeedVersion,
    bool Force,
    bool ShowHelp)
{
    private const string DefaultSeedVersion = "fieldledger-demo-v1";

    public static CliOptions Parse(string[] args)
    {
        var command = SeederCommand.Seed;
        var seedVersion = Environment.GetEnvironmentVariable("SEED_VERSION") ?? DefaultSeedVersion;
        var force = false;
        var showHelp = false;
        var index = 0;

        if (args.Length > 0 && !args[0].StartsWith("-", StringComparison.Ordinal))
        {
            command = args[0] switch
            {
                "seed" => SeederCommand.Seed,
                "migrate" => SeederCommand.Migrate,
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
            };
            index = 1;
        }

        for (; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--seed-version" when command == SeederCommand.Seed:
                    if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                    {
                        throw new ArgumentException("--seed-version requires a non-empty value.");
                    }

                    seedVersion = args[index];
                    break;
                case "--force" when command == SeederCommand.Seed:
                    force = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"Option '{args[index]}' is not valid for the {command.ToString().ToLowerInvariant()} command.");
            }
        }

        if (string.IsNullOrWhiteSpace(seedVersion))
        {
            throw new ArgumentException("The seed version cannot be empty.");
        }

        return new CliOptions(command, seedVersion, force, showHelp);
    }
}
