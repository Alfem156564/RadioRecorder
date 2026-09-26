using RadioRecorder.Configuration;
using RadioRecorder.Models;
using RadioRecorder.Services;
using Microsoft.EntityFrameworkCore;
using RadioRecorder.Data;

Console.WriteLine("======================================");
Console.WriteLine("          RADIO RECORDER");
Console.WriteLine("======================================");
Console.WriteLine();

//
//DB 
//
var dbPath =
    Path.Combine(
        AppContext.BaseDirectory,
        "RadioRecorder.db");

var options =
    new DbContextOptionsBuilder<RadioRecorderDbContext>()
        .UseSqlite(
            $"Data Source={dbPath}")
        .Options;

await using var dbContext =
    new RadioRecorderDbContext(options);

var databaseService =
    new DatabaseService(
        dbContext);

await databaseService.InitializeAsync();

//
// Estaciones
//

var stationRepository =
    new StationRepository(
        dbContext);

var stationInitializationService =
    new StationInitializationService(
        stationRepository);

var configuredStations =
    RadioStationConfiguration.GetStations();

var stations =
    await stationInitializationService
        .InitializeAsync(
            configuredStations);

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
            TimeSpan.FromMinutes(2),

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

var ffmpegService =
    new FFmpegService(
        recordingConfiguration);

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