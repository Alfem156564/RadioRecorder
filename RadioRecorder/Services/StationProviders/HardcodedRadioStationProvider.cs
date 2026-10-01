using RadioRecorder.Configuration;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services.StationProviders;

public class HardcodedRadioStationProvider : IRadioStationProvider
{
    public Task<IReadOnlyList<RadioStation>> GetStationsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RadioStation> stations =
            RadioStationConfiguration.GetStations();

        return Task.FromResult(stations);
    }
}