using System.Security.Claims;
using Npgsql;

namespace FieldLedger.Api.Infrastructure.Database;

// The NpgsqlDataSource is built from DATABASE_URL and connects as fieldledger_api
// (member of `authenticated`, no BYPASSRLS). Migrator credentials are never used here.
public sealed class FieldLedgerDbSession
{
    private readonly NpgsqlDataSource _dataSource;

    public FieldLedgerDbSession(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<T> InUserTransaction<T>(
        ClaimsPrincipal user,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        // `sub` is the user uuid from the API's own HS256 JWT.
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("Authenticated user is missing subject claim.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                set local role authenticated;
                select set_config('app.user_id', @user_id, true);
                """;
            cmd.Parameters.AddWithValue("user_id", userId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            var result = await work(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
