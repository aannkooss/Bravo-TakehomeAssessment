using System.Net;
using System.Net.Http.Json;
using PartsInventory.Api.Dtos;
using Xunit;

namespace PartsInventory.Tests;

/// <summary>
/// Fires N concurrent stock decrements and asserts no lost updates and no overselling.
/// Each concurrent operation is a separate HTTP request, so each gets its own DI scope and
/// its own DbContext/SQLite connection — a shared DbContext would serialise and prove nothing.
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public async Task Concurrent_decrements_never_oversell_and_never_lose_updates()
    {
        const int initial = 100;
        const int requests = 100;

        using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/parts",
            new CreatePartRequest("Widget", "SKU-CONC", initial, null));
        var part = await create.Content.ReadFromJsonAsync<PartResponse>();
        var id = part!.Id;

        // Fire all decrements concurrently.
        var tasks = Enumerable.Range(0, requests).Select(_ =>
            client.PostAsJsonAsync($"/api/parts/{id}/transactions",
                new CreateTransactionRequest(-1, "concurrent sale")));
        var responses = await Task.WhenAll(tasks);

        var successful = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        // Any non-success must be the "would go negative" business rejection, never a 500.
        Assert.All(responses, r =>
            Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest,
                $"Unexpected status {(int)r.StatusCode}"));

        var finalGet = await client.GetAsync($"/api/parts/{id}");
        var final = await finalGet.Content.ReadFromJsonAsync<PartResponse>();

        // No lost updates: final == initial - successful. No overselling: never below zero.
        Assert.Equal(initial - successful, final!.Quantity);
        Assert.True(final.Quantity >= 0, "Quantity went below zero (oversold).");

        // With 100 available and 100 x -1, every request should have succeeded and landed at 0.
        Assert.Equal(requests, successful);
        Assert.Equal(0, final.Quantity);
    }
}
