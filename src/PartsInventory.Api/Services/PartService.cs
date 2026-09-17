using Microsoft.EntityFrameworkCore;
using PartsInventory.Api.Data;
using PartsInventory.Api.Domain;
using PartsInventory.Api.Dtos;

namespace PartsInventory.Api.Services;

public class PartService : IPartService
{
    private readonly AppDbContext _db;

    public PartService(AppDbContext db) => _db = db;

    public async Task<PaginatedResponse<PartResponse>> GetPartsAsync(int page, int pageSize, CancellationToken ct)
    {
        // Filtering (soft delete) and paging both happen in the database.
        // AsNoTracking: read-only projection, no need for the change tracker.
        var query = _db.Parts.AsNoTracking().OrderBy(p => p.Id);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => ToResponse(p))
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PaginatedResponse<PartResponse>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<PartResponse?> GetByIdAsync(int id, CancellationToken ct)
    {
        var part = await _db.Parts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return part is null ? null : ToResponse(part);
    }

    public async Task<PartResponse> CreateAsync(CreatePartRequest request, CancellationToken ct)
    {
        var part = new Part
        {
            Name = request.Name,
            Sku = request.Sku,
            Quantity = request.Quantity,
            Location = request.Location,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = Guid.NewGuid()
        };

        _db.Parts.Add(part);
        await _db.SaveChangesAsync(ct);
        return ToResponse(part);
    }

    public async Task<PartResponse?> UpdateAsync(int id, UpdatePartRequest request, CancellationToken ct)
    {
        var part = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (part is null) return null;

        part.Name = request.Name;
        part.Sku = request.Sku;
        part.Location = request.Location;
        part.RowVersion = Guid.NewGuid();

        await _db.SaveChangesAsync(ct);
        return ToResponse(part);
    }

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var part = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (part is null) return false;

        part.IsActive = false;
        part.RowVersion = Guid.NewGuid();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AddTransactionResult> AddTransactionAsync(int id, CreateTransactionRequest request, CancellationToken ct)
    {
        // NOTE: Checkpoint 2 implements the straightforward read-modify-write. The optimistic
        // concurrency retry loop (RowVersion) is added in Checkpoint 4 so concurrent decrements
        // cannot oversell or lose updates.
        var part = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (part is null)
            return new AddTransactionResult(TransactionOutcome.PartNotFound, null, 0);

        var newQuantity = part.Quantity + request.QuantityChange;
        if (newQuantity < 0)
            return new AddTransactionResult(TransactionOutcome.WouldGoNegative, null, part.Quantity);

        var tx = new StockTransaction
        {
            PartId = part.Id,
            QuantityChange = request.QuantityChange,
            Reason = request.Reason,
            TimestampUtc = DateTime.UtcNow
        };

        part.Quantity = newQuantity;
        part.RowVersion = Guid.NewGuid();
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        return new AddTransactionResult(TransactionOutcome.Success, ToResponse(tx), part.Quantity);
    }

    public async Task<IReadOnlyList<TransactionResponse>?> GetTransactionsAsync(int id, CancellationToken ct)
    {
        // Confirm the part is active first; history for a soft-deleted part returns 404 (invisible everywhere).
        var exists = await _db.Parts.AsNoTracking().AnyAsync(p => p.Id == id, ct);
        if (!exists) return null;

        return await _db.Transactions.AsNoTracking()
            .Where(t => t.PartId == id)
            .OrderByDescending(t => t.TimestampUtc)
            .Select(t => ToResponse(t))
            .ToListAsync(ct);
    }

    public Task<bool> SkuExistsAsync(string sku, int? excludePartId, CancellationToken ct) =>
        _db.Parts.AsNoTracking()
            .AnyAsync(p => p.Sku == sku && (excludePartId == null || p.Id != excludePartId), ct);

    private static PartResponse ToResponse(Part p) =>
        new(p.Id, p.Name, p.Sku, p.Quantity, p.Location, p.IsActive, p.CreatedAtUtc);

    private static TransactionResponse ToResponse(StockTransaction t) =>
        new(t.Id, t.PartId, t.QuantityChange, t.Reason, t.TimestampUtc);
}
