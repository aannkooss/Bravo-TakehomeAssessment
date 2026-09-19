# Part 1 — Decisions

Short justifications for the six required choices. Where AI influenced a choice, it's noted.

## 1. Minimal APIs vs controllers vs FastEndpoints

I chose **Minimal APIs** because this is a new, small service and Minimal APIs are Microsoft's
current recommendation for new projects; with `TypedResults` and `.Produces(...)` metadata I get
an OpenAPI document that advertises the real status codes without controller ceremony (see
`src/PartsInventory.Api/Endpoints/PartsEndpoints.cs`). What would change my mind: if the surface
grew to many resources needing model binding conventions, filters, and API-versioning at scale, or
a team already standardised on controllers, the controller programming model's structure would win.
FastEndpoints would only make sense if I were already fluent in it — I am not, so picking it here
would trade a defensible choice for an unfamiliar one.

## 2. Database provider for tests

Tests use **SQLite** (a unique temp *file* per test, `tests/PartsInventory.Tests/TestApiFactory.cs`),
not the EF In-Memory provider. SQLite is a real relational engine, so it honours the unique index on
`Sku`, foreign keys, and gives real SQL — the exact behaviours this assignment grades and the exact
things In-Memory silently ignores. A file (not a shared in-memory connection) lets each request scope
open its own connection, which is what makes the concurrency test meaningful. What SQLite still hides
vs SQL Server: it is dynamically typed so `MaxLength` isn't enforced at the DB layer, it has looser
concurrency semantics (one writer at a time), and it lacks a native `rowversion` — so my concurrency
token is app-managed here (see Q on concurrency in DEFENSE).

## 3. Exception-handling mechanism

I used **`IExceptionHandler` + `AddProblemDetails()`** (`Program.cs:24-25,42`,
`src/PartsInventory.Api/Infrastructure/GlobalExceptionHandler.cs`). `AddProblemDetails()` makes
framework-generated errors and my own handler emit the **same** `application/problem+json` shape, so
a client never sees two different error contracts. The handler logs the full exception server-side and
returns only a generic message plus a `traceId` that maps to that log line, so internals never leak.
Over custom middleware, `IExceptionHandler` is the modern (.NET 8+) first-class hook and composes with
the built-in ProblemDetails writer instead of hand-rolling serialization.

## 4. Validation approach and where each rule lives

**FluentValidation run in the pipeline** via a generic `IEndpointFilter`
(`src/PartsInventory.Api/Validation/ValidationFilter.cs`), not hand-invoked in each handler, and not
the deprecated `FluentValidation.AspNetCore` MVC integration. **Field-shape rules** (required, lengths,
non-zero change) and the **async SKU-uniqueness** rule live in the validators
(`CreatePartRequestValidator`, `UpdatePartRequestValidator`). The **negative-quantity** business rule
lives in the service (`PartService.AddTransactionAsync`, lines 100-102) — *not* in a validator —
because it depends on the part's current on-hand quantity and must be evaluated **atomically with the
write** to be race-safe; a stateless field validator cannot do that without a check-then-act race.
Both surfaces render one consistent `ValidationProblemDetails`. Putting it "all in one place" would
force the business rule out of the transaction boundary and reintroduce the race.

## 5. DI lifetime for `IPartService`

**Scoped** (`Program.cs:18`). It depends on `AppDbContext`, which EF registers Scoped and which is
**not thread-safe** and tracks a per-request change set. If I registered it Singleton it would capture
one `DbContext` for the app's lifetime — cross-request state bleed and threading bugs under concurrent
requests. Transient would create a new service per injection but still needs to match the DbContext's
scope, so it buys nothing and can be wasteful. Scoped ties the service's lifetime to the request,
which is exactly the unit of work here.

## 6. Container base image and multi-stage

**Multi-stage** with `sdk:10.0` to build and `aspnet:10.0-noble-chiseled` to run (`Dockerfile`).
Restore runs before the full source copy so the dependency layer caches. Chiseled gives a small
attack surface (no shell/package manager) and already runs as the non-root `app` user on port 8080.
I deliberately left out an in-image `HEALTHCHECK` (chiseled has no `curl`/shell — probe `/health`
from the host/orchestrator instead) and left out Kubernetes/CI per the brief. For production I'd add
image scanning in CI, a real database (Postgres/SQL Server) instead of a file SQLite, secrets from a
vault rather than env defaults, and structured/observable logging.

---

**AI disclosure:** These choices were made with AI assistance (Claude). The SQLite-concurrency-token
approach (Q2/Q5-adjacent) and the required-navigation query-filter fix were things AI surfaced that I
reviewed and agreed with; the security-advisory pins in Q6 came from `dotnet build` NU1903 warnings I
followed up on. See `Part4/AI-LOG.md`.
