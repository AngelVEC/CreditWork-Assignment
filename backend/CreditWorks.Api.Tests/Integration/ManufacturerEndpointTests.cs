using System.Net;
using System.Net.Http.Json;
using CreditWorks.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace CreditWorks.Api.Tests.Integration;

public class ManufacturerEndpointTests : IDisposable
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
    public async Task Create_WithoutLogin_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/manufacturers", new ManufacturerRequest { Name = "Subaru" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_LoggedIn_Returns201AndAppearsInList()
    {
        var client = await NewAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/manufacturers", new ManufacturerRequest { Name = "Subaru" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ManufacturerResponse>();
        created!.Name.Should().Be("Subaru");
        created.Id.Should().BeGreaterThan(0);

        // Reading the list back doesn't require auth.
        var list = await _factory.CreateClient().GetFromJsonAsync<List<ManufacturerResponse>>("/api/manufacturers");
        list!.Should().Contain(m => m.Name == "Subaru");
    }

    [Fact]
    public async Task Create_DuplicateName_CaseInsensitive_Returns400()
    {
        var client = await NewAuthenticatedClientAsync();

        await client.PostAsJsonAsync("/api/manufacturers", new ManufacturerRequest { Name = "Subaru" });
        var response = await client.PostAsJsonAsync("/api/manufacturers", new ManufacturerRequest { Name = "subaru" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_LoggedInButBlankName_Returns400()
    {
        var client = await NewAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/manufacturers", new ManufacturerRequest { Name = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
