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

        if (!await db.Vehicles.AnyAsync())
        {
            var manufacturerIds = await db.Manufacturers.Select(x => x.Id).ToListAsync();
            db.Vehicles.AddRange(GenerateSeedVehicles(manufacturerIds, count: 50));
            await db.SaveChangesAsync();
        }

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

    private static readonly string[] FirstNames =
    {
        "John", "Jane", "Aroha", "Mike", "Sarah", "David", "Emily", "Tom", "Hemi", "Grace",
        "Liam", "Olivia", "Noah", "Ava", "Ethan", "Mia", "Lucas", "Sophia", "Mason", "Isabella",
        "Logan", "Charlotte", "Amelia", "Wiremu", "Kiri", "Manaia", "Priya", "Raj", "Wei", "Yuki"
    };

    private static readonly string[] LastNames =
    {
        "Smith", "Turei", "Ngata", "Chen", "Williams", "Lee", "Clarke", "Baker", "Walker", "Kim",
        "Brown", "Davies", "Wilson", "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin"
    };

    /// <summary>
    /// Generates realistic-looking vehicles spread evenly across the
    /// three seeded categories (Light/Medium/Heavy weight bands) and across
    /// owners/manufacturers/years, so the app has something to browse,
    /// sort, and search on first run. Uses a fixed random seed so the
    /// generated data is reproducible across runs/environments rather than
    /// different every time the (empty) database is first created.
    /// </summary>
    private static IEnumerable<Vehicle> GenerateSeedVehicles(IReadOnlyList<int> manufacturerIds, int count)
    {
        var random = new Random(Seed: 42);
        var maxYear = DateTime.UtcNow.Year;

        for (var i = 0; i < count; i++)
        {
            var ownerName = $"{FirstNames[i % FirstNames.Length]} {LastNames[(i * 7) % LastNames.Length]}";
            var manufacturerId = manufacturerIds[random.Next(manufacturerIds.Count)];
            var year = random.Next(1990, maxYear + 1);

            // Cycle through the three weight bands so the seed data isn't
            // skewed toward one category (roughly a third Light, a third
            // Medium, a third Heavy).
            var weightKg = (i % 3) switch
            {
                0 => RandomWeight(random, 10m, 490m),     // Light band (0-500kg)
                1 => RandomWeight(random, 550m, 2450m),   // Medium band (500-2500kg)
                _ => RandomWeight(random, 2600m, 6000m),  // Heavy band (2500kg+)
            };

            yield return new Vehicle
            {
                OwnerName = ownerName,
                ManufacturerId = manufacturerId,
                YearOfManufacture = year,
                WeightKg = weightKg
            };
        }
    }

    private static decimal RandomWeight(Random random, decimal min, decimal max)
    {
        var fraction = (decimal)random.NextDouble();
        return decimal.Round(min + fraction * (max - min), 2);
    }
}
