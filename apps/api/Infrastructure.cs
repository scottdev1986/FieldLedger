using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace FieldLedger.Api;

public sealed record AuthOptions(string Secret, string Issuer, string Audience, int LifetimeMinutes)
{
    public static AuthOptions FromConfiguration(IConfiguration configuration)
    {
        var secret = configuration["AUTH_JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("AUTH_JWT_SECRET must be at least 32 bytes.");
        }

        var issuer = configuration["AUTH_JWT_ISSUER"] ?? "fieldledger-api";
        var audience = configuration["AUTH_JWT_AUDIENCE"] ?? "fieldledger";
        var lifetime = int.TryParse(
            configuration["AUTH_TOKEN_LIFETIME_MINUTES"],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 720;

        if (lifetime <= 0)
        {
            throw new InvalidOperationException("AUTH_TOKEN_LIFETIME_MINUTES must be positive.");
        }

        return new AuthOptions(secret, issuer, audience, lifetime);
    }
}

public interface IJwtTokenService
{
    string Issue(AppUser user);
    ClaimsPrincipal Validate(string token);
}

public sealed class JwtTokenService(
    AuthOptions options,
    TimeProvider timeProvider) : IJwtTokenService
{
    private readonly SymmetricSecurityKey _key =
        new(Encoding.UTF8.GetBytes(options.Secret));

    public string Issue(AppUser user)
    {
        var now = timeProvider.GetUtcNow();
        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName)
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(options.LifetimeMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, CreateValidationParameters(options, timeProvider), out _);
    }

    public static TokenValidationParameters CreateValidationParameters(
        AuthOptions options,
        TimeProvider timeProvider)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Name,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                if (notBefore is not null && expires is not null && notBefore > expires)
                {
                    throw new SecurityTokenInvalidLifetimeException(
                        "The token's not-before time is after its expiration time.");
                }

                if (notBefore is not null && notBefore > now)
                {
                    throw new SecurityTokenNotYetValidException("The token is not yet valid.");
                }

                if (expires is not null && expires <= now)
                {
                    throw new SecurityTokenExpiredException("The token has expired.");
                }

                return expires is not null;
            }
        };
    }
}

public interface IFieldLedgerDbSession
{
    Task<T> InUserTransaction<T>(
        ClaimsPrincipal user,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work,
        CancellationToken cancellationToken = default);
}

public sealed class FieldLedgerDbSession(NpgsqlDataSource dataSource) : IFieldLedgerDbSession
{
    public async Task<T> InUserTransaction<T>(
        ClaimsPrincipal user,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new ApiException(401, "unauthorized", "The bearer token has no subject claim.");
        if (!Guid.TryParse(subject, out var userId))
        {
            throw new ApiException(401, "unauthorized", "The bearer token subject is invalid.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var contextCommand = new NpgsqlCommand(
            "set local role authenticated; select set_config('app.user_id', @user_id, true);",
            connection,
            transaction))
        {
            contextCommand.Parameters.AddWithValue("user_id", userId.ToString());
            await contextCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var result = await work(connection, transaction);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}

public interface IAuthRepository
{
    Task<AppUser> CreateAsync(
        string email,
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken);
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<Guid?> FindIdByEmailAsync(string email, CancellationToken cancellationToken);
}

public sealed class AuthRepository(NpgsqlDataSource dataSource) : IAuthRepository
{
    public async Task<AppUser> CreateAsync(
        string email,
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select *
            from app.register_user(@email, @display_name, @password_hash);
            """,
            connection);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("display_name", displayName);
        command.Parameters.AddWithValue("password_hash", passwordHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return DbReaders.ReadUser(reader, passwordHash);
    }

    public async Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select *
            from app.get_user_for_login(@email);
            """,
            connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AppUser(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.MinValue,
                reader.GetString(3))
            : null;
    }

    public async Task<Guid?> FindIdByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "select id from app.get_user_for_login(@email);",
            connection);
        command.Parameters.AddWithValue("email", email);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }
}

public sealed class ApiException(
    int statusCode,
    string code,
    string message,
    IReadOnlyDictionary<string, string[]>? fieldErrors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; } = fieldErrors;
}

public sealed record MappedDatabaseError(int StatusCode, string Code, string Message);

