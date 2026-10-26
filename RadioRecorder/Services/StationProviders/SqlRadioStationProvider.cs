using Microsoft.Data.SqlClient;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public sealed class SqlRadioStationProvider : IRadioStationProvider
{
    private const string Server = "valleoriente.homeftp.net,3000";
    private const string Database = "OrbitMedia_Test";
    private const string User = "StreamRecorder";
    private readonly string _connectionString;

    public SqlRadioStationProvider(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "La cadena de conexión SQL no puede estar vacía.",
                nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// Crea el proveedor con las credenciales guardadas en una variable de entorno.
    /// No se almacena la contraseña en el código fuente.
    /// </summary>
    public static SqlRadioStationProvider CreateFromEnvironment()
    {
        var password = Environment.GetEnvironmentVariable(
            "RADIORECORDER_SQL_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "No se configuró la variable de entorno " +
                "'RADIORECORDER_SQL_PASSWORD'. Configúrala en Windows y reinicia " +
                "Visual Studio antes de iniciar el proveedor SQL.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            UserID = User,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 15,
            ApplicationName = "RadioRecorder"
        };

        return new SqlRadioStationProvider(builder.ConnectionString);
    }

    public async Task<IReadOnlyList<RadioStation>> GetStationsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                e.NUM_EMIS,
                e.SIGLAS,
                e.TIT_EMIS,
                e.FRECUENCIA,
                e.URL,
                e.ACTIVO,
                p.CIUDAD,
                p.INICIO_RADIO,
                p.FIN_RADIO,
                p.ZONA_HR
            FROM dbo.EMISORAS AS e
            INNER JOIN dbo.PLAZAS AS p
                ON p.NUM_PLAZA = e.NUM_PLAZA
            WHERE
                ISNULL(e.ACTIVO, 1) = 1
                AND e.URL IS NOT NULL
                AND LTRIM(RTRIM(e.URL)) <> ''
                AND e.ID_MEDIO = 1
            ORDER BY p.CIUDAD, e.TIT_EMIS;
            """;

        var stations = new List<RadioStation>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 30
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var ordinalNumEmis = reader.GetOrdinal("NUM_EMIS");
        var ordinalSiglas = reader.GetOrdinal("SIGLAS");
        var ordinalTitEmis = reader.GetOrdinal("TIT_EMIS");
        var ordinalFrecuencia = reader.GetOrdinal("FRECUENCIA");
        var ordinalUrl = reader.GetOrdinal("URL");
        var ordinalCiudad = reader.GetOrdinal("CIUDAD");
        var ordinalInicioRadio = reader.GetOrdinal("INICIO_RADIO");
        var ordinalFinRadio = reader.GetOrdinal("FIN_RADIO");
        var ordinalZonaHr = reader.GetOrdinal("ZONA_HR");

        while (await reader.ReadAsync(cancellationToken))
        {
            var stationId = reader.GetInt16(ordinalNumEmis);
            var name = reader.GetString(ordinalTitEmis).Trim();
            var abbreviation = reader.GetString(ordinalSiglas).Trim();
            var city = reader.GetString(ordinalCiudad).Trim();

            var streamUrl = reader.IsDBNull(ordinalUrl)
                ? string.Empty
                : reader.GetString(ordinalUrl).Trim();

            var recordingStart = reader.IsDBNull(ordinalInicioRadio)
                ? TimeOnly.MinValue
                : TimeOnly.FromDateTime(
                    reader.GetDateTime(ordinalInicioRadio));

            var recordingEnd = reader.IsDBNull(ordinalFinRadio)
                ? TimeOnly.MaxValue
                : TimeOnly.FromDateTime(
                    reader.GetDateTime(ordinalFinRadio));

            stations.Add(new RadioStation
            {
                Id = CreateGuidFromDatabaseId(stationId),
                Abbreviation = abbreviation,
                Name = name,
                Frequency = reader.IsDBNull(ordinalFrecuencia)
                    ? string.Empty
                    : reader.GetString(ordinalFrecuencia).Trim(),
                City = city,
                // En este esquema, URL se toma como la URL directa del stream.
                // Si contiene una página web en lugar de un stream, habrá que
                // distinguir ambas cosas con una columna o regla adicional.
                StreamUrl = streamUrl,
                StationPageUrl = string.Empty,
                StreamFormat = "auto",
                RecordingStartTime = recordingStart,
                RecordingEndTime = recordingEnd,
                // ZONA_HR define el desfase horario usado solo para el nombre del archivo.
                RecordingFileTimeOffsetHours = reader.IsDBNull(ordinalZonaHr)
                    ? 0
                    : Convert.ToInt32(reader.GetValue(ordinalZonaHr))
            });
        }

        return stations;
    }

    private static Guid CreateGuidFromDatabaseId(short id)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes((int)id).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
