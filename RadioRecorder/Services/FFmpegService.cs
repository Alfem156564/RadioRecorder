using System.Diagnostics;
using RadioRecorder.Configuration;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class FFmpegService
{
    private readonly RecordingConfiguration _configuration;

    public FFmpegService(RecordingConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<Process> StartRecordingAsync(
        RadioStation station,
        string outputFile)
    {
        var outputDirectory = Path.GetDirectoryName(outputFile);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (!string.Equals(
                _configuration.AudioFormat,
                "mp3",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "RadioRecorder actualmente solamente permite grabaciones MP3.");
        }

        if (!string.Equals(
                Path.GetExtension(outputFile),
                ".mp3",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El archivo de grabación debe tener extensión .mp3.");
        }

        var inputFormat = string.Empty;

        if (!string.IsNullOrWhiteSpace(station.StreamFormat) &&
            !string.Equals(
                station.StreamFormat,
                "auto",
                StringComparison.OrdinalIgnoreCase))
        {
            inputFormat = $"-f {station.StreamFormat} ";
        }

        var arguments =
            $"-hide_banner " +
            $"{inputFormat}" +
            $"-i \"{station.StreamUrl}\" " +
            $"-vn " +
            $"-c:a libmp3lame " +
            $"-b:a {_configuration.AudioBitrateKbps}k " +
            $"-ar {_configuration.AudioSampleRate} " +
            $"-f mp3 " +
            $"\"{outputFile}\"";

        Console.WriteLine($"🎙️ Iniciando grabación: {station.Name}");
        Console.WriteLine($"FFmpeg: {arguments}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process
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

        await Task.CompletedTask;

        return process;
    }

    public async Task StopRecordingAsync(Process process)
    {
        if (process.HasExited)
        {
            process.Dispose();
            return;
        }

        try
        {
            await process.StandardInput.WriteLineAsync("q");
            await process.StandardInput.FlushAsync();

            await process.WaitForExitAsync();
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
}