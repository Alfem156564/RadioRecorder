using System.Diagnostics;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class RadioRecorderService
{
    private readonly FFmpegService _ffmpegService;

    private readonly Dictionary<Guid, List<ActiveRecording>>
        _activeRecordings = [];

    public RadioRecorderService(
        FFmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
    }

    public bool IsRecording(
        RadioStation station)
    {
        if (!_activeRecordings.TryGetValue(
                station.Id,
                out var recordings))
        {
            return false;
        }

        recordings.RemoveAll(
            recording =>
                recording.Process.HasExited);

        return recordings.Count > 0;
    }

    public IReadOnlyList<Recording> GetCurrentRecordings(
        RadioStation station)
    {
        if (!_activeRecordings.TryGetValue(
                station.Id,
                out var recordings))
        {
            return [];
        }

        return recordings
            .Where(recording =>
                !recording.Process.HasExited)
            .Select(recording =>
                recording.Recording)
            .ToList();
    }

    public async Task<Recording> StartAsync(
    RadioStation station,
    RadioProgram? program = null,
    DateTime? fileNameStart = null,
    DateTime? fileNameEnd = null,
    CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;

        // Este tiempo ya representa la hora lógica para el nombre del archivo,
        // incluyendo RecordingFileTimeOffsetHours cuando corresponde.
        var logicalStart =
            fileNameStart ?? startTime;

        // Estructura: Recordings/Ciudad/mmm-dd/
        var cityFolder =
            SanitizeFolderName(
                string.IsNullOrWhiteSpace(station.City)
                    ? "Sin ciudad"
                    : station.City.Trim());

        var monthAbbreviation =
            GetSpanishMonthAbbreviation(logicalStart.Month);

        var dayFolder =
            $"{monthAbbreviation}-{logicalStart:dd}";

        var recordingsFolder =
            Path.Combine(
                AppContext.BaseDirectory,
                "Recordings",
                cityFolder,
                dayFolder);

        Directory.CreateDirectory(
            recordingsFolder);

        // Si todavía no se detectó el programa, se usa "Nada".
        var programName =
            program is null
                ? "NADA"
                : SanitizeFileName(
                    program.Name);

        var stationAbbreviation =
            GetStationAbbreviation(station);

        // Formato: SIGLAS-Nada_DD-MM-AA_HHMMSS.mp3
        var fileName =
            $"{stationAbbreviation}-{programName}_" +
            $"{logicalStart:dd-MM-yy_HHmmss}.mp3".ToUpper();

        var filePath =
            Path.Combine(
                recordingsFolder,
                fileName);

        var recording =
            new Recording
            {
                Station = station,
                Program = program,
                FilePath = filePath,
                StartTime = startTime
            };

        var process = await _ffmpegService.StartRecordingAsync(
            station,
            filePath,
            cancellationToken);

        var activeRecording =
            new ActiveRecording(
                recording,
                process);

        if (!_activeRecordings.TryGetValue(
                station.Id,
                out var recordings))
        {
            recordings = [];

            _activeRecordings[
                station.Id] = recordings;
        }

        recordings.Add(
            activeRecording);

        return recording;
    }

    public async Task StopAsync(
        RadioStation station,
        Recording recording)
    {
        if (!_activeRecordings.TryGetValue(
                station.Id,
                out var recordings))
        {
            return;
        }

        var activeRecording =
            recordings.FirstOrDefault(
                item =>
                    item.Recording.Id ==
                    recording.Id);

        if (activeRecording is null)
        {
            return;
        }

        await _ffmpegService.StopRecordingAsync(
            activeRecording.Process);

        activeRecording.Recording.EndTime =
            DateTime.Now;

        recordings.Remove(
            activeRecording);

        if (recordings.Count == 0)
        {
            _activeRecordings.Remove(
                station.Id);
        }
    }

    public async Task StopAllAsync(
        RadioStation station)
    {
        if (!_activeRecordings.TryGetValue(
                station.Id,
                out var recordings))
        {
            return;
        }

        foreach (var activeRecording in
                 recordings.ToList())
        {
            if (!activeRecording.Process.HasExited)
            {
                await _ffmpegService
                    .StopRecordingAsync(
                        activeRecording.Process);
            }

            activeRecording.Recording.EndTime =
                DateTime.Now;
        }

        recordings.Clear();

        _activeRecordings.Remove(
            station.Id);
    }

    public Process? GetProcess(
        Recording recording)
    {
        foreach (var stationRecordings
                 in _activeRecordings.Values)
        {
            var active =
                stationRecordings.FirstOrDefault(
                    item =>
                        item.Recording.Id ==
                        recording.Id);

            if (active is not null)
            {
                return active.Process;
            }
        }

        return null;
    }

    private static readonly string[] SpanishMonthAbbreviations =
    [
        "ene", "feb", "mar", "abr", "may", "jun",
        "jul", "ago", "sep", "oct", "nov", "dic"
    ];

    private static string GetSpanishMonthAbbreviation(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        return SpanishMonthAbbreviations[month - 1];
    }

    private static string GetStationAbbreviation(RadioStation station)
    {
        if (!string.IsNullOrWhiteSpace(station.Abbreviation))
        {
            return SanitizeFileName(
                    station.Abbreviation.Trim())
                .ToUpperInvariant();
        }

        // Compatibilidad con estaciones SQL que todavía no tienen siglas
        // almacenadas en la base de datos: se usa la primera palabra útil.
        var words = station.Name
            .Split(
                [' ', '-', '_'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        var ignoredWords = new HashSet<string>(
            ["LA", "EL", "LOS", "LAS", "DE", "DEL", "RADIO"],
            StringComparer.OrdinalIgnoreCase);

        var usefulWord = words.FirstOrDefault(
            word => !ignoredWords.Contains(word));

        var abbreviation = string.IsNullOrWhiteSpace(usefulWord)
            ? "RADIO"
            : usefulWord;

        return SanitizeFileName(abbreviation)
            .ToUpperInvariant();
    }

    private static string SanitizeFileName(
        string value)
    {
        foreach (var invalidChar
            in Path.GetInvalidFileNameChars())
        {
            value =
                value.Replace(
                    invalidChar,
                    '_');
        }

        return value;
    }

    private static string SanitizeFolderName(
        string value)
    {
        foreach (var invalidChar
            in Path.GetInvalidFileNameChars())
        {
            value =
                value.Replace(
                    invalidChar,
                    '_');
        }

        return value;
    }

    private class ActiveRecording
    {
        public Recording Recording { get; }

        public Process Process { get; }

        public ActiveRecording(
            Recording recording,
            Process process)
        {
            Recording = recording;
            Process = process;
        }
    }
}