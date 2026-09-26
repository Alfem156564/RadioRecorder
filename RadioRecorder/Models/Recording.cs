namespace RadioRecorder.Models;

public class Recording
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public RadioStation Station { get; set; } = null!;

    public RadioProgram? Program { get; set; }

    public string FilePath { get; set; } = string.Empty;

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

    public bool IsCompleted => EndTime.HasValue;
}