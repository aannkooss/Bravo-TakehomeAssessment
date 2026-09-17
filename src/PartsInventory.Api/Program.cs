using Microsoft.EntityFrameworkCore;
using PartsInventory.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// SQLite (file mode by default). Connection string is externalized and can be
// overridden by env var ConnectionStrings__Default (note the __ nesting separator).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=partsinventory.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

app.MapGet("/", () => "Parts Inventory API");

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap the app in tests.
public partial class Program { }
