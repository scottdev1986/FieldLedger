using System.Security.Cryptography;
using System.Text;

namespace FieldLedger.Seeder;

internal enum CropType
{
    Corn,
    Soybean,
    Wheat
}

internal static class CropTypeExtensions
{
    public static string ToSqlValue(this CropType crop) => crop switch
    {
        CropType.Corn => "corn",
        CropType.Soybean => "soybean",
        CropType.Wheat => "wheat",
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };

    public static string ToDisplayName(this CropType crop) => crop switch
    {
        CropType.Corn => "Corn",
        CropType.Soybean => "Soybean",
        CropType.Wheat => "Wheat",
        _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, null)
    };
}

internal sealed record DemoUser(string Email, string DisplayName)
{
    public Guid SeededId => StableId.From($"user:{Email}");
}

internal sealed record DemoField(
    string Name,
    decimal Acreage,
    CropType DefaultCrop,
    CropType Crop2024,
    CropType Crop2025,
    CropType Crop2026,
    bool Archived)
{
    public CropType CropFor(int year) => year switch
    {
        2024 => Crop2024,
        2025 => Crop2025,
        2026 => Crop2026,
        _ => throw new ArgumentOutOfRangeException(nameof(year), year, "Unsupported demo season.")
    };
}

internal sealed record DemoOrganization(
    string Name,
    string Slug,
    IReadOnlyList<DemoField> Fields)
{
    public Guid SeededId => StableId.From($"organization:{Slug}");
}

internal static class DemoSeedData
{
    public const string DataSetVersion = "fieldledger-demo-v1";
    public const string Password = "FieldLedgerDemo!2026";

    public static readonly DateTimeOffset CreatedAt =
        new(2023, 8, 15, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset ArchivedAt =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset SeedCompletedAt =
        new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static readonly DemoUser Owner =
        new("owner@fieldledger.demo", "Morgan Owner");

    public static readonly DemoUser Agronomist =
        new("agronomist@fieldledger.demo", "Alex Agronomist");

    public static readonly DemoUser Viewer =
        new("viewer@fieldledger.demo", "Taylor Viewer");

    public static IReadOnlyList<DemoUser> Users { get; } =
        [Owner, Agronomist, Viewer];

    public static IReadOnlyList<int> SeasonYears { get; } = [2024, 2025, 2026];

    public static IReadOnlyList<DemoOrganization> Organizations { get; } =
    [
        new(
            "North Fork Farms",
            "north-fork-farms",
            [
                new("River Bottom 40", 40.0m, CropType.Corn,
                    CropType.Corn, CropType.Soybean, CropType.Corn, false),
                new("West Ridge", 62.5m, CropType.Soybean,
                    CropType.Soybean, CropType.Corn, CropType.Soybean, false),
                new("South Pivot", 118.0m, CropType.Corn,
                    CropType.Corn, CropType.Soybean, CropType.Corn, false),
                new("Home Place Wheat", 35.0m, CropType.Wheat,
                    CropType.Wheat, CropType.Soybean, CropType.Wheat, true)
            ]),
        new(
            "Prairie View Ag Co",
            "prairie-view-ag-co",
            [
                new("Big Creek 80", 80.0m, CropType.Corn,
                    CropType.Corn, CropType.Soybean, CropType.Corn, false),
                new("East Bench", 54.25m, CropType.Soybean,
                    CropType.Soybean, CropType.Corn, CropType.Soybean, false),
                new("Mill Road Wheat", 72.0m, CropType.Wheat,
                    CropType.Wheat, CropType.Soybean, CropType.Wheat, false)
            ])
    ];
}

internal static class StableId
{
    public static Guid From(string value)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(digest.AsSpan(0, 16));
    }
}
