# Part 3b — Research Task

> ⚠️ **WRITE THIS IN YOUR OWN WORDS (~250 words).** Pick ONE topic your API actually touches. The
> scaffold below is a checklist of what must be present — fill each section yourself, open the docs,
> and record the date you accessed them. The graders **will** open your links.

**Suggested topic (your API uses all four; this is the strongest fit):**
**Optimistic concurrency for a read-modify-write update in EF Core** — because your stock path
(`PartService.AddTransactionAsync`) implements exactly this with a `RowVersion` token + retry.

_(Alternative topics you could pick instead: global exception handling/ProblemDetails; validation in
Minimal APIs on .NET 10; containerizing with chiseled/non-root images. All are in your repo.)_

Your ~250 words **must** include:

**(a) Exact documentation URL(s) + access date.**
- e.g. Microsoft Learn "Handling concurrency conflicts" (EF Core) — paste the exact URL you used.
- Accessed: _(YYYY-MM-DD)_.

**(b) Specific API / method / attribute names.**
- e.g. `IsConcurrencyToken()`, `[ConcurrencyCheck]`, `[Timestamp]`/`IsRowVersion()`,
  `DbUpdateConcurrencyException`, `entry.OriginalValues`/`GetDatabaseValues()`.

**(c) What changed between .NET versions.**
- e.g. how the guidance/APIs evolved, and — important for your write-up — **why SQL Server's
  `rowversion`/`[Timestamp]` pattern doesn't work on SQLite**, which is why your code uses an
  app-managed `Guid` token instead. _(Your words.)_

**(d) One thing that surprised you / where AI's first answer was wrong, and how you found out.**
- You have a real one: the first instinct was a `byte[]` rowversion; SQLite has no native
  auto-updating rowversion, so it silently never triggers. You found out by checking the docs /
  reasoning about the generated SQL. (See `Part4/AI-LOG.md` Entry 1.) _(Write this in your own words.)_

Prefer primary sources (Microsoft Learn, the library's own docs).
