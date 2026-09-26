using RadioRecorder.Models;

namespace RadioRecorder.Interfaces;

public interface IScheduleProvider
{
    Task<IReadOnlyList<RadioProgram>> GetProgramsAsync(
        RadioStation station,
        CancellationToken cancellationToken = default);
}