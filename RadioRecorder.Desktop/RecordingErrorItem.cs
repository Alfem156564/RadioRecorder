namespace RadioRecorder.Desktop;

/// <summary>
/// Una fila de la vista de errores detectados.
/// Count representa las apariciones acumuladas desde que inició la grabación.
/// </summary>
public sealed record RecordingErrorItem(
    string Station,
    string Error,
    int Count);
