using System.Net;
using System.Net.Http.Json;
using PartsInventory.Api.Dtos;
using Xunit;

namespace PartsInventory.Tests;

/// <summary>
/// Pagination edge cases. Each must be well defined (clamped here) and must never produce a
/// 500, a divide-by-zero, or a negative Skip.
/// </summary>
public class PaginationTests
{
    [Theory]
    [InlineData("page=0&pageSize=20")]
    [InlineData("page=-5&pageSize=20")]
    [InlineData("page=1&pageSize=0")]
    [InlineData("page=1&pageSize=1000000")]
    [InlineData("page=999999&pageSize=20")]
    public async Task Pagination_edge_cases_return_200_and_never_500(string query)
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        // Seed a few parts so paging has something to clamp against.
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/parts",
                new CreatePartRequest($"Part {i}", $"SKU-PAGE-{i}", i, null));

        var resp = await client.GetAsync($"/api/parts?{query}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var page = await resp.Content.ReadFromJsonAsync<PaginatedResponse<PartResponse>>();
        Assert.NotNull(page);
        Assert.True(page!.Page >= 1, "Page clamped to >= 1");
        Assert.InRange(page.PageSize, 1, 100);
        Assert.True(page.TotalCount >= 0);
    }
}
