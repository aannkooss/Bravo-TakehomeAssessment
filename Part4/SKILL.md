# AI Skill / Instruction File — Parts Inventory API

Custom instructions for an AI assistant working in **this** repo. Every type/file/convention named
below exists in the submitted code.

> This is a distilled version of the standards that actually drove the build; it matches the shipped
> code as of the final commit.

## Stack & structure
- .NET 10, ASP.NET Core **Minimal APIs** with `TypedResults`. EF Core 10 + **SQLite** (file mode).
- Endpoints live in `src/PartsInventory.Api/Endpoints/PartsEndpoints.cs` (grouped under `/api/parts`).
- Layers: `Domain/` (entities), `Data/AppDbContext.cs`, `Dtos/`, `Services/` (`IPartService` +
  `PartService`), `Validation/`, `Infrastructure/GlobalExceptionHandler.cs`.

## Hard rules
1. **All data access is async** — `ToListAsync`, `SaveChangesAsync`, `AnyAsync`. Never `.Result` or
   `.Wait()`.
2. **Never return EF entities** from an endpoint. Return the DTOs in `Dtos/` (`PartResponse`,
   `TransactionResponse`, `PaginatedResponse<T>`).
3. **`IPartService` is Scoped** (it wraps the request-scoped `AppDbContext`). Don't make it Singleton.
4. **Read queries use `AsNoTracking()`**; filtering and paging happen **in the database**, never in
   memory.
5. **Validation runs in the pipeline** via `ValidationFilter<T>` (an `IEndpointFilter`). Field-shape
   and async SKU-uniqueness live in FluentValidation validators; the **negative-quantity** rule lives
   in `PartService.AddTransactionAsync` because it needs current state and must be atomic with the write.
6. **One error shape.** All errors are `application/problem+json`. Unhandled exceptions go through
   `GlobalExceptionHandler` (`IExceptionHandler` + `AddProblemDetails()`); validation failures return
   `ValidationProblemDetails`. Never leak exception details; log server-side with the `traceId`.
7. **Soft delete is invisible everywhere.** Global query filters on `Part` (`IsActive`) and
   `StockTransaction` (`t.Part!.IsActive`). A soft-deleted part 404s on get/update/add-transaction/history.
8. **Concurrency:** the stock path must not oversell or lose updates. Keep the `RowVersion`
   optimistic-concurrency token + retry loop; regenerate the token on every mutating save.

## Status codes
- Create → `201` + `Location`. Get missing → `404`. Delete → `204`. Update invalid → `400`, missing →
  `404`. Transaction that would go negative → `400` (as `ValidationProblemDetails`).

## Tests (`tests/PartsInventory.Tests`)
- SQLite only (temp file via `TestApiFactory`); **never** the EF In-Memory provider.
- Keep the four kinds: HTTP-layer (`WebApplicationFactory<Program>`), concurrency (each op its own
  scope), pagination edge cases, business rules. `dotnet test` must stay green.

## Container
- Multi-stage, non-root, chiseled, port **8080**. Config via env vars (`ConnectionStrings__Default`),
  never baked secrets. Keep `.dockerignore` excluding `bin/obj/.git/tests/*.db`.

---

**Why a saved skill beats re-typing:** it keeps every session consistent with the decisions already
made (async-only, DTOs, one error shape, the concurrency rule) instead of relying on me to re-state
them and the AI to re-derive them — which is where drift and reinvented types creep in. It also
doubles as onboarding: a new contributor (human or AI) reads one file and knows the house style.
