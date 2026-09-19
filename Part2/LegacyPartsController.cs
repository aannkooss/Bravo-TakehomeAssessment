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
