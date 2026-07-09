using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FieldLedger.Seeder;

internal sealed record SeedActivity(
    Guid Id,
    string ActivityType,
    DateOnly ActivityDate,
    decimal? Quantity,
    string? QuantityUnit,
    decimal CostAmount,
    decimal RevenueAmount,
    string? Notes,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

internal sealed record FieldSeasonPlan(
    decimal TargetYieldPerAcre,
    IReadOnlyList<SeedActivity> Activities);

internal static class DeterministicActivityGenerator
{
    public static FieldSeasonPlan Generate(
        string organizationSlug,
        DemoField field,
        CropType crop,
        int seasonYear,
        Guid ownerId,
        Guid? agronomistId)
    {
        var seedKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{DemoSeedData.DataSetVersion}:{organizationSlug}:{field.Name}:{crop.ToSqlValue()}:{seasonYear}");
        var random = StableRandom.From(seedKey);
        var targetYield = GenerateYield(random, crop);
        var activities = new List<ActivityDraft>();

        var plantingDate = RandomDate(random, PlantingWindow(crop, seasonYear));
        activities.Add(new ActivityDraft(
            "planting",
            plantingDate,
            field.Acreage,
            "acres",
            PerAcreCost(random, field.Acreage, SeedCostRange(crop)),
            0m,
            FormattableString.Invariant(
                $"Planted {crop.ToDisplayName()} across {field.Acreage:0.##} acres."),
            PickCreator(random, ownerId, agronomistId)));

        if (random.NextBool(3, 4))
        {
            var fertilizerDate = crop == CropType.Wheat
                ? RandomDate(random, new DateWindow(
                    new DateOnly(seasonYear, 3, 15),
                    new DateOnly(seasonYear, 4, 20)))
                : plantingDate.AddDays(random.NextInt(7, 24));
            var poundsPerAcre = random.NextInt(70, 180);
            activities.Add(new ActivityDraft(
                "fertilizer",
                fertilizerDate,
                RoundQuantity(field.Acreage * poundsPerAcre),
                "lb",
                PerAcreCost(random, field.Acreage, (5_500, 11_500)),
                0m,
                FormattableString.Invariant(
                    $"Applied {poundsPerAcre} lb/acre nutrient blend."),
                PickCreator(random, ownerId, agronomistId)));
        }

        var sprayCount = random.NextInt(1, 3);
        var sprayDates = Enumerable.Range(0, sprayCount)
            .Select(_ => RandomDate(random, SprayWindow(crop, seasonYear)))
            .Order()
            .ToArray();

        for (var index = 0; index < sprayDates.Length; index++)
        {
            activities.Add(new ActivityDraft(
                "spraying",
                sprayDates[index],
                field.Acreage,
                "acres",
                PerAcreCost(random, field.Acreage, (1_800, 3_600)),
                0m,
                FormattableString.Invariant(
                    $"Crop protection pass {index + 1} of {sprayDates.Length}."),
                PickCreator(random, ownerId, agronomistId)));
        }

        var irrigationRange = IrrigationCountRange(field, crop);
        var irrigationCount = random.NextInt(irrigationRange.Minimum, irrigationRange.Maximum);
        var irrigationDates = Enumerable.Range(0, irrigationCount)
            .Select(_ => RandomDate(random, IrrigationWindow(crop, seasonYear)))
            .Order()
            .ToArray();

        foreach (var irrigationDate in irrigationDates)
        {
            var inches = random.NextInt(50, 150) / 100m;
            activities.Add(new ActivityDraft(
                "irrigation",
                irrigationDate,
                inches,
                "inches",
                PerAcreCost(random, field.Acreage, (800, 2_000)),
                0m,
                FormattableString.Invariant($"Applied {inches:0.00} inches of irrigation."),
                PickCreator(random, ownerId, agronomistId)));
        }

        var harvestDate = RandomDate(random, HarvestWindow(crop, seasonYear));
        var actualYield = GenerateYield(random, crop);
        var pricePerBushel = GeneratePrice(random, crop);
        var harvestedBushels = RoundQuantity(actualYield * field.Acreage);
        var harvestRevenue = Money(actualYield * field.Acreage * pricePerBushel);
        activities.Add(new ActivityDraft(
            "harvest",
            harvestDate,
            harvestedBushels,
            "bushels",
            PerAcreCost(random, field.Acreage, (4_500, 7_500)),
            harvestRevenue,
            FormattableString.Invariant(
                $"Harvested {actualYield:0.00} bu/acre at ${pricePerBushel:0.00}/bu."),
            PickCreator(random, ownerId, agronomistId)));

        if (random.NextBool(1, 2))
        {
            activities.Add(new ActivityDraft(
                "note",
                harvestDate.AddDays(random.NextInt(1, 7)),
                null,
                null,
                0m,
                0m,
                "Season closeout: yields and input records reconciled.",
                PickCreator(random, ownerId, agronomistId)));
        }

        var seededActivities = activities
            .OrderBy(activity => activity.ActivityDate)
            .ThenBy(activity => activity.ActivityType, StringComparer.Ordinal)
            .Select((activity, index) => new SeedActivity(
                StableId.From(FormattableString.Invariant($"activity:{seedKey}:{index}")),
                activity.ActivityType,
                activity.ActivityDate,
                activity.Quantity,
                activity.QuantityUnit,
                activity.CostAmount,
                activity.RevenueAmount,
                activity.Notes,
                activity.CreatedBy,
                AtNoonUtc(activity.ActivityDate)))
            .ToArray();

        return new FieldSeasonPlan(targetYield, seededActivities);
    }

    private static DateWindow PlantingWindow(CropType crop, int year) => crop switch
    {
        CropType.Corn => new(new DateOnly(year, 4, 10), new DateOnly(year, 5, 25)),
        CropType.Soybean => new(new DateOnly(year, 5, 1), new DateOnly(year, 6, 20)),
        CropType.Wheat => new(new DateOnly(year - 1, 9, 10), new DateOnly(year - 1, 10, 20)),
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    private static DateWindow SprayWindow(CropType crop, int year) => crop switch
    {
        CropType.Corn => new(new DateOnly(year, 5, 15), new DateOnly(year, 7, 31)),
        CropType.Soybean => new(new DateOnly(year, 6, 1), new DateOnly(year, 8, 15)),
        CropType.Wheat => new(new DateOnly(year, 4, 1), new DateOnly(year, 5, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    private static DateWindow IrrigationWindow(CropType crop, int year) => crop switch
    {
        CropType.Corn => new(new DateOnly(year, 6, 1), new DateOnly(year, 8, 25)),
        CropType.Soybean => new(new DateOnly(year, 6, 15), new DateOnly(year, 8, 20)),
        CropType.Wheat => new(new DateOnly(year, 4, 15), new DateOnly(year, 5, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    private static DateWindow HarvestWindow(CropType crop, int year) => crop switch
    {
        CropType.Corn => new(new DateOnly(year, 9, 20), new DateOnly(year, 11, 15)),
        CropType.Soybean => new(new DateOnly(year, 9, 10), new DateOnly(year, 10, 31)),
        CropType.Wheat => new(new DateOnly(year, 6, 15), new DateOnly(year, 7, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    private static (int Minimum, int Maximum) IrrigationCountRange(DemoField field, CropType crop)
    {
        if (field.Name.Contains("Pivot", StringComparison.Ordinal))
        {
            return (2, 4);
        }

        return crop switch
        {
            CropType.Corn => (0, 3),
            CropType.Soybean => (0, 2),
            CropType.Wheat => (0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
        };
    }

    private static (int Minimum, int Maximum) SeedCostRange(CropType crop) => crop switch
    {
        CropType.Corn => (9_000, 13_000),
        CropType.Soybean => (5_500, 8_500),
        CropType.Wheat => (4_000, 6_500),
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    private static decimal GenerateYield(StableRandom random, CropType crop) => crop switch
    {
        CropType.Corn => random.NextInt(15_000, 23_000) / 100m,
        CropType.Soybean => random.NextInt(4_000, 7_500) / 100m,
        CropType.Wheat => random.NextInt(5_500, 10_000) / 100m,
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    private static decimal GeneratePrice(StableRandom random, CropType crop) => crop switch
    {
        CropType.Corn => random.NextInt(380, 550) / 100m,
        CropType.Soybean => random.NextInt(980, 1_380) / 100m,
        CropType.Wheat => random.NextInt(500, 780) / 100m,
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    private static decimal PerAcreCost(
        StableRandom random,
        decimal acreage,
        (int Minimum, int Maximum) centsRange) =>
        Money(acreage * random.NextInt(centsRange.Minimum, centsRange.Maximum) / 100m);

    private static DateOnly RandomDate(StableRandom random, DateWindow window) =>
        window.Start.AddDays(random.NextInt(0, window.End.DayNumber - window.Start.DayNumber));

    private static Guid PickCreator(StableRandom random, Guid ownerId, Guid? agronomistId) =>
        agronomistId is not null && random.NextBool(2, 3)
            ? agronomistId.Value
            : ownerId;

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundQuantity(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static DateTimeOffset AtNoonUtc(DateOnly date) =>
        new(date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));

    private readonly record struct DateWindow(DateOnly Start, DateOnly End);

    private sealed record ActivityDraft(
        string ActivityType,
        DateOnly ActivityDate,
        decimal? Quantity,
        string? QuantityUnit,
        decimal CostAmount,
        decimal RevenueAmount,
        string? Notes,
        Guid CreatedBy);
}

internal sealed class StableRandom
{
    private uint _state;

    private StableRandom(uint seed)
    {
        _state = seed == 0 ? 0x9E3779B9u : seed;
    }

    public static StableRandom From(string seedValue)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(seedValue));
        return new StableRandom(BinaryPrimitives.ReadUInt32LittleEndian(digest));
    }

    public int NextInt(int minimumInclusive, int maximumInclusive)
    {
        if (minimumInclusive > maximumInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumInclusive),
                "Minimum must be less than or equal to maximum.");
        }

        var range = (uint)(maximumInclusive - minimumInclusive + 1);
        return minimumInclusive + (int)(NextUInt32() % range);
    }

    public bool NextBool(int numerator, int denominator)
    {
        if (numerator < 0 || denominator <= 0 || numerator > denominator)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "Invalid probability fraction.");
        }

        return NextInt(1, denominator) <= numerator;
    }

    private uint NextUInt32()
    {
        var value = _state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _state = value;
        return value;
    }
}
