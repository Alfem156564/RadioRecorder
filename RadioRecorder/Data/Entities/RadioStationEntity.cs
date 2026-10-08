namespace RadioRecorder.Data.Entities;

public class RadioStationEntity
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string? Frecuencia { get; set; }

    public int CiudadId { get; set; }

    public string? PaginaWeb { get; set; }

    public string UrlStream { get; set; } = string.Empty;

    public string? FormatoStream { get; set; }

    public TimeOnly? HorarioInicio { get; set; }

    public TimeOnly? HorarioFin { get; set; }

    public bool Activa { get; set; }

    public CityEntity? Ciudad { get; set; }
}