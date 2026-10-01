using System.Net;
using System.Text.RegularExpressions;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services.StreamResolvers;

public class RadioGrupoStreamResolver : IStreamResolver
{
    private readonly HttpClient _httpClient;

    public RadioGrupoStreamResolver(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanResolve(RadioStation station)
    {
        return station.StationPageUrl
            .Contains(
                "radiogrupo.com",
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
            Console.WriteLine(
                $"⚠️ [{station.Name}] " +
                $"No se encontró el stream en:");

            Console.WriteLine(
                $"   {station.StationPageUrl}");

            return null;
        }

        streamUrl =
            WebUtility.HtmlDecode(streamUrl);

        streamUrl =
            streamUrl.Replace("\\/", "/");

        streamUrl =
            streamUrl.Replace("\\u0026", "&");

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
        var patterns = new[]
        {
            @"https?://[^""'\s<>\\]+radiogrupo\.com[^""'\s<>\\]+",

            @"https?://[^""'\s<>\\]+streaming[^""'\s<>\\]+",

            @"https?://[^""'\s<>\\]+stream[^""'\s<>\\]+"
        };

        foreach (var pattern in patterns)
        {
            var match =
                Regex.Match(
                    html,
                    pattern,
                    RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }
}