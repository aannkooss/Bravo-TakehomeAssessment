# Take-Home Assignment: Junior .NET Developer

## 📋 Overview

Welcome! This assignment evaluates your practical .NET skills **and** how you work with AI tools. We *expect and encourage* you to use AI (Copilot, ChatGPT, Claude, Cursor, etc.) — but a core part of your grade is how well you **direct, review, validate, and own** what AI produces.

* **Time budget:** 4–6 hours. Do not exceed ~8. We prefer a smaller, well-understood submission over a large, unverified one.
* **If you run short on time**, ship fewer endpoints done correctly rather than all seven done shakily; and say so in your README. We score that honestly.
* **Stack:** Modern .NET (8 or later; .NET 10 is fine), ASP.NET Core, EF Core.
* **Database:** Use **SQLite** (file mode, or in-memory *connection* mode). **Do not use the EF Core In-Memory provider** (`Microsoft.EntityFrameworkCore.InMemory`). Microsoft's own guidance discourages it for testing because it is not a relational database: it silently ignores unique indexes, check constraints and referential integrity, and produces no SQL you can inspect. Those are exactly the behaviours this assignment grades.
* **Deliverable:** A Git repository (GitHub link or zip) containing all five parts below.

### 🚩 How we grade, and what "your own words" means

* **Parts 1 and 2** — use AI freely. Building and reviewing code with AI assistance is the job.
* **Parts 3 and 5** — these must be **your own words about your own repository**. Not because AI writing is dishonest, but because these answers are the only evidence we have that you understand what you shipped. An answer that is fluent, correct in general, and wrong about *your* code tells us more than a clumsy answer that is right about it.

**Submitting work you cannot explain is the fastest way to fail this assignment.** Submitting AI-assisted work you *can* explain, and saying so, is exactly what we want.

### Time budget

| Part | What | Target |
| --- | --- | --- |
| 1a | Parts Inventory API | ~2.5 hrs |
| 1b | Tests | ~30 min |
| 1c | Containerize it | ~30–45 min |
| 1d | `DECISIONS.md` | ~20 min |
| 2 | AI code review exercise | ~45 min |
| 3 | Written answers + research task | ~35 min |
| 4 | AI log, skill file, agent plan | ~45 min |
| 5 | Code defense (written) | ~30 min |
| | **Total** | **~6 hrs** |

---

## Part 1: Build a Small API — "Parts Inventory Service" (~2.5–3 hrs)

Build a small ASP.NET Core Web API for managing maintenance parts inventory.

### Data model

* `Part`: `Id`, `Name` (required, max 100 chars), `Sku` (required, unique, **bounded length** — do not leave it unbounded), `Quantity` (≥ 0), `Location`, `IsActive`, `CreatedAtUtc`
* `StockTransaction`: `Id`, `PartId` (FK), `QuantityChange` (positive or negative), `Reason`, `TimestampUtc`

### Required endpoints

| Method | Route | Behavior |
| --- | --- | --- |
| `GET` | `/api/parts` | List **active** parts. Must support pagination (`?page=1&pageSize=20`). |
| `GET` | `/api/parts/{id}` | Single part. `404` if missing. |
| `POST` | `/api/parts` | Create. Validate input; reject duplicate SKUs. Return `201` with a `Location` header. |
| `PUT` | `/api/parts/{id}` | Update. `400` on invalid input, `404` if missing. |
| `DELETE` | `/api/parts/{id}` | **Soft delete** (set `IsActive = false`). Return `204`. |
| `POST` | `/api/parts/{id}/transactions` | Add a stock transaction and update the part's `Quantity`. Reject if it would make quantity negative. |
| `GET` | `/api/parts/{id}/transactions` | Transaction history for a part. |

Soft delete must be enforced **everywhere**, not just on one endpoint: a soft-deleted part must not accept new stock transactions and must not appear in the list. Whatever you decide for its transaction history, make it deliberate and consistent, and say so.

