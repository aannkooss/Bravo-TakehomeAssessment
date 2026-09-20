# Part 3a — Written Answers

## 1. Value types vs. reference types — and predict the output

**Predicted output:** `Washer`

A value type like an `int`, `char`, `bool` directly hold data. For example, assigning a value to a variable like `b = a` copies the value creating two independent copies. A reference type like a class holds a reference as a pointer to an object. For example, when assigning `b = a`, the reference is now copied, so both variables point at the same object. In the code example, Part is a reference type since it is a class. When `b = a`, `b` and `a` both point at one object, `b.Name = "Washer"` changes the object and `a.Name` reads that object. Therefore, the output will print `Washer` because a and b reference the same object and `b.Name = "Washer"` mutated it. If Part were a value type instead, then b = a would copy the entire value making `a` and `b` independent - `b.Name` would never know what `a` is and the output would be `Bolt`

## 2. `IEnumerable<T>` vs `IQueryable<T>` and why it matters for EF Core

`IQueryable<T>` carries an expression tree that the EF Core translates to SQL which then executes in the database. `IEnumerable<T>` uses in-memory iteration which means it runs in the app. One example of where this occurs in my code was in `src/PartsInventory.Api/Services/PartService.cs:18-26` Line 18, `var query = _db.Parts.AsNoTracking().OrderBy(p => p.Id);` is IQueryable as well as lines 22-26, `.Skip(...).Take(...).Select(...).ToListAsync(ct)`. If line 18 were to end in `.ToList()`, everything after would be IEnumerable and we would bring it into memory. This matters for EF Core performance because if you materialize too early, you pull the entire table into memory and start filtering there which wastes the database's indexes and bandwidth.

## 3. The life of an HTTP request through *my* pipeline
 
 A `POST /api/parts` enters through `UseExceptionHandler` (`Program.cs:42`), which is registered first because middleware nests in the order it is added — so it wraps the whole pipeline and anything downstream that throws is logged server-side with a `traceId` and returned as one `ProblemDetails`. The request is then routed to the create-part endpoint via `MapPartsEndpoints()` (`Program.cs:47`). The `ValidationFilter<CreatePartRequest>` (`Validation/ValidationFilter.cs`) runs after model binding and returns a 400 `ValidationProblemDetails` if the model is invalid. If valid, the handler calls the Scoped `IPartService` (`Program.cs:18`) into `AppDbContext` into `SQLite` and returns a `PartResponse` DTO with 201 + Location. Finally, the response leaves back through this pipeline.
