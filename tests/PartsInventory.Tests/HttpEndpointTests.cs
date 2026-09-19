using System.Net;
using System.Net.Http.Json;
using PartsInventory.Api.Dtos;
using Xunit;

namespace PartsInventory.Tests;

/// <summary>
/// HTTP-layer tests through the real pipeline: asserts status codes, the Location header,
/// and the ValidationProblemDetails shape on invalid input.
/// </summary>
public class HttpEndpointTests
{
    [Fact]
    public async Task Post_creates_201_with_location_then_get_200_then_delete_204_then_404()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        // POST -> 201 + Location
        var post = await client.PostAsJsonAsync("/api/parts",
            new CreatePartRequest("Bolt", "SKU-LIFECYCLE", 5, "A1"));
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.NotNull(post.Headers.Location);

        // GET the Location -> 200
        var location = post.Headers.Location!;
        var get = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var part = await get.Content.ReadFromJsonAsync<PartResponse>();
        Assert.NotNull(part);
        Assert.Equal("SKU-LIFECYCLE", part!.Sku);

        // DELETE -> 204 (soft delete)
        var delete = await client.DeleteAsync(location);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // GET after soft delete -> 404 (invisible everywhere)
        var getAfter = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
    }

    [Fact]
    public async Task Get_missing_id_returns_404()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/parts/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Invalid_body_returns_400_problem_json_validationproblemdetails()
    {
        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        // Empty name + empty SKU violates field-shape rules.
        var resp = await client.PostAsJsonAsync("/api/parts",
            new CreatePartRequest("", "", 0, null));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType?.MediaType);

        var problem = await resp.Content.ReadFromJsonAsync<ValidationProblemView>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.NotEmpty(problem.Errors);
    }

    // Minimal view of ValidationProblemDetails for assertions.
    private record ValidationProblemView(string Title, int Status, Dictionary<string, string[]> Errors);
}
