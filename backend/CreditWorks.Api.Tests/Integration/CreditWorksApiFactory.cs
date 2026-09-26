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
