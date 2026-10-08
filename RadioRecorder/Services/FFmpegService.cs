using RadioRecorder.Configuration;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;

namespace RadioRecorder.Services;

public class FFmpegService
{
    private readonly RecordingConfiguration _configuration;
    private readonly IStreamResolver _streamResolver;

    private readonly ConcurrentDictionary<int, long>
    _ffmpegProgress = new();

    private readonly ConcurrentDictionary<int, string>
        _processStations = new();

    // Los diagnósticos se reenvían a la interfaz WPF.
    public event Action<string, string>? DiagnosticReported;

    private void ReportDiagnostic(string stationName, string message)
    {
        Console.WriteLine($"⚠️ [{stationName}] {message}");

        try
        {
            DiagnosticReported?.Invoke(stationName, message);
        }
        catch (Exception ex)
        {
            // Un error en la notificación no debe afectar la grabación.
            Console.WriteLine(
                $"No se pudo notificar el diagnóstico a la interfaz: {ex.Message}");
        }
    }

    public FFmpegService(
     RecordingConfiguration configuration,
     IStreamResolver streamResolver)
    {
        _configuration = configuration;
        _streamResolver = streamResolver;
    }

    public async Task<Process> StartRecordingAsync(
    RadioStation station,
    string outputFile,
    CancellationToken cancellationToken = default)
    {
        var outputDirectory =
            Path.GetDirectoryName(outputFile);

        if (!string.IsNullOrWhiteSpace(
                outputDirectory))
        {
            Directory.CreateDirectory(
                outputDirectory);
        }

        if (!string.Equals(
                _configuration.AudioFormat,
                "mp3",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "RadioRecorder actualmente solamente " +
                "permite grabaciones MP3.");
        }

        if (!string.Equals(
                Path.GetExtension(outputFile),
                ".mp3",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El archivo de grabación debe tener " +
                "extensión .mp3.");
        }

        //
        // 1. Utilizamos la URL que ya conocemos.
        // Si no existe, la buscamos.
        //

        // 1. Obtener la URL inicial.
        var streamUrl = await GetInitialStreamUrlAsync(
            station,
            cancellationToken);

        // 2. Probar la URL actual y después una URL renovada.
        for (var urlAttempt = 0; urlAttempt < 2; urlAttempt++)
        {
            // Primero autodetección; después AAC.
            var formats = new List<string>();

            if (!string.IsNullOrWhiteSpace(station.StreamFormat))
            {
                formats.Add(station.StreamFormat.Trim());
            }

            formats.Add("auto");
            formats.Add("aac");

            var formatsToTry = formats
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var format in formatsToTry)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine();
                Console.WriteLine(
                    $"🔎 [{station.Name}] Probando formato: {format}");

                var process = await TryStartWithFormatAsync(
                    station,
                    streamUrl,
                    outputFile,
                    format,
                    cancellationToken);

                if (process is not null)
                {
                    Console.WriteLine(
                        $"✅ [{station.Name}] Grabación iniciada " +
                        $"correctamente con formato de entrada: {format}");

                    if (!string.Equals(
                            format,
                            station.StreamFormat,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ReportDiagnostic(
                            station.Name,
                            $"La grabación se recuperó usando '{format}'. " +
                            $"El formato configurado era '{station.StreamFormat}'.");
                    }

                    return process;
                }

                ReportDiagnostic(
                    station.Name,
                    $"El formato de entrada '{format}' no produjo progreso de audio. " +
                    "Se probará el siguiente formato disponible.");
            }

            // Si fallaron ambos formatos, renovar la URL.
            if (urlAttempt == 0)
            {
                streamUrl = await RefreshStreamUrlAsync(
                    station,
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"No fue posible iniciar una grabación válida para " +
            $"{station.Name}. Fallaron los intentos con auto y AAC.");
    }


    public async Task StopRecordingAsync(Process process)
    {
        try
        {
            if (process.HasExited)
                return;

            // Primero intentamos cerrar FFmpeg normalmente.
            try
            {
                await process.StandardInput.WriteLineAsync("q");
                await process.StandardInput.FlushAsync();

                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(10));

                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                var message =
                    "FFmpeg no respondió al cierre normal en 10 segundos; " +
                    "se forzará su cierre.";

                if (_processStations.TryGetValue(
                        process.Id,
                        out var stationName))
                {
                    ReportDiagnostic(stationName, message);
                }
                else
                {
                    Console.WriteLine(
                        $"⚠️ FFmpeg [{process.Id}] {message}");
                }
            }
            catch (InvalidOperationException)
            {
                // El proceso pudo terminar entre las comprobaciones.
            }
            catch (System.IO.IOException)
            {
                // La entrada estándar pudo cerrarse antes de enviar "q".
            }

            // Si continúa vivo, forzamos su cierre.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);

                using var killTimeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));

