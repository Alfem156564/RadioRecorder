using System.Text.Json;
using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services.StationProviders;

public class JsonFileRadioStationProvider : IRadioStationProvider
{
    private readonly string _filePath;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

    public JsonFileRadioStationProvider(
        string filePath)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<RadioStation>>
    GetStationsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException(
                "No se encontró el archivo de estaciones.",
                _filePath);
        }

        var json =
            await File.ReadAllTextAsync(
                _filePath,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var stations =
                JsonSerializer.Deserialize<
                    List<RadioStation>>(
                        json,
                        _jsonOptions);

            return stations ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"El archivo de estaciones contiene " +
                $"JSON inválido: {_filePath}",
                ex);
        }
    }
}