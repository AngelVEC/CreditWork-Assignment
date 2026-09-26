using CreditWorks.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Data;

/// <summary>
/// Creates the database (if it doesn't exist) and seeds fixed reference
/// data + the initial category configuration + a dev-only admin account.
///
/// Design decision (documented per assignment section 23): we use
/// Database.EnsureCreated() rather than EF Core migrations. The schema
/// here is fully expressed by AppDbContext's model, so migrations could be
/// generated trivially later (`dotnet ef migrations add InitialCreate`)
/// once the project is opened somewhere with the .NET SDK / EF tools
/// available. EnsureCreated + idempotent seeding was chosen because it is
/// simpler, requires no separately-maintained migration snapshot files,
/// and still fully satisfies "the solution contains everything required to
/// create or initialise the database" — a reviewer just needs to run the
/// app once against an empty database. The trade-off (documented in the
/// README) is that EnsureCreated does not support incremental schema
/// changes the way migrations do; that's an acceptable trade for a
/// small exercise like this one, but would be revisited for a real
/// production system with an evolving schema.
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.Manufacturers.AnyAsync())
        {
            db.Manufacturers.AddRange(
                new Manufacturer { Name = "Mazda" },
                new Manufacturer { Name = "Mercedes" },
                new Manufacturer { Name = "Honda" },
                new Manufacturer { Name = "Ferrari" },
                new Manufacturer { Name = "Toyota" }
            );
        }

        if (!await db.VehicleCategories.AnyAsync())
        {
            db.VehicleCategories.AddRange(
                new VehicleCategory { Name = "Light", IconKey = "Feather", MinWeightKg = 0m, MaxWeightKg = 500m },
                new VehicleCategory { Name = "Medium", IconKey = "Truck", MinWeightKg = 500m, MaxWeightKg = 2500m },
                new VehicleCategory { Name = "Heavy", IconKey = "Container", MinWeightKg = 2500m, MaxWeightKg = null }
            );
        }

        await db.SaveChangesAsync();

        if (!await db.Admins.AnyAsync())
        {
            var username = config["SeedAdmin:Username"] ?? "admin";
            var password = config["SeedAdmin:Password"];

            if (string.IsNullOrWhiteSpace(password))
            {
                // Deliberately fail loudly rather than seed a guessable
                // default credential — see README "Configuration" section.
                logger.LogWarning(
                    "SeedAdmin:Password is not configured — skipping admin account seeding. " +
                    "Set the SeedAdmin__Password environment variable (or user-secret) and restart to create the dev admin account.");
                return;
            }

            var admin = new Admin { Username = username, Role = "Admin" };
            var hasher = new PasswordHasher<Admin>();
            admin.PasswordHash = hasher.HashPassword(admin, password);

            db.Admins.Add(admin);
            await db.SaveChangesAsync();

            logger.LogInformation("Seeded dev admin account '{Username}'. This is a development-only default — never use it in production.", username);
        }
    }
}
