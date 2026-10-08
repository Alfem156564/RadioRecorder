using System.Net;
using System.Text.RegularExpressions;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services.StreamResolvers;

public class Listen2MyRadioStreamResolver : IStreamResolver
{
    private readonly HttpClient _httpClient;

    public Listen2MyRadioStreamResolver(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanResolve(RadioStation station)
    {
        return station.StationPageUrl
            .Contains(
                "radiostream321.com",
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> ResolveStreamUrlAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                station.StationPageUrl))
        {
            return station.StreamUrl;
        }

        var html =
            await _httpClient.GetStringAsync(
                station.StationPageUrl,
                cancellationToken);

        var streamUrl =
            FindStreamUrl(html);

        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            return null;
        }

        streamUrl =
            WebUtility.HtmlDecode(streamUrl);

        streamUrl =
            streamUrl.Replace(
                "\\/",
                "/");

        streamUrl =
            streamUrl.Replace(
                "\\u0026",
                "&");

        var available =
            await IsStreamAvailableAsync(
                streamUrl,
                cancellationToken);

        if (!available)
        {
            Console.WriteLine(
                $"⚠️ [{station.Name}] " +
                $"El stream encontrado no está disponible.");

            Console.WriteLine(
                $"   {streamUrl}");

            return null;
        }

        station.StreamUrl =
            streamUrl;

        Console.WriteLine(
            $"🔎 [{station.Name}] " +
            $"Stream encontrado:");

        Console.WriteLine(
            $"   {streamUrl}");

        return streamUrl;
    }

    private static string? FindStreamUrl(
    string html)
    {
        // 1. Buscar el formato clásico de Listen2MyRadio.
        const string listen2MyRadioPattern =
            @"https?://[^""'\s<>\\]+listen2myradio\.com/live\.mp3\?typeportmount=[^""'\s<>\\]+";

        var listen2MyRadioMatch =
            Regex.Match(
                html,
                listen2MyRadioPattern,
                RegexOptions.IgnoreCase);

        if (listen2MyRadioMatch.Success)
        {
            return listen2MyRadioMatch.Value;
        }

        // 2. Buscar URLs de stream AAC conocidas.
        var aacMatches =
            Regex.Matches(
                html,
                @"https?://[^""'\s<>\\]+(?:\.aac|\.aac\?[^""'\s<>\\]*)",
                RegexOptions.IgnoreCase);

        foreach (Match match in aacMatches)
        {
            if (IsInvalidListen2MyRadioResource(match.Value))
            {
                continue;
            }

            return match.Value;
        }

        // 3. Buscar posibles streams MP3.
        var mp3Matches =
            Regex.Matches(
                html,
                @"https?://[^""'\s<>\\]+\.mp3(?:\?[^""'\s<>\\]*)?",
                RegexOptions.IgnoreCase);

        foreach (Match match in mp3Matches)
        {
            if (IsInvalidListen2MyRadioResource(match.Value))
            {
                continue;
            }

            return match.Value;
        }

        return null;
    }

    private static bool IsInvalidListen2MyRadioResource(
    string url)
    {
        var lowerUrl =
            url.ToLowerInvariant();

        // Recursos utilizados por el reproductor,
        // pero que no representan el stream de radio.
        if (lowerUrl.Contains("/intro.mp3"))
        {
            return true;
        }

        if (lowerUrl.Contains("radio12345.com"))
        {
            return true;
        }

        return false;
    }

    private async Task<bool> IsStreamAvailableAsync(
    string streamUrl,
    CancellationToken cancellationToken)
    {
        try
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    streamUrl);

            request.Headers.Range =
                new System.Net.Http.Headers.RangeHeaderValue(
                    0,
                    1023);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var contentType =
                response.Content.Headers.ContentType?
                    .MediaType;

            if (string.IsNullOrWhiteSpace(contentType))
            {
                return true;
            }

            if (contentType.Contains(
                    "text/html",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}