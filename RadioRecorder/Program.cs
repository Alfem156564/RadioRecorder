using RadioRecorder.Configuration;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;
using RadioRecorder.Services;
using RadioRecorder.Services.StationProviders;
using RadioRecorder.Services.StreamResolvers;


Console.WriteLine("======================================");
Console.WriteLine("          RADIO RECORDER");
Console.WriteLine("======================================");
Console.WriteLine();

//
// Estaciones
//

IRadioStationProvider stationProvider =
    new HardcodedRadioStationProvider();

var stations =
    await stationProvider.GetStationsAsync();

Console.WriteLine(
    $"📻 Estaciones configuradas: " +
    $"{stations.Count}");

foreach (var station in stations)
{
    Console.WriteLine(
        $"   • {station.Name} " +
        $"({station.Frequency}) - " +
        $"{station.City}");
}

Console.WriteLine();

//
//Recording Configuration
//

var recordingConfiguration =
    new RecordingConfiguration
    {
        SegmentDuration =
            TimeSpan.FromHours(2),

        SegmentOverlap =
            TimeSpan.FromSeconds(20),

        HealthCheckInterval =
            TimeSpan.FromSeconds(30),

        AudioFormat =
            "mp3",

        AudioBitrateKbps =
            128
    };

//
// ==========================================
// SERVICIOS
// ==========================================
//

var httpClient =
    new HttpClient();

httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Mozilla/5.0 RadioRecorder/1.0");

var streamResolver =
    new CompositeStreamResolver(
        new IStreamResolver[]
        {
            new Listen2MyRadioStreamResolver(
                httpClient),

            new RadioGrupoStreamResolver(
                httpClient),

            new MMRadioStreamResolver(
                httpClient),

            new DirectStreamResolver()
        });

var ffmpegService =
    new FFmpegService(
        recordingConfiguration,
        streamResolver);

var recorderService =
    new RadioRecorderService(
        ffmpegService);

var stationMonitorService =
    new StationMonitorService();

var programDetectionService =
    new ProgramDetectionService();

var scheduleProvider =
    new ManualScheduleProvider();

var programHistoryService =
    new ProgramHistoryService();

//
// ==========================================
// CONFIGURACIÓN DE PROGRAMAS
// ==========================================
//

foreach (var station in stations)
{
    ManualScheduleConfiguration.Configure(
        scheduleProvider,
        station);
}

//
// ==========================================
// VALIDAR HORARIOS
// ==========================================
//

var allPrograms =
    new List<RadioProgram>();

foreach (var station in stations)
{
    var programs =
        await scheduleProvider
            .GetProgramsAsync(station);

    allPrograms.AddRange(
        programs);
}

var validator =
    new ScheduleValidator();

var errors =
    validator.Validate(
        allPrograms);

if (errors.Count > 0)
{
    Console.WriteLine();

    Console.WriteLine(
        "❌ Se encontraron errores " +
        "en los horarios:");

    foreach (var error in errors)
    {
        Console.WriteLine(
            $"   • {error}");
    }

    return;
}

//
// ==========================================
// GRABACIÓN CONTINUA
// ==========================================
//

var continuousRecordingService =
    new ContinuousRecordingService(
        recorderService,
        recordingConfiguration);

//
// ==========================================
// MONITOR DE PROGRAMAS
// ==========================================
//

var programMonitorService =
    new ProgramMonitorService(
        stationMonitorService,
        programDetectionService,
        scheduleProvider,
        programHistoryService);

//
// ==========================================
// INICIAR TODO
// ==========================================
//

await continuousRecordingService.StartAsync(
    stations);

await programMonitorService.StartAsync(
    stations);

Console.WriteLine();

Console.WriteLine(
    "======================================");

Console.WriteLine(
    "       RADIO RECORDER ACTIVO");

Console.WriteLine(
    "======================================");

Console.WriteLine();

Console.WriteLine(
    "🎙️ Grabación continua: ACTIVA");

Console.WriteLine(
    "🔎 Detección de programas: ACTIVA");

Console.WriteLine();

Console.WriteLine(
    "Presiona ENTER para detener.");

Console.ReadLine();

//
// ==========================================
// DETENER TODO
// ==========================================
//

Console.WriteLine();

await programMonitorService.StopAsync();

await continuousRecordingService.StopAsync();

Console.WriteLine();

Console.WriteLine(
    "======================================");

Console.WriteLine(
    "          RADIO RECORDER");

Console.WriteLine(
    "             DETENIDO");

Console.WriteLine(
    "======================================");

Console.WriteLine();

Console.WriteLine(
    "Presiona ENTER para cerrar.");

Console.ReadLine();