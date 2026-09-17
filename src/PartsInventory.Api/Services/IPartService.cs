using PartsInventory.Api.Dtos;

namespace PartsInventory.Api.Services;

/// <summary>Outcome of attempting to add a stock transaction to a part.</summary>
public enum TransactionOutcome
{
    Success,
    PartNotFound,
    WouldGoNegative
}

/// <summary>Result envelope for <see cref="IPartService.AddTransactionAsync"/>.</summary>
public record AddTransactionResult(TransactionOutcome Outcome, TransactionResponse? Transaction, int NewQuantity);

/// <summary>
/// Application service for parts and their stock ledger. Registered <b>Scoped</b> because it
/// depends on the request-scoped <c>AppDbContext</c> (which is not thread-safe and tracks a
/// per-request change set). A Singleton would capture one DbContext for the app's lifetime
/// (state bleed + threading bugs); Transient would be needlessly wasteful and could still
/// mismatch the DbContext scope.
/// </summary>
public interface IPartService
{
    Task<PaginatedResponse<PartResponse>> GetPartsAsync(int page, int pageSize, CancellationToken ct);

    Task<PartResponse?> GetByIdAsync(int id, CancellationToken ct);

    Task<PartResponse> CreateAsync(CreatePartRequest request, CancellationToken ct);

    /// <summary>Returns the updated part, or null if no active part with that id exists.</summary>
    Task<PartResponse?> UpdateAsync(int id, UpdatePartRequest request, CancellationToken ct);

    /// <summary>Soft delete (sets IsActive = false). Returns false if no active part with that id exists.</summary>
    Task<bool> SoftDeleteAsync(int id, CancellationToken ct);

    Task<AddTransactionResult> AddTransactionAsync(int id, CreateTransactionRequest request, CancellationToken ct);

    /// <summary>Transaction history for an active part, or null if no active part with that id exists.</summary>
    Task<IReadOnlyList<TransactionResponse>?> GetTransactionsAsync(int id, CancellationToken ct);

    /// <summary>True if an active part with this SKU already exists (used by the async validation rule).</summary>
    Task<bool> SkuExistsAsync(string sku, int? excludePartId, CancellationToken ct);
}
