using System.Text.Json;

namespace RadioRecorder.Models;

public class RadioProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public List<DayOfWeek> Days { get; set; } = [];

    public RadioStation Station { get; set; } = null!;

    public bool RunsOn(DayOfWeek day)
    {
        return Days.Contains(day);
    }
}