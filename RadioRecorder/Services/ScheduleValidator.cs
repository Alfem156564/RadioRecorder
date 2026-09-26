using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class ScheduleValidator
{
    public List<string> Validate(
        IEnumerable<RadioProgram> programs)
    {
        var errors = new List<string>();

        var groupedPrograms = programs
            .GroupBy(program => program.Station.Id);

        foreach (var stationGroup in groupedPrograms)
        {
            var stationPrograms = stationGroup.ToList();

            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                var programsForDay = stationPrograms
                    .Where(program => program.RunsOn(day))
                    .ToList();

                for (var i = 0; i < programsForDay.Count; i++)
                {
                    for (var j = i + 1; j < programsForDay.Count; j++)
                    {
                        if (ProgramsOverlap(
                                programsForDay[i],
                                programsForDay[j]))
                        {
                            errors.Add(
                                $"Solapamiento el {day}: " +
                                $"'{programsForDay[i].Name}' " +
                                $"{programsForDay[i].StartTime}-{programsForDay[i].EndTime} " +
                                $"y " +
                                $"'{programsForDay[j].Name}' " +
                                $"{programsForDay[j].StartTime}-{programsForDay[j].EndTime}."
                            );
                        }
                    }
                }
            }
        }

        return errors;
    }

    private static bool ProgramsOverlap(
        RadioProgram first,
        RadioProgram second)
    {
        var firstIntervals =
            GetIntervals(first.StartTime, first.EndTime);

        var secondIntervals =
            GetIntervals(second.StartTime, second.EndTime);

        foreach (var firstInterval in firstIntervals)
        {
            foreach (var secondInterval in secondIntervals)
            {
                var overlaps =
                    firstInterval.Start < secondInterval.End &&
                    secondInterval.Start < firstInterval.End;

                if (overlaps)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<TimeInterval> GetIntervals(
        TimeOnly start,
        TimeOnly end)
    {
        if (start < end)
        {
            return
            [
                new TimeInterval(start, end)
            ];
        }

        // Programa que cruza medianoche.
        return
        [
            new TimeInterval(
                start,
                new TimeOnly(23, 59, 59)
            ),

            new TimeInterval(
                TimeOnly.MinValue,
                end
            )
        ];
    }

    private record TimeInterval(
        TimeOnly Start,
        TimeOnly End);
}