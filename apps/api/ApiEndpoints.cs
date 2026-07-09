using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;

namespace FieldLedger.Api;

public static class ApiEndpoints
{
    private static readonly string[] AllowedCrops = ["corn", "soybean", "wheat"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/auth/register", RegisterAsync).AllowAnonymous();
        api.MapPost("/auth/login", LoginAsync).AllowAnonymous();
        api.MapGet("/auth/me", MeAsync);

        api.MapGet("/orgs", ListOrganizationsAsync);
        api.MapPost("/orgs", CreateOrganizationAsync);
        api.MapGet("/orgs/{orgId:guid}", GetOrganizationAsync);
        api.MapPatch("/orgs/{orgId:guid}", UpdateOrganizationAsync);
        api.MapGet("/orgs/{orgId:guid}/dashboard", GetDashboardAsync);

        api.MapGet("/orgs/{orgId:guid}/members", ListMembersAsync);
        api.MapPost("/orgs/{orgId:guid}/members", AddMemberAsync);
        api.MapPatch("/orgs/{orgId:guid}/members/{userId:guid}", UpdateMemberAsync);
        api.MapDelete("/orgs/{orgId:guid}/members/{userId:guid}", DeleteMemberAsync);

        api.MapGet("/orgs/{orgId:guid}/fields", ListFieldsAsync);
        api.MapPost("/orgs/{orgId:guid}/fields", CreateFieldAsync);
        api.MapGet("/orgs/{orgId:guid}/fields/{fieldId:guid}", GetFieldAsync);
        api.MapPatch("/orgs/{orgId:guid}/fields/{fieldId:guid}", UpdateFieldAsync);
        api.MapDelete("/orgs/{orgId:guid}/fields/{fieldId:guid}", ArchiveFieldAsync);

        api.MapGet("/orgs/{orgId:guid}/seasons", ListSeasonsAsync);
        api.MapPost("/orgs/{orgId:guid}/seasons", CreateSeasonAsync);
        api.MapGet("/orgs/{orgId:guid}/seasons/{seasonId:guid}", GetSeasonAsync);
        api.MapPatch("/orgs/{orgId:guid}/seasons/{seasonId:guid}", UpdateSeasonAsync);

        api.MapGet("/orgs/{orgId:guid}/fields/{fieldId:guid}/activities", ListActivitiesAsync);
        api.MapPost("/orgs/{orgId:guid}/fields/{fieldId:guid}/activities", CreateActivityAsync);
        api.MapPatch("/orgs/{orgId:guid}/activities/{activityId:guid}", UpdateActivityAsync);
        api.MapDelete("/orgs/{orgId:guid}/activities/{activityId:guid}", DeleteActivityAsync);

        api.MapGet("/orgs/{orgId:guid}/insights", GetInsightsAsync);
        api.MapGet("/orgs/{orgId:guid}/seasons/{seasonId:guid}/report", GetSeasonReportAsync);
        api.MapGet("/orgs/{orgId:guid}/exports/activities.csv", ExportActivitiesCsvAsync);

        api.MapGet("/orgs/{orgId:guid}/billing", GetBillingAsync);
        api.MapPost("/orgs/{orgId:guid}/billing/upgrade", UpgradeAsync);
        api.MapPost("/orgs/{orgId:guid}/billing/downgrade", DowngradeAsync);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IAuthRepository repository,
        IPasswordHasher<AppUser> passwordHasher,
        IJwtTokenService jwt,
        CancellationToken cancellationToken)
    {
        var errors = ValidateAuthInput(request.Email, request.Password, request.DisplayName);
        ThrowIfInvalid(errors);
        var email = NormalizeEmail(request.Email!);
        var displayName = request.DisplayName!.Trim();
        var shell = new AppUser(Guid.Empty, email, displayName, DateTimeOffset.MinValue);
        var passwordHash = passwordHasher.HashPassword(shell, request.Password!);

        try
        {
            var user = await repository.CreateAsync(email, displayName, passwordHash, cancellationToken);
            return Results.Created("/api/auth/me", new AuthResponse(jwt.Issue(user), user));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ApiException(409, "email_already_registered", "An account with this email already exists.");
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthRepository repository,
        IPasswordHasher<AppUser> passwordHasher,
        IJwtTokenService jwt,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        var errors = ValidateAuthInput(request.Email, request.Password, null, validateDisplayName: false);
        ThrowIfInvalid(errors);
        var user = await repository.FindByEmailAsync(NormalizeEmail(request.Email!), cancellationToken);

        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!)
            == PasswordVerificationResult.Failed)
        {
            throw new ApiException(401, "invalid_credentials", "The email or password is incorrect.");
        }

