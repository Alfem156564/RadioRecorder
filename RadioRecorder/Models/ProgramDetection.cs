namespace RadioRecorder.Models;

public class ProgramDetection
{
    public RadioProgram? Program { get; set; }

    public string? DetectedName { get; set; }

    public DetectionSource Source { get; set; }

    public double Confidence { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.Now;
}