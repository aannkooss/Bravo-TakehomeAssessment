# Part 3a — Written Answers

> ⚠️ **WRITE THIS IN YOUR OWN WORDS.** The README grades Part 3 as evidence *you* understand your
> repo — do not paste AI text. The scaffolding and file/line pointers below are here so you know
> exactly what to talk about; replace every _(your words)_ block with your own explanation. Verify
> the line numbers still match before submitting (they were captured at the final commit).

## 1. Value types vs. reference types — and predict the output

**Predicted output:** `Washer`

_(Your words, 3–6 sentences.)_ Explain: `Part` is a `class`, so it's a **reference type**; `a` and
`b` hold references to the **same** object on the heap, so `b.Name = "Washer"` mutates the one object
`a` also points at. Contrast with a value type (`struct`/`int`), where `b = a` copies the value and the
two are independent. Mention where the reference lives vs. where the object lives.

## 2. `IEnumerable<T>` vs `IQueryable<T>` and why it matters for EF Core

_(Your words.)_ Core idea: `IQueryable<T>` builds an **expression tree** the provider translates to
SQL, so `Where`/`Skip`/`Take` run **in the database**; `IEnumerable<T>` is in-memory LINQ-to-Objects,
so composing on it after materialising pulls rows into the app first.

**Where it mattered in my code:** `src/PartsInventory.Api/Services/PartService.cs:18-26`
(`GetPartsAsync`). `var query = _db.Parts.AsNoTracking().OrderBy(...)` is `IQueryable`; the `Skip`/
`Take`/`Select` at lines 22-26 compose into **one SQL query** with paging done by the database. If I
had called `.ToList()` at line 18 first, everything after would be `IEnumerable` and I'd be paging the
whole table in memory (exactly the bug in the Part 2 legacy `GetLowStock`). _(Add a sentence in your
own words on the performance consequence.)_

## 3. The life of an HTTP request through *my* pipeline

_(Your words.)_ Walk a real request (e.g. `POST /api/parts`) through **my** registration order in
`src/PartsInventory.Api/Program.cs`:

- `app.UseExceptionHandler()` — `Program.cs:42` — wraps everything so any unhandled exception becomes
  one `ProblemDetails`.
- `app.MapOpenApi()` — `Program.cs:44`.
- Endpoint routing to `MapPartsEndpoints()` — `Program.cs:47` → the route group in
  `Endpoints/PartsEndpoints.cs`.
- For `POST`/`PUT`/transactions, the **endpoint filter** `ValidationFilter<T>` runs *before* the
  handler (`Validation/ValidationFilter.cs`) — on failure it short-circuits with
  `ValidationProblemDetails`.
- The handler calls the Scoped `IPartService`, which hits the DB; the response is a DTO.
- Health endpoints are mapped at `Program.cs:50-51`.

_(Explain in your own words why `UseExceptionHandler` is registered first, and what the validation
filter does to the request before your handler sees it.)_
