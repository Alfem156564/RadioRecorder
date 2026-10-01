using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services.StreamResolvers;

public class CompositeStreamResolver : IStreamResolver
{
    private readonly List<IStreamResolver> _resolvers;

    public CompositeStreamResolver(
        IEnumerable<IStreamResolver> resolvers)
    {
        _resolvers = resolvers.ToList();
    }

    public bool CanResolve(RadioStation station)
    {
        return _resolvers.Any(
            resolver =>
                resolver.CanResolve(station));
    }

    public async Task<string?> ResolveStreamUrlAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        foreach (var resolver in _resolvers)
        {
            if (!resolver.CanResolve(station))
            {
                continue;
            }

            try
            {
                var streamUrl =
                    await resolver.ResolveStreamUrlAsync(
                        station,
                        cancellationToken);

                if (!string.IsNullOrWhiteSpace(streamUrl))
                {
                    return streamUrl;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"⚠️ Error usando " +
                    $"{resolver.GetType().Name}:");

                Console.WriteLine(
                    $"   {ex.Message}");
            }
        }

        return null;
    }
}