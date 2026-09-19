# Part 5 — Code Defense

> ⚠️ **WRITE THIS IN YOUR OWN WORDS, AND WRITE IT LAST.** The README wants this committed on its own
> **after the code is final** so the line numbers still match. Every answer must name the file + line
> numbers. The pointers below are accurate as of the current commit — **re-verify them right before
> you write**, because any later edit shifts lines. Replace each _(your words)_ block.

## 1. Walk the stock-transaction path line by line
**File:** `src/PartsInventory.Api/Services/PartService.cs:84-128` (`AddTransactionAsync`).
_(Your words — go line by line.)_ Key lines to cover:
- `:93` the `for` retry loop wraps the whole read-modify-write.
- `:96-98` re-read the part each attempt (query filter → soft-deleted reads as null → `PartNotFound`).
- `:100-102` compute `newQuantity` and reject if it would go below zero (**the negative-quantity rule
  lives here**, not in a validator — say why).
- `:104-114` build the `StockTransaction`, set new `Quantity`, **regenerate `RowVersion` at :113**.
- `:118` `SaveChangesAsync`; `:121-125` catch `DbUpdateConcurrencyException`, clear the tracker, retry.
Explain **why the order matters** (re-check must be on fresh data; token regen before save). If any
line came from AI and you kept it, say which (e.g. the `RootContextData`/token idea) and why.

## 2. The concurrency question — two clerks sell the last unit
_(Your words.)_ Trace it with line numbers:
- Both read `Quantity` at `:96` with the same `RowVersion`.
- Clerk A saves first at `:118`; EF's `UPDATE ... WHERE RowVersion=@original`
  (config at `Data/AppDbContext.cs:34`, token field `Domain/Part.cs:33`) succeeds and moves the token.
- Clerk B's save at `:118` matches **0 rows** → `DbUpdateConcurrencyException` at `:121` → retry →
  re-reads fresh `Quantity` at `:96` → the `:100-102` check now rejects if none left.
**Does it prevent overselling?** Yes — name the mechanism (optimistic concurrency token + re-check on
retry). State it plainly in your own words. (Verified: `ConcurrencyTests` — 100×`-1` from 100 → 0.)

## 3. Soft delete — every place that checks `IsActive`
List with line numbers (verify before submitting):
- `Data/AppDbContext.cs:38` — global query filter on `Part` (`p => p.IsActive`).
- `Data/AppDbContext.cs:54` — matching filter on `StockTransaction` (`t.Part!.IsActive`).
- `Services/PartService.cs:75` — `SoftDeleteAsync` sets `IsActive = false`.
- `Services/PartService.cs:46` — `CreateAsync` sets `IsActive = true`.
- `Domain/Part.cs:23` — default `true`.
_(Your words.)_ Is there any path that can still touch a deleted part? Consider: the filters apply to
all LINQ queries, but `IgnoreQueryFilters()` (not used) or raw SQL would bypass them. State whether any
such path exists in your code (it doesn't — but say so and why).

## 4. The requirement change — switch to hard delete
_(Your words.)_ Files you'd change and how:
- `Services/PartService.cs` — `SoftDeleteAsync` (`:71-78`) becomes a real `Remove` + `SaveChanges`.
- `Data/AppDbContext.cs` — remove the two query filters (`:38`, `:54`); rely on the existing FK
  **cascade delete** (`:48-51`) so a part's transactions go with it.
- `Domain/Part.cs` — `IsActive` (`:23`) and its DTO field become unnecessary; a migration drops the column.
Address: **existing transaction history** (cascade-deleted — call out the audit-trail loss),
**delete idempotency** (a second delete now 404s instead of being a no-op), and **SKU reuse** (a hard
delete frees the SKU for reuse; soft delete keeps it occupied — the unique index still sees it).

## 5. Argue against yourself (DECISIONS Q1)
_(Your words.)_ Make the strongest case **for controllers** (the option you rejected): built-in model
binding/validation conventions, `[ApiController]` behaviors, filters, familiarity, API versioning at
scale, easier for a large team. Then state what would have to be true for you to switch (e.g. the
surface grows past ~a dozen resources, or the team already standardises on MVC).

## 6. What is weakest
_(Your words — be honest, this scores well.)_ Candid options grounded in the code:
- The `RowVersion` token is **app-managed** (regenerated in code) rather than a DB-native rowversion,
  because SQLite has none — on SQL Server I'd use `IsRowVersion()`. Under pathological contention the
  retry loop (`PartService.cs:82` max 10) could exhaust and surface a 500.
- `MaxLength` isn't enforced by SQLite at the DB layer (only by validation).
Name **one line/block an AI wrote that you didn't fully understand at first** and what you did about it
(e.g. `ValidationFilter.cs` RootContextData, or `ChangeTracker.Clear()` at `PartService.cs:125`) — and
what you'd do with two more hours.
