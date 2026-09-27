using System.Linq;
using CreditWorks.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CreditWorks.Api.Tests.Integration;

/// <summary>
/// Boots the real ASP.NET Core pipeline (auth, middleware, controllers)
/// against an isolated in-memory database per test class instance, and
/// supplies the config values Program.cs requires (connection string, JWT
/// signing key, seed admin credentials) so startup doesn't fail outside a
/// real environment with those env vars set.
/// </summary>
public class CreditWorksApiFactory : WebApplicationFactory<Program>
{
    public readonly string DatabaseName = Guid.NewGuid().ToString();
    public const string SeedAdminPassword = "Test-Only-Password-123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // AuthController sets the auth cookie's Secure flag from
        // _env.IsDevelopment(). WebApplicationFactory's TestServer talks
        // over plain HTTP, not HTTPS — if Secure ended up true here (e.g.
        // because the environment defaulted to something other than
        // Development), the cookie would still be set on login, but the
        // .NET HttpClient cookie container silently refuses to resend a
        // Secure cookie over a non-HTTPS connection. That makes login look
        // like it succeeds while every subsequent "authenticated" request
        // comes back 401, which is confusing to debug from the outside.
        // Forcing Development here keeps the test environment's cookie
        // behavior matched to its actual (HTTP) transport.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-characters-long",
                ["Jwt:Issuer"] = "CreditWorksVehicleApp",
                ["Jwt:Audience"] = "CreditWorksVehicleApp",
                ["Jwt:ExpiryMinutes"] = "90",
                ["SeedAdmin:Username"] = "admin",
                ["SeedAdmin:Password"] = SeedAdminPassword,
                ["Cors:AllowedOrigin"] = "http://localhost:5173",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(DatabaseName));
        });
    }
}
