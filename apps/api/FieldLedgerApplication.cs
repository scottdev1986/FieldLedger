using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Npgsql;

namespace FieldLedger.Api;

public static class FieldLedgerApplication
{
    public static WebApplication Build(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configureBuilder?.Invoke(builder);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("FieldLedgerWeb", policy =>
            {
                var webOrigin = builder.Configuration["APP_PUBLIC_URL"] ?? "http://localhost:3000";
                policy.WithOrigins(webOrigin).AllowAnyHeader().AllowAnyMethod();
            });
        });

        var authOptions = AuthOptions.FromConfiguration(builder.Configuration);
        builder.Services.AddSingleton(authOptions);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>,
            Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>>();

        builder.Services.AddSingleton(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>()["DATABASE_URL"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("DATABASE_URL is required.");
            return NpgsqlDataSource.Create(connectionString);
        });
        builder.Services.AddSingleton<IFieldLedgerDbSession, FieldLedgerDbSession>();
        builder.Services.AddSingleton<IAuthRepository, AuthRepository>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = JwtTokenService.CreateValidationParameters(
                    authOptions,
                    TimeProvider.System);
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await ApiErrorWriter.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "unauthorized",
                            "A valid bearer token is required.");
                    }
                };
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseCors("FieldLedgerWeb");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () =>
            Results.Ok(new HealthResponse("ok", "fieldledger-api", DateTimeOffset.UtcNow)))
            .AllowAnonymous();

        var api = app.MapGroup("/api").RequireAuthorization();
        ApiEndpoints.Map(api);
        return app;
    }
}
