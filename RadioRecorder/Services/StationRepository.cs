using Microsoft.EntityFrameworkCore;
using RadioRecorder.Data;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class StationRepository
{
    private readonly RadioRecorderDbContext _context;

    public StationRepository(
        RadioRecorderDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RadioStation>>
        GetAllAsync(
            CancellationToken cancellationToken = default)
    {
        return await _context.RadioStations
            .AsNoTracking()
            .OrderBy(station => station.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<RadioStation?>
        GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
    {
        return await _context.RadioStations
            .FirstOrDefaultAsync(
                station => station.Id == id,
                cancellationToken);
    }

    public async Task<RadioStation?>
        GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
    {
        return await _context.RadioStations
            .FirstOrDefaultAsync(
                station => station.Name == name,
                cancellationToken);
    }

    public async Task AddAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        await _context.RadioStations.AddAsync(
            station,
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task UpdateAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        _context.RadioStations.Update(station);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var station =
            await GetByIdAsync(
                id,
                cancellationToken);

        if (station is null)
        {
            return;
        }

        _context.RadioStations.Remove(station);

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}