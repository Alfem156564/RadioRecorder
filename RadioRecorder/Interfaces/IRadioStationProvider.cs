using RadioRecorder.Models;

namespace RadioRecorder.Interfaces;

public interface IRadioStationProvider
{
    Task<IReadOnlyList<RadioStation>> GetStationsAsync(
        CancellationToken cancellationToken = default);
}