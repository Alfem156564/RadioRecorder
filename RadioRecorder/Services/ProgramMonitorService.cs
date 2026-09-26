using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class ProgramMonitorService
{
    private readonly StationMonitorService _stationMonitorService;

    private readonly ProgramDetectionService _detectionService;

    private readonly ProgramHistoryService
    _programHistoryService;

    private readonly IScheduleProvider _scheduleProvider;

    private CancellationTokenSource?
        _cancellationTokenSource;

    private readonly List<Task> _stationTasks = [];

    public ProgramMonitorService(
     StationMonitorService stationMonitorService,
     ProgramDetectionService detectionService,
     IScheduleProvider scheduleProvider,
     ProgramHistoryService programHistoryService)
    {
        _stationMonitorService =
            stationMonitorService;

        _detectionService =
            detectionService;

        _scheduleProvider =
            scheduleProvider;

        _programHistoryService =
            programHistoryService;
    }

    public async Task StartAsync(
        IReadOnlyList<RadioStation> stations)
    {
        if (_cancellationTokenSource is not null)
        {
            return;
        }

        _cancellationTokenSource =
            new CancellationTokenSource();

        _stationTasks.Clear();

        foreach (var station in stations)
        {
            var task =
                MonitorStationAsync(
                    station,
                    _cancellationTokenSource.Token);

            _stationTasks.Add(task);
        }

        Console.WriteLine();
        Console.WriteLine(
            "🔎 Monitor de programas iniciado.");

        Console.WriteLine(
            $"📻 Estaciones monitoreadas: " +
            $"{stations.Count}");
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            "🛑 Deteniendo monitor de programas...");

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

        //
        // Cerrar programas activos.
        //

        _programHistoryService.StopAll(
            DateTime.Now);

        _stationTasks.Clear();

        _cancellationTokenSource.Dispose();

        _cancellationTokenSource = null;
    }

    private async Task MonitorStationAsync(
        RadioStation station,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"🔎 Monitoreando programas: " +
            $"{station.Name}");

        var programs =
            await _scheduleProvider
                .GetProgramsAsync(
                    station,
                    cancellationToken);

        Console.WriteLine(
            $"📅 [{station.Name}] " +
            $"Programas configurados: " +
            $"{programs.Count}");

        ProgramDetection? lastDetection = null;

        while (!cancellationToken
                   .IsCancellationRequested)
        {
            var now =
                DateTime.Now;

            //
            // Primero intentamos obtener
            // información del stream.
            //

            var streamInfo =
                await _stationMonitorService
                    .GetCurrentStreamInfoAsync(
                        station,
                        cancellationToken);

            ProgramDetection? detection =
                null;

            //
            // Intentamos detectar mediante
            // metadata.
            //

            if (streamInfo is not null)
            {
                detection =
                    _detectionService
                        .DetectFromMetadata(
                            streamInfo,
                            programs,
                            now);
            }

            //
            // Si metadata no identificó
            // ningún programa, usamos horario.
            //

            if (detection is null)
            {
                detection =
                    _detectionService
                        .DetectFromSchedule(
                            programs,
                            now);
            }

            //
            // Mostrar únicamente cuando
            // cambie el programa detectado.
            //

            if (HasDetectionChanged(
                lastDetection,
                detection))
            {
                HandleDetectionChange(
                    station,
                    detection,
                    now);

                lastDetection =
                    detection;
            }

            //
            // Esperamos antes de volver
            // a consultar.
            //

            await Task.Delay(
                TimeSpan.FromSeconds(30),
                cancellationToken);
        }
    }

    private static bool HasDetectionChanged(
        ProgramDetection? previous,
        ProgramDetection? current)
    {
        if (previous is null &&
            current is null)
        {
            return false;
        }

        if (previous is null ||
            current is null)
        {
            return true;
        }

        if (previous.Program?.Id !=
            current.Program?.Id)
        {
            return true;
        }

        if (previous.DetectedName !=
            current.DetectedName)
        {
            return true;
        }

        return false;
    }

    private static void PrintDetection(
        RadioStation station,
        ProgramDetection? detection)
    {
        Console.WriteLine();

        if (detection is null)
        {
            Console.WriteLine(
                $"📻 [{station.Name}] " +
                $"No se pudo identificar " +
                $"el programa actual.");

            return;
        }

        Console.WriteLine(
            $"🎙️ [{station.Name}] " +
            $"Programa detectado:");

        Console.WriteLine(
            $"   Nombre: " +
            $"{detection.DetectedName}");

        Console.WriteLine(
            $"   Fuente: " +
            $"{detection.Source}");

        Console.WriteLine(
            $"   Confianza: " +
            $"{detection.Confidence:P0}");

        Console.WriteLine(
            $"   Hora: " +
            $"{detection.DetectedAt:HH:mm:ss}");
    }

    private void HandleDetectionChange(
    RadioStation station,
    ProgramDetection? detection,
    DateTime now)
    {
        //
        // Si no pudimos detectar programa,
        // cerramos el actual.
        //

        if (detection is null)
        {
            var current =
                _programHistoryService
                    .GetCurrentProgram(station);

            if (current is not null)
            {
                _programHistoryService.EndProgram(
                    station,
                    now);

                Console.WriteLine();

                Console.WriteLine(
                    $"⏹️ [{station.Name}] " +
                    $"Programa finalizado:");

                Console.WriteLine(
                    $"   {current.DetectedName}");

                Console.WriteLine(
                    $"   Fin: {now:HH:mm:ss}");
            }

            return;
        }

        //
        // Registrar nuevo programa.
        //

        var history =
            _programHistoryService.StartProgram(
                station,
                detection,
                now);

        Console.WriteLine();

        Console.WriteLine(
            $"🎙️ [{station.Name}] " +
            $"Nuevo programa:");

        Console.WriteLine(
            $"   {history.DetectedName}");

        Console.WriteLine(
            $"   Inicio: " +
            $"{history.StartTime:HH:mm:ss}");

        Console.WriteLine(
            $"   Fuente: " +
            $"{history.Source}");

        Console.WriteLine(
            $"   Confianza: " +
            $"{history.Confidence:P0}");
    }
}