public static class DatabaseErrorMapper
{
    public static MappedDatabaseError Map(string sqlState, string message, string? constraintName = null)
    {
        if (message.Contains("field_limit_reached", StringComparison.OrdinalIgnoreCase))
        {
            return new(422, "field_limit_reached", "Free organizations can have up to 3 active fields.");
        }

        if (message.Contains("only an organization owner", StringComparison.OrdinalIgnoreCase))
        {
            return new(403, "forbidden", "Only an organization owner can change the plan.");
        }

        return sqlState switch
        {
            PostgresErrorCodes.InsufficientPrivilege =>
                new(403, "forbidden", "You do not have permission to perform this operation."),
            PostgresErrorCodes.ForeignKeyViolation =>
                new(404, "not_found", "The requested resource was not found."),
            PostgresErrorCodes.UniqueViolation =>
                new(409, ConstraintCode(constraintName), "A resource with the same unique value already exists."),
            PostgresErrorCodes.CheckViolation =>
                new(422, "business_rule_failed", "The request violates a business rule."),
            _ => new(500, "internal_error", "An unexpected server error occurred.")
        };
    }

    private static string ConstraintCode(string? constraintName) => constraintName switch
    {
        "users_email_key" => "email_already_registered",
        "organizations_slug_key" => "slug_already_exists",
        "fields_organization_id_name_key" => "field_name_exists",
        "seasons_organization_id_year_key" => "season_year_exists",
        "organization_members_pkey" => "already_a_member",
        _ => "conflict"
    };
}

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException exception)
        {
            await ApiErrorWriter.WriteAsync(
                context,
                exception.StatusCode,
                exception.Code,
                exception.Message,
                exception.FieldErrors);
        }
        catch (PostgresException exception)
        {
            var mapped = DatabaseErrorMapper.Map(
                exception.SqlState,
                exception.MessageText,
                exception.ConstraintName);
            if (mapped.StatusCode >= 500)
            {
                logger.LogError(exception, "Unhandled PostgreSQL error {SqlState}", exception.SqlState);
            }

            await ApiErrorWriter.WriteAsync(
                context,
                mapped.StatusCode,
                mapped.Code,
                mapped.Message);
        }
        catch (BadHttpRequestException exception)
        {
            await ApiErrorWriter.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "validation_error",
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API exception");
            await ApiErrorWriter.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected server error occurred.");
        }
    }
}

public static class ApiErrorWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(
            new ErrorEnvelope(new ApiError(code, message, traceId, fieldErrors)),
            Options,
            context.RequestAborted);
    }
}

public sealed record EntitlementDecision(bool Allowed, string? Code = null, string? Message = null);

public static class EntitlementGate
{
    public static EntitlementDecision CanCreateActiveField(int? maxFields, int activeFieldCount) =>
        maxFields is null || activeFieldCount < maxFields
            ? new(true)
            : new(false, "field_limit_reached", $"This organization can have up to {maxFields} active fields.");

    public static EntitlementDecision CanExportCsv(bool enabled, MemberRole role) =>
        !enabled
            ? new(false, "pro_required", "CSV export requires the Pro plan.")
            : role is MemberRole.Owner or MemberRole.Agronomist
                ? new(true)
                : new(false, "forbidden", "Viewers cannot export activity data.");

    public static EntitlementDecision CanViewSeasonReport(bool enabled) =>
        enabled
            ? new(true)
            : new(false, "pro_required", "Season reports require the Pro plan.");
}

public static class CsvFormatter
{
    public static string FormatRow(params object?[] values) =>
        string.Join(',', values.Select(FormatValue));

    private static string FormatValue(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}

internal static class DbReaders
{
    public static AppUser ReadUser(NpgsqlDataReader reader, string passwordHash = "") => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetFieldValue<DateTimeOffset>(3),
        passwordHash);

    public static MemberRole ParseRole(string value) => value switch
    {
        "owner" => MemberRole.Owner,
        "agronomist" => MemberRole.Agronomist,
        "viewer" => MemberRole.Viewer,
        _ => throw new InvalidOperationException($"Unknown member role '{value}'.")
    };

    public static PlanTier ParsePlan(string value) => value switch
    {
        "free" => PlanTier.Free,
        "pro" => PlanTier.Pro,
        _ => throw new InvalidOperationException($"Unknown plan '{value}'.")
    };

    public static ActivityKind ParseActivity(string value) => value switch
    {
        "planting" => ActivityKind.Planting,
        "spraying" => ActivityKind.Spraying,
        "irrigation" => ActivityKind.Irrigation,
        "fertilizer" => ActivityKind.Fertilizer,
        "harvest" => ActivityKind.Harvest,
        "note" => ActivityKind.Note,
        _ => throw new InvalidOperationException($"Unknown activity type '{value}'.")
    };

    public static string ToDb(MemberRole value) => value.ToString().ToLowerInvariant();
    public static string ToDb(PlanTier value) => value.ToString().ToLowerInvariant();
    public static string ToDb(ActivityKind value) => value.ToString().ToLowerInvariant();
}
