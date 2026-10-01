using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services.StreamResolvers;

public class DirectStreamResolver : IStreamResolver
{
    public bool CanResolve(RadioStation station)
    {
        return string.IsNullOrWhiteSpace(
            station.StationPageUrl);
    }

    public Task<string?> ResolveStreamUrlAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            string.IsNullOrWhiteSpace(station.StreamUrl)
                ? null
                : station.StreamUrl);
    }
}