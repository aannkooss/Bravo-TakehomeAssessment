# Code Review — `LegacyPartsController`

Reviewing this as a PR from a teammate. It compiles conceptually but has serious correctness,
security, and performance problems. Findings are ordered by severity.

## 🔴 Blocking

### 1. SQL injection in `Search` (line 7-8)
`"...Name LIKE '%" + name + "%'"` concatenates untrusted input straight into SQL. A `name` of
`'; DROP TABLE Parts;--` is catastrophic. **Why it matters:** classic injection — data theft or
destruction. Use a parameter, or better, don't use raw SQL at all: `Where(p => EF.Functions.Like(...))`
or `Where(p => p.Name.Contains(name))` produces a safe parameterised query.

### 2. Shared `static` `DbContext` (line 3)
`private static AppDbContext _context = new AppDbContext();` is a single instance for the whole app.
**Why it matters:** `DbContext` is **not thread-safe**; concurrent requests will throw
(`A second operation was started on this context...`) or corrupt the change tracker. It also never
gets disposed and its change tracker grows unbounded (a memory leak). **Fix:** inject `AppDbContext`
via the constructor (DI, Scoped lifetime) so each request gets its own.

### 3. `new AppDbContext()` bypasses DI/configuration (line 3)
Newing up the context ignores the configured provider, connection string, and options registered in
DI. **Why it matters:** it won't use the app's real database configuration and can't be tested or
pooled. **Fix:** constructor injection.

### 4. Blocking on async with `.Result` in `Import` (line 27)
`_context.SaveChangesAsync().Result` blocks the request thread and can **deadlock** under load.
**Why it matters:** the brief explicitly bans `.Result`/`.Wait()`. **Fix:** make the action `async
Task<IActionResult>` and `await _context.SaveChangesAsync()`.

### 5. Path traversal / unvalidated file access in `Import` (line 25-26)
`new StreamReader(filePath)` opens an arbitrary caller-supplied path with no validation. **Why it
matters:** a client can read any file the process can (`/etc/passwd`, appsettings with secrets).
**Fix:** never accept a raw server path from the client; restrict to an allow-listed directory,
validate/normalise the path, or accept an uploaded file (`IFormFile`) instead.

## 🟠 Correctness / performance

### 6. Client-side evaluation in `GetLowStock` (line 16-17)
`_context.Parts.ToList()` pulls **every** part into memory, then filters with LINQ-to-Objects. **Why
it matters:** on a large table this loads the whole table over the wire and defeats indexes. **Fix:**
filter in the database — `Where(p => p.Quantity < 10 && p.IsActive)` **before** `ToListAsync()`.

### 7. N+1 query loading transactions (line 18-22)
The `foreach` issues a separate `Transactions` query per part. **Why it matters:** 100 low-stock
parts → 101 round-trips. **Fix:** use `Include(p => p.Transactions)` (or a projection) so it's one
query, and only if the transactions are actually needed.

### 8. Synchronous data access throughout (lines 8, 16, 20)
`ToList()` everywhere blocks threads that could be serving other requests. **Why it matters:**
throughput/scalability; the brief requires all data access to be async. **Fix:** `ToListAsync()`,
`AnyAsync()`, etc.

### 9. `SaveChangesAsync` in `Import` saves nothing meaningful (line 27)
Nothing was added/modified before `SaveChanges`, and the file content is read but never persisted.
**Why it matters:** the endpoint claims "Imported successfully" while importing nothing — a lie to the
caller. **Fix:** actually parse `content` into entities and add them, or remove the misleading save.

### 10. Returns EF entities directly (lines 9, 23)
`Ok(parts)` / `Ok(lowStock)` serialise EF entities, including the `Transactions` navigation. **Why it
matters:** over-posting/leaking shape, possible cycles, and coupling the API contract to the schema.
**Fix:** project to DTOs.

### 11. `StreamReader` never disposed (line 25)
No `using`, so the file handle leaks. **Fix:** `using var reader = new StreamReader(...)` or
`File.ReadAllTextAsync`.

### 12. No error handling / wrong status codes
Missing file → unhandled exception → 500 with a stack trace; no `[ProducesResponseType]`, no
validation of `name`/`filePath`. **Fix:** validate inputs, return `400`/`404` appropriately, and rely
on a global exception handler with a consistent contract.

## Corrected version

```csharp
[ApiController]
public class PartsController : ControllerBase
{
    private readonly AppDbContext _context;

    // DI: each request gets its own Scoped, thread-safe-per-request DbContext.
    public PartsController(AppDbContext context) => _context = context;

    [HttpGet("parts/search")]
    [ProducesResponseType(typeof(IReadOnlyList<PartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("name is required.");

        // Parameterised + filtered in the database; no raw SQL, no injection.
        var parts = await _context.Parts
            .AsNoTracking()
            .Where(p => p.IsActive && EF.Functions.Like(p.Name, $"%{name}%"))
            .Select(p => new PartDto(p.Id, p.Name, p.Sku, p.Quantity))
            .ToListAsync(ct);

        return Ok(parts);
    }

    [HttpGet("parts/low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<PartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock(CancellationToken ct)
    {
        // Filter in the DB; single query; project to a DTO (no entity leakage, no N+1).
        var lowStock = await _context.Parts
            .AsNoTracking()
            .Where(p => p.IsActive && p.Quantity < 10)
            .Select(p => new PartDto(p.Id, p.Name, p.Sku, p.Quantity))
            .ToListAsync(ct);

        return Ok(lowStock);
    }

    [HttpPost("parts/import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        // Accept an uploaded file instead of a server path -> no path traversal.
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(ct);

        // ... parse `content` into Part entities and _context.Parts.AddRange(...) ...
        var saved = await _context.SaveChangesAsync(ct);

        return Ok(new { rowsAffected = saved, bytes = content.Length });
    }

    public record PartDto(int Id, string Name, string Sku, int Quantity);
}
```
