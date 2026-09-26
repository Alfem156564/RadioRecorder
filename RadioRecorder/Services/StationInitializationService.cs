using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class StationInitializationService
{
    private readonly StationRepository _repository;

    public StationInitializationService(
        StationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<RadioStation>>
        InitializeAsync(
            IReadOnlyList<RadioStation> configuredStations,
            CancellationToken cancellationToken = default)
    {
        var databaseStations =
            await _repository.GetAllAsync(
                cancellationToken);

        foreach (var configuredStation
                 in configuredStations)
        {
            var existingStation =
                databaseStations.FirstOrDefault(
                    station =>
                        station.Id ==
                        configuredStation.Id);

            if (existingStation is null)
            {
                await _repository.AddAsync(
                    configuredStation,
                    cancellationToken);

                Console.WriteLine(
                    $"🆕 Estación agregada: " +
                    $"{configuredStation.Name}");

                continue;
            }

            var changed =
                existingStation.Name !=
                    configuredStation.Name
                ||
                existingStation.Frequency !=
                    configuredStation.Frequency
                ||
                existingStation.City !=
                    configuredStation.City
                ||
                existingStation.StreamUrl !=
                    configuredStation.StreamUrl
                ||
                existingStation.StreamFormat !=
                    configuredStation.StreamFormat
                ||
                existingStation.RecordingStartTime !=
                    configuredStation.RecordingStartTime
                ||
                existingStation.RecordingEndTime !=
                    configuredStation.RecordingEndTime
                ||
                existingStation.RecordingFileTimeOffsetHours !=
                    configuredStation.RecordingFileTimeOffsetHours;

            if (!changed)
            {
                continue;
            }

            existingStation.Name =
                configuredStation.Name;

            existingStation.Frequency =
                configuredStation.Frequency;

            existingStation.City =
                configuredStation.City;

            existingStation.StreamUrl =
                configuredStation.StreamUrl;

            existingStation.StreamFormat =
                configuredStation.StreamFormat;

            existingStation.RecordingStartTime =
                configuredStation.RecordingStartTime;

            existingStation.RecordingEndTime =
                configuredStation.RecordingEndTime;

            existingStation.RecordingFileTimeOffsetHours =
                configuredStation.RecordingFileTimeOffsetHours;

            await _repository.UpdateAsync(
                existingStation,
                cancellationToken);

            Console.WriteLine(
                $"🔄 Estación actualizada: " +
                $"{existingStation.Name}");
        }

        return await _repository.GetAllAsync(
            cancellationToken);
    }
}