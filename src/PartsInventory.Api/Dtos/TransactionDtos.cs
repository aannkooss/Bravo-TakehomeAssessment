namespace PartsInventory.Api.Dtos;

/// <summary>Payload to add a stock movement. Signed: negative issues stock, positive receives it.</summary>
public record CreateTransactionRequest(
    int QuantityChange,
    string? Reason);

/// <summary>Shape returned to clients for a stock transaction.</summary>
public record TransactionResponse(
    int Id,
    int PartId,
    int QuantityChange,
    string? Reason,
    DateTime TimestampUtc);
