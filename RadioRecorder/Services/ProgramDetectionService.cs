using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class ProgramDetectionService
{
    public ProgramDetection?
        DetectFromSchedule(
            IReadOnlyList<RadioProgram> programs,
            DateTime now)
    {
        var currentTime =
            TimeOnly.FromDateTime(now);

        var program =
            programs.FirstOrDefault(
                item =>
                    item.RunsOn(now.DayOfWeek) &&
                    IsTimeInsideProgram(
                        currentTime,
                        item.StartTime,
                        item.EndTime));

        if (program is null)
        {
            return null;
        }

        return new ProgramDetection
        {
            Program = program,

            DetectedName =
                program.Name,

            Source =
                DetectionSource.Schedule,

            Confidence = 1.0,

            DetectedAt = now
        };
    }

    public ProgramDetection?
        DetectFromMetadata(
            StationStreamInfo streamInfo,
            IReadOnlyList<RadioProgram> programs,
            DateTime now)
    {
        if (string.IsNullOrWhiteSpace(
                streamInfo.StreamTitle))
        {
            return null;
        }

        var streamTitle =
            streamInfo.StreamTitle.Trim();

        //
        // Intentamos encontrar un programa
        // cuyo nombre coincida con el metadata.
        //

        var program =
            programs.FirstOrDefault(
                item =>
                    item.Name.Contains(
                        streamTitle,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    streamTitle.Contains(
                        item.Name,
                        StringComparison.OrdinalIgnoreCase));

        if (program is null)
        {
            return null;
        }

        return new ProgramDetection
        {
            Program = program,

            DetectedName =
                program.Name,

            Source =
                DetectionSource.StreamMetadata,

            Confidence = 1.0,

            DetectedAt = now
        };
    }

    private static bool IsTimeInsideProgram(
        TimeOnly current,
        TimeOnly start,
        TimeOnly end)
    {
        //
        // Programa normal:
        //
        // 10:00 -> 12:00
        //

        if (start < end)
        {
            return current >= start &&
                   current < end;
        }

        //
        // Programa que cruza medianoche:
        //
        // 23:00 -> 01:00
        //

        return current >= start ||
               current < end;
    }
}