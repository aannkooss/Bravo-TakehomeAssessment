# AI Collaboration Log

> **Note to reviewer:** this whole build was done with Claude Code as a pair. The entries below are
> the interactions that actually shaped the code, with the commits they landed in. I've kept what I
> verified and why. (Author: review each entry against your own recollection before submitting.)

Commit map: `86f9300` data/model · `50407d3` endpoints+validation · `52e1fe2` concurrency ·
`3c2f09b` tests · `e8a165e` container+security pins.

---

## Entry 1 — AI's first instinct was wrong: rowversion on SQLite
- **Goal:** Add an optimistic-concurrency token to `Part`.
- **Prompt & context:** Asked for optimistic concurrency for the stock-decrement path on EF Core 10
  **with SQLite** (I emphasised the provider). 
- **What came back:** The initial idea leaned toward a `byte[]` `[Timestamp]`/`IsRowVersion()`
  column — the SQL Server pattern.
- **Validation:** I checked the EF Core docs: SQLite has **no native auto-updating rowversion**, so a
  `byte[]` rowversion won't change on its own and the concurrency check silently never fires. Confirmed
  by reasoning about the generated SQL.
- **What I changed:** Switched to a `Guid RowVersion` marked `IsConcurrencyToken()` that the service
  **regenerates on every mutating save**, so EF emits `UPDATE ... WHERE Id=@id AND RowVersion=@original`.
- **Commit:** `86f9300` (token + config), `52e1fe2` (retry loop).

## Entry 2 — AI taught me something: query filters and required navigations
- **Goal:** Enforce soft delete everywhere via a global query filter.
- **Prompt & context:** Added `HasQueryFilter(p => p.IsActive)` on `Part` and ran
  `dotnet ef migrations add`.
- **What came back / happened:** EF warned that `Part` has a global filter and is the **required end**
  of the `StockTransaction` relationship, which "may lead to unexpected results." I didn't know that a
  filter on the principal needs a matching filter on the dependent.
- **Validation:** Read the linked EF docs (fwlink 2131316); confirmed the fix is a matching filter on
  the dependent.
- **What I changed:** Added `HasQueryFilter(t => t.Part!.IsActive)` on `StockTransaction`, which both
  silences the warning and makes "deleted = invisible everywhere" consistent (history 404s too).
- **Commit:** `86f9300`.

## Entry 3 — AI/scaffolding pulled vulnerable transitive packages; I caught it
- **Goal:** Clean build with no known-vulnerable dependencies.
- **Prompt & context:** After adding EF Core 10 + `Microsoft.AspNetCore.OpenApi`, `dotnet build`
  emitted **NU1903** warnings for `SQLitePCLRaw.lib.e_sqlite3 2.1.11` and `Microsoft.OpenApi 2.0.0`.
- **What came back:** The generated project happily used the transitive versions; nothing flagged them
  until I read the build output.
- **Validation:** Queried the NuGet flat-container API for patched versions; confirmed `2.1.13` and
  `2.12.2` exist, pinned them, rebuilt (warnings gone), re-ran `dotnet test` (12 pass), and re-probed
  `/openapi/v1.json` to confirm the OpenApi bump didn't break doc generation.
- **What I changed:** Added direct `PackageReference` pins for both.
- **Commit:** `e8a165e`.

## Entry 4 — Getting the route id into an async validator
- **Goal:** SKU-uniqueness on **update** must exclude the part being updated.
- **Prompt & context:** The `IEndpointFilter` validates the request body, which has no id; the id is a
  route value.
- **What came back:** Suggestion to use FluentValidation's `ValidationContext.RootContextData`.
- **Validation:** Verified by testing update-with-same-sku (should pass) vs update-to-another-part's-sku
  (should 400).
- **What I changed:** The `ValidationFilter<T>` copies the route `id` into `RootContextData`; the update
  validator reads it and passes `excludePartId` to `SkuExistsAsync`.
- **Commit:** `50407d3`.
