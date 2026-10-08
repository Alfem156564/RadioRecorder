using RadioRecorder.Interfaces;
using RadioRecorder.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RadioRecorder.Services.StreamResolvers;

public class StereoLuzStreamResolver : IStreamResolver
{
    private readonly HttpClient _httpClient;

    public StereoLuzStreamResolver(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanResolve(
        RadioStation station)
    {
        return station.Name.Contains(
                   "Stereo Luz",
                   StringComparison.OrdinalIgnoreCase)
               ||
               station.StationPageUrl.Contains(
                   "stereoluz",
                   StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> ResolveStreamUrlAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        var pageUrls =
            new[]
            {
                station.StationPageUrl,
                "https://stereoluzfm.com.mx/"
            }
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var pageUrl in pageUrls)
        {
            try
            {
                Console.WriteLine();

                Console.WriteLine(
                    $"🔎 [{station.Name}] " +
                    $"Buscando stream en:");

                Console.WriteLine(
                    $"   {pageUrl}");

                var html =
                    await _httpClient.GetStringAsync(
                        pageUrl,
                        cancellationToken);

                var urlAddress =
    ExtractUrlAddress(html);

                if (!string.IsNullOrWhiteSpace(urlAddress))
                {

                    var cleanedUrl =
                        CleanUrl(urlAddress);

                    if (Uri.TryCreate(
                            cleanedUrl,
                            UriKind.Absolute,
                            out _))
                    {

                        if (IsPossibleStream(cleanedUrl) &&
                             await IsAudioStreamAsync(
                                 cleanedUrl,
                                 cancellationToken))
                        {
                            station.StreamUrl =
                                cleanedUrl;

                            Console.WriteLine();
                            Console.WriteLine(
                                $"✅ [{station.Name}] " +
                                $"Stream encontrado mediante #urladdress:");

                            Console.WriteLine(
                                $"   {cleanedUrl}");

                            return cleanedUrl;
                        }
                    }
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "   ⚠️ No se encontró #urladdress en el HTML.");
                }

                var radioId =
    ExtractRadioId(html);

                if (!string.IsNullOrWhiteSpace(radioId))
                {
                    var openFireStream =
                        await GetOpenFireStreamAsync(
                            pageUrl,
                            radioId,
                            cancellationToken);

                    if (!string.IsNullOrWhiteSpace(
                            openFireStream))
                    {
                        station.StreamUrl =
                            openFireStream;

                        Console.WriteLine();
                        Console.WriteLine(
                            $"✅ [{station.Name}] " +
                            $"Stream encontrado mediante OpenFire:");

                        Console.WriteLine(
                            $"   {openFireStream}");

                        return openFireStream;
                    }
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"   ⚠️ No se encontró radio_id_tag.");
                }

                //
                // 1. Buscar directamente en HTML
                //

                var candidates =
                    FindPossibleStreamUrls(html);

                //
                // 2. Buscar en JavaScript inline
                //

                var scriptContents =
                    ExtractInlineScripts(html);

                foreach (var script in scriptContents)
                {
                    candidates =
                        candidates
                            .Concat(
                                FindPossibleStreamUrls(
                                    script));
                }

                //
                // 3. Buscar y descargar JavaScript externo
                //

                var scriptUrls =
                    ExtractScriptUrls(
                        html,
                        pageUrl);

                foreach (var scriptUrl in scriptUrls)
                {
                    try
                    {

                        var script =
                            await _httpClient.GetStringAsync(
                                scriptUrl,
                                cancellationToken);

                        candidates =
                            candidates
                                .Concat(
                                    FindPossibleStreamUrls(
                                        script));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"      ⚠️ No se pudo leer:");

                        Console.WriteLine(
                            $"         {ex.Message}");
                    }
                }

                var uniqueCandidates =
                    candidates
                        .Select(CleanUrl)
                        .Where(IsPossibleStream)
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToList();

                foreach (var streamUrl in uniqueCandidates)
                {
                    Console.WriteLine();

                    Console.WriteLine(
                        $"   🔍 Posible stream:");

                    Console.WriteLine(
                        $"      {streamUrl}");

                    if (!await IsAudioStreamAsync(
                            streamUrl,
                            cancellationToken))
                    {
                        Console.WriteLine(
                            $"      ❌ No parece ser " +
                            $"un stream de audio.");

                        continue;
                    }

                    station.StreamUrl =
                        streamUrl;

                    Console.WriteLine();

                    Console.WriteLine(
                        $"✅ [{station.Name}] " +
                        $"Stream encontrado:");

                    Console.WriteLine(
                        $"   {streamUrl}");

                    return streamUrl;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine();

                Console.WriteLine(
                    $"⚠️ [{station.Name}] " +
                    $"Error consultando:");

                Console.WriteLine(
                    $"   {pageUrl}");

                Console.WriteLine(
                    $"   {ex.Message}");
            }
        }

