using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class StationSchedulerService
{
    private readonly RadioRecorderService _recorderService;

    private readonly IScheduleProvider _scheduleProvider;

    private readonly ProgramDetectionService
    _programDetectionService;

    private readonly StationMonitorService
    _stationMonitorService;

    private readonly List<RadioStation> _stations = [];

    private CancellationTokenSource? _cancellationTokenSource;

    private readonly List<Task> _stationTasks = [];

    public bool IsRunning =>
        _cancellationTokenSource is not null;

    public StationSchedulerService(
    RadioRecorderService recorderService,
    IScheduleProvider scheduleProvider,
    ProgramDetectionService programDetectionService,
    StationMonitorService stationMonitorService)
    {
        _recorderService =
            recorderService;

        _scheduleProvider =
            scheduleProvider;

        _programDetectionService =
            programDetectionService;

        _stationMonitorService =
            stationMonitorService;
    }

    public void AddStation(
        RadioStation station)
    {
        if (_stations.Any(
                existing =>
                    existing.Id == station.Id))
        {
            return;
        }

        _stations.Add(station);
    }

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        if (_stations.Count == 0)
        {
            throw new InvalidOperationException(
                "No hay estaciones configuradas.");
        }

        _cancellationTokenSource =
            new CancellationTokenSource();

        _stationTasks.Clear();

        foreach (var station in _stations)
        {
            var task =
                RunStationAsync(
                    station,
                    _cancellationTokenSource.Token);

            _stationTasks.Add(task);
        }

        Console.WriteLine();
        Console.WriteLine(
            $"📻 Estaciones monitoreadas: " +
            $"{_stations.Count}");
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            "🛑 Deteniendo estaciones...");

        _cancellationTokenSource.Cancel();

        try
        {
            await Task.WhenAll(
                _stationTasks);
        }
        catch (OperationCanceledException)
        {
            // Cancelación esperada.
        }

        foreach (var station in _stations)
        {
            if (_recorderService.IsRecording(
                    station))
            {
                await _recorderService.StopAllAsync(
                    station);
            }
        }

        _stationTasks.Clear();

        _cancellationTokenSource.Dispose();

        _cancellationTokenSource = null;
    }

    private async Task RunStationAsync(
    RadioStation station,
    CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"📡 Iniciando monitor: " +
            $"{station.Name}");

        var programs =
            await _scheduleProvider
                .GetProgramsAsync(
                    station,
                    cancellationToken);

        Console.WriteLine(
            $"📅 {station.Name}: " +
            $"{programs.Count} programas cargados.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            ProgramDetection? detection = null;

            try
            {
                var streamInfo =
                    await _stationMonitorService
                        .GetCurrentStreamInfoAsync(
                            station,
                            cancellationToken);

                Console.WriteLine(
                    $"🔎 [{station.Name}] " +
                    $"Metadata: " +
                    $"{streamInfo?.StreamTitle ?? "(sin metadata)"}");

                if (streamInfo is not null &&
                    !string.IsNullOrWhiteSpace(
                        streamInfo.StreamTitle))
                {
                    detection =
                        _programDetectionService
                            .DetectFromMetadata(
                                streamInfo,
                                programs,
                                now);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"⚠️ [{station.Name}] " +
                    $"Error leyendo metadata: " +
                    $"{ex.Message}");
            }

            if (detection?.Program is null)
            {
                detection =
                    _programDetectionService
                        .DetectFromSchedule(
                            programs,
                            now);
            }

            if (detection?.Program is not null)
            {
                var currentProgram =
                    detection.Program;

                if (!_recorderService.IsRecording(
                        station))
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"📻 [{station.Name}] " +
                        $"Programa detectado: " +
                        $"{detection.DetectedName}");

                    Console.WriteLine(
                        $"🔎 Método: " +
                        $"{detection.Source}");

                    Console.WriteLine(
                        $"🎯 Confianza: " +
                        $"{detection.Confidence:P0}");

                    Console.WriteLine(
                        "🔴 Iniciando grabación...");

                    await _recorderService.StartAsync(
                        station,
                        currentProgram);
                }

                await WaitUntilProgramEndsAsync(
                    programs,
                    currentProgram,
                    station,
                    cancellationToken);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(30),
                cancellationToken);
        }
    }

    private async Task WaitUntilProgramEndsAsync(
    IReadOnlyList<RadioProgram> programs,
    RadioProgram program,
    RadioStation station,
    CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            var now = DateTime.Now;

            var detection =
                _programDetectionService
                    .DetectFromSchedule(
                        programs,
                        now);

            if (detection?.Program?.Id != program.Id)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"⏹ [{station.Name}] " +
                    $"Finalizó: " +
                    $"{program.Name}");

                var recordings =
                    _recorderService
                        .GetCurrentRecordings(
                            station);

                foreach (var recording in recordings)
                {
                    await _recorderService.StopAsync(
                        station,
                        recording);
                }

                return;
            }
        }
    }
}