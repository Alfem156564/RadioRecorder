using System.Text;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class StationMonitorService
{
    private readonly HttpClient _httpClient;

    public StationMonitorService()
    {
        _httpClient = new HttpClient();

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "RadioRecorder/1.0");

        _httpClient.Timeout =
            TimeSpan.FromSeconds(15);
    }

    public async Task<StationStreamInfo?> GetCurrentStreamInfoAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    station.StreamUrl);

            // Pedimos al servidor que incluya metadata ICY.
            request.Headers.TryAddWithoutValidation(
                "Icy-MetaData",
                "1");

            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            var info = new StationStreamInfo();

            // ==========================================
            // HEADERS ICY
            // ==========================================

            if (response.Headers.TryGetValues(
                    "icy-name",
                    out var icyName))
            {
                info.IcyName =
                    icyName.FirstOrDefault()
                    ?? string.Empty;
            }

            if (response.Headers.TryGetValues(
                    "icy-description",
                    out var description))
            {
                info.Description =
                    description.FirstOrDefault()
                    ?? string.Empty;
            }

            if (response.Headers.TryGetValues(
                    "icy-genre",
                    out var genre))
            {
                info.Genre =
                    genre.FirstOrDefault()
                    ?? string.Empty;
            }

            if (response.Headers.TryGetValues(
                    "icy-br",
                    out var bitrate))
            {
                info.Bitrate =
                    bitrate.FirstOrDefault()
                    ?? string.Empty;
            }

            // ==========================================
            // ICY METADATA INTERVAL
            // ==========================================

            var icyMetaInt =
                GetHeaderValue(
                    response,
                    "icy-metaint");

            if (!int.TryParse(
                    icyMetaInt,
                    out var metaInt) ||
                metaInt <= 0)
            {
                return info;
            }

            // ==========================================
            // LEER STREAM
            // ==========================================

            await using var stream =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            // Para obtener la primera metadata
            // necesitamos saltar los primeros bytes
            // correspondientes al audio.
            var audioBuffer =
                new byte[metaInt];

            var bytesRead =
                await ReadExactlyAsync(
                    stream,
                    audioBuffer,
                    cancellationToken);

            if (bytesRead < metaInt)
            {
                return info;
            }

            // ==========================================
            // TAMAÑO DE METADATA
            // ==========================================

            var lengthByte =
                stream.ReadByte();

            if (lengthByte < 0)
            {
                return info;
            }

            var metadataLength =
                lengthByte * 16;

            if (metadataLength == 0)
            {
                return info;
            }

            // ==========================================
            // LEER METADATA
            // ==========================================

            var metadataBuffer =
                new byte[metadataLength];

            bytesRead =
                await ReadExactlyAsync(
                    stream,
                    metadataBuffer,
                    cancellationToken);

            if (bytesRead < metadataLength)
            {
                return info;
            }

            var metadata =
                Encoding.UTF8.GetString(
                    metadataBuffer);

            info.StreamTitle =
                ExtractStreamTitle(
                    metadata);

            return info;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"⚠️ Error leyendo stream " +
                $"{station.Name}: {ex.Message}");

            return null;
        }
    }

    private static string GetHeaderValue(
        HttpResponseMessage response,
        string headerName)
    {
        if (response.Headers.TryGetValues(
                headerName,
                out var values))
        {
            return values.FirstOrDefault()
                ?? string.Empty;
        }

        if (response.Content.Headers.TryGetValues(
                headerName,
                out values))
        {
            return values.FirstOrDefault()
                ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ExtractStreamTitle(
        string metadata)
    {
        const string prefix =
            "StreamTitle='";

        var start =
            metadata.IndexOf(
                prefix,
                StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            return string.Empty;
        }

        start += prefix.Length;

        var end =
            metadata.IndexOf(
                "';",
                start,
                StringComparison.OrdinalIgnoreCase);

        if (end < 0)
        {
            return string.Empty;
        }

        return metadata[
            start..end
        ].Trim();
    }

    private static async Task<int> ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var bytesRead =
                await stream.ReadAsync(
                    buffer.AsMemory(
                        totalRead,
                        buffer.Length - totalRead),
                    cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }
}