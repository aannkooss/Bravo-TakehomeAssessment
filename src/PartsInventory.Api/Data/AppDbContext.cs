using Microsoft.EntityFrameworkCore;
using PartsInventory.Api.Domain;

namespace PartsInventory.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Part> Parts => Set<Part>();
    public DbSet<StockTransaction> Transactions => Set<StockTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Part>(part =>
        {
            part.HasKey(p => p.Id);

            part.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);

            // Sku is bounded (NOT unbounded) and unique.
            part.Property(p => p.Sku)
                .IsRequired()
                .HasMaxLength(64);
            part.HasIndex(p => p.Sku).IsUnique();

            part.Property(p => p.Location).HasMaxLength(100);

            // Guid concurrency token, regenerated on each mutating save (see PartService).
            // EF emits UPDATE ... WHERE Id = @id AND RowVersion = @original, so a racing
            // update that already moved the token affects 0 rows -> DbUpdateConcurrencyException.
            part.Property(p => p.RowVersion).IsConcurrencyToken();

            // Global soft-delete filter: deleted parts are invisible to every query
            // (list, get, update, transactions) unless explicitly IgnoreQueryFilters().
            part.HasQueryFilter(p => p.IsActive);
        });

        modelBuilder.Entity<StockTransaction>(tx =>
        {
            tx.HasKey(t => t.Id);
            tx.Property(t => t.Reason).HasMaxLength(200);

            tx.HasOne(t => t.Part)
              .WithMany(p => p.Transactions)
              .HasForeignKey(t => t.PartId)
              .OnDelete(DeleteBehavior.Cascade);

            // Matching filter so transactions of a soft-deleted part are also hidden,
            // keeping "deleted = invisible everywhere" consistent and silencing EF's
            // required-navigation query-filter warning.
            tx.HasQueryFilter(t => t.Part!.IsActive);
        });
    }
}
