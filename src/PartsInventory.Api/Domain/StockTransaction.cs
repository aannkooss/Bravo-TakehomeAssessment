namespace PartsInventory.Api.Domain;

/// <summary>
/// An immutable ledger entry recording a change to a part's on-hand quantity.
/// </summary>
public class StockTransaction
{
    public int Id { get; set; }

    /// <summary>Foreign key to the owning <see cref="Part"/>.</summary>
    public int PartId { get; set; }

    /// <summary>Signed change applied to the part's quantity (positive = receipt, negative = issue).</summary>
    public int QuantityChange { get; set; }

    /// <summary>Free-text reason for the movement (e.g. "cycle count", "sold").</summary>
    public string? Reason { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public Part? Part { get; set; }
}
