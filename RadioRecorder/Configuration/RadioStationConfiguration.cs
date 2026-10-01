using RadioRecorder.Models;

namespace RadioRecorder.Configuration;

public static class RadioStationConfiguration
{
    public static List<RadioStation> GetStations()
    {
        return
        [
            new RadioStation
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "La Lupe",
                Frequency = "105.3 FM",
                City = "Monterrey",
                StationPageUrl = "https://www.mmradio.com/estaciones/la-lupe-1053-fm-monterrey#",
                StreamUrl = "https://mmradio-lb.mmlabs.mx/xhlupefm",
                StreamFormat = "auto",
                RecordingStartTime = new TimeOnly(6, 0),
                RecordingEndTime = new TimeOnly(20, 0),
                RecordingFileTimeOffsetHours = 0
            },

            new RadioStation
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Magia 101",
                Frequency = "101.7 FM",
                City = "Aguascalientes",
                StationPageUrl = "https://radiogrupo.com/magia101/",
                StreamUrl = "https://magia101.streaming.radiogrupo.com",
                StreamFormat = "auto",
                RecordingStartTime = new TimeOnly(6, 0),
                RecordingEndTime = new TimeOnly(20, 0),
                RecordingFileTimeOffsetHours = -1
            },

            new RadioStation
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Stereo Luz FM",
                Frequency = "99.9 FM",
                City = "Tehuacán",
                StationPageUrl = "https://stereoluzfm.radiostream321.com",
                StreamUrl = String.Empty,//"https://uk22freenew.listen2myradio.com/live.mp3?typeportmount=s1_18487_stream_436530085",
                StreamFormat = "aac",
                RecordingStartTime = new TimeOnly(6, 0),
                RecordingEndTime = new TimeOnly(20, 0),
                RecordingFileTimeOffsetHours = 0
            }
        ];
    }
}