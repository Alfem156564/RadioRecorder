
using RadioRecorder.Configuration;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;
using RadioRecorder.Services;
using RadioRecorder.Services.StreamResolvers;
using System.Net.Http;

namespace RadioRecorder.Desktop.Services;

public sealed class RecordingApplicationService : IAsyncDisposable
{
    private readonly RecordingConfiguration _configuration;

    private readonly HttpClient _httpClient;

    private ContinuousRecordingService? _recordingService;

    private ProgramMonitorService? _programMonitorService;

    private bool _isRunning;

    public bool IsRunning => _isRunning;

    // Notifica a la interfaz cuando se registra un error de grabación.
    public event Action<string, string, int>? RecordingErrorRegistered;

    public IReadOnlyList<RadioStation> Stations { get; private set; }
        = Array.Empty<RadioStation>();

    public RecordingApplicationService(
        RecordingConfiguration? configuration = null)
    {
        _configuration = configuration ?? new RecordingConfiguration();

        _httpClient = new HttpClient();

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 RadioRecorder/1.0");
    }

    public async Task StartAsync(
        IRadioStationProvider stationProvider,
        CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            return;

        var stations = await stationProvider.GetStationsAsync(
            cancellationToken);

        if (stations.Count == 0)
        {
            throw new InvalidOperationException(
                "El provider seleccionado no devolvió estaciones.");
        }

        // Configurar los programas como lo hacía Program.cs.
        var scheduleProvider = new ManualScheduleProvider();

        foreach (var station in stations)
        {
            ManualScheduleConfiguration.Configure(
                scheduleProvider,
                station);
        }

        // Validar los horarios antes de iniciar.
        var allPrograms = new List<RadioProgram>();

        foreach (var station in stations)
        {
            var programs = await scheduleProvider.GetProgramsAsync(
                station,
                cancellationToken);

            allPrograms.AddRange(programs);
        }

        var validator = new ScheduleValidator();

        var errors = validator.Validate(allPrograms);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Hay errores en los horarios:\n" +
                string.Join("\n", errors.Select(error => $"• {error}")));
        }

        // Crear los servicios que necesita el grabador.
        var streamResolver = new CompositeStreamResolver(
            new IStreamResolver[]
            {
                new Listen2MyRadioStreamResolver(_httpClient),
                new StereoLuzStreamResolver(_httpClient),
                new RadioGrupoStreamResolver(_httpClient),
                new MMRadioStreamResolver(_httpClient),
                new DirectStreamResolver()
            });

        var ffmpegService = new FFmpegService(
            _configuration,
            streamResolver);

        ffmpegService.DiagnosticReported +=
            (stationName, message) =>
                RecordingErrorRegistered?.Invoke(
                    stationName,
                    message,
                    0);

        var recorderService = new RadioRecorderService(
            ffmpegService);

        var stationMonitorService = new StationMonitorService();

        var detectionService = new ProgramDetectionService();

        var programHistoryService = new ProgramHistoryService();

        var recordingService = new ContinuousRecordingService(
            recorderService,
            _configuration);

        recordingService.ErrorRegistered +=
            (stationName, error, count) =>
                RecordingErrorRegistered?.Invoke(
                    stationName,
                    error,
                    count);

        var programMonitorService = new ProgramMonitorService(
            stationMonitorService,
            streamResolver,
            detectionService,
            scheduleProvider,
            programHistoryService);

        // Iniciar primero la grabación y después el monitor.
        try
        {
            await recordingService.StartAsync(stations);

            await programMonitorService.StartAsync(stations);

            Stations = stations;
            _recordingService = recordingService;
            _programMonitorService = programMonitorService;
            _isRunning = true;
        }
        catch
        {
            await programMonitorService.StopAsync();
            await recordingService.StopAsync();

            throw;
        }
    }


    public async Task StopAsync()
    {
        if (!_isRunning &&
            _recordingService is null &&
            _programMonitorService is null)
        {
            return;
        }

        var errors = new List<Exception>();

        // Intentar detener el monitor.
        if (_programMonitorService is not null)
        {
            try
            {
                await _programMonitorService.StopAsync();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
            finally
            {
                _programMonitorService = null;
            }
        }

        // Aunque falle el monitor, debemos intentar
        // detener las grabaciones.
        if (_recordingService is not null)
        {
            try
            {
                await _recordingService.StopAsync();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
            finally
            {
                _recordingService = null;
            }
        }

        _isRunning = false;

        if (errors.Count > 0)
        {
            throw new AggregateException(
                "Se produjeron errores al detener los servicios.",
                errors);
        }
    }


    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        finally
        {
            _httpClient.Dispose();
        }
    }
}
