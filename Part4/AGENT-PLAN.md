# Agent Plan — building Part 1 with an AI agent

If I handed Part 1 to an AI **agent** rather than a chat window, I'd break it into focused sub-agents
with a review gate between each, roughly mirroring the checkpoints I actually used.

## Sub-agents / steps
1. **Scaffolder** — create the solution, API project, test project with a real `ProjectReference`,
   add EF Core + SQLite. Exit: solution builds.
2. **Data modeller** — entities, `AppDbContext`, unique index on `Sku`, soft-delete query filters,
   initial migration. Exit: migration applies; generated SQL shows the unique index.
3. **Service/DTO builder** — `IPartService` (Scoped), DTOs, async DB-side queries.
4. **Endpoint + contract builder** — Minimal API endpoints with `TypedResults`, the validation
   `IEndpointFilter`, and the `ProblemDetails` exception handler.
5. **Concurrency specialist** — the `RowVersion` + retry remedy on the stock path.
6. **Test author** — the four required test kinds.
7. **Container/security pass** — Dockerfile, `.dockerignore`, health checks, dependency-advisory check.
8. **Reviewer** — a separate pass that reads the diff for the rules in `SKILL.md` (async-only, DTOs,
   one error shape, no leaked entities).

## What I insist on reviewing myself
- The **concurrency** implementation — I read the generated `UPDATE` and prove it with a real
  concurrent test, never taking "it works" on faith.
- The **error contract** — that framework and custom errors are genuinely one shape.
- **Migrations** — I read them before applying; I don't let an agent auto-migrate a real database.
- Anything the agent wrote that I couldn't explain line by line.

## What I never paste into an AI tool
- Secrets, connection strings with real credentials, API keys, certificates.
- Customer/production data or PII.
- Proprietary code from other systems.
(For this repo the connection string is a local file path and safe; a production one would not be.)

## Exit criteria (beyond "it builds")
- `dotnet test` is green, **including the concurrency test** (100 concurrent `-1` from qty 100 ends at
  exactly 0, never below zero).
- `curl http://localhost:8080/health` returns **200** from the running container, and
  `curl http://localhost:8080/health/ready` returns 200 (DB reachable).
- A `POST` create returns `201` with a `Location` header, and a duplicate-SKU `POST` returns `400`
  as `application/problem+json`.
- No NU1903 (known-vulnerability) warnings in the build.
