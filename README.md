# Parts Inventory Service

A small ASP.NET Core (.NET 10) Web API for managing maintenance parts inventory, built for the
Bravo take-home assignment. Minimal APIs + EF Core + SQLite.

The assignment brief lives in [`docs/README.md`](docs/README.md). Per-part write-ups:
`Part1/DECISIONS.md`, `Part2/CODE-REVIEW.md`, `Part3/ANSWERS.md` + `RESEARCH.md`,
`Part4/AI-LOG.md` + `SKILL.md` + `AGENT-PLAN.md`, `Part5/DEFENSE.md`.

## Tech stack

- .NET 10, ASP.NET Core **Minimal APIs** with `TypedResults`
- EF Core 10 + **SQLite** (file mode; the EF In-Memory provider is deliberately NOT used)
- FluentValidation (validation runs in the pipeline via an `IEndpointFilter`)
- `IExceptionHandler` + `AddProblemDetails()` for one consistent error contract
- Optimistic concurrency via a `RowVersion` token + retry loop

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- A container runtime (Docker/Podman) — optional, only for the container path

## Run locally

```bash
dotnet run --project src/PartsInventory.Api
```

The app applies EF Core migrations at startup and creates `partsinventory.db` in the working
directory. Override the connection string via environment variable (note the `__` separator):

```bash
ConnectionStrings__Default="Data Source=/tmp/parts.db" dotnet run --project src/PartsInventory.Api
```

Useful endpoints once running (default dev port is chosen by launch settings; see console output):

- OpenAPI document: `GET /openapi/v1.json`
- Liveness: `GET /health` · Readiness (checks the DB): `GET /health/ready`

## Run the tests

```bash
dotnet test
```

12 tests: HTTP-layer (`WebApplicationFactory<Program>`), concurrency, pagination edge cases,
and business rules. SQLite only. The test project builds standalone (`dotnet build` inside
`tests/PartsInventory.Tests` succeeds).

## Run in a container

The image is a multi-stage build on a **chiseled, non-root** base and listens on **port 8080**.

```bash
# Build
docker build -t partsinventory:local .

# Run (mount a named volume so the SQLite file survives container removal)
docker run -d --name partsinv -p 8080:8080 -v partsdata:/data partsinventory:local

# Probe it
curl -i http://localhost:8080/health          # liveness  -> 200 Healthy
curl -i http://localhost:8080/health/ready     # readiness -> 200 Healthy (DB reachable)
curl -i -X POST http://localhost:8080/api/parts \
  -H 'Content-Type: application/json' \
  -d '{"name":"Bolt","sku":"BOLT-001","quantity":10,"location":"A1"}'
```

Configuration is externalized via environment variables and never baked into the image. The
default connection string is `Data Source=/data/partsinventory.db`; override with
`-e ConnectionStrings__Default="..."`.

> **SQLite persistence:** the database file lives at `/data` inside the container. That path is
> **ephemeral** — it disappears when the container is removed unless you mount a volume (the
> `-v partsdata:/data` above). Without the volume, each `docker rm` loses your data.

> **Chiseled trade-off:** the runtime image has no shell or package manager (smaller attack
> surface, can't be exec'd into for debugging), so a `curl`-based Docker `HEALTHCHECK` won't work
> from inside — probe `/health` from the host or an orchestrator instead.

## API surface

| Method | Route | Behavior |
| --- | --- | --- |
| `GET` | `/api/parts?page=1&pageSize=20` | List active parts (paged; params clamped) |
| `GET` | `/api/parts/{id}` | Single part; 404 if missing/soft-deleted |
| `POST` | `/api/parts` | Create; 201 + `Location`; duplicate SKU → 400 |
| `PUT` | `/api/parts/{id}` | Update Name/Sku/Location; 400 invalid, 404 missing |
| `DELETE` | `/api/parts/{id}` | Soft delete (`IsActive=false`); 204 |
| `POST` | `/api/parts/{id}/transactions` | Add stock movement; rejects negative result (400) |
| `GET` | `/api/parts/{id}/transactions` | Transaction history |

Soft delete is enforced everywhere via EF global query filters: a soft-deleted part is invisible
to list, get, update, add-transaction, and history (all 404 / excluded).

## Concurrency

Concurrent stock changes cannot oversell or lose updates. `AddTransactionAsync` re-reads the part,
re-checks the negative rule, and saves under a `RowVersion` optimistic-concurrency token; a losing
writer gets a `DbUpdateConcurrencyException` and retries against fresh state. Verified by
`ConcurrencyTests`: 100 concurrent `-1` against a starting quantity of 100 ends at exactly 0.

## Project structure

```
src/PartsInventory.Api/     # the API (Domain, Data, Dtos, Services, Validation, Endpoints, Infrastructure)
tests/PartsInventory.Tests/ # xUnit tests (SQLite only), real ProjectReference to the API
Dockerfile, .dockerignore   # multi-stage chiseled non-root container
docs/                       # the original assignment brief
```
