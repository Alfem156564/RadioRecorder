using Microsoft.EntityFrameworkCore;
using RadioRecorder.Data;

namespace RadioRecorder.Services;

public class DatabaseService
{
    private readonly RadioRecorderDbContext _context;

    public DatabaseService(
        RadioRecorderDbContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();

        Console.WriteLine(
            "🗄️ Base de datos inicializada.");
    }
}