using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PartsInventory.Tests;

/// <summary>
/// Boots the real API via WebApplicationFactory&lt;Program&gt; against a unique temporary
/// SQLite <b>file</b> (not the EF In-Memory provider — the assignment forbids it, and a file
/// lets multiple request scopes open their own connections so the concurrency test is real).
/// The app's startup Migrate() builds the schema; the file is deleted on dispose.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"parts-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { /* best-effort cleanup */ }
        }
    }
}
