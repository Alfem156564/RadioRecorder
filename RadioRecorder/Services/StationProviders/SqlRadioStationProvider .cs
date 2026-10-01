using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services.StationProviders;

public class SqlRadioStationProvider : IRadioStationProvider
{
    private readonly StationRepository _repository;

    public SqlRadioStationProvider(
        StationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<RadioStation>> GetStationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(
            cancellationToken);
    }
}