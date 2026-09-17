namespace PartsInventory.Api.Dtos;

/// <summary>Shape returned to clients for a part. Never the EF entity.</summary>
public record PartResponse(
    int Id,
    string Name,
    string Sku,
    int Quantity,
    string? Location,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>Create payload. Quantity is the opening balance; later changes go through transactions.</summary>
public record CreatePartRequest(
    string Name,
    string Sku,
    int Quantity,
    string? Location);

/// <summary>
/// Update payload. Name, Sku and Location are editable here; Quantity is deliberately
/// NOT updatable via PUT — it is only ever moved through stock transactions so the ledger
/// stays the single source of truth.
/// </summary>
public record UpdatePartRequest(
    string Name,
    string Sku,
    string? Location);

/// <summary>Generic paginated envelope returned by list endpoints.</summary>
public record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