                await process.WaitForExitAsync(killTimeout.Token);
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task ReadOutputAsync(Process process)
    {
        try
        {
            string? stationName;
            while (true)
            {
                var line =
                    await process.StandardError.ReadLineAsync();

                if (line is null)
                    break;

                Console.WriteLine(
                    $"FFmpeg [{process.Id}]: {line}");

                if (IsSignificantFfmpegError(line) &&
                    _processStations.TryGetValue(
                        process.Id,
                        out stationName))
                {
                    ReportDiagnostic(
                        stationName,
                        $"FFmpeg: {NormalizeFfmpegError(line)}");
                }
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 &&
                _processStations.TryGetValue(
                    process.Id,
                    out stationName))
            {
                ReportDiagnostic(
                    stationName,
                    $"FFmpeg terminó inesperadamente con código {process.ExitCode}.");
            }

            Console.WriteLine(
                $"FFmpeg [{process.Id}] terminó. " +
                $"ExitCode: {process.ExitCode}");
        }
        catch (ObjectDisposedException)
        {
            // Puede ocurrir durante un cierre normal: StopRecordingAsync
            // dispone el proceso después de solicitar que FFmpeg termine.
        }
        catch (InvalidOperationException)
        {
            // El proceso puede cambiar de estado mientras se está cerrando.
        }
        catch (Exception ex)
        {
            if (_processStations.TryGetValue(
                    process.Id,
                    out var stationName))
            {
                ReportDiagnostic(
                    stationName,
                    $"No se pudo leer la salida de FFmpeg: {ex.Message}");
            }
            else
            {
                Console.WriteLine(
                    $"⚠️ Error leyendo salida de FFmpeg [{process.Id}]: " +
                    $"{ex.Message}");
            }
        }
        finally
        {
            _processStations.TryRemove(process.Id, out _);
        }
    }

    private static bool IsSignificantFfmpegError(string line)
    {
        var value = line.ToLowerInvariant();

        return value.Contains("error") ||
               value.Contains("failed") ||
               value.Contains("invalid data") ||
               value.Contains("connection refused") ||
               value.Contains("connection reset") ||
               value.Contains("timed out") ||
               value.Contains("http error") ||
               value.Contains("server returned") ||
               value.Contains("404 not found") ||
               value.Contains("403 forbidden") ||
               value.Contains("input/output error");
    }

    private static string NormalizeFfmpegError(string line)
    {
        // Evita filas excesivamente largas en la tabla.
        const int maxLength = 220;
        var normalized = line.Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

    private async Task<string> GetInitialStreamUrlAsync(
    RadioStation station,
    CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(
                station.StreamUrl))
        {
            return station.StreamUrl;
        }

        var streamUrl =
            await _streamResolver.ResolveStreamUrlAsync(
                station,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            throw new InvalidOperationException(
                $"No fue posible obtener el stream " +
                $"de {station.Name}.");
        }

        return streamUrl;
    }

