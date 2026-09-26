using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class ScheduleService
{
    private readonly RadioRecorderService _recorderService;
    private readonly IScheduleProvider _scheduleProvider;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _schedulerTask;

    private List<RadioProgram> _programs = [];

    public bool IsRunning =>
        _schedulerTask is not null &&
        !_schedulerTask.IsCompleted;

    public ScheduleService(
        RadioRecorderService recorderService,
        IScheduleProvider scheduleProvider)
    {
        _recorderService = recorderService;
        _scheduleProvider = scheduleProvider;
    }

    public async Task StartAsync(
        RadioStation station)
    {
        if (IsRunning)
        {
            return;
        }

        _cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            await LoadProgramsAsync(
                station,
                _cancellationTokenSource.Token);

            _schedulerTask = RunAsync(
                _cancellationTokenSource.Token);
        }
        catch
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        _cancellationTokenSource.Cancel();

        if (_schedulerTask is not null)
        {
            try
            {
                await _schedulerTask;
            }
            catch (OperationCanceledException)
            {
                // Cancelación esperada.
            }
        }

        _cancellationTokenSource.Dispose();

        _cancellationTokenSource = null;
        _schedulerTask = null;
    }

    private async Task LoadProgramsAsync(
        RadioStation station,
        CancellationToken cancellationToken)
    {
        var programs =
            await _scheduleProvider.GetProgramsAsync(
                station,
                cancellationToken);

        _programs = programs.ToList();

        Console.WriteLine();
        Console.WriteLine(
            $"📅 Programas cargados: {_programs.Count}");

        foreach (var program in _programs)
        {
            Console.WriteLine(
                $"   • {program.Name} " +
                $"{program.StartTime}-{program.EndTime}");
        }
    }

    private async Task RunAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            var currentProgram =
                FindCurrentProgram(now);

            if (currentProgram is not null)
            {
                await HandleCurrentProgramAsync(
                    currentProgram,
                    cancellationToken);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(10),
                cancellationToken);
        }
    }

    private RadioProgram? FindCurrentProgram(
        DateTime now)
    {
        var currentTime =
            TimeOnly.FromDateTime(now);

        return _programs.FirstOrDefault(program =>
            program.Station is not null &&
            program.RunsOn(now.DayOfWeek) &&
            IsTimeInsideProgram(
                currentTime,
                program.StartTime,
                program.EndTime));
    }

    private static bool IsTimeInsideProgram(
        TimeOnly currentTime,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        // Programa normal.
        //
        // Ejemplo:
        // 10:00 -> 12:00
        //
        if (startTime <= endTime)
        {
            return currentTime >= startTime &&
                   currentTime < endTime;
        }

        // Programa que cruza medianoche.
        //
        // Ejemplo:
        // 23:00 -> 01:00
        //
        return currentTime >= startTime ||
               currentTime < endTime;
    }

    private async Task HandleCurrentProgramAsync(
    RadioProgram program,
    CancellationToken cancellationToken)
    {
        if (!_recorderService.IsRecording(
                program.Station))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"📻 Programa detectado: " +
                $"{program.Name}");

            Console.WriteLine(
                $"🔴 Iniciando grabación...");

            await _recorderService.StartAsync(
                program.Station,
                program);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            var currentProgram =
                FindCurrentProgram(now);

            if (currentProgram?.Id != program.Id)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"⏹ Finalizó: {program.Name}");

                var recording =
                    _recorderService
                        .GetCurrentRecordings(
                            program.Station)
                        .FirstOrDefault();

                if (recording is not null)
                {
                    await _recorderService.StopAsync(
                        program.Station,
                        recording);
                }

                return;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(10),
                cancellationToken);
        }
    }
}