### Choosing your API surface

Microsoft currently recommends **Minimal APIs** for new projects. **Controllers** remain fully supported and are the right answer in some contexts. **FastEndpoints** is acceptable if you are already fluent in it. Pick whichever you can defend — **we grade the justification, not the choice** (see `DECISIONS.md` below).

Whatever you pick, your **OpenAPI document must advertise your real response types and status codes** — via `TypedResults` / `Produces` / `ProducesValidationProblem` for Minimal APIs, or `[ProducesResponseType]` for controllers. An OpenAPI document that advertises none of the outcomes your API actually returns loses points.

### Technical requirements (each one is graded)

1. **All data access must be async** (`ToListAsync`, `SaveChangesAsync`, etc.). No `.Result` or `.Wait()` anywhere.
2. **Return DTOs, never EF entities**, from your endpoints.
3. **Use dependency injection**: define an `IPartService` (or repository) interface, implement it, and register it with an appropriate lifetime. Explain *why* you chose that lifetime in `DECISIONS.md`.
4. **Correct HTTP status codes** per the table above, plus `400`/`422` for validation failures.
5. **Global exception handling with a consistent error contract.** Implement it, and **justify your mechanism** in `DECISIONS.md` — custom middleware, `IExceptionHandler` (`AddExceptionHandler` + `UseExceptionHandler`, available since .NET 8), or `UseExceptionHandler` with `AddProblemDetails()`. Internal exception details must not leak to the client; log the real exception server-side. A correlation/trace ID in the response that maps to the log line is a plus. **Your API must not answer in two different error shapes** — framework-generated errors and your own errors should look the same to a client.
6. **Queries must filter/paginate in the database**, not in memory. Use `AsNoTracking()` where appropriate.
7. **Validation must run in the pipeline, not be hand-invoked inside every action.** Note that `FluentValidation.AspNetCore`'s automatic MVC integration is **deprecated by its maintainer and removed in v12** — do not use it. Acceptable modern approaches: an `IEndpointFilter` resolving `IValidator<T>` (Minimal APIs), `SharpGrip.FluentValidation.AutoValidation`, a MediatR validation pipeline behaviour, or — for simple field-shape rules — DataAnnotations / .NET 10's built-in `AddValidation()`. At minimum, enforce the **async SKU-uniqueness rule** and the **negative-quantity rule**. Validation failures must return one consistent `ValidationProblemDetails` (`application/problem+json`). State in `DECISIONS.md` where each rule lives and why.
8. **Concurrency — stated as an outcome, not a technique.** Concurrent stock changes on the same part must not **oversell** (quantity must never go below zero) and must not **lose updates** (100 concurrent `-1` requests that all return success against a starting quantity of 100 must leave the quantity at 0, not at 97). Choose and implement a remedy. If you run out of time, **document the race, its mechanism and your intended fix in the README** rather than leaving a silent bug.

### 1b. Tests

Tests must live in a **separate test project** with a real `<ProjectReference>` to the API project. That project must **build on its own** (`dotnet build` inside the test folder must succeed), and test code and test packages must **not** ship inside the deployable app. SQLite only.

Minimum required tests:

1. **HTTP-layer test** using `WebApplicationFactory<Program>` asserting real status codes and headers: `POST` → `201` + `Location`; `GET` that location → `200`; `DELETE` → `204`; missing id → `404`; invalid body → `400`/`422` shaped as `ValidationProblemDetails`.
2. **Concurrency test**: fire N concurrent stock decrements, then assert final quantity == initial − (successful requests) **and** that quantity never went below zero. Each concurrent operation must use its own `DbContext` / DI scope — a `DbContext` is not thread-safe, and sharing one will either throw or serialise the work and prove nothing.
3. **Pagination edge cases**: `page=0`, negative `page`, `pageSize=0`, `pageSize=1000000`, and a very large `page`. Each must be well defined — clamped or rejected with `400` — and must never produce a `500`, a divide-by-zero, or a negative `Skip`.
4. **Business rules**: successful create, duplicate SKU rejection, and a transaction that would go negative.