        return null;
    }

    private static IEnumerable<string>
        FindPossibleStreamUrls(
            string content)
    {
        var urls =
            new List<string>();

        //
        // URL normal
        //

        var normalMatches =
            Regex.Matches(
                content,
                @"https?://[^""'\s<>\\]+",
                RegexOptions.IgnoreCase);

        foreach (Match match in normalMatches)
        {
            urls.Add(match.Value);
        }

        //
        // URL escapada:
        //
        // https:\/\/servidor\/live.mp3
        //

        var escapedMatches =
            Regex.Matches(
                content,
                @"https?:\\?/\\?/[^""'\s<>]+",
                RegexOptions.IgnoreCase);

        foreach (Match match in escapedMatches)
        {
            urls.Add(match.Value);
        }

        //
        // Buscar específicamente Listen2MyRadio
        //

        var listenMatches =
            Regex.Matches(
                content,
                @"https?:(?:\\/|//)+[^""'\s<>]+listen2myradio\.com[^""'\s<>]+",
                RegexOptions.IgnoreCase);

        foreach (Match match in listenMatches)
        {
            urls.Add(match.Value);
        }

        return urls;
    }

    private static IEnumerable<string>
        ExtractInlineScripts(
            string html)
    {
        var matches =
            Regex.Matches(
                html,
                @"<script\b[^>]*>(?<content>.*?)</script>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var content =
                match.Groups["content"].Value;

            if (!string.IsNullOrWhiteSpace(content))
            {
                yield return content;
            }
        }
    }

    private static IEnumerable<string>
        ExtractScriptUrls(
            string html,
            string pageUrl)
    {
        var matches =
            Regex.Matches(
                html,
                @"<script\b[^>]*src\s*=\s*[""'](?<src>[^""']+)[""']",
                RegexOptions.IgnoreCase);

        if (!Uri.TryCreate(
                pageUrl,
                UriKind.Absolute,
                out var baseUri))
        {
            yield break;
        }

        foreach (Match match in matches)
        {
            var src =
                match.Groups["src"].Value;

            if (string.IsNullOrWhiteSpace(src))
            {
                continue;
            }

            if (Uri.TryCreate(
                    src,
                    UriKind.Absolute,
                    out var absoluteUri))
            {
                yield return absoluteUri.ToString();
                continue;
            }

            if (Uri.TryCreate(
                    baseUri,
                    src,
                    out var relativeUri))
            {
                yield return relativeUri.ToString();
            }
        }
    }

    private static string CleanUrl(
        string url)
    {
        return WebUtility.HtmlDecode(url)
            .Replace("\\/", "/")
            .Replace("\\u0026", "&")
            .Replace("&amp;", "&")
            .Trim(
                '"',
                '\'',
                ' ',
                '\r',
                '\n',
                ',',
                ';',
                ')',
                ']');
    }

    private static bool IsPossibleStream(
        string url)
    {
        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var lower =
            url.ToLowerInvariant();

        //
        // Recursos conocidos que NO son audio.
        //

        if (lower.Contains(
                "/intro.mp3"))
        {
            return false;
        }

        if (lower.Contains(
                "radio12345.com"))
        {
            return false;
        }

        if (lower.Contains(
                "/chat/"))
        {
            return false;
        }

        if (lower.Contains(
                "company-contact"))
        {
            return false;
        }

        if (lower.Contains(
                "company-privacy"))
        {
            return false;
        }

        if (lower.Contains(
                "phpqrcode"))
        {
            return false;
        }

        //
        // Recursos multimedia.
        //

        return lower.Contains(".mp3")
               || lower.Contains(".aac")
               || lower.Contains(".m3u")
               || lower.Contains(".m3u8")
               || lower.Contains(
                    "listen2myradio.com")
               || lower.Contains(
                    "typeportmount")
               || lower.Contains(
                    "/stream")
               || lower.Contains(
                    "/listen");
    }

    private async Task<bool> IsAudioStreamAsync(
         string streamUrl,
         CancellationToken cancellationToken)
    {
        try
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    streamUrl);

            request.Headers.TryAddWithoutValidation(
                "Icy-MetaData",
                "1");

            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            var contentType =
                response.Content.Headers.ContentType?
                    .MediaType;

            Console.WriteLine(
                $"         HTTP {(int)response.StatusCode}");

            Console.WriteLine(
                $"         Content-Type: " +
                $"{contentType ?? "(vacío)"}");

            if (!response.IsSuccessStatusCode)
                return false;

            //
            // Primero rechazamos respuestas claramente
            // no relacionadas con audio.
            //

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                if (contentType.Contains(
                        "text/html",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (contentType.Contains(
                        "application/javascript",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (contentType.Contains(
                        "text/javascript",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (contentType.Contains(
                        "image/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            //
            // Ahora comprobamos los bytes reales.
            //

            using var stream =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            var buffer =
                new byte[8192];

            var totalRead = 0;

            while (totalRead < buffer.Length)
            {
                var read =
                    await stream.ReadAsync(
                        buffer.AsMemory(
                            totalRead,
                            buffer.Length - totalRead),
                        cancellationToken);

                if (read == 0)
                    break;

                totalRead += read;
            }

            if (totalRead == 0)
            {
                Console.WriteLine(
                    "         ❌ El servidor no envió datos.");

                return false;
            }

            //
            // Mostrar los primeros bytes para poder
            // identificar qué está devolviendo realmente
            // el servidor.
            //

            var previewLength =
                Math.Min(totalRead, 32);

            var preview =
                BitConverter.ToString(
                    buffer,
                    0,
                    previewLength);


            //
            // MP3 con etiqueta ID3.
            //

            if (totalRead >= 3 &&
                buffer[0] == 0x49 &&
                buffer[1] == 0x44 &&
                buffer[2] == 0x33)
            {
                return true;
            }

            //
            // MP3 normalmente comienza con un frame
            // cuya sincronización empieza con FF Ex.
            //

            for (var i = 0; i < totalRead - 1; i++)
            {
                if (buffer[i] == 0xFF &&
                    (buffer[i + 1] & 0xE0) == 0xE0)
                {
                    Console.WriteLine(
                        "         🎵 Detectado frame MP3.");

                    return true;
                }
            }

            //
            // AAC ADTS.
            //

            for (var i = 0; i < totalRead - 1; i++)
            {
                if (buffer[i] == 0xFF &&
                    (buffer[i + 1] & 0xF6) == 0xF0)
                {
                    Console.WriteLine(
                        "         🎵 Detectado frame AAC.");

                    return true;
                }
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"         ❌ Error comprobando audio: " +
                $"{ex.Message}");

            return false;
        }
    }

    private static string? ExtractRadioId(string html)
    {
        var match =
            Regex.Match(
                html,
                @"id\s*=\s*[""']radio_id_tag[""'][^>]*>\s*(?<id>\d+)\s*<",
                RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Groups["id"].Value;
        }

        return null;
    }

    private async Task<string?> GetOpenFireStreamAsync(
    string pageUrl,
    string radioId,
    CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(
                pageUrl,
                UriKind.Absolute,
                out var baseUri))
        {
            return null;
        }

        var endpoint =
            new Uri(
                baseUri,
                $"openfire.ajax.php?radio_id={radioId}");

        try
        {
            using var response =
                await _httpClient.GetAsync(
                    endpoint,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"      ❌ HTTP {(int)response.StatusCode}");

                return null;
            }

            var content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            var openFireData =
    ParseOpenFireResponse(content);

            if (openFireData is not null)
            {
                if (!string.IsNullOrWhiteSpace(
                        openFireData.RadioServerMainIp))
                {
                    var stream =
                        await ResolveListen2MyRadioStreamAsync(
                            openFireData.RadioServerMainIp,
                            radioId,
                            cancellationToken);

                    if (!string.IsNullOrWhiteSpace(stream))
                    {
                        return stream;
                    }
                }
            }

            var firewallUrl =
                ExtractFirewallUrl(
                    content,
                    baseUri);

            if (string.IsNullOrWhiteSpace(
                    firewallUrl))
            {
                Console.WriteLine(
                    "   ⚠️ OpenFire no devolvió " +
                    "un endpoint firewall.");

                return null;
            }

            return await ResolveFirewallEndpointAsync(
    firewallUrl,
    radioId,
    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"      ⚠️ Error consultando OpenFire:");

            Console.WriteLine(
                $"         {ex.Message}");

            return null;
        }
    }

    private static string? ExtractFirewallUrl(
    string content,
    Uri baseUri)
    {
        var cleaned =
            CleanUrl(content);

        if (cleaned.Contains(
                "aplication_xmp_firewall_open_cp.php",
                StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(
                    cleaned,
                    UriKind.Absolute,
                    out var absoluteUri))
            {
                return absoluteUri.ToString();
            }

            if (Uri.TryCreate(
                    baseUri,
                    cleaned,
                    out var relativeUri))
            {
                return relativeUri.ToString();
            }
        }

        return null;
    }
    
    private async Task<string?> ResolveFirewallEndpointAsync(
    string firewallUrl,
    string radioId,
    CancellationToken cancellationToken)
    {
        try
        {

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    firewallUrl);

            request.Headers.Referrer =
                new Uri(
                    "https://stereoluzfm.radiostream321.com/");

            request.Headers.TryAddWithoutValidation(
                "Icy-MetaData",
                "1");

            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            //
            // Si el servidor redirige,
            // HttpClient normalmente seguirá
            // la redirección automáticamente.
            //

            var finalUrl =
                response.RequestMessage?
                    .RequestUri?
                    .ToString();

            if (!string.IsNullOrWhiteSpace(
                    finalUrl))
            {
                Console.WriteLine();
                Console.WriteLine(
                    "   🌐 URL final:");

                Console.WriteLine(
                    $"      {finalUrl}");
            }

            var contentType =
                response.Content.Headers
                    .ContentType?
                    .MediaType;

            //
            // Si ya llegamos directamente
            // a audio, tenemos nuestro stream.
            //

            if (!string.IsNullOrWhiteSpace(
                    contentType) &&
                IsAudioContentType(contentType))
            {
                Console.WriteLine();
                Console.WriteLine(
                    "   🎵 El endpoint devolvió AUDIO.");

                return finalUrl;
            }

            //
            // Si devuelve texto, lo analizamos.
            //

            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            //
            // El firewall puede devolver JSON con
            // el servidor real de Listen2MyRadio.
            //

            var firewallData =
                ParseOpenFireResponse(body);

            if (firewallData is not null)
            {

                if (!string.IsNullOrWhiteSpace(
                        firewallData.RadioServerMainIp))
                {
                    var stream =
                        await ResolveListen2MyRadioStreamAsync(
                            firewallData.RadioServerMainIp,
                            radioId,
                            cancellationToken);

                    if (!string.IsNullOrWhiteSpace(stream))
                    {
                        return stream;
                    }
                }
            }

            //
            // También intentamos buscar URLs directamente
            // dentro de la respuesta.
            //

            var candidates =
                FindPossibleStreamUrls(body)
                    .Select(CleanUrl)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            foreach (var candidate in candidates)
            {

                if (await IsAudioStreamAsync(
                        candidate,
                        cancellationToken))
                {
                    return candidate;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(
                "   ⚠️ Error consultando firewall:");

            Console.WriteLine(
                $"      {ex.Message}");

            return null;
        }
    }

    private static bool IsAudioContentType(
    string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        return
            contentType.Contains(
                "audio",
                StringComparison.OrdinalIgnoreCase)
            ||
            contentType.Contains(
                "mpeg",
                StringComparison.OrdinalIgnoreCase)
            ||
            contentType.Contains(
                "aac",
                StringComparison.OrdinalIgnoreCase)
            ||
            contentType.Contains(
                "ogg",
                StringComparison.OrdinalIgnoreCase)
            ||
            contentType.Contains(
                "x-mpegurl",
                StringComparison.OrdinalIgnoreCase);
    }

    private static OpenFireResponse?
    ParseOpenFireResponse(
        string content)
    {
        try
        {
            return JsonSerializer.Deserialize<OpenFireResponse>(
                content,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> ResolveListen2MyRadioStreamAsync(
     string server,
     string radioId,
     CancellationToken cancellationToken)
    {
        var candidates =
    new[]
    {
        $"https://{server}/live.mp3",
        $"http://{server}/live.mp3",

        $"https://{server}/live.aac",
        $"http://{server}/live.aac",

        $"https://{server}/stream",
        $"http://{server}/stream",

        $"https://{server}/radio",
        $"http://{server}/radio",

        $"https://{server}/radio.mp3",
        $"http://{server}/radio.mp3",

        $"https://{server}/live.mp3?radio_id={radioId}",
        $"http://{server}/live.mp3?radio_id={radioId}"
    };

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var request =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        candidate);

                request.Headers.TryAddWithoutValidation(
                    "Icy-MetaData",
                    "1");

                using var response =
                    await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                var contentType =
                    response.Content.Headers.ContentType?.MediaType;

                if (!response.IsSuccessStatusCode)
                    continue;

                if (IsAudioContentType(contentType))
                {

                    if (await IsAudioStreamAsync(
                            candidate,
                            cancellationToken))
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            "      🎵 ¡Stream de audio válido!");

                        Console.WriteLine(
                            $"         {candidate}");

                        return candidate;
                    }
                }
                else
                {
                    Console.WriteLine(
                        "         ❌ No parece ser audio.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"         ❌ Error: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "   ⚠️ No se encontró un endpoint de audio.");

        return null;
    }

    private sealed class OpenFireResponse
    {
        public string? Error { get; set; }

        [JsonPropertyName("radio_server_main_ip")]
        public string? RadioServerMainIp { get; set; }
    }

    private static string? ExtractUrlAddress(
    string html)
    {
        //
        // Busca algo como:
        //
        // <div id="urladdress">
        //     https://servidor/stream...
        // </div>
        //

        var match =
            Regex.Match(
                html,
                @"id\s*=\s*[""']urladdress[""'][^>]*>\s*(?<url>.*?)\s*<",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (!match.Success)
            return null;

        var value =
            match.Groups["url"].Value.Trim();

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return WebUtility.HtmlDecode(value);
    }
}