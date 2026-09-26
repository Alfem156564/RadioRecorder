using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class ProgramHistoryService
{
    private readonly Dictionary<Guid, ProgramHistory>
        _currentPrograms = [];

    private readonly List<ProgramHistory>
        _history = [];

    public ProgramHistory? GetCurrentProgram(
        RadioStation station)
    {
        if (_currentPrograms.TryGetValue(
                station.Id,
                out var history))
        {
            return history;
        }

        return null;
    }

    public IReadOnlyList<ProgramHistory>
        GetHistory(
            RadioStation station)
    {
        return _history
            .Where(item =>
                item.Station.Id == station.Id)
            .OrderBy(item =>
                item.StartTime)
            .ToList();
    }

    public ProgramHistory StartProgram(
        RadioStation station,
        ProgramDetection detection,
        DateTime startTime)
    {
        //
        // Si ya había un programa activo,
        // cerrarlo primero.
        //

        if (_currentPrograms.TryGetValue(
                station.Id,
                out var current))
        {
            current.EndTime =
                startTime;

            _currentPrograms.Remove(
                station.Id);
        }

        var history =
            new ProgramHistory
            {
                Station = station,

                Program = detection.Program,

                DetectedName =
                    detection.DetectedName
                    ?? detection.Program?.Name
                    ?? "Desconocido",

                Source =
                    detection.Source,

                Confidence =
                    detection.Confidence,

                StartTime =
                    startTime
            };

        _history.Add(history);

        _currentPrograms[
            station.Id] = history;

        return history;
    }

    public void EndProgram(
        RadioStation station,
        DateTime endTime)
    {
        if (!_currentPrograms.TryGetValue(
                station.Id,
                out var current))
        {
            return;
        }

        current.EndTime =
            endTime;

        _currentPrograms.Remove(
            station.Id);
    }

    public void StopAll(
        DateTime endTime)
    {
        foreach (var current
                 in _currentPrograms.Values.ToList())
        {
            current.EndTime =
                endTime;
        }

        _currentPrograms.Clear();
    }
}