### 1c. Containerize it

Your API must build and run as a container image.

**We do not care which runtime you use** — Docker, Podman, containerd, nerdctl are all fine. Use a `Containerfile`/`Dockerfile`, **or** the .NET SDK's built-in container publishing (`dotnet publish /t:PublishContainer`). Either is acceptable.

* **The bar (required):** the image builds, the container runs, and your README gives the exact build and run commands.
* **Above the bar (differentiating):** multi-stage build with restore before full source copy for layer caching; runs as **non-root** and you verified it; an appropriately slim base image (`-noble-chiseled` / `-jammy-chiseled` / Alpine) with a note on the trade-off; a `.dockerignore` that keeps `bin/`, `obj/`, `.git/` and secrets out of the build context; configuration **externalized** via environment variables (remember the `__` nesting separator, e.g. `ConnectionStrings__Default`) and **never baked into the image**; a health endpoint (`AddHealthChecks()` + `MapHealthChecks("/health")`, ideally with `AddDbContextCheck()` on a readiness route) that you actually probed.

> [!NOTE]
> Since .NET 8 the official ASP.NET Core images already run as a non-root `app` user, and the default container port changed from **80 to 8080** (`ASPNETCORE_HTTP_PORTS`). If your README says port 80, we will assume you did not run it.

A SQLite *file* inside a container is ephemeral — it disappears when the container is removed unless you mount a volume. Noticing and writing about that is exactly the kind of awareness we are looking for.

**Out of scope — do not spend time on:** Kubernetes manifests, CI/CD pipelines, cloud deployment. A minimal `compose` file is welcome but not required.

> [!WARNING]
> **Do not use .NET Aspire for this assignment.** Aspire's `ServiceDefaults` would hand you health checks, OpenTelemetry and resilience without your having written them, which defeats the point. If you know Aspire, tell us about it in `DECISIONS.md` or `RESEARCH.md` instead.

### 1d. `DECISIONS.md`

Create `Part1/DECISIONS.md` and answer these six in 2–4 sentences each. **We grade the reasoning, not the choice you made.** A confidently wrong reason scores below an honest trade-off.

1. Why Minimal APIs, controllers, or FastEndpoints — and what would change your mind?
2. Why this database provider for your tests, and what does it hide or reveal about how your code would behave on SQL Server?
3. Why this exception-handling mechanism, and what does it give you over the alternatives?
4. Why this validation approach, and where does each rule live (field-shape vs business rule)? Why not put it all in one place?
5. Why this DI lifetime for `IPartService`, and what breaks if you pick the wrong one?
6. Why this container base image, why multi-stage (or why not), what did you deliberately leave out, and what would change for production?

**If your AI tools made or suggested any of these choices, say so — and tell us whether you agreed and why.**

---

## Part 2: AI Code Review Exercise (~45–60 min)

The file below was "generated by AI" for a previous developer. Copy it into your repo as `Part2/LegacyPartsController.cs` (it does not need to compile in your project).

**Your task:** Create `Part2/CODE-REVIEW.md` and write a code review as if this were a pull request from a teammate. Identify **every problem you can find**, explain *why* each is a problem, and show the corrected code at the end. We planted **at least 8 distinct issues**.

Check your rendered markdown before you submit. Corrected code belongs in a fenced code block.

