using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;

namespace FieldLedger.Seeder;

internal sealed class DemoSeeder(string connectionString)
{
    private const long AdvisoryLockKey = 0x464C445345454445;

    public async Task RunAsync(
        string seedVersion,
        bool force,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Seed version: {seedVersion}");
        Console.WriteLine($"Force rerun: {force}");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await AcquireSeedLockAsync(connection, transaction, cancellationToken);

            if (!force && await SeedMarkerExistsAsync(
                    connection,
                    transaction,
                    seedVersion,
                    cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                Console.WriteLine($"Seed marker '{seedVersion}' already exists; nothing to do.");
                return;
            }

            var passwordHasher = new PasswordHasher<DemoUser>();
            var userIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            foreach (var user in DemoSeedData.Users)
            {
                userIds[user.Email] = await UpsertUserAsync(
                    connection,
                    transaction,
                    passwordHasher,
                    user,
                    cancellationToken);
            }

            var ownerId = userIds[DemoSeedData.Owner.Email];
            var agronomistId = userIds[DemoSeedData.Agronomist.Email];
            var viewerId = userIds[DemoSeedData.Viewer.Email];
            var fieldCount = 0;
            var seasonCount = 0;
            var fieldSeasonCount = 0;
            var activityCount = 0;

            foreach (var organization in DemoSeedData.Organizations)
            {
                var organizationId = await UpsertOrganizationAsync(
                    connection,
                    transaction,
                    organization,
                    ownerId,
                    cancellationToken);

                await NormalizeMembershipsAsync(
                    connection,
                    transaction,
                    organization,
                    organizationId,
                    ownerId,
                    agronomistId,
                    viewerId,
                    cancellationToken);

                // A previous demo may have upgraded to Pro and unarchived or added fields.
                // Archive first so resetting to Free cannot trip the active-field trigger.
                await ArchiveCurrentFieldsAsync(
                    connection,
                    transaction,
                    organizationId,
                    cancellationToken);
                await UpsertFreeEntitlementAsync(
                    connection,
                    transaction,
                    organizationId,
                    ownerId,
                    cancellationToken);

                var seasonIds = new Dictionary<int, Guid>();
                foreach (var year in DemoSeedData.SeasonYears)
                {
                    seasonIds[year] = await UpsertSeasonAsync(
                        connection,
                        transaction,
                        organizationId,
                        organization.Slug,
                        year,
                        cancellationToken);
                    seasonCount++;
                }

                foreach (var field in organization.Fields)
                {
                    var fieldId = await UpsertFieldAsync(
                        connection,
                        transaction,
                        organizationId,
                        organization.Slug,
                        field,
                        cancellationToken);
                    fieldCount++;

                    foreach (var year in DemoSeedData.SeasonYears)
                    {
                        var crop = field.CropFor(year);
                        var activityPlan = DeterministicActivityGenerator.Generate(
                            organization.Slug,
                            field,
                            crop,
                            year,
                            ownerId,
                            organization.Slug == "north-fork-farms" ? agronomistId : null);

                        await UpsertFieldSeasonAsync(
                            connection,
                            transaction,
                            organizationId,
                            fieldId,
                            seasonIds[year],
                            organization.Slug,
                            field.Name,
                            crop,
                            year,
                            activityPlan.TargetYieldPerAcre,
                            cancellationToken);
                        fieldSeasonCount++;

                        await DeleteActivitiesAsync(
                            connection,
                            transaction,
                            fieldId,
                            seasonIds[year],
                            cancellationToken);

                        foreach (var activity in activityPlan.Activities)
                        {
                            await InsertActivityAsync(
                                connection,
                                transaction,
                                organizationId,
                                fieldId,
                                seasonIds[year],
                                activity,
                                cancellationToken);
                            activityCount++;
                        }
                    }
                }
            }

            await UpsertSeedMarkerAsync(
                connection,
                transaction,
                seedVersion,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            Console.WriteLine(
                $"Seed complete: {DemoSeedData.Users.Count} users, " +
                $"{DemoSeedData.Organizations.Count} organizations, {fieldCount} fields, " +
                $"{seasonCount} seasons, {fieldSeasonCount} field seasons, " +
                $"{activityCount} activities.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task AcquireSeedLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(@lock_key);",
            connection,
            transaction);
        command.Parameters.AddWithValue("lock_key", AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> SeedMarkerExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string seedVersion,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select exists (select 1 from seed_runs where seed_key = @seed_key);",
            connection,
            transaction);
        command.Parameters.AddWithValue("seed_key", seedVersion);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Seed marker query returned no value."));
    }

    private static async Task<Guid> UpsertUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PasswordHasher<DemoUser> passwordHasher,
        DemoUser user,
        CancellationToken cancellationToken)
    {
        string? existingHash = null;
        await using (var lookupCommand = new NpgsqlCommand(
            "select password_hash from users where email = @email;",
            connection,
            transaction))
        {
            lookupCommand.Parameters.AddWithValue("email", user.Email);
            existingHash = (string?)await lookupCommand.ExecuteScalarAsync(cancellationToken);
        }

        var passwordHash = existingHash;
        if (passwordHash is null ||
            passwordHasher.VerifyHashedPassword(user, passwordHash, DemoSeedData.Password) ==
            PasswordVerificationResult.Failed)
        {
            passwordHash = passwordHasher.HashPassword(user, DemoSeedData.Password);
        }

        const string sql = """
            insert into users (id, email, display_name, password_hash, created_at)
            values (@id, @email, @display_name, @password_hash, @created_at)
            on conflict (email) do update
            set display_name = excluded.display_name,
                password_hash = excluded.password_hash,
                created_at = excluded.created_at
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", user.SeededId);
        command.Parameters.AddWithValue("email", user.Email);
        command.Parameters.AddWithValue("display_name", user.DisplayName);
        command.Parameters.AddWithValue("password_hash", passwordHash);
        command.Parameters.AddWithValue("created_at", DemoSeedData.CreatedAt);
        return await ExecuteGuidScalarAsync(command, cancellationToken);
    }

    private static async Task<Guid> UpsertOrganizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DemoOrganization organization,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into organizations (id, name, slug, created_by, created_at, archived_at)
            values (@id, @name, @slug, @created_by, @created_at, null)
            on conflict (slug) do update
            set name = excluded.name,
                created_by = excluded.created_by,
                created_at = excluded.created_at,
                archived_at = null
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", organization.SeededId);
        command.Parameters.AddWithValue("name", organization.Name);
        command.Parameters.AddWithValue("slug", organization.Slug);
        command.Parameters.AddWithValue("created_by", ownerId);
        command.Parameters.AddWithValue("created_at", DemoSeedData.CreatedAt);
        return await ExecuteGuidScalarAsync(command, cancellationToken);
    }

    private static async Task NormalizeMembershipsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DemoOrganization organization,
        Guid organizationId,
        Guid ownerId,
        Guid agronomistId,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        await UpsertMembershipAsync(
            connection,
            transaction,
            organizationId,
            ownerId,
            "owner",
            cancellationToken);

        if (organization.Slug == "north-fork-farms")
        {
            await UpsertMembershipAsync(
                connection,
                transaction,
                organizationId,
                agronomistId,
                "agronomist",
                cancellationToken);
            await UpsertMembershipAsync(
                connection,
                transaction,
                organizationId,
                viewerId,
                "viewer",
                cancellationToken);
            return;
        }

        await using var command = new NpgsqlCommand(
            """
            delete from organization_members
            where organization_id = @organization_id
              and user_id in (@agronomist_id, @viewer_id);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("agronomist_id", agronomistId);
        command.Parameters.AddWithValue("viewer_id", viewerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertMembershipAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into organization_members (
              organization_id,
              user_id,
              role,
              status,
              created_at
            )
            values (
              @organization_id,
              @user_id,
              cast(@role as member_role),
              'active',
              @created_at
            )
            on conflict (organization_id, user_id) do update
            set role = excluded.role,
                status = excluded.status,
                created_at = excluded.created_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("created_at", DemoSeedData.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ArchiveCurrentFieldsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            update fields
            set archived_at = @archived_at
            where organization_id = @organization_id
              and archived_at is null;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("archived_at", DemoSeedData.ArchivedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertFreeEntitlementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into entitlements (
              organization_id,
              plan,
              max_fields,
              csv_export_enabled,
              season_report_enabled,
              updated_by,
              updated_at
            )
            values (
              @organization_id,
              'free',
              3,
              false,
              false,
              @updated_by,
              @updated_at
            )
            on conflict (organization_id) do update
            set plan = excluded.plan,
                max_fields = excluded.max_fields,
                csv_export_enabled = excluded.csv_export_enabled,
                season_report_enabled = excluded.season_report_enabled,
                updated_by = excluded.updated_by,
                updated_at = excluded.updated_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("updated_by", ownerId);
        command.Parameters.AddWithValue("updated_at", DemoSeedData.SeedCompletedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> UpsertSeasonAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        string organizationSlug,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into seasons (
              id,
              organization_id,
              year,
              name,
              starts_on,
              ends_on,
              created_at
            )
            values (
              @id,
              @organization_id,
              @year,
              @name,
              @starts_on,
              @ends_on,
              @created_at
            )
            on conflict (organization_id, year) do update
            set name = excluded.name,
                starts_on = excluded.starts_on,
                ends_on = excluded.ends_on,
                created_at = excluded.created_at
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(
            "id",
            StableId.From(FormattableString.Invariant($"season:{organizationSlug}:{year}")));
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("year", year);
        command.Parameters.AddWithValue("name", FormattableString.Invariant($"{year} Season"));
        command.Parameters.AddWithValue("starts_on", new DateOnly(year - 1, 9, 1));
        command.Parameters.AddWithValue("ends_on", new DateOnly(year, 12, 15));
        command.Parameters.AddWithValue("created_at", DemoSeedData.CreatedAt);
        return await ExecuteGuidScalarAsync(command, cancellationToken);
    }

    private static async Task<Guid> UpsertFieldAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        string organizationSlug,
        DemoField field,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into fields (
              id,
              organization_id,
              name,
              acreage,
              default_crop,
              created_at,
              archived_at
            )
            values (
              @id,
              @organization_id,
              @name,
              @acreage,
              cast(@default_crop as crop_type),
              @created_at,
              @archived_at
            )
            on conflict (organization_id, name) do update
            set acreage = excluded.acreage,
                default_crop = excluded.default_crop,
                created_at = excluded.created_at,
                archived_at = excluded.archived_at
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", StableId.From($"field:{organizationSlug}:{field.Name}"));
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("name", field.Name);
        command.Parameters.AddWithValue("acreage", field.Acreage);
        command.Parameters.AddWithValue("default_crop", field.DefaultCrop.ToSqlValue());
        command.Parameters.AddWithValue("created_at", DemoSeedData.CreatedAt);
        command.Parameters.Add("archived_at", NpgsqlDbType.TimestampTz).Value =
            field.Archived ? DemoSeedData.ArchivedAt : DBNull.Value;
        return await ExecuteGuidScalarAsync(command, cancellationToken);
    }

    private static async Task UpsertFieldSeasonAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        Guid fieldId,
        Guid seasonId,
        string organizationSlug,
        string fieldName,
        CropType crop,
        int year,
        decimal targetYieldPerAcre,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into field_seasons (
              id,
              organization_id,
              field_id,
              season_id,
              crop,
              target_yield_per_acre,
              created_at
            )
            values (
              @id,
              @organization_id,
              @field_id,
              @season_id,
              cast(@crop as crop_type),
              @target_yield_per_acre,
              @created_at
            )
            on conflict (field_id, season_id) do update
            set organization_id = excluded.organization_id,
                crop = excluded.crop,
                target_yield_per_acre = excluded.target_yield_per_acre,
                created_at = excluded.created_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(
            "id",
            StableId.From(FormattableString.Invariant(
                $"field-season:{organizationSlug}:{fieldName}:{year}")));
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("field_id", fieldId);
        command.Parameters.AddWithValue("season_id", seasonId);
        command.Parameters.AddWithValue("crop", crop.ToSqlValue());
        command.Parameters.AddWithValue("target_yield_per_acre", targetYieldPerAcre);
        command.Parameters.AddWithValue("created_at", DemoSeedData.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteActivitiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid fieldId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "delete from activities where field_id = @field_id and season_id = @season_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("field_id", fieldId);
        command.Parameters.AddWithValue("season_id", seasonId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertActivityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        Guid fieldId,
        Guid seasonId,
        SeedActivity activity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into activities (
              id,
              organization_id,
              field_id,
              season_id,
              activity_type,
              activity_date,
              quantity,
              quantity_unit,
              cost_amount,
              revenue_amount,
              notes,
              created_by,
              created_at
            )
            values (
              @id,
              @organization_id,
              @field_id,
              @season_id,
              cast(@activity_type as activity_type),
              @activity_date,
              @quantity,
              @quantity_unit,
              @cost_amount,
              @revenue_amount,
              @notes,
              @created_by,
              @created_at
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", activity.Id);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("field_id", fieldId);
        command.Parameters.AddWithValue("season_id", seasonId);
        command.Parameters.AddWithValue("activity_type", activity.ActivityType);
        command.Parameters.AddWithValue("activity_date", activity.ActivityDate);
        command.Parameters.Add("quantity", NpgsqlDbType.Numeric).Value =
            activity.Quantity is null ? DBNull.Value : activity.Quantity.Value;
        command.Parameters.Add("quantity_unit", NpgsqlDbType.Text).Value =
            activity.QuantityUnit is null ? DBNull.Value : activity.QuantityUnit;
        command.Parameters.AddWithValue("cost_amount", activity.CostAmount);
        command.Parameters.AddWithValue("revenue_amount", activity.RevenueAmount);
        command.Parameters.Add("notes", NpgsqlDbType.Text).Value =
            activity.Notes is null ? DBNull.Value : activity.Notes;
        command.Parameters.AddWithValue("created_by", activity.CreatedBy);
        command.Parameters.AddWithValue("created_at", activity.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertSeedMarkerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string seedVersion,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into seed_runs (seed_key, completed_at, seed_version)
            values (@seed_key, @completed_at, @seed_version)
            on conflict (seed_key) do update
            set completed_at = excluded.completed_at,
                seed_version = excluded.seed_version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("seed_key", seedVersion);
        command.Parameters.AddWithValue("completed_at", DemoSeedData.SeedCompletedAt);
        command.Parameters.AddWithValue("seed_version", seedVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> ExecuteGuidScalarAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken) =>
        (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Database upsert returned no identifier."));
}
