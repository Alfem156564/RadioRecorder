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
        const string pattern =
            @"https?://[^""'\s<>\\]+listen2myradio\.com/live\.mp3\?typeportmount=[^""'\s<>\\]+";

        var match =
            Regex.Match(
                html,
                pattern,
                RegexOptions.IgnoreCase);

        return match.Success
            ? match.Value
            : null;
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