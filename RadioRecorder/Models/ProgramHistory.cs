namespace RadioRecorder.Models;

public class ProgramHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public RadioStation Station { get; set; } = null!;

    public RadioProgram? Program { get; set; }

    public string DetectedName { get; set; } = string.Empty;

    public DetectionSource Source { get; set; }

    public double Confidence { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public TimeSpan? Duration
    {
        get
        {
            if (EndTime is null)
            {
                return null;
            }

            return EndTime.Value - StartTime;
        }
    }

    public bool IsCompleted =>
        EndTime.HasValue;
}