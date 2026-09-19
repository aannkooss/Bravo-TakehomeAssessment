using Microsoft.AspNetCore.Http.HttpResults;
using PartsInventory.Api.Dtos;
using PartsInventory.Api.Services;
using PartsInventory.Api.Validation;

namespace PartsInventory.Api.Endpoints;

public static class PartsEndpoints
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapPartsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/parts").WithTags("Parts");

        group.MapGet("/", ListParts)
            .WithName("ListParts")
            .Produces<PaginatedResponse<PartResponse>>();

        group.MapGet("/{id:int}", GetPart)
            .WithName("GetPart")
            .Produces<PartResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreatePart)
            .WithName("CreatePart")
            .AddEndpointFilter<ValidationFilter<CreatePartRequest>>()
            .Produces<PartResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:int}", UpdatePart)
            .WithName("UpdatePart")
            .AddEndpointFilter<ValidationFilter<UpdatePartRequest>>()
            .Produces<PartResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:int}", DeletePart)
            .WithName("DeletePart")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:int}/transactions", AddTransaction)
            .WithName("AddTransaction")
            .AddEndpointFilter<ValidationFilter<CreateTransactionRequest>>()
            .Produces<TransactionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapGet("/{id:int}/transactions", GetTransactions)
            .WithName("GetTransactions")
            .Produces<IReadOnlyList<TransactionResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<PaginatedResponse<PartResponse>>> ListParts(
        IPartService parts, CancellationToken ct, int page = 1, int pageSize = DefaultPageSize)
    {
        // Clamp rather than 500: out-of-range paging is well-defined, never a negative Skip.
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var result = await parts.GetPartsAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<PartResponse>, NotFound>> GetPart(
        int id, IPartService parts, CancellationToken ct)
    {
        var part = await parts.GetByIdAsync(id, ct);
        return part is null ? TypedResults.NotFound() : TypedResults.Ok(part);
    }

    private static async Task<Created<PartResponse>> CreatePart(
        CreatePartRequest request, IPartService parts, CancellationToken ct)
    {
        var created = await parts.CreateAsync(request, ct);
        return TypedResults.Created($"/api/parts/{created.Id}", created);
    }

    private static async Task<Results<Ok<PartResponse>, NotFound>> UpdatePart(
        int id, UpdatePartRequest request, IPartService parts, CancellationToken ct)
    {
        var updated = await parts.UpdateAsync(id, request, ct);
        return updated is null ? TypedResults.NotFound() : TypedResults.Ok(updated);
    }

    private static async Task<Results<NoContent, NotFound>> DeletePart(
        int id, IPartService parts, CancellationToken ct)
    {
        var deleted = await parts.SoftDeleteAsync(id, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<Results<Created<TransactionResponse>, NotFound, ValidationProblem>> AddTransaction(
        int id, CreateTransactionRequest request, IPartService parts, CancellationToken ct)
    {
        var result = await parts.AddTransactionAsync(id, request, ct);
        return result.Outcome switch
        {
            TransactionOutcome.PartNotFound => TypedResults.NotFound(),
            TransactionOutcome.WouldGoNegative => TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["quantityChange"] = [$"This change would drive quantity below zero (current on-hand is {result.NewQuantity})."]
            }),
            _ => TypedResults.Created($"/api/parts/{id}/transactions", result.Transaction)
        };
    }

    private static async Task<Results<Ok<IReadOnlyList<TransactionResponse>>, NotFound>> GetTransactions(
        int id, IPartService parts, CancellationToken ct)
    {
        var history = await parts.GetTransactionsAsync(id, ct);
        return history is null ? TypedResults.NotFound() : TypedResults.Ok(history);
    }
}
