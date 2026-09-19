using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PartsInventory.Api.Data;
using PartsInventory.Api.Endpoints;
using PartsInventory.Api.Infrastructure;
using PartsInventory.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// SQLite (file mode by default). Connection string is externalized and can be
// overridden by env var ConnectionStrings__Default (note the __ nesting separator).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=partsinventory.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// Scoped: shares the request-scoped, non-thread-safe AppDbContext lifetime.
builder.Services.AddScoped<IPartService, PartService>();

// FluentValidation validators (run in the pipeline via ValidationFilter<T>).
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// One consistent error contract: ProblemDetails for framework errors AND our own handler.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations at startup so the SQLite schema exists on a fresh container/volume.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();

app.MapOpenApi();

app.MapGet("/", () => "Parts Inventory API");
app.MapPartsEndpoints();

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap the app in tests.
public partial class Program { }
