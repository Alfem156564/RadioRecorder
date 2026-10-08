using RadioRecorder.Configuration;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;
using System.Diagnostics;
using System.Threading;

namespace RadioRecorder.Services;

public class FFmpegService
{
    private readonly RecordingConfiguration _configuration;
    private readonly IStreamResolver _streamResolver;

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

        var streamUrl =
            await GetInitialStreamUrlAsync(
                station,
                cancellationToken);

        //
        // 2. Primer intento.
        //

        var process =
            StartFfmpegProcess(
                station,
                streamUrl,
                outputFile);

        //
        // 3. Esperamos unos segundos para comprobar
        // si FFmpeg pudo abrir realmente el stream.
        //

        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(3),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await StopRecordingAsync(process);
            throw;
        }

        //
        // FFmpeg sigue vivo.
        // Todo correcto.
        //

        if (!process.HasExited)
        {
            return process;
        }

        //
        // FFmpeg murió inmediatamente.
        // Es posible que la URL haya cambiado.
        //

        Console.WriteLine();

        Console.WriteLine(
            $"⚠️ [{station.Name}] " +
            $"FFmpeg no pudo utilizar el stream actual.");

        Console.WriteLine(
            $"🔄 [{station.Name}] " +
            $"Se buscará nuevamente en la página.");

        process.Dispose();

        //
        // 4. Obtener una URL nueva.
        //

        var newStreamUrl =
            await RefreshStreamUrlAsync(
                station,
                cancellationToken);

        //
        // 5. Segundo intento con la URL nueva.
        //

        process =
            StartFfmpegProcess(
                station,
                newStreamUrl,
                outputFile);

        await Task.Delay(
            TimeSpan.FromSeconds(3),
            cancellationToken);

        if (process.HasExited)
        {
            var exitCode =
                process.ExitCode;

            process.Dispose();

            throw new InvalidOperationException(
                $"FFmpeg tampoco pudo iniciar con " +
                $"la nueva URL. ExitCode: {exitCode}");
        }

        return process;
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
                Console.WriteLine(
                    $"⚠️ FFmpeg [{process.Id}] no respondió a tiempo. " +
                    "Se forzará su cierre.");
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
            while (true)
            {
                var line =
                    await process.StandardError.ReadLineAsync();

                if (line is null)
                    break;

                Console.WriteLine(
                    $"FFmpeg [{process.Id}]: {line}");
            }

            await process.WaitForExitAsync();

            Console.WriteLine(
                $"FFmpeg [{process.Id}] terminó. " +
                $"ExitCode: {process.ExitCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"⚠️ Error leyendo salida de FFmpeg [{process.Id}]: " +
                $"{ex.Message}");
        }
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

        var streamUrl =
            await _streamResolver.ResolveStreamUrlAsync(
                station,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            throw new InvalidOperationException(
                $"No fue posible encontrar un nuevo " +
                $"stream para {station.Name}.");
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
    string outputFile)
    {
        var inputFormat =
            string.Empty;

        if (!string.IsNullOrWhiteSpace(
                station.StreamFormat) &&
            !string.Equals(
                station.StreamFormat,
                "auto",
                StringComparison.OrdinalIgnoreCase))
        {
            inputFormat =
                $"-f {station.StreamFormat} ";
        }

        var arguments =
            $"-hide_banner " +
            $"{inputFormat}" +
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

        _ = ReadOutputAsync(process);

        return process;
    }
}