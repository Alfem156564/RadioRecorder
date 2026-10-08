using Microsoft.EntityFrameworkCore;
using RadioRecorder.Data;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class SqlRadioStationProvider : IRadioStationProvider
{
    private readonly IDbContextFactory<RadioDbContext> _contextFactory;

    public SqlRadioStationProvider(
        IDbContextFactory<RadioDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<RadioStation>> GetStationsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities = await context.RadioStations
            .Include(x => x.Ciudad)
            .Where(x => x.Activa)
            .ToListAsync(cancellationToken);

        return entities
            .Select(RadioStationMapper.ToModel)
            .ToList();
    }
}