    private async Task<string> RefreshStreamUrlAsync(
    RadioStation station,
    CancellationToken cancellationToken)
    {
        Console.WriteLine();

        Console.WriteLine(
            $"🔄 [{station.Name}] " +
            $"Actualizando URL del stream...");

        string? streamUrl;

        try
        {
            streamUrl = await _streamResolver.ResolveStreamUrlAsync(
                station,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportDiagnostic(
                station.Name,
                $"No se pudo actualizar la URL del stream: {ex.Message}");
            throw;
        }

        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            const string message = "No fue posible encontrar una nueva URL del stream.";
            ReportDiagnostic(station.Name, message);

            throw new InvalidOperationException(
                $"{message} Estación: {station.Name}.");
        }

        station.StreamUrl =
            streamUrl;

        Console.WriteLine(
            $"✅ [{station.Name}] " +
            $"Nueva URL encontrada:");

        Console.WriteLine(
            $"   {streamUrl}");

        return streamUrl;
    }

    private Process StartFfmpegProcess(
    RadioStation station,
    string streamUrl,
    string outputFile,
    string inputFormat)
    {
        var inputFormatArgument =
            string.Equals(
                inputFormat,
                "auto",
                StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"-f {inputFormat} ";

        var arguments =
            $"-hide_banner " +
            $"-progress pipe:1 -nostats " +
            $"{inputFormatArgument}" +
            $"-i \"{streamUrl}\" " +
            $"-vn " +
            $"-c:a libmp3lame " +
            $"-b:a {_configuration.AudioBitrateKbps}k " +
            $"-ar {_configuration.AudioSampleRate} " +
            $"-ac 2 " +
            $"-f mp3 " +
            $"\"{outputFile}\"";

        Console.WriteLine(
            $"🎙️ Iniciando grabación: " +
            $"{station.Name}");

        Console.WriteLine(
            $"FFmpeg: {arguments}");

        var startInfo =
            new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,

                UseShellExecute = false,

                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,

                CreateNoWindow = true
            };

        var process =
            new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

        if (!process.Start())
        {
            process.Dispose();

            throw new InvalidOperationException(
                "No fue posible iniciar FFmpeg.");
        }

        _ffmpegProgress[process.Id] = 0;
        _processStations[process.Id] = station.Name;

        _ = ReadProgressAsync(process);
        _ = ReadOutputAsync(process);

        return process;
    }

    private async Task<Process?> TryStartWithFormatAsync(
    RadioStation station,
    string streamUrl,
    string outputFile,
    string inputFormat,
    CancellationToken cancellationToken)
    {
        Process? process = null;

        try
        {
            // Eliminar cualquier archivo parcial de un intento anterior.
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            // Iniciar FFmpeg con el formato que estamos probando.
            process = StartFfmpegProcess(
                station,
                streamUrl,
                outputFile,
                inputFormat);

            // Dar tiempo a FFmpeg para recibir y escribir audio.
            var timeout = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (process.HasExited)
                {
                    Console.WriteLine(
                        $"⚠️ [{station.Name}] FFmpeg terminó " +
                        $"con código {process.ExitCode}.");

                    break;
                }

                if (_ffmpegProgress.TryGetValue(
                        process.Id,
                        out var progress) &&
                    progress > 0)
                {
                    return process;
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(250),
                    cancellationToken);
            }

            Console.WriteLine(
                $"⚠️ [{station.Name}] No se detectó progreso de audio " +
                $"con el formato {inputFormat}.");

            await StopRecordingAsync(process);
            process = null;

            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            if (process is not null)
            {
                await StopRecordingAsync(process);
            }

            throw;
        }
        catch (Exception ex)
        {
            ReportDiagnostic(
                station.Name,
                $"Falló el intento con formato '{inputFormat}': {ex.Message}");

            if (process is not null)
            {
                await StopRecordingAsync(process);
            }

            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            return null;
        }
    }

    private async Task ReadProgressAsync(Process process)
    {
        try
        {
            while (true)
            {
                var line =
                    await process.StandardOutput.ReadLineAsync();

                if (line is null)
                {
                    break;
                }

                if (line.StartsWith(
                        "out_time_us=",
                        StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(
                        line["out_time_us=".Length..],
                        out var progress))
                {
                    _ffmpegProgress[process.Id] = progress;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"⚠️ Error leyendo progreso de FFmpeg " +
                $"[{process.Id}]: {ex.Message}");
        }
        finally
        {
            _ffmpegProgress.TryRemove(process.Id, out _);
        }
    }
}