namespace RadioRecorder.Data;

using RadioRecorder.Data.Entities;
using RadioRecorder.Models;

public static class RadioStationMapper
{
    public static RadioStation ToModel(
        RadioStationEntity entity)
    {
        return new RadioStation
        {
            Id = CreateGuidFromDatabaseId(entity.Id),

            Name = entity.Nombre,

            Frequency = entity.Frecuencia ?? string.Empty,

            City = entity.Ciudad?.Nombre ?? string.Empty,

            StationPageUrl = entity.PaginaWeb ?? string.Empty,

            StreamUrl = entity.UrlStream,

            StreamFormat = string.IsNullOrWhiteSpace(entity.FormatoStream)
                ? "auto"
                : entity.FormatoStream,

            RecordingStartTime = entity.HorarioInicio
                ?? TimeOnly.MinValue,

            RecordingEndTime = entity.HorarioFin
                ?? TimeOnly.MaxValue,

            // No existe en la tabla SQL actualmente.
            RecordingFileTimeOffsetHours = 0
        };
    }

    private static Guid CreateGuidFromDatabaseId(int id)
    {
        var bytes = new byte[16];

        BitConverter
            .GetBytes(id)
            .CopyTo(bytes, 0);

        return new Guid(bytes);
    }
}