namespace RadioRecorder.Data.Entities;

public class CityEntity
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public string Pais { get; set; } = "México";

    public ICollection<RadioStationEntity> Estaciones { get; set; }
        = new List<RadioStationEntity>();
}