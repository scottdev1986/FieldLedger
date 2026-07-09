namespace FieldLedger.Api.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_DATABASE_URL")))
        {
            Skip = "Set TEST_DATABASE_URL to run PostgreSQL-backed API integration tests.";
        }
    }
}
