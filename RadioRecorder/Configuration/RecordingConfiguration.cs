namespace RadioRecorder.Configuration;

public class RecordingConfiguration
{
    // Duración lógica de cada segmento.
    public TimeSpan SegmentDuration { get; set; } =
        TimeSpan.FromHours(2);

    // Tiempo adicional que permanece grabando
    // el segmento anterior después de iniciar
    // el siguiente.
    public TimeSpan SegmentOverlap { get; set; } =
        TimeSpan.FromSeconds(20);

    // Cada cuánto revisamos FFmpeg.
    public TimeSpan HealthCheckInterval { get; set; } =
        TimeSpan.FromSeconds(30);

    // Tiempo entre intentos de recuperación
    // cuando FFmpeg falla.
    public TimeSpan RecoveryRetryInterval { get; set; } =
        TimeSpan.FromSeconds(10);

    public string AudioFormat { get; set; } = "mp3";

    public int AudioBitrateKbps { get; set; } = 96;

    public int AudioSampleRate { get; set; } = 44100;
}