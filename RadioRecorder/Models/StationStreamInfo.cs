namespace RadioRecorder.Models;

public class StationStreamInfo
{
    public string IcyName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public string Bitrate { get; set; } = string.Empty;

    public string StreamTitle { get; set; } = string.Empty;

    public string AudioInformation { get; set; } = string.Empty;

    public DateTime RetrievedAt { get; set; } = DateTime.Now;
}