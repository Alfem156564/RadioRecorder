using RadioRecorder.Models;

public interface IStreamResolver
{
    bool CanResolve(RadioStation station);

    Task<string?> ResolveStreamUrlAsync(
        RadioStation station,
        CancellationToken cancellationToken = default);
}