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

    // Cada cuánto se comprueba que el proceso FFmpeg siga activo.
    // También determina la frecuencia con la que se detectan fallos inesperados.
    // Modifica este valor para ajustar el chequeo (por defecto, 1 minuto).
    public TimeSpan HealthCheckInterval { get; set; } =
        TimeSpan.FromMinutes(1);

    // Tiempo entre intentos de recuperación
    // cuando FFmpeg falla.
    public TimeSpan RecoveryRetryInterval { get; set; } =
        TimeSpan.FromSeconds(10);

    // Número de fallos consecutivos antes de
    // mostrar una alerta de estación.
    public int MaxConsecutiveFailuresBeforeAlert { get; set; } = 3;

    public string AudioFormat { get; set; } = "mp3";

    public int AudioBitrateKbps { get; set; } = 96;

    public int AudioSampleRate { get; set; } = 44100;
}