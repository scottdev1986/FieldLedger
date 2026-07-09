using FieldLedger.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace FieldLedger.Api.Tests;

internal sealed class ApiTestHost : IAsyncDisposable
{
    private const string Secret = "fieldledger-integration-test-secret-0123456789";
    private readonly WebApplication _application;

    private ApiTestHost(WebApplication application, HttpClient client)
    {
        _application = application;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<ApiTestHost> StartAsync()
    {
        var databaseUrl = Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
            ?? throw new InvalidOperationException("TEST_DATABASE_URL is required.");
        var app = FieldLedgerApplication.Build([], builder =>
        {
            builder.Configuration["DATABASE_URL"] = databaseUrl;
            builder.Configuration["AUTH_JWT_SECRET"] = Secret;
            builder.Configuration["AUTH_JWT_ISSUER"] = "fieldledger-api";
            builder.Configuration["AUTH_JWT_AUDIENCE"] = "fieldledger";
            builder.Configuration["AUTH_TOKEN_LIFETIME_MINUTES"] = "60";
            builder.Configuration["APP_PUBLIC_URL"] = "http://localhost:3000";
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        });
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new ApiTestHost(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}