```csharp
public class LegacyPartsController : ControllerBase
{
    private static AppDbContext _context = new AppDbContext();

    [HttpGet("parts/search")]
    public IActionResult Search(string name)
    {
        var sql = "SELECT * FROM Parts WHERE Name LIKE '%" + name + "%'";
        var parts = _context.Parts.FromSqlRaw(sql).ToList();
        return Ok(parts);
    }

    [HttpGet("parts/low-stock")]
    public IActionResult GetLowStock()
    {
        var parts = _context.Parts.ToList();
        var lowStock = parts.Where(p => p.Quantity < 10 && p.IsActive).ToList();
        foreach (var p in lowStock)
        {
            p.Transactions = _context.Transactions
                .Where(t => t.PartId == p.Id).ToList();
        }
        return Ok(lowStock);
    }

    [HttpPost("parts/import")]
    public IActionResult Import(string filePath)
    {
        var reader = new StreamReader(filePath);
        var content = reader.ReadToEnd();
        var result = _context.SaveChangesAsync().Result;
        return Ok("Imported successfully: " + content.Length + " bytes");
    }
}
```

---

## Part 3: Written Answers & Research (~35 min)

### 3a. Short written answers

Create `Part3/ANSWERS.md`. Answer in your own words, 3–6 sentences each. **Do not paste AI-generated answers.**

1. Explain **value types vs. reference types**, and predict the output of:

    ```csharp
    var a = new Part { Name = "Bolt" };
    var b = a;
    b.Name = "Washer";
    Console.WriteLine(a.Name);

    public class Part
    {
        public string Name { get; set; }
    }
    ```

2. Explain the difference between `IEnumerable<T>` and `IQueryable<T>` and why it matters for EF Core performance. **Then point at one place in your own Part 1 code where the distinction mattered**, with the file and line.
3. Walk through what happens to an HTTP request from the moment it reaches your Part 1 API until the response leaves, mentioning the middleware pipeline. Describe **your** pipeline, in the order *you* registered things — not a generic one. Name the file and the lines.

### 3b. Research task

Create `Part3/RESEARCH.md`. Pick **one** topic your API actually touches and write ~250 words on the **current** recommended approach:

* Global exception handling / `ProblemDetails` in ASP.NET Core
* Validation in Minimal APIs as of .NET 10
* Optimistic concurrency for a read-modify-write update in EF Core
* Containerizing a .NET app (SDK container publishing vs Dockerfile, chiseled/non-root images)

Your write-up **must** include:

* **(a)** The exact documentation URL(s) you relied on, and the date you accessed them.
* **(b)** The specific API / method / attribute names involved.
* **(c)** **What changed between .NET versions** — e.g. "before .NET 8 the pattern was X; now it's Y."
* **(d)** **One thing that surprised you, or one place where your AI's first answer was wrong, outdated or incomplete — and how you found out.**

