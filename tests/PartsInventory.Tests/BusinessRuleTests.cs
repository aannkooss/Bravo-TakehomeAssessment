using System.Net;
using System.Net.Http.Json;
using PartsInventory.Api.Dtos;
using Xunit;

namespace PartsInventory.Tests;

/// <summary>
/// Core business rules: successful create, duplicate-SKU rejection, and a transaction that
/// would drive quantity negative.
/// </summary>
public class BusinessRuleTests
{
    [Fact]
    public async Task Create_succeeds_with_valid_input()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/parts",
            new CreatePartRequest("Nut", "SKU-CREATE-OK", 12, "B2"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var part = await resp.Content.ReadFromJsonAsync<PartResponse>();
        Assert.Equal(12, part!.Quantity);
        Assert.True(part.IsActive);
    }

    [Fact]
    public async Task Duplicate_sku_is_rejected_400()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/parts",
            new CreatePartRequest("First", "SKU-DUP", 1, null));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/parts",
            new CreatePartRequest("Second", "SKU-DUP", 1, null));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Transaction_that_would_go_negative_is_rejected_400()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/parts",
            new CreatePartRequest("Screw", "SKU-NEG", 5, null));
        var part = await create.Content.ReadFromJsonAsync<PartResponse>();

        var resp = await client.PostAsJsonAsync($"/api/parts/{part!.Id}/transactions",
            new CreateTransactionRequest(-6, "oversell attempt"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType?.MediaType);
    }
}
