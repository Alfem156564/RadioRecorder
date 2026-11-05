using System.Text.Json;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

/// <summary>
/// Persiste los errores de grabación en un JSON diario centralizado:
/// Recordings/Errores/{mes}-{día}/errores.json.
/// La ciudad se utiliza como clave principal del JSON.
/// </summary>
public static class RecordingErrorLogService
{
    private static readonly object Sync = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Register(RadioStation station, string message, DateTime occurredAt)
    {
        ArgumentNullException.ThrowIfNull(station);

        // Se conserva el desfase configurado para la hora lógica del registro.
        var logicalTime = occurredAt.AddHours(station.RecordingFileTimeOffsetHours);
        var city = string.IsNullOrWhiteSpace(station.City)
            ? "Sin ciudad"
            : station.City.Trim();

        var dayFolder = $"{GetSpanishMonthAbbreviation(logicalTime.Month)}-{logicalTime:dd}";
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "Recordings",
            "Errores",
            dayFolder);

        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "errores.json");
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Error sin descripción"
            : message.Trim();

        lock (Sync)
        {
            Dictionary<string, List<ErrorLogEntry>> errorsByCity;

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                errorsByCity = JsonSerializer.Deserialize<Dictionary<string, List<ErrorLogEntry>>>(json, JsonOptions)
                    ?? new Dictionary<string, List<ErrorLogEntry>>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                errorsByCity = new Dictionary<string, List<ErrorLogEntry>>(StringComparer.OrdinalIgnoreCase);
            }

            // La ciudad es la clave principal. Dentro de ella se separan los errores
            // por estación y mensaje para no mezclar fallos distintos de una misma ciudad.
            var cityKey = errorsByCity.Keys.FirstOrDefault(key =>
                string.Equals(key, city, StringComparison.OrdinalIgnoreCase)) ?? city;

            if (!errorsByCity.TryGetValue(cityKey, out var cityErrors))
            {
                cityErrors = new List<ErrorLogEntry>();
                errorsByCity[cityKey] = cityErrors;
            }

            var existing = cityErrors.FirstOrDefault(item =>
                string.Equals(item.Estacion, station.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Mensaje, normalizedMessage, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                existing = new ErrorLogEntry
                {
                    Estacion = station.Name,
                    Mensaje = normalizedMessage
                };
                cityErrors.Add(existing);
            }

            existing.Numero++;
            existing.Horarios.Add(logicalTime.ToString("HH:mm:ss"));

            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(errorsByCity, JsonOptions));

            if (File.Exists(filePath))
                File.Move(temporaryPath, filePath, true);
            else
                File.Move(temporaryPath, filePath);
        }
    }

    private static string GetSpanishMonthAbbreviation(int month)
    {
        string[] months =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];
        return months[month - 1];
    }

    private sealed class ErrorLogEntry
    {
        public string Estacion { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public int Numero { get; set; }
        public List<string> Horarios { get; set; } = new();
    }
}
