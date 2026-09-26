using RadioRecorder.Models;
using RadioRecorder.Services;

namespace RadioRecorder.Configuration;

public static class ManualScheduleConfiguration
{
    public static void Configure(
        ManualScheduleProvider provider,
        RadioStation station)
    {
        var now = DateTime.Now;

        var program = new RadioProgram
        {
            Name = "Programa de prueba",

            StartTime =
                TimeOnly.FromDateTime(
                    now.AddMinutes(0.3)),

            EndTime =
                TimeOnly.FromDateTime(
                    now.AddMinutes(2)),

            Days =
            [
                now.DayOfWeek
            ],

            Station = station
        };

        provider.AddProgram(program);
    }
}