        var loginPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())],
            authenticationType: "verified_credentials"));
        var profile = await db.InUserTransaction(
            loginPrincipal,
            (connection, transaction) => ReadCurrentUserAsync(
                connection,
                transaction,
                cancellationToken),
            cancellationToken)
            ?? throw new InvalidOperationException("The authenticated user could not be read.");

        return Results.Ok(new AuthResponse(jwt.Issue(profile), profile));
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            var appUser = await ReadCurrentUserAsync(
                connection,
                transaction,
                cancellationToken);

            if (appUser is null)
            {
                throw NotFound("The current user no longer exists.");
            }

            var memberships = new List<MembershipResponse>();
            await using (var command = new NpgsqlCommand(
                """
                select om.organization_id, o.name, o.slug, om.role::text, e.plan::text
                from organization_members om
                join organizations o on o.id = om.organization_id
                join entitlements e on e.organization_id = om.organization_id
                where om.user_id = app.current_user_id()
                  and om.status = 'active'
                  and o.archived_at is null
                order by o.name;
                """, connection, transaction))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    memberships.Add(new MembershipResponse(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        DbReaders.ParseRole(reader.GetString(3)),
                        DbReaders.ParsePlan(reader.GetString(4))));
                }
            }

            return Results.Ok(new MeResponse(appUser, memberships));
        }, cancellationToken);
    }

    private static async Task<IResult> ListOrganizationsAsync(
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            var organizations = new List<OrganizationResponse>();
            await using var command = new NpgsqlCommand(OrganizationListSql, connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                organizations.Add(ReadOrganization(reader));
            }

            return Results.Ok(new { organizations });
        }, cancellationToken);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        OrganizationCreateRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateOrganization(request.Name, request.Slug));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            Guid id;
            try
            {
                await using var create = new NpgsqlCommand(
                    "select (app.create_organization(@name, @slug)).id;",
                    connection,
                    transaction);
                create.Parameters.AddWithValue("name", request.Name!.Trim());
                create.Parameters.AddWithValue("slug", request.Slug!.Trim().ToLowerInvariant());
                id = (Guid)(await create.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Organization creation returned no id."));
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ApiException(409, "slug_already_exists", "An organization with this slug already exists.");
            }

            var organization = await ReadOrganizationAsync(connection, transaction, id, cancellationToken)
                ?? throw new InvalidOperationException("The new organization could not be read.");
            return Results.Created($"/api/orgs/{id}", new { organization });
        }, cancellationToken);
    }

    private static async Task<IResult> GetOrganizationAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            var organization = await ReadOrganizationAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound();
            return Results.Ok(new { organization });
        }, cancellationToken);
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        Guid orgId,
        OrganizationUpdateRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateOrganization(request.Name, request.Slug));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken, MemberRole.Owner);
            try
            {
                await using var update = new NpgsqlCommand(
                    """
                    update organizations
                    set name = @name, slug = @slug
                    where id = @org_id and archived_at is null;
                    """, connection, transaction);
                update.Parameters.AddWithValue("name", request.Name!.Trim());
                update.Parameters.AddWithValue("slug", request.Slug!.Trim().ToLowerInvariant());
                update.Parameters.AddWithValue("org_id", orgId);
                if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    throw NotFound();
                }
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ApiException(409, "slug_already_exists", "An organization with this slug already exists.");
            }

            var organization = await ReadOrganizationAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound();
            return Results.Ok(new { organization });
        }, cancellationToken);
    }

    private static async Task<IResult> GetDashboardAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            var organization = await ReadOrganizationAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound();
            var currentSeason = organization.CurrentSeason;

            DashboardMetrics metrics;
            await using (var command = new NpgsqlCommand(
                """
                with field_totals as (
                  select count(*) filter (where archived_at is null)::int active_fields,
                         coalesce(sum(acreage) filter (where archived_at is null), 0) total_acreage
                  from fields
                  where organization_id = @org_id
                ), activity_totals as (
                  select count(*)::int activities,
                         coalesce(sum(cost_amount) filter (where activity_type <> 'harvest'), 0) input_cost,
                         coalesce(sum(revenue_amount) filter (where activity_type = 'harvest'), 0) harvest_value,
                         coalesce(sum(quantity) filter (where activity_type = 'harvest'), 0) harvest_quantity
                  from activities
                  where organization_id = @org_id and season_id = @season_id
                )
                select active_fields, total_acreage, activities, input_cost, harvest_value, harvest_quantity
                from field_totals cross join activity_totals;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.Add(new NpgsqlParameter("season_id", NpgsqlDbType.Uuid) { Value = (object?)currentSeason?.Id ?? DBNull.Value });
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                var acreage = reader.GetDecimal(1);
                var inputCost = reader.GetDecimal(3);
                var harvestValue = reader.GetDecimal(4);
                var harvestQuantity = reader.GetDecimal(5);
                decimal? seasonProgress = currentSeason is null
                    ? null
                    : CalculateSeasonProgress(currentSeason.StartsOn, currentSeason.EndsOn, DateOnly.FromDateTime(DateTime.UtcNow));
                metrics = new DashboardMetrics(
                    reader.GetInt32(0),
                    acreage,
                    seasonProgress,
                    reader.GetInt32(2),
                    inputCost,
                    harvestValue,
                    harvestValue - inputCost,
                    acreage > 0 && harvestQuantity > 0 ? decimal.Round(harvestQuantity / acreage, 2) : null);
            }

            var recentActivities = await ReadActivitiesAsync(
                connection,
                transaction,
                orgId,
                fieldId: null,
                seasonId: null,
                limit: 8,
                cancellationToken);

            var cropProgress = new List<CropProgressResponse>();
            await using (var command = new NpgsqlCommand(
                """
                select coalesce(fs.crop, f.default_crop)::text,
                       coalesce(sum(f.acreage), 0),
                       count(*)::int
                from fields f
                left join field_seasons fs
                  on fs.field_id = f.id and fs.season_id = @season_id
                where f.organization_id = @org_id and f.archived_at is null
                group by coalesce(fs.crop, f.default_crop)::text
                order by coalesce(sum(f.acreage), 0) desc;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.Add(new NpgsqlParameter("season_id", NpgsqlDbType.Uuid) { Value = (object?)currentSeason?.Id ?? DBNull.Value });
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    int? days = currentSeason is null
                        ? null
                        : Math.Max(0, currentSeason.EndsOn.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber);
                    cropProgress.Add(new CropProgressResponse(
                        reader.GetString(0), reader.GetDecimal(1), reader.GetInt32(2), days));
                }
            }

            int? maxFields;
            await using (var command = new NpgsqlCommand(
                "select max_fields from entitlements where organization_id = @org_id;",
                connection,
                transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                var value = await command.ExecuteScalarAsync(cancellationToken);
                maxFields = value is null or DBNull ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            return Results.Ok(new DashboardResponse(
                organization,
                currentSeason,
                metrics,
                recentActivities,
                cropProgress,
                new DashboardLimits(maxFields)));
        }, cancellationToken);
    }

    private static async Task<IResult> ListMembersAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var members = await ReadMembersAsync(connection, transaction, orgId, cancellationToken);
            return Results.Ok(new { members });
        }, cancellationToken);
    }

    private static async Task<IResult> AddMemberAsync(
        Guid orgId,
        MemberCreateRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        IAuthRepository repository,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!IsValidEmail(request.Email)) errors["email"] = ["A valid email is required."];
        if (request.Role is null) errors["role"] = ["A role is required."];
        ThrowIfInvalid(errors);

        await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection,
                transaction,
                orgId,
                cancellationToken,
                MemberRole.Owner);
            return true;
        }, cancellationToken);

        var memberId = await repository.FindIdByEmailAsync(
            NormalizeEmail(request.Email!),
            cancellationToken);
        if (memberId is null)
        {
            throw new ApiException(404, "user_not_found", "No registered user has this email address.");
        }
        var resolvedMemberId = memberId.Value;

        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken, MemberRole.Owner);
            try
            {
                await using var insert = new NpgsqlCommand(
                    """
                    insert into organization_members (organization_id, user_id, role)
                    values (@org_id, @user_id, @role::member_role);
                    """, connection, transaction);
                insert.Parameters.AddWithValue("org_id", orgId);
                insert.Parameters.AddWithValue("user_id", resolvedMemberId);
                insert.Parameters.AddWithValue("role", DbReaders.ToDb(request.Role!.Value));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ApiException(409, "already_a_member", "This user is already an organization member.");
            }

            var member = await ReadMemberAsync(connection, transaction, orgId, resolvedMemberId, cancellationToken)
                ?? throw new InvalidOperationException("The new member could not be read.");
            return Results.Created($"/api/orgs/{orgId}/members/{resolvedMemberId}", new { member });
        }, cancellationToken);
    }

    private static async Task<IResult> UpdateMemberAsync(
        Guid orgId,
        Guid userId,
        MemberUpdateRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        if (request.Role is null)
        {
            ThrowIfInvalid(new Dictionary<string, string[]> { ["role"] = ["A role is required."] });
        }

        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken, MemberRole.Owner);
            var existing = await ReadMemberAsync(connection, transaction, orgId, userId, cancellationToken)
                ?? throw NotFound();
            if (existing.Role == MemberRole.Owner && request.Role != MemberRole.Owner
                && await CountOwnersAsync(connection, transaction, orgId, cancellationToken) <= 1)
            {
                throw new ApiException(422, "last_owner_required", "An organization must retain at least one owner.");
            }

            await using var update = new NpgsqlCommand(
                """
                update organization_members
                set role = @role::member_role
                where organization_id = @org_id and user_id = @user_id and status = 'active';
                """, connection, transaction);
            update.Parameters.AddWithValue("role", DbReaders.ToDb(request.Role!.Value));
            update.Parameters.AddWithValue("org_id", orgId);
            update.Parameters.AddWithValue("user_id", userId);
            await update.ExecuteNonQueryAsync(cancellationToken);

            var member = await ReadMemberAsync(connection, transaction, orgId, userId, cancellationToken)
                ?? throw NotFound();
            return Results.Ok(new { member });
        }, cancellationToken);
    }

    private static async Task<IResult> DeleteMemberAsync(
        Guid orgId,
        Guid userId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken, MemberRole.Owner);
            var callerId = GetSubject(user);
            if (callerId == userId)
            {
                throw new ApiException(422, "cannot_remove_self", "Transfer ownership before removing yourself.");
            }

            var existing = await ReadMemberAsync(connection, transaction, orgId, userId, cancellationToken)
                ?? throw NotFound();
            if (existing.Role == MemberRole.Owner
                && await CountOwnersAsync(connection, transaction, orgId, cancellationToken) <= 1)
            {
                throw new ApiException(422, "last_owner_required", "An organization must retain at least one owner.");
            }

            await using var delete = new NpgsqlCommand(
                "delete from organization_members where organization_id = @org_id and user_id = @user_id;",
                connection,
                transaction);
            delete.Parameters.AddWithValue("org_id", orgId);
            delete.Parameters.AddWithValue("user_id", userId);
            if (await delete.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw NotFound();
            }

            return Results.NoContent();
        }, cancellationToken);
    }

    private static async Task<IResult> ListFieldsAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var fields = await ReadFieldsAsync(connection, transaction, orgId, null, cancellationToken);
            return Results.Ok(new { fields });
        }, cancellationToken);
    }

    private static async Task<IResult> CreateFieldAsync(
        Guid orgId,
        FieldWriteRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateField(request));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);

            int? maxFields;
            int activeFields;
            await using (var gate = new NpgsqlCommand(
                """
                select e.max_fields,
                       count(f.id) filter (where f.archived_at is null)::int
                from entitlements e
                left join fields f on f.organization_id = e.organization_id
                where e.organization_id = @org_id
                group by e.max_fields;
                """, connection, transaction))
            {
                gate.Parameters.AddWithValue("org_id", orgId);
                await using var reader = await gate.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) throw NotFound();
                maxFields = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                activeFields = reader.GetInt32(1);
            }

            var decision = EntitlementGate.CanCreateActiveField(maxFields, activeFields);
            if (!decision.Allowed)
            {
                throw new ApiException(422, decision.Code!, decision.Message!);
            }

            Guid id;
            try
            {
                await using var insert = new NpgsqlCommand(
                    """
                    insert into fields (organization_id, name, acreage, default_crop)
                    values (@org_id, @name, @acreage, @crop::crop_type)
                    returning id;
                    """, connection, transaction);
                insert.Parameters.AddWithValue("org_id", orgId);
                insert.Parameters.AddWithValue("name", request.Name!.Trim());
                insert.Parameters.AddWithValue("acreage", request.Acreage);
                insert.Parameters.AddWithValue("crop", NormalizeCrop(request.DefaultCrop!));
                id = (Guid)(await insert.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Field creation returned no id."));
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ApiException(409, "field_name_exists", "A field with this name already exists.");
            }

            var field = (await ReadFieldsAsync(connection, transaction, orgId, id, cancellationToken)).Single();
            return Results.Created($"/api/orgs/{orgId}/fields/{id}", new FieldDetailResponse(field, []));
        }, cancellationToken);
    }

    private static async Task<IResult> GetFieldAsync(
        Guid orgId,
        Guid fieldId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var fields = await ReadFieldsAsync(connection, transaction, orgId, fieldId, cancellationToken);
            if (fields.Count == 0) throw NotFound();
            var rollups = await ReadFieldRollupsAsync(connection, transaction, orgId, fieldId, cancellationToken);
            return Results.Ok(new FieldDetailResponse(fields[0], rollups));
        }, cancellationToken);
    }

    private static async Task<IResult> UpdateFieldAsync(
        Guid orgId,
        Guid fieldId,
        FieldWriteRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateField(request));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);
            try
            {
                await using var update = new NpgsqlCommand(
                    """
                    update fields
                    set name = @name, acreage = @acreage, default_crop = @crop::crop_type
                    where id = @field_id and organization_id = @org_id;
                    """, connection, transaction);
                update.Parameters.AddWithValue("name", request.Name!.Trim());
                update.Parameters.AddWithValue("acreage", request.Acreage);
                update.Parameters.AddWithValue("crop", NormalizeCrop(request.DefaultCrop!));
                update.Parameters.AddWithValue("field_id", fieldId);
                update.Parameters.AddWithValue("org_id", orgId);
                if (await update.ExecuteNonQueryAsync(cancellationToken) == 0) throw NotFound();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ApiException(409, "field_name_exists", "A field with this name already exists.");
            }

            var field = (await ReadFieldsAsync(connection, transaction, orgId, fieldId, cancellationToken)).Single();
            var rollups = await ReadFieldRollupsAsync(connection, transaction, orgId, fieldId, cancellationToken);
            return Results.Ok(new FieldDetailResponse(field, rollups));
        }, cancellationToken);
    }

    private static async Task<IResult> ArchiveFieldAsync(
        Guid orgId,
        Guid fieldId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);
            bool exists;
            bool archived;
            await using (var read = new NpgsqlCommand(
                "select archived_at is not null from fields where id = @field_id and organization_id = @org_id;",
                connection,
                transaction))
            {
                read.Parameters.AddWithValue("field_id", fieldId);
                read.Parameters.AddWithValue("org_id", orgId);
                var value = await read.ExecuteScalarAsync(cancellationToken);
                exists = value is not null;
                archived = value is true;
            }

            if (!exists) throw NotFound();
            if (archived)
            {
                throw new ApiException(409, "field_already_archived", "This field is already archived.");
            }

            await using var update = new NpgsqlCommand(
                "update fields set archived_at = now() where id = @field_id and organization_id = @org_id;",
                connection,
                transaction);
            update.Parameters.AddWithValue("field_id", fieldId);
            update.Parameters.AddWithValue("org_id", orgId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            return Results.NoContent();
        }, cancellationToken);
    }

    private static async Task<List<FieldResponse>> ReadFieldsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid? fieldId,
        CancellationToken cancellationToken)
    {
        var fields = new List<FieldResponse>();
        await using var command = new NpgsqlCommand(
            """
            select f.id, f.name, f.acreage, f.default_crop::text,
                   case when f.archived_at is null then 'active' else 'archived' end,
                   current_fs.crop::text,
                   last_activity.activity_type::text,
                   last_activity.activity_date
            from fields f
            left join lateral (
              select fs.crop
              from field_seasons fs
              join seasons s on s.id = fs.season_id
              where fs.field_id = f.id
              order by case when current_date between s.starts_on and s.ends_on then 0 else 1 end,
                       s.year desc
              limit 1
            ) current_fs on true
            left join lateral (
              select a.activity_type, a.activity_date
              from activities a
              where a.field_id = f.id
              order by a.activity_date desc, a.created_at desc
              limit 1
            ) last_activity on true
            where f.organization_id = @org_id
              and (@field_id is null or f.id = @field_id)
            order by f.archived_at nulls first, f.name;
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.Add(new NpgsqlParameter("field_id", NpgsqlDbType.Uuid) { Value = (object?)fieldId ?? DBNull.Value });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var lastActivity = reader.IsDBNull(6)
                ? null
                : new LastActivityResponse(
                    DbReaders.ParseActivity(reader.GetString(6)),
                    reader.GetFieldValue<DateOnly>(7));
            fields.Add(new FieldResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetString(3),
                null,
                reader.GetString(4) == "active" ? FieldStatus.Active : FieldStatus.Archived,
                reader.IsDBNull(5) ? null : reader.GetString(5),
                lastActivity));
        }

        return fields;
    }

    private static async Task<List<FieldSeasonRollupResponse>> ReadFieldRollupsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid fieldId,
        CancellationToken cancellationToken)
    {
        var raw = new List<(Guid Id, string Name, int Year, string Crop, DateOnly? Planted, decimal? Yield, decimal Cost, decimal Value)>();
        await using var command = new NpgsqlCommand(
            """
            select s.id, s.name, s.year, fs.crop::text,
                   min(a.activity_date) filter (where a.activity_type = 'planting'),
                   case when sum(a.quantity) filter (where a.activity_type = 'harvest') is null then null
                        else round(sum(a.quantity) filter (where a.activity_type = 'harvest') / nullif(f.acreage, 0), 2)
                   end,
                   coalesce(sum(a.cost_amount) filter (where a.activity_type <> 'harvest'), 0),
                   coalesce(sum(a.revenue_amount) filter (where a.activity_type = 'harvest'), 0)
            from field_seasons fs
            join seasons s on s.id = fs.season_id and s.organization_id = fs.organization_id
            join fields f on f.id = fs.field_id and f.organization_id = fs.organization_id
            left join activities a
              on a.field_id = fs.field_id and a.season_id = fs.season_id
            where fs.organization_id = @org_id and fs.field_id = @field_id
            group by s.id, s.name, s.year, fs.crop, f.acreage
            order by s.year;
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("field_id", fieldId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            raw.Add((
                reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.GetDecimal(6), reader.GetDecimal(7)));
        }

        var result = new List<FieldSeasonRollupResponse>();
        decimal? previousYield = null;
        foreach (var item in raw)
        {
            decimal? delta = item.Yield is not null && previousYield is > 0
                ? decimal.Round((item.Yield.Value - previousYield.Value) / previousYield.Value * 100, 1)
                : null;
            result.Add(new FieldSeasonRollupResponse(
                item.Id, item.Name, item.Year, item.Crop, item.Planted, item.Yield,
                item.Cost, item.Value, item.Value - item.Cost, delta));
            if (item.Yield is not null) previousYield = item.Yield;
        }

        result.Reverse();
        return result;
    }

    private static async Task<IResult> ListSeasonsAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var seasons = await ReadSeasonsAsync(connection, transaction, orgId, null, cancellationToken);
            return Results.Ok(new { seasons });
        }, cancellationToken);
    }

    private static async Task<IResult> CreateSeasonAsync(
        Guid orgId,
        SeasonWriteRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateSeason(request));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);
            Guid id;
            try
            {
                await using var insert = new NpgsqlCommand(
                    """
                    insert into seasons (organization_id, year, name, starts_on, ends_on)
                    values (@org_id, @year, @name, @starts_on, @ends_on)
                    returning id;
                    """, connection, transaction);
                AddSeasonParameters(insert, orgId, request);
                id = (Guid)(await insert.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Season creation returned no id."));
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ApiException(409, "season_year_exists", "A season already exists for this year.");
            }

            var season = (await ReadSeasonsAsync(connection, transaction, orgId, id, cancellationToken)).Single();
            return Results.Created($"/api/orgs/{orgId}/seasons/{id}", new { season });
        }, cancellationToken);
    }

    private static async Task<IResult> GetSeasonAsync(
        Guid orgId,
        Guid seasonId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var seasons = await ReadSeasonsAsync(connection, transaction, orgId, seasonId, cancellationToken);
            if (seasons.Count == 0) throw NotFound();
            return Results.Ok(new { season = seasons[0] });
        }, cancellationToken);
    }

    private static async Task<IResult> UpdateSeasonAsync(
        Guid orgId,
        Guid seasonId,
        SeasonWriteRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateSeason(request));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);
            try
            {
                await using var update = new NpgsqlCommand(
                    """
                    update seasons
                    set year = @year, name = @name, starts_on = @starts_on, ends_on = @ends_on
                    where id = @season_id and organization_id = @org_id;
                    """, connection, transaction);
                AddSeasonParameters(update, orgId, request);
                update.Parameters.AddWithValue("season_id", seasonId);
                if (await update.ExecuteNonQueryAsync(cancellationToken) == 0) throw NotFound();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ApiException(409, "season_year_exists", "A season already exists for this year.");
            }

            var season = (await ReadSeasonsAsync(connection, transaction, orgId, seasonId, cancellationToken)).Single();
            return Results.Ok(new { season });
        }, cancellationToken);
    }

    private static void AddSeasonParameters(NpgsqlCommand command, Guid orgId, SeasonWriteRequest request)
    {
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("year", request.Year);
        command.Parameters.AddWithValue("name", request.Name!.Trim());
        command.Parameters.AddWithValue("starts_on", request.StartsOn);
        command.Parameters.AddWithValue("ends_on", request.EndsOn);
    }

    private static async Task<List<SeasonResponse>> ReadSeasonsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid? seasonId,
        CancellationToken cancellationToken)
    {
        var seasons = new List<SeasonResponse>();
        await using var command = new NpgsqlCommand(
            """
            select id, year, name, starts_on, ends_on
            from seasons
            where organization_id = @org_id and (@season_id is null or id = @season_id)
            order by year desc;
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.Add(new NpgsqlParameter("season_id", NpgsqlDbType.Uuid) { Value = (object?)seasonId ?? DBNull.Value });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            seasons.Add(new SeasonResponse(
                reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetFieldValue<DateOnly>(3), reader.GetFieldValue<DateOnly>(4)));
        }
        return seasons;
    }

    private static Dictionary<string, string[]> ValidateField(FieldWriteRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            errors["name"] = ["Name is required and must be at most 120 characters."];
        if (request.Acreage <= 0 || request.Acreage > 99_999_999.99m)
            errors["acreage"] = ["Acreage must be greater than zero."];
        if (string.IsNullOrWhiteSpace(request.DefaultCrop)
            || !AllowedCrops.Contains(NormalizeCrop(request.DefaultCrop), StringComparer.Ordinal))
            errors["defaultCrop"] = ["Default crop must be corn, soybean, or wheat."];
        if (request.SoilType?.Length > 100)
            errors["soilType"] = ["Soil type must be at most 100 characters."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateSeason(SeasonWriteRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Year is < 2000 or > 2100) errors["year"] = ["Year must be between 2000 and 2100."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            errors["name"] = ["Name is required and must be at most 120 characters."];
        if (request.StartsOn >= request.EndsOn)
        {
            throw new ApiException(422, "invalid_season_dates", "Season start must be before season end.");
        }
        return errors;
    }

    private static string NormalizeCrop(string crop) => crop.Trim().ToLowerInvariant();

    private static async Task<IResult> ListActivitiesAsync(
        Guid orgId,
        Guid fieldId,
        Guid? seasonId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            if (!await FieldExistsAsync(connection, transaction, orgId, fieldId, cancellationToken))
                throw NotFound();
            if (seasonId is not null
                && !await SeasonExistsAsync(connection, transaction, orgId, seasonId.Value, cancellationToken))
                throw NotFound();
            var activities = await ReadActivitiesAsync(
                connection, transaction, orgId, fieldId, seasonId, null, cancellationToken);
            return Results.Ok(new { activities });
        }, cancellationToken);
    }

    private static async Task<IResult> CreateActivityAsync(
        Guid orgId,
        Guid fieldId,
        ActivityWriteRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateActivity(request));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);
            await EnsureFieldSeasonConsistencyAsync(
                connection, transaction, orgId, fieldId, request.SeasonId, cancellationToken);

            Guid id;
            await using (var insert = new NpgsqlCommand(
                """
                insert into activities (
                  organization_id, field_id, season_id, activity_type, activity_date,
                  quantity, quantity_unit, cost_amount, revenue_amount, notes, created_by)
                values (
                  @org_id, @field_id, @season_id, @type::activity_type, @activity_date,
                  @quantity, @quantity_unit, @cost_amount, @revenue_amount, @notes, app.current_user_id())
                returning id;
                """, connection, transaction))
            {
                AddActivityParameters(insert, orgId, fieldId, request);
                id = (Guid)(await insert.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Activity creation returned no id."));
            }

            var activity = await ReadActivityByIdAsync(
                connection, transaction, orgId, id, cancellationToken)
                ?? throw new InvalidOperationException("The new activity could not be read.");
            return Results.Created($"/api/orgs/{orgId}/activities/{id}", new { activity });
        }, cancellationToken);
    }

    private static async Task<IResult> UpdateActivityAsync(
        Guid orgId,
        Guid activityId,
        ActivityWriteRequest request,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        ThrowIfInvalid(ValidateActivity(request));
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);
            Guid fieldId;
            await using (var find = new NpgsqlCommand(
                "select field_id from activities where id = @activity_id and organization_id = @org_id;",
                connection,
                transaction))
            {
                find.Parameters.AddWithValue("activity_id", activityId);
                find.Parameters.AddWithValue("org_id", orgId);
                var value = await find.ExecuteScalarAsync(cancellationToken);
                if (value is null) throw NotFound();
                fieldId = (Guid)value;
            }

            await EnsureFieldSeasonConsistencyAsync(
                connection, transaction, orgId, fieldId, request.SeasonId, cancellationToken);
            await using (var update = new NpgsqlCommand(
                """
                update activities
                set season_id = @season_id,
                    activity_type = @type::activity_type,
                    activity_date = @activity_date,
                    quantity = @quantity,
                    quantity_unit = @quantity_unit,
                    cost_amount = @cost_amount,
                    revenue_amount = @revenue_amount,
                    notes = @notes
                where id = @activity_id and organization_id = @org_id;
                """, connection, transaction))
            {
                AddActivityParameters(update, orgId, fieldId, request);
                update.Parameters.AddWithValue("activity_id", activityId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            var activity = await ReadActivityByIdAsync(
                connection, transaction, orgId, activityId, cancellationToken)
                ?? throw NotFound();
            return Results.Ok(new { activity });
        }, cancellationToken);
    }

    private static async Task<IResult> DeleteActivityAsync(
        Guid orgId,
        Guid activityId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(
                connection, transaction, orgId, cancellationToken, MemberRole.Owner, MemberRole.Agronomist);
            await using var delete = new NpgsqlCommand(
                "delete from activities where id = @activity_id and organization_id = @org_id;",
                connection,
                transaction);
            delete.Parameters.AddWithValue("activity_id", activityId);
            delete.Parameters.AddWithValue("org_id", orgId);
            if (await delete.ExecuteNonQueryAsync(cancellationToken) == 0) throw NotFound();
            return Results.NoContent();
        }, cancellationToken);
    }

    private static void AddActivityParameters(
        NpgsqlCommand command,
        Guid orgId,
        Guid fieldId,
        ActivityWriteRequest request)
    {
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("field_id", fieldId);
        command.Parameters.AddWithValue("season_id", request.SeasonId);
        command.Parameters.AddWithValue("type", DbReaders.ToDb(request.Type!.Value));
        command.Parameters.AddWithValue("activity_date", request.ActivityDate);
        command.Parameters.Add(new NpgsqlParameter("quantity", NpgsqlDbType.Numeric) { Value = (object?)request.Quantity ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("quantity_unit", NpgsqlDbType.Text) { Value = (object?)CleanOptional(request.QuantityUnit) ?? DBNull.Value });
        command.Parameters.AddWithValue("cost_amount", request.CostAmount ?? 0m);
        command.Parameters.AddWithValue("revenue_amount", request.RevenueAmount ?? 0m);
        command.Parameters.Add(new NpgsqlParameter("notes", NpgsqlDbType.Text) { Value = (object?)CleanOptional(request.Notes) ?? DBNull.Value });
    }

    private static async Task EnsureFieldSeasonConsistencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid fieldId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        string? defaultCrop;
        await using (var field = new NpgsqlCommand(
            """
            select default_crop::text
            from fields
            where id = @field_id and organization_id = @org_id and archived_at is null;
            """, connection, transaction))
        {
            field.Parameters.AddWithValue("field_id", fieldId);
            field.Parameters.AddWithValue("org_id", orgId);
            defaultCrop = (string?)await field.ExecuteScalarAsync(cancellationToken);
        }

        if (defaultCrop is null
            || !await SeasonExistsAsync(connection, transaction, orgId, seasonId, cancellationToken))
        {
            throw new ApiException(
                422,
                "season_field_mismatch",
                "The field and season must belong to this organization, and the field must be active.");
        }

        await using var link = new NpgsqlCommand(
            """
            insert into field_seasons (organization_id, field_id, season_id, crop)
            values (@org_id, @field_id, @season_id, @crop::crop_type)
            on conflict (field_id, season_id) do nothing;
            """, connection, transaction);
        link.Parameters.AddWithValue("org_id", orgId);
        link.Parameters.AddWithValue("field_id", fieldId);
        link.Parameters.AddWithValue("season_id", seasonId);
        link.Parameters.AddWithValue("crop", defaultCrop);
        await link.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> FieldExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid fieldId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select exists(select 1 from fields where id = @field_id and organization_id = @org_id);",
            connection,
            transaction);
        command.Parameters.AddWithValue("field_id", fieldId);
        command.Parameters.AddWithValue("org_id", orgId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> SeasonExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select exists(select 1 from seasons where id = @season_id and organization_id = @org_id);",
            connection,
            transaction);
        command.Parameters.AddWithValue("season_id", seasonId);
        command.Parameters.AddWithValue("org_id", orgId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private const string ActivitySelectSql =
        """
        select a.id, a.organization_id, a.field_id, f.name, f.acreage,
               a.season_id, s.name, a.activity_type::text, a.activity_date,
               a.quantity, a.quantity_unit,
               case when a.cost_amount = 0 then null else a.cost_amount end,
               case when a.revenue_amount = 0 then null else a.revenue_amount end,
               a.notes,
               coalesce(u.id, a.created_by),
               coalesce(u.display_name, 'FieldLedger member'),
               a.created_at
        from activities a
        join fields f on f.id = a.field_id and f.organization_id = a.organization_id
        join seasons s on s.id = a.season_id and s.organization_id = a.organization_id
        left join users u on u.id = a.created_by
        """;

    private static async Task<List<ActivityResponse>> ReadActivitiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid? fieldId,
        Guid? seasonId,
        int? limit,
        CancellationToken cancellationToken)
    {
        var activities = new List<ActivityResponse>();
        await using var command = new NpgsqlCommand(
            ActivitySelectSql + "\n" +
            """
            where a.organization_id = @org_id
              and (@field_id is null or a.field_id = @field_id)
              and (@season_id is null or a.season_id = @season_id)
            order by a.activity_date desc, a.created_at desc
            limit @limit;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.Add(new NpgsqlParameter("field_id", NpgsqlDbType.Uuid) { Value = (object?)fieldId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("season_id", NpgsqlDbType.Uuid) { Value = (object?)seasonId ?? DBNull.Value });
        command.Parameters.AddWithValue("limit", limit ?? int.MaxValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(ReadActivity(reader));
        }
        return activities;
    }

    private static async Task<ActivityResponse?> ReadActivityByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid activityId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            ActivitySelectSql + " where a.organization_id = @org_id and a.id = @activity_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("activity_id", activityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadActivity(reader) : null;
    }

    private static ActivityResponse ReadActivity(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.GetDecimal(4),
        reader.GetGuid(5),
        reader.GetString(6),
        DbReaders.ParseActivity(reader.GetString(7)),
        reader.GetFieldValue<DateOnly>(8),
        reader.IsDBNull(9) ? null : reader.GetDecimal(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetDecimal(11),
        reader.IsDBNull(12) ? null : reader.GetDecimal(12),
        reader.IsDBNull(13) ? null : reader.GetString(13),
        new ActivityCreatorResponse(
            reader.IsDBNull(14) ? Guid.Empty : reader.GetGuid(14),
            reader.GetString(15)),
        reader.GetFieldValue<DateTimeOffset>(16));

    private static Dictionary<string, string[]> ValidateActivity(ActivityWriteRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.SeasonId == Guid.Empty) errors["seasonId"] = ["A valid season id is required."];
        if (request.Type is null) errors["type"] = ["An activity type is required."];
        if (request.ActivityDate == default) errors["activityDate"] = ["An activity date is required."];
        if (request.Quantity is < 0) errors["quantity"] = ["Quantity cannot be negative."];
        if (request.CostAmount is < 0) errors["costAmount"] = ["Cost cannot be negative."];
        if (request.RevenueAmount is < 0) errors["revenueAmount"] = ["Revenue cannot be negative."];
        if (request.QuantityUnit?.Length > 50) errors["quantityUnit"] = ["Quantity unit must be at most 50 characters."];
        if (request.Notes?.Length > 4000) errors["notes"] = ["Notes must be at most 4000 characters."];
        return errors;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<IResult> GetInsightsAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var seasonId = await SelectCurrentSeasonIdAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound("This organization has no seasons to analyze.");

            InsightTotals totals;
            await using (var command = new NpgsqlCommand(
                """
                with field_totals as (
                  select count(*)::int active_fields, coalesce(sum(acreage), 0) total_acreage
                  from fields
                  where organization_id = @org_id and archived_at is null
                ), activity_totals as (
                  select
                    coalesce(sum(cost_amount) filter (where activity_type <> 'harvest'), 0) input_cost,
                    coalesce(sum(revenue_amount) filter (where activity_type = 'harvest'), 0) harvest_value
                  from activities
                  where organization_id = @org_id and season_id = @season_id
                )
                select active_fields, total_acreage, input_cost, harvest_value
                from field_totals cross join activity_totals;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.AddWithValue("season_id", seasonId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                var inputCost = reader.GetDecimal(2);
                var harvestValue = reader.GetDecimal(3);
                totals = new InsightTotals(
                    reader.GetInt32(0), reader.GetDecimal(1), inputCost, harvestValue, harvestValue - inputCost);
            }

            var yieldBySeason = new List<YieldBySeasonResponse>();
            await using (var command = new NpgsqlCommand(
                """
                with per_field as (
                  select s.year, fs.crop::text crop, f.id, f.acreage,
                         coalesce(sum(a.quantity) filter (where a.activity_type = 'harvest'), 0) harvest_quantity
                  from field_seasons fs
                  join seasons s on s.id = fs.season_id
                  join fields f on f.id = fs.field_id
                  left join activities a on a.field_id = fs.field_id and a.season_id = fs.season_id
                  where fs.organization_id = @org_id
                  group by s.year, fs.crop, f.id, f.acreage
                )
                select year, crop, round(sum(harvest_quantity) / nullif(sum(acreage), 0), 2)
                from per_field
                where harvest_quantity > 0
                group by year, crop
                order by year, crop;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    yieldBySeason.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2)));
            }

            var costVsValue = new List<CostVsValueResponse>();
            await using (var command = new NpgsqlCommand(
                """
                select s.year,
                       coalesce(sum(a.cost_amount) filter (where a.activity_type <> 'harvest'), 0),
                       coalesce(sum(a.revenue_amount) filter (where a.activity_type = 'harvest'), 0)
                from seasons s
                left join activities a on a.season_id = s.id and a.organization_id = s.organization_id
                where s.organization_id = @org_id
                group by s.id, s.year
                order by s.year;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    costVsValue.Add(new(reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2)));
            }

            var cropMix = new List<CropMixResponse>();
            await using (var command = new NpgsqlCommand(
                """
                select coalesce(fs.crop, f.default_crop)::text, coalesce(sum(f.acreage), 0)
                from fields f
                left join field_seasons fs on fs.field_id = f.id and fs.season_id = @season_id
                where f.organization_id = @org_id and f.archived_at is null
                group by coalesce(fs.crop, f.default_crop)::text
                order by sum(f.acreage) desc;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.AddWithValue("season_id", seasonId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    cropMix.Add(new(reader.GetString(0), reader.GetDecimal(1)));
            }

            var fieldNetValue = new List<FieldNetValueResponse>();
            await using (var command = new NpgsqlCommand(
                """
                select f.id, f.name,
                       coalesce(sum(a.revenue_amount) filter (where a.activity_type = 'harvest'), 0)
                       - coalesce(sum(a.cost_amount) filter (where a.activity_type <> 'harvest'), 0)
                from fields f
                left join activities a on a.field_id = f.id and a.season_id = @season_id
                where f.organization_id = @org_id and f.archived_at is null
                group by f.id, f.name
                order by 3 desc;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.AddWithValue("season_id", seasonId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    fieldNetValue.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2)));
            }

            var activityCountByType = await ReadActivityCountsAsync(
                connection, transaction, orgId, seasonId, cancellationToken);
            return Results.Ok(new InsightsResponse(
                seasonId, totals, yieldBySeason, costVsValue, cropMix, fieldNetValue, activityCountByType));
        }, cancellationToken);
    }

    private static async Task<IResult> GetSeasonReportAsync(
        Guid orgId,
        Guid seasonId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var entitlement = await ReadEntitlementAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound();
            var gate = EntitlementGate.CanViewSeasonReport(entitlement.SeasonReportEnabled);
            if (!gate.Allowed) throw new ApiException(403, gate.Code!, gate.Message!);

            var organization = await ReadOrganizationAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound();
            var seasons = await ReadSeasonsAsync(connection, transaction, orgId, seasonId, cancellationToken);
            if (seasons.Count == 0) throw NotFound();

            var fields = new List<ReportField>();
            await using (var command = new NpgsqlCommand(
                """
                select f.id, f.name, coalesce(fs.crop, f.default_crop)::text, f.acreage,
                       count(a.id)::int,
                       coalesce(sum(a.cost_amount) filter (where a.activity_type <> 'harvest'), 0),
                       coalesce(sum(a.revenue_amount) filter (where a.activity_type = 'harvest'), 0),
                       case when sum(a.quantity) filter (where a.activity_type = 'harvest') is null then null
                            else round(sum(a.quantity) filter (where a.activity_type = 'harvest') / nullif(f.acreage, 0), 2)
                       end
                from fields f
                left join field_seasons fs on fs.field_id = f.id and fs.season_id = @season_id
                left join activities a on a.field_id = f.id and a.season_id = @season_id
                where f.organization_id = @org_id
                group by f.id, f.name, fs.crop, f.default_crop, f.acreage
                order by f.name;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.AddWithValue("season_id", seasonId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    fields.Add(new ReportField(
                        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3),
                        reader.GetInt32(4), reader.GetDecimal(5), reader.GetDecimal(6),
                        reader.IsDBNull(7) ? null : reader.GetDecimal(7)));
                }
            }

            var activeFieldRows = await ReadFieldsAsync(connection, transaction, orgId, null, cancellationToken);
            var active = activeFieldRows.Where(field => field.Status == FieldStatus.Active).ToList();
            var totalAcreage = active.Sum(field => field.Acreage);
            var inputCost = fields.Sum(field => field.InputCost);
            var harvestValue = fields.Sum(field => field.HarvestValue);
            var yieldFields = fields.Where(field => field.YieldPerAcre is not null).ToList();
            decimal? averageYield = yieldFields.Count == 0
                ? null
                : decimal.Round(yieldFields.Average(field => field.YieldPerAcre!.Value), 2);
            var activitySummary = await ReadActivityCountsAsync(
                connection, transaction, orgId, seasonId, cancellationToken);
            var activities = await ReadActivitiesAsync(
                connection, transaction, orgId, null, seasonId, null, cancellationToken);

            return Results.Ok(new SeasonReportResponse(
                new ReportOrganization(orgId, organization.Name, organization.Plan),
                seasons[0],
                DateTimeOffset.UtcNow,
                new ReportSummary(
                    active.Count, totalAcreage, inputCost, harvestValue,
                    harvestValue - inputCost, averageYield),
                fields,
                activitySummary,
                activities));
        }, cancellationToken);
    }

    private static Task<IResult> ExportActivitiesCsvAsync(
        Guid orgId,
        Guid? seasonId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        IResult result = new ActivityCsvResult(orgId, seasonId, user, db);
        return Task.FromResult(result);
    }

    private static async Task<List<ActivityCountResponse>> ReadActivityCountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var counts = Enum.GetValues<ActivityKind>().ToDictionary(kind => kind, _ => 0);
        await using var command = new NpgsqlCommand(
            """
            select activity_type::text, count(*)::int
            from activities
            where organization_id = @org_id and season_id = @season_id
            group by activity_type;
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("season_id", seasonId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            counts[DbReaders.ParseActivity(reader.GetString(0))] = reader.GetInt32(1);
        return counts.Select(pair => new ActivityCountResponse(pair.Key, pair.Value)).ToList();
    }

    private static async Task<Guid?> SelectCurrentSeasonIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select id
            from seasons
            where organization_id = @org_id
            order by case when current_date between starts_on and ends_on then 0 else 1 end, year desc
            limit 1;
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static async Task<IResult> GetBillingAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken);
            var entitlement = await ReadEntitlementAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound();
            var history = new List<PlanChangeResponse>();
            await using var command = new NpgsqlCommand(
                """
                select pc.from_plan::text, pc.to_plan::text,
                       coalesce(u.email::text, pc.changed_by::text), pc.changed_at
                from plan_changes pc
                left join users u on u.id = pc.changed_by
                where pc.organization_id = @org_id
                order by pc.changed_at desc
                limit 20;
                """, connection, transaction);
            command.Parameters.AddWithValue("org_id", orgId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                history.Add(new PlanChangeResponse(
                    DbReaders.ParsePlan(reader.GetString(0)),
                    DbReaders.ParsePlan(reader.GetString(1)),
                    reader.GetString(2),
                    reader.GetFieldValue<DateTimeOffset>(3)));
            }

            return Results.Ok(new BillingResponse(
                entitlement.Plan,
                new BillingLimits(
                    entitlement.MaxFields,
                    entitlement.CsvExportEnabled,
                    entitlement.SeasonReportEnabled),
                new BillingUsage(entitlement.ActiveFieldCount),
                history));
        }, cancellationToken);
    }

    private static Task<IResult> UpgradeAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken) =>
        ChangePlanAsync(orgId, PlanTier.Pro, user, db, cancellationToken);

    private static Task<IResult> DowngradeAsync(
        Guid orgId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken) =>
        ChangePlanAsync(orgId, PlanTier.Free, user, db, cancellationToken);

    private static async Task<IResult> ChangePlanAsync(
        Guid orgId,
        PlanTier targetPlan,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db,
        CancellationToken cancellationToken)
    {
        return await db.InUserTransaction(user, async (connection, transaction) =>
        {
            await RequireRoleAsync(connection, transaction, orgId, cancellationToken, MemberRole.Owner);
            var current = await ReadEntitlementAsync(connection, transaction, orgId, cancellationToken)
                ?? throw NotFound();
            if (current.Plan == targetPlan)
            {
                throw new ApiException(409, "already_on_plan", $"This organization is already on the {targetPlan.ToString().ToLowerInvariant()} plan.");
            }

            if (targetPlan == PlanTier.Free && current.ActiveFieldCount > 3)
            {
                throw new ApiException(
                    422,
                    "too_many_active_fields_for_free",
                    $"This organization has {current.ActiveFieldCount} active fields. Archive all but three before downgrading to Free.");
            }

            await using (var command = new NpgsqlCommand(
                "select app.set_org_plan(@org_id, @plan::plan_tier);",
                connection,
                transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.AddWithValue("plan", DbReaders.ToDb(targetPlan));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            DateTimeOffset changedAt;
            await using (var command = new NpgsqlCommand(
                "select updated_at from entitlements where organization_id = @org_id;",
                connection,
                transaction))
            {
                command.Parameters.AddWithValue("org_id", orgId);
                var scalar = await command.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Updated entitlement was not found.");
                changedAt = scalar switch
                {
                    DateTimeOffset dto => dto,
                    DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),
                    _ => throw new InvalidOperationException("Unexpected updated_at type.")
                };
            }
            return Results.Ok(new PlanMutationResponse(targetPlan, changedAt));
        }, cancellationToken);
    }

    private sealed record EntitlementState(
        PlanTier Plan,
        int? MaxFields,
        bool CsvExportEnabled,
        bool SeasonReportEnabled,
        int ActiveFieldCount);

    private static async Task<EntitlementState?> ReadEntitlementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select e.plan::text, e.max_fields, e.csv_export_enabled, e.season_report_enabled,
                   count(f.id) filter (where f.archived_at is null)::int
            from entitlements e
            left join fields f on f.organization_id = e.organization_id
            where e.organization_id = @org_id
            group by e.plan, e.max_fields, e.csv_export_enabled, e.season_report_enabled;
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new EntitlementState(
                DbReaders.ParsePlan(reader.GetString(0)),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3),
                reader.GetInt32(4))
            : null;
    }

    private sealed class ActivityCsvResult(
        Guid orgId,
        Guid? seasonId,
        ClaimsPrincipal user,
        IFieldLedgerDbSession db) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await db.InUserTransaction(user, async (connection, transaction) =>
            {
                var role = await RequireRoleAsync(connection, transaction, orgId, httpContext.RequestAborted);
                var entitlement = await ReadEntitlementAsync(
                    connection, transaction, orgId, httpContext.RequestAborted) ?? throw NotFound();
                var decision = EntitlementGate.CanExportCsv(entitlement.CsvExportEnabled, role);
                if (!decision.Allowed)
                {
                    var status = decision.Code == "pro_required" ? 403 : 403;
                    throw new ApiException(status, decision.Code!, decision.Message!);
                }
                if (seasonId is not null
                    && !await SeasonExistsAsync(
                        connection, transaction, orgId, seasonId.Value, httpContext.RequestAborted))
                {
                    throw NotFound();
                }

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "text/csv; charset=utf-8";
                httpContext.Response.Headers.ContentDisposition =
                    $"attachment; filename=fieldledger-activities-{orgId:N}.csv";
                await using var writer = new StreamWriter(
                    httpContext.Response.Body,
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    leaveOpen: true);
                await writer.WriteLineAsync(
                    "organization,field,season,crop,activity_type,activity_date,quantity,quantity_unit,cost_amount,revenue_amount,notes");

                await using var command = new NpgsqlCommand(
                    """
                    select o.name, f.name, s.name, coalesce(fs.crop, f.default_crop)::text,
                           a.activity_type::text, a.activity_date, a.quantity, a.quantity_unit,
                           a.cost_amount, a.revenue_amount, a.notes
                    from activities a
                    join organizations o on o.id = a.organization_id
                    join fields f on f.id = a.field_id
                    join seasons s on s.id = a.season_id
                    left join field_seasons fs on fs.field_id = a.field_id and fs.season_id = a.season_id
                    where a.organization_id = @org_id
                      and (@season_id is null or a.season_id = @season_id)
                    order by a.activity_date, a.created_at;
                    """, connection, transaction);
                command.Parameters.AddWithValue("org_id", orgId);
                command.Parameters.Add(new NpgsqlParameter("season_id", NpgsqlDbType.Uuid) { Value = (object?)seasonId ?? DBNull.Value });
                await using var reader = await command.ExecuteReaderAsync(
                    System.Data.CommandBehavior.SequentialAccess,
                    httpContext.RequestAborted);
                while (await reader.ReadAsync(httpContext.RequestAborted))
                {
                    var row = CsvFormatter.FormatRow(
                        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                        reader.GetString(4), reader.GetFieldValue<DateOnly>(5),
                        reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7),
                        reader.GetDecimal(8), reader.GetDecimal(9),
                        reader.IsDBNull(10) ? null : reader.GetString(10));
                    await writer.WriteLineAsync(row.AsMemory(), httpContext.RequestAborted);
                }
                await writer.FlushAsync(httpContext.RequestAborted);
                return true;
            }, httpContext.RequestAborted);
        }
    }

    private const string OrganizationListSql =
        """
        select o.id, o.name, o.slug, om.role::text, e.plan::text,
               count(distinct f.id) filter (where f.archived_at is null)::int,
               count(distinct s.id)::int,
               cs.id as current_season_id, cs.year as current_season_year,
               cs.name as current_season_name, cs.starts_on as current_season_starts_on,
               cs.ends_on as current_season_ends_on
        from organizations o
        join organization_members om
          on om.organization_id = o.id
         and om.user_id = app.current_user_id()
         and om.status = 'active'
        join entitlements e on e.organization_id = o.id
        left join fields f on f.organization_id = o.id
        left join seasons s on s.organization_id = o.id
        left join lateral (
          select sx.id, sx.year, sx.name, sx.starts_on, sx.ends_on
          from seasons sx
          where sx.organization_id = o.id
          order by
            case when current_date between sx.starts_on and sx.ends_on then 0 else 1 end,
            sx.year desc
          limit 1
        ) cs on true
        where o.archived_at is null
        group by o.id, o.name, o.slug, om.role, e.plan,
                 cs.id, cs.year, cs.name, cs.starts_on, cs.ends_on
        order by o.name;
        """;

    private static async Task<OrganizationResponse?> ReadOrganizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"select * from ({OrganizationListSql.Trim().TrimEnd(';')}) organizations where id = @org_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadOrganization(reader) : null;
    }

    private static OrganizationResponse ReadOrganization(NpgsqlDataReader reader)
    {
        SeasonResponse? currentSeason = reader.IsDBNull(7)
            ? null
            : new SeasonResponse(
                reader.GetGuid(7),
                reader.GetInt32(8),
                reader.GetString(9),
                reader.GetFieldValue<DateOnly>(10),
                reader.GetFieldValue<DateOnly>(11));
        return new OrganizationResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            DbReaders.ParseRole(reader.GetString(3)),
            DbReaders.ParsePlan(reader.GetString(4)),
            reader.GetInt32(5),
            reader.GetInt32(6),
            currentSeason);
    }

    private static async Task<MemberRole> RequireRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        CancellationToken cancellationToken,
        params MemberRole[] allowed)
    {
        await using var command = new NpgsqlCommand(
            "select app.member_role(@org_id)::text;",
            connection,
            transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
        {
            throw NotFound();
        }

        var role = DbReaders.ParseRole((string)value);
        if (allowed.Length > 0 && !allowed.Contains(role))
        {
            throw new ApiException(403, "forbidden", "Your organization role does not allow this operation.");
        }

        return role;
    }

    private static async Task<List<MemberResponse>> ReadMembersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        var members = new List<MemberResponse>();
        await using var command = new NpgsqlCommand(
            """
            select om.user_id,
                   coalesce(u.display_name, 'FieldLedger member'),
                   coalesce(u.email::text, ''),
                   om.role::text,
                   om.created_at
            from organization_members om
            left join users u on u.id = om.user_id
            where om.organization_id = @org_id and om.status = 'active'
            order by om.created_at;
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new MemberResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                DbReaders.ParseRole(reader.GetString(3)),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return members;
    }

    private static async Task<AppUser?> ReadCurrentUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select id, email::text, display_name, created_at
            from users
            where id = app.current_user_id();
            """,
            connection,
            transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? DbReaders.ReadUser(reader) : null;
    }

    private static async Task<MemberResponse?> ReadMemberAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var members = await ReadMembersAsync(connection, transaction, orgId, cancellationToken);
        return members.SingleOrDefault(member => member.UserId == userId);
    }

    private static async Task<int> CountOwnersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select count(*)::int
            from organization_members
            where organization_id = @org_id and status = 'active' and role = 'owner';
            """, connection, transaction);
        command.Parameters.AddWithValue("org_id", orgId);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static Dictionary<string, string[]> ValidateAuthInput(
        string? email,
        string? password,
        string? displayName,
        bool validateDisplayName = true)
    {
        var errors = new Dictionary<string, string[]>();
        if (!IsValidEmail(email)) errors["email"] = ["A valid email is required."];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            errors["password"] = ["Password must be at least 8 characters."];
        if (validateDisplayName && (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100))
            errors["displayName"] = ["Display name is required and must be at most 100 characters."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateOrganization(string? name, string? slug)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
            errors["name"] = ["Name is required and must be at most 120 characters."];
        if (string.IsNullOrWhiteSpace(slug)
            || slug.Length > 80
            || slug.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            errors["slug"] = ["Slug must contain only letters, numbers, and hyphens."];
        return errors;
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var address = new MailAddress(email.Trim());
            return address.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ApiException(400, "validation_error", "One or more fields are invalid.", errors);
        }
    }

    private static ApiException NotFound(string message = "The requested resource was not found.") =>
        new(404, "not_found", message);

    private static Guid GetSubject(ClaimsPrincipal user)
    {
        var subject = user.FindFirst("sub")?.Value;
        return Guid.TryParse(subject, out var id)
            ? id
            : throw new ApiException(401, "unauthorized", "The bearer token subject is invalid.");
    }

    private static decimal CalculateSeasonProgress(DateOnly start, DateOnly end, DateOnly today)
    {
        var total = Math.Max(1, end.DayNumber - start.DayNumber);
        var elapsed = today.DayNumber - start.DayNumber;
        return decimal.Round(Math.Clamp(elapsed * 100m / total, 0m, 100m), 1);
    }
}
