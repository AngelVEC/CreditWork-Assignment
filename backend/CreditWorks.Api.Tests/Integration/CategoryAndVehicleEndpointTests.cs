using System.Net;
using System.Net.Http.Json;
using CreditWorks.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace CreditWorks.Api.Tests.Integration;

// Note: xUnit creates a new instance of this test class per test method, so
// each test gets its own factory and its own isolated in-memory database —
// avoiding state leaking between tests (e.g. the "delete every category"
// test would otherwise corrupt category data for every other test in this class).
public class CategoryAndVehicleEndpointTests : IDisposable
{
    private readonly CreditWorksApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> NewAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient(new() { HandleCookies = true });
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });
        return client;
    }

    [Fact]
    public async Task CreateCategory_ThatWouldLeaveAGap_Returns400()
    {
        var client = await NewAuthenticatedClientAsync();

        // Standard seed is Light(0-500)/Medium(500-2500)/Heavy(2500-null).
        // Inserting a bounded category that doesn't connect to anything breaks coverage.
        var response = await client.PostAsJsonAsync("/api/categories", new CategoryRequest
        {
            Name = "Rogue",
            IconKey = "Feather",
            MinWeightKg = 10000,
            MaxWeightKg = 20000
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteCategory_WhenOnlyOneRemains_Returns409()
    {
        var client = await NewAuthenticatedClientAsync();

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");
        categories.Should().NotBeNull();

        // Delete down to a single category, then attempt to delete the last one.
        foreach (var category in categories!.OrderBy(c => c.MinWeightKg).Take(categories.Count - 1))
        {
            await client.DeleteAsync($"/api/categories/{category.Id}");
        }

        var remaining = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories");
        var lastId = remaining!.Single().Id;

        var response = await client.DeleteAsync($"/api/categories/{lastId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetVehicles_SortedByWeightDescending_ReturnsCorrectOrder()
    {
        var adminClient = await NewAuthenticatedClientAsync();
        var manufacturers = await adminClient.GetFromJsonAsync<List<ManufacturerResponse>>("/api/manufacturers");
        var manufacturerId = manufacturers!.First().Id;

        var publicClient = _factory.CreateClient();

        await publicClient.PostAsJsonAsync("/api/vehicles", new VehicleRequest
        {
            OwnerName = "Light Owner",
            ManufacturerId = manufacturerId,
            YearOfManufacture = 2020,
            WeightKg = 100m
        });
        await publicClient.PostAsJsonAsync("/api/vehicles", new VehicleRequest
        {
            OwnerName = "Heavy Owner",
            ManufacturerId = manufacturerId,
            YearOfManufacture = 2020,
            WeightKg = 5000m
        });

        var response = await publicClient.GetAsync("/api/vehicles?sortBy=weight&sortDir=desc");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();
        vehicles!.First().OwnerName.Should().Be("Heavy Owner");
        vehicles!.First().CategoryName.Should().Be("Heavy");
    }

    [Fact]
    public async Task CreateVehicle_MissingRequiredFields_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vehicles", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetVehicleById_UnknownId_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VehiclesAndManufacturers_AreAccessibleWithoutAuth()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/vehicles")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/manufacturers")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
