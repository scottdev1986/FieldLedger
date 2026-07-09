using System.Text.Json;
using FieldLedger.Api;
using Npgsql;

namespace FieldLedger.Api.Tests;

public sealed class ErrorMappingTests
{
    [Theory]
    [InlineData("P0001", "field_limit_reached", null, 422, "field_limit_reached")]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege, "permission denied", null, 403, "forbidden")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "duplicate", "users_email_key", 409, "email_already_registered")]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "missing", null, 404, "not_found")]
    public void Map_ReturnsContractStatusAndCode(
        string sqlState,
        string message,
        string? constraint,
        int expectedStatus,
        string expectedCode)
    {
        var mapped = DatabaseErrorMapper.Map(sqlState, message, constraint);

        Assert.Equal(expectedStatus, mapped.StatusCode);
        Assert.Equal(expectedCode, mapped.Code);
    }

    [Fact]
    public void ErrorEnvelope_SerializesToContractShape()
    {
        var json = JsonSerializer.Serialize(
            new ErrorEnvelope(new ApiError("forbidden", "Denied", "trace-1")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            "{\"error\":{\"code\":\"forbidden\",\"message\":\"Denied\",\"traceId\":\"trace-1\",\"fieldErrors\":null}}",
            json);
    }
}
