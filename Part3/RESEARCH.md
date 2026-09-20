# Part 3b — Research: Optimistic Concurrency in EF Core

EF Core implements optimistic concurrency by assuming that conflicts are rare and takes no locks. Instead, it arranges for a save to fail if the row changed since it was read. A new property is configured as a concurrency token which is tracked and loaded by EF. On `SaveChanges`, it adds the token to an `UPDATE ... WHERE` clause that compares the original value it read against the database. If a concurrent write already moved the token, the `UPDATE` matches zero rows and EF throws a `DbUpdateConcurrencyException`.

In my API, this protects the stock-decrement path. I mark a `Guid RowVersion` with `IsConcurrencyToken()` (`AppDbContext.cs:34`, `Part.cs:33`) and regenerate it on every new save. Then I wrap `AddTransactionAsync()` in a retry loop that catches the exception, clears the tracker, re-reads the quantity, checks the rule, and retries again. Managing the token manually gives me the freedom to control when a conflict happens.

One big nuance I found was actually a provider difference, not a .NET version one. The docs show the timestamp and row version token maps to a SQL Server `rowversion` column that auto updates, but they state plainly that some providers do not have this native type, like SQLite. Instead of a database-generated `rowversion`, the documents suggest an application-managed token via `[ConcurrencyCheck]`/`IsConcurrencyToken()`, which was the approach that I went with.

One thing that surprised me was that the most common pattern, being `byte[] [Timestamp]` rowversion, actually does nothing on SQLite. The column never actually auto-updates which causes the concurrency check to not occur. This also results in code that looks functionally correct but does not deliver what it promises. I was able to catch this by reading the "Native database-generated concurrency tokens" section and reasoning about the generated SQL, and then switched to the `Guid` token.

*Source: Microsoft Learn, "Handling Concurrency Conflicts – EF Core," https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=data-annotations (last updated 2025-10-30; accessed September 20, 2026).*
