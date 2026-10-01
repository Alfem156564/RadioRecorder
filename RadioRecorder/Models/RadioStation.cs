namespace RadioRecorder.Models;

public class RadioStation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Frequency { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string StationPageUrl { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string StreamFormat { get; set; } = "auto";

    public TimeOnly RecordingStartTime { get; set; }

    public TimeOnly RecordingEndTime { get; set; }

    public int RecordingFileTimeOffsetHours { get; set; }
}