Prefer primary sources (Microsoft Learn, the library's own docs) over blog posts. We will open the links you cite.

---

## Part 4: AI Usage Log & Skill File (~45 min)

This part carries significant weight. Create a `Part4/` folder containing:

### 4a. `AI-LOG.md` — Your AI collaboration log

Document **3–5 real interactions** you had with AI during this assignment. For each one:

* **Goal:** What you were trying to accomplish.
* **Prompt & context:** The actual prompt you used, and what context you gave it (framework version, code files, error messages, constraints), and why you chose that context.
* **What came back:** Summarize the output.
* **Validation:** How you verified it was correct (compiled? tested? checked generated SQL? checked docs? checked the NuGet package exists and is maintained?).
* **What you changed:** What you kept, edited, or threw away.
* **Commit SHA(s):** Where that change actually landed in your history.

Two entries are required by kind:

* At least one where **the AI was wrong or suboptimal** and you caught it. *If AI was never wrong during this whole assignment, we will assume you didn't check.*
* At least one where **you were wrong and the AI was right**, or where it taught you something you didn't know.

Bundling an important change into an unrelated commit weakens your evidence — if the best story in your log has no commit we can look at, it is worth less. Commit deliberately.

### 4b. `SKILL.md` — A reusable AI skill / instruction file

Write a reusable custom-instruction file (like a `.cursorrules`, Copilot instructions, or Claude skill) that you would give an AI assistant working in **this repo**, encoding your standards; for example, async-only EF Core, DTOs from endpoints, no `.Result`/`.Wait()`, validation in the pipeline, the concurrency rule, status code conventions, test expectations.

**Every type, file and convention it names must actually exist in your submitted repo — we will spot-check.** A skill file that names types you never wrote will steer an agent into inventing them.

If you submit the real instruction file that actually drove your build (`CLAUDE.md`, `.cursorrules`, etc.), that is acceptable and even preferred — but say so, and confirm it still matches the code you shipped. A rule that contradicts your own final code is a finding.

Then answer in 2–3 sentences: **why is a saved skill better than re-typing these instructions every session?**

### 4c. `AGENT-PLAN.md` — Agent workflow (short)

In half a page: if you had an AI **agent** (not just a chat window) build Part 1 for you, how would you break the work into steps or **sub-agents** (example: scaffolding, tests, review pass)? What would you still insist on reviewing yourself, and what would you **never** paste into an AI tool (secrets, connection strings, proprietary or customer data)?

Include **at least one exit criterion beyond "it builds"** — for example "the concurrency test passes," or "curl against the running container returns 200 from `/health`."

---

## Part 5: Code Defense (~30 min)

This replaces the follow-up interview. Create `Part5/DEFENSE.md`.

**Every answer must name the file and the line numbers you are talking about.** Keep answers short — a few sentences each. We are not looking for polish; we are looking for whether you know your own code. Write this **last**, after the code is final, and commit it in its own commit so the line numbers still match.

1. **Walk the stock-transaction path line by line.** Take the method that adds a stock transaction and updates `Quantity`. Explain what each step does and why it is in that order. If any line came from an AI and you kept it, say which one and why you kept it.
2. **The concurrency question.** Two clerks sell the last unit at the same instant. Trace what your code does, in order, naming the lines. Does your implementation prevent overselling? If it does, name the exact mechanism. If it does not, say so plainly — a clear-eyed "here is the race I did not close, and here is how I would close it" scores better than a confident answer that is wrong.
3. **Soft delete.** List every place in your code that checks `IsActive`, with line numbers. Is there any path that can still touch a deleted part? If yes, which one?
4. **The requirement change.** We have decided to switch from soft delete to **hard delete**. List every file you would change and what you would change in it. Mention what happens to existing transaction history, to delete idempotency, and to SKU reuse.
5. **Argue against yourself.** Take question 1 of your `DECISIONS.md` and make the strongest case *for the option you rejected*. Then say what would actually have to be true for you to switch.
6. **What is weakest.** Name the part of your submission you are least confident about, and say what you would do with two more hours. Name one line or block that an AI wrote that you did not fully understand at first, and what you did about it.

Question 6 is not a trap. In previous rounds, the candidates who scored highest were the ones who volunteered a flaw in their own work before we found it.

---

## Submission checklist

* [ ] Part 1 API builds and runs; README has accurate run instructions
* [ ] `Part1/DECISIONS.md` with all six justifications
* [ ] OpenAPI advertises your real response types and status codes
* [ ] Validation runs in the pipeline; one consistent `ValidationProblemDetails` shape
* [ ] Concurrency handled on the transactions endpoint — or documented honestly in the README
* [ ] Separate test project with a real `<ProjectReference>` that builds standalone; `dotnet test` passes
* [ ] HTTP-layer test, concurrency test, pagination edge-case tests, and business-rule tests all present
* [ ] Container image builds and runs; README has the build + run commands
* [ ] `.dockerignore` present; no secrets baked into the image
* [ ] `Part2/CODE-REVIEW.md` with findings + corrected code (fenced, and check it renders)
* [ ] `Part3/ANSWERS.md` in your own words, with file/line references where asked
* [ ] `Part3/RESEARCH.md` with cited URLs and access date
* [ ] `Part4/AI-LOG.md` (with commit SHAs), `SKILL.md`, `AGENT-PLAN.md`
* [ ] `Part5/DEFENSE.md` with file and line numbers throughout, committed last
* [ ] Meaningful Git commit history (not one giant "final" commit)
