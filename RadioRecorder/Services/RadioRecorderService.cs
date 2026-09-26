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
    DateTime? fileNameEnd = null)
    {
        var recordingsFolder =
            Path.Combine(
                AppContext.BaseDirectory,
                "Recordings",
                SanitizeFolderName(
                    station.Name));

        Directory.CreateDirectory(
            recordingsFolder);

        var startTime = DateTime.Now;

        var programName =
            program is null
                ? "Continuo"
                : SanitizeFileName(
                    program.Name);

        var logicalStart =
            fileNameStart ?? startTime;

        var logicalEnd =
            fileNameEnd;

        string fileName;

        if (logicalEnd.HasValue)
        {
            fileName =
                $"{SanitizeFileName(station.Name)}_" +
                $"{programName}_" +
                $"{logicalStart:yyyy-MM-dd_HH-mm-ss}_" +
                $"{logicalEnd.Value:HH-mm-ss}.mp3";
        }
        else
        {
            fileName =
                $"{SanitizeFileName(station.Name)}_" +
                $"{programName}_" +
                $"{logicalStart:yyyy-MM-dd_HH-mm-ss}.mp3";
        }

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

        var process =
            await _ffmpegService.StartRecordingAsync(
                station,
                filePath);

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