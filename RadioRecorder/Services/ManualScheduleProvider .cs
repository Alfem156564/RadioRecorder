using RadioRecorder.Interfaces;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class ManualScheduleProvider : IScheduleProvider
{
    private readonly List<RadioProgram> _programs = [];

    public void AddProgram(RadioProgram program)
    {
        _programs.Add(program);
    }

    public Task<IReadOnlyList<RadioProgram>> GetProgramsAsync(
        RadioStation station,
        CancellationToken cancellationToken = default)
    {
        var programs = _programs
            .Where(program =>
                program.Station.Id == station.Id)
            .ToList();

        return Task.FromResult<
            IReadOnlyList<RadioProgram>>(programs);
    }
}