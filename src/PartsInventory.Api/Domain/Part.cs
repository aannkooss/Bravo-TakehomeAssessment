namespace PartsInventory.Api.Domain;

/// <summary>
/// A maintenance part tracked in inventory. Deletes are soft (see <see cref="IsActive"/>).
/// </summary>
public class Part
{
    public int Id { get; set; }

    /// <summary>Human-readable name. Required, max 100 chars (enforced in the model + validation).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stock-keeping unit. Required, unique, bounded length (see AppDbContext config).</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>On-hand quantity. Never allowed below zero.</summary>
    public int Quantity { get; set; }

    /// <summary>Optional storage location (bin/shelf/warehouse).</summary>
    public string? Location { get; set; }

    /// <summary>Soft-delete flag. False = deleted; hidden by the global query filter.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optimistic-concurrency token. Regenerated on every mutating save so that
    /// two racing read-modify-write updates cannot both win (the loser gets a
    /// DbUpdateConcurrencyException and is retried). Chosen over a byte[] rowversion
    /// because SQLite has no native auto-updating rowversion column.
    /// </summary>
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public List<StockTransaction> Transactions { get; set; } = new();
}
