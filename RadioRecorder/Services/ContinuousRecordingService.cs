using RadioRecorder.Configuration;
using RadioRecorder.Models;

namespace RadioRecorder.Services;

public class ContinuousRecordingService
{
    private readonly RadioRecorderService _recorderService;

    private readonly RecordingConfiguration _configuration;

    private CancellationTokenSource? _cancellationTokenSource;

    private readonly List<Task> _stationTasks = [];

    private readonly Dictionary<Guid, Recording>
        _currentSegments = [];

    public ContinuousRecordingService(
        RadioRecorderService recorderService,
        RecordingConfiguration configuration)
    {
        _recorderService = recorderService;
        _configuration = configuration;
    }

    public async Task StartAsync(
        IReadOnlyList<RadioStation> stations)
    {
        if (_cancellationTokenSource is not null)
        {
            return;
        }

        ValidateConfiguration();

        _cancellationTokenSource =
            new CancellationTokenSource();

        _stationTasks.Clear();

        foreach (var station in stations)
        {
            var task =
                RunStationAsync(
                    station,
                    _cancellationTokenSource.Token);

            _stationTasks.Add(task);
        }

        Console.WriteLine();
        Console.WriteLine(
            "🎙️ Grabación programada iniciada.");

        Console.WriteLine(
            $"📻 Estaciones: {stations.Count}");

        Console.WriteLine(
            $"⏱️ Segmentos: " +
            $"{_configuration.SegmentDuration}");

        Console.WriteLine(
            $"🔄 Respaldo: " +
            $"{_configuration.SegmentOverlap}");
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            "🛑 Deteniendo grabación...");

        _cancellationTokenSource.Cancel();

        try
        {
            await Task.WhenAll(_stationTasks);
        }
        catch (OperationCanceledException)
        {
            // Cancelación esperada.
        }

        foreach (var stationId
                 in _currentSegments.Keys.ToList())
        {
            var recording =
                _currentSegments[stationId];

            await _recorderService.StopAsync(
                recording.Station,
                recording);
        }

        _currentSegments.Clear();

        _stationTasks.Clear();

        _cancellationTokenSource.Dispose();

        _cancellationTokenSource = null;
    }

    private async Task RunStationAsync(
        RadioStation station,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"📡 [{station.Name}] " +
            $"Horario: " +
            $"{station.RecordingStartTime:HH\\:mm} - " +
            $"{station.RecordingEndTime:HH\\:mm}");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            //
            // Esperar hasta que la estación
            // entre en su horario.
            //

            if (!IsInsideRecordingSchedule(
                    station,
                    now))
            {
                var nextStart =
                    GetNextScheduleStart(
                        station,
                        now);

                Console.WriteLine();
                Console.WriteLine(
                    $"⏸️ [{station.Name}] " +
                    $"Fuera de horario.");

                Console.WriteLine(
                    $"   Próximo inicio: " +
                    $"{nextStart:yyyy-MM-dd HH:mm:ss}");

                await DelayUntilAsync(
                    nextStart,
                    cancellationToken);

                continue;
            }

            //
            // Estamos dentro del horario.
            //

            await RunCurrentRecordingWindowAsync(
                station,
                cancellationToken);
        }
    }

    private async Task RunCurrentRecordingWindowAsync(
        RadioStation station,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        var window =
            CalculateCurrentWindow(
                station,
                now);

        Console.WriteLine();
        Console.WriteLine(
            $"🎙️ [{station.Name}] " +
            $"Ventana actual:");

        Console.WriteLine(
            $"   Real: {window.Start:HH:mm:ss} → " +
            $"{window.End:HH:mm:ss}");

        Console.WriteLine(
            $"   Archivo: {window.FileNameStart:HH:mm:ss} → " +
            $"{window.FileNameEnd:HH:mm:ss}");

        //
        // Si estamos dentro de una ventana
        // que todavía no ha terminado,
        // comenzamos inmediatamente.
        //

        var currentRecording =
            await StartRecordingAsync(
                station,
                window.FileNameStart,
                window.FileNameEnd,
                cancellationToken);

        _currentSegments[station.Id] =
            currentRecording;

        //
        // Vigilar hasta llegar al final
        // de esta ventana.
        //

        while (
            DateTime.Now < window.End &&
            !cancellationToken.IsCancellationRequested)
        {
            if (!IsRecordingAlive(
                    currentRecording))
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"⚠️ [{station.Name}] " +
                    $"FFmpeg terminó inesperadamente.");

                var failureTime =
                    DateTime.Now;

                currentRecording.EndTime =
                    failureTime;

                //
                // Intentamos recuperar.
                //

                currentRecording =
                    await RecoverRecordingAsync(
                        station,
                        window,
                        failureTime,
                        cancellationToken);

                _currentSegments[station.Id] =
                    currentRecording;

                continue;
            }

            var remaining =
                window.End -
                DateTime.Now;

            var delay =
                remaining <
                _configuration.HealthCheckInterval
                    ? remaining
                    : _configuration.HealthCheckInterval;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(
                    delay,
                    cancellationToken);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        //
        // ==========================================
        // SIGUIENTE VENTANA
        // ==========================================
        //

        var nextWindow =
            CalculateNextWindow(
                station,
                window);

        //
        // Ajustar el final al horario de la estación.
        //

        if (nextWindow.Start >=
            GetScheduleEndForDate(
                station,
                nextWindow.Start))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"⏹️ [{station.Name}] " +
                $"Horario de grabación terminado.");

            await _recorderService.StopAsync(
                station,
                currentRecording);

            _currentSegments.Remove(
                station.Id);

            return;
        }

        nextWindow =
            nextWindow with
            {
                End = Min(
                    nextWindow.End,
                    GetScheduleEndForDate(
                        station,
                        nextWindow.Start))
            };

        //
        // Iniciar el siguiente segmento.
        //

        Console.WriteLine();
        Console.WriteLine(
            $"🔄 [{station.Name}] " +
            $"Iniciando siguiente ventana:");

        Console.WriteLine(
            $"   {nextWindow.Start:HH:mm:ss} → " +
            $"{nextWindow.End:HH:mm:ss}");

        Recording? nextRecording = null;

        while (
            nextRecording is null &&
            !cancellationToken.IsCancellationRequested)
        {
            try
            {
                nextRecording =
                    await StartRecordingAsync(
                        station,
                        nextWindow.FileNameStart,
                        nextWindow.FileNameEnd,
                        cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"⚠️ [{station.Name}] " +
                    $"No fue posible iniciar " +
                    $"el siguiente segmento:");

                Console.WriteLine(
                    $"   {ex.Message}");

                await Task.Delay(
                    _configuration.RecoveryRetryInterval,
                    cancellationToken);
            }
        }

        if (nextRecording is null)
        {
            return;
        }

        _currentSegments[station.Id] =
            nextRecording;

        //
        // ==========================================
        // RESPALDO DE 20 SEGUNDOS
        // ==========================================
        //
        // El segmento anterior continúa vivo
        // mientras el nuevo ya está grabando.
        //

        var overlapEnd =
            window.End +
            _configuration.SegmentOverlap;

        Console.WriteLine();
        Console.WriteLine(
            $"🛡️ [{station.Name}] " +
            $"Respaldo del segmento anterior " +
            $"hasta {overlapEnd:HH:mm:ss}.");

        while (
            DateTime.Now < overlapEnd &&
            !cancellationToken.IsCancellationRequested)
        {
            var remaining =
                overlapEnd -
                DateTime.Now;

            var delay =
                remaining <
                _configuration.HealthCheckInterval
                    ? remaining
                    : _configuration.HealthCheckInterval;

            await Task.Delay(
                delay,
                cancellationToken);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            await _recorderService.StopAsync(
                station,
                currentRecording);

            Console.WriteLine();
            Console.WriteLine(
                $"✅ [{station.Name}] " +
                $"Segmento anterior cerrado.");
        }

        //
        // El nuevo segmento ya queda como actual.
        //
        // No hacemos recursión.
        // El loop exterior volverá a evaluar
        // su estado.
        //

        if (!cancellationToken.IsCancellationRequested)
        {
            await RunRecordingFromExistingWindowAsync(
                station,
                nextRecording,
                nextWindow,
                cancellationToken);
        }
    }

    private async Task RunRecordingFromExistingWindowAsync(
        RadioStation station,
        Recording recording,
        RecordingWindow window,
        CancellationToken cancellationToken)
    {
        while (
            DateTime.Now < window.End &&
            !cancellationToken.IsCancellationRequested)
        {
            if (!IsRecordingAlive(recording))
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"⚠️ [{station.Name}] " +
                    $"El segmento " +
                    $"{window.Start:HH:mm:ss}-{window.End:HH:mm:ss} " +
                    $"falló.");

                var failureTime =
                    DateTime.Now;

                await _recorderService.StopAsync(
                    station,
                    recording);

                var recovered =
                    await RecoverRecordingAsync(
                        station,
                        window,
                        failureTime,
                        cancellationToken);

                _currentSegments[station.Id] =
                    recovered;

                recording =
                    recovered;

                continue;
            }

            var remaining =
                window.End -
                DateTime.Now;

            var delay =
                remaining <
                _configuration.HealthCheckInterval
                    ? remaining
                    : _configuration.HealthCheckInterval;

            await Task.Delay(
                delay,
                cancellationToken);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            //
            // Si esta era la última ventana del horario,
            // cerramos exactamente al final del horario.
            //

            await _recorderService.StopAsync(
                station,
                recording);

            _currentSegments.Remove(
                station.Id);

            //
            // Volvemos al loop principal.
            //
        }
    }

    private async Task<Recording> RecoverRecordingAsync(
        RadioStation station,
        RecordingWindow window,
        DateTime failureTime,
        CancellationToken cancellationToken)
    {
        while (
            !cancellationToken.IsCancellationRequested)
        {
            if (DateTime.Now >= window.End)
            {
                throw new InvalidOperationException(
                    $"La ventana de grabación de " +
                    $"{station.Name} ya terminó.");
            }

            Console.WriteLine();
            Console.WriteLine(
                $"🔄 [{station.Name}] " +
                $"Intentando recuperación...");

            await Task.Delay(
                _configuration.RecoveryRetryInterval,
                cancellationToken);

            var recoveryStart =
                DateTime.Now;

            if (recoveryStart >= window.End)
            {
                throw new InvalidOperationException(
                    "No queda tiempo suficiente " +
                    "para recuperar la grabación.");
            }

            try
            {
                var recording =
                    await StartRecordingAsync(
                        station,
                        window.FileNameStart,
                        window.FileNameEnd,
                        cancellationToken);

                Console.WriteLine();
                Console.WriteLine(
                    $"🟢 [{station.Name}] " +
                    $"Grabación recuperada.");

                Console.WriteLine(
                    $"   {recoveryStart:HH:mm:ss} → " +
                    $"{window.End:HH:mm:ss}");

                return recording;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"⚠️ [{station.Name}] " +
                    $"La recuperación falló: " +
                    $"{ex.Message}");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        throw new InvalidOperationException(
            "No fue posible recuperar la grabación.");
    }

    private async Task<Recording> StartRecordingAsync(
        RadioStation station,
        DateTime fileNameStart,
        DateTime fileNameEnd,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var recording =
            await _recorderService.StartAsync(
                station,
                null,
                fileNameStart,
                fileNameEnd);

        Console.WriteLine();
        Console.WriteLine(
            $"🔴 [{station.Name}] Grabando:");

        Console.WriteLine(
            $"   {recording.FilePath}");

        return recording;
    }

    private bool IsRecordingAlive(
        Recording recording)
    {
        var process =
            _recorderService.GetProcess(
                recording);

        return process is not null &&
               !process.HasExited;
    }

    private RecordingWindow CalculateCurrentWindow(
    RadioStation station,
    DateTime now)
    {
        /*
         * El horario REAL de la estación no cambia.
         *
         * Ejemplo:
         *
         * 06:00 → 20:00
         *
         * El offset solamente modifica la línea
         * de tiempo utilizada para nombrar los archivos.
         */

        var realScheduleStart =
            now.Date.Add(
                station.RecordingStartTime.ToTimeSpan());

        var realScheduleEnd =
            GetScheduleEndForDate(
                station,
                now);

        /*
         * Convertimos la hora actual a la línea
         * de tiempo lógica del archivo.
         *
         * Magia:
         *
         * Real:    06:30
         * Offset:  -1
         * Archivo: 05:30
         */
        var logicalNow =
            now.AddHours(
                station.RecordingFileTimeOffsetHours);

        var logicalScheduleStart =
            realScheduleStart.AddHours(
                station.RecordingFileTimeOffsetHours);

        var logicalScheduleEnd =
            realScheduleEnd.AddHours(
                station.RecordingFileTimeOffsetHours);

        /*
         * Calculamos los límites de los bloques
         * utilizando la línea de tiempo lógica.
         */
        var logicalWindowStart =
            CalculateLogicalWindowStart(
                logicalScheduleStart,
                logicalNow);

        var logicalWindowEnd =
            CalculateLogicalWindowEnd(
                logicalScheduleStart,
                logicalWindowStart,
                logicalScheduleEnd);

        /*
         * Convertimos nuevamente los límites
         * a horario REAL.
         *
         * Ejemplo:
         *
         * Archivo: 05:00 → 06:00
         *
         * Real:    06:00 → 07:00
         */
        var realWindowStart =
            logicalWindowStart.AddHours(
                -station.RecordingFileTimeOffsetHours);

        var realWindowEnd =
            logicalWindowEnd.AddHours(
                -station.RecordingFileTimeOffsetHours);

        /*
         * Nos aseguramos de no salir del horario real.
         */
        if (realWindowStart <
            realScheduleStart)
        {
            realWindowStart =
                realScheduleStart;
        }

        if (realWindowEnd >
            realScheduleEnd)
        {
            realWindowEnd =
                realScheduleEnd;
        }

        return new RecordingWindow(
            realWindowStart,
            realWindowEnd,
            logicalWindowStart,
            logicalWindowEnd);
    }

    private bool IsInsideRecordingSchedule(
        RadioStation station,
        DateTime now)
    {
        var current =
            TimeOnly.FromDateTime(now);

        var start =
            station.RecordingStartTime;

        var end =
            station.RecordingEndTime;

        if (start <= end)
        {
            return current >= start &&
                   current < end;
        }

        //
        // Horario que cruza medianoche.
        //

        return current >= start ||
               current < end;
    }

    private DateTime GetNextScheduleStart(
        RadioStation station,
        DateTime now)
    {
        var todayStart =
            now.Date.Add(
                station.RecordingStartTime.ToTimeSpan());

        if (now < todayStart)
        {
            return todayStart;
        }

        return todayStart.AddDays(1);
    }

    private DateTime GetScheduleEndForDate(
        RadioStation station,
        DateTime date)
    {
        var start =
            date.Date.Add(
                station.RecordingStartTime.ToTimeSpan());

        var end =
            date.Date.Add(
                station.RecordingEndTime.ToTimeSpan());

        if (end <= start)
        {
            end = end.AddDays(1);
        }

        return end;
    }

    private static DateTime Min(
        DateTime first,
        DateTime second)
    {
        return first < second
            ? first
            : second;
    }

    private static async Task DelayUntilAsync(
        DateTime target,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var remaining =
                target - DateTime.Now;

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            var delay =
                remaining > TimeSpan.FromMinutes(1)
                    ? TimeSpan.FromMinutes(1)
                    : remaining;

            await Task.Delay(
                delay,
                cancellationToken);
        }
    }

    private void ValidateConfiguration()
    {
        if (_configuration.SegmentDuration <=
            TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "SegmentDuration debe ser mayor que cero.");
        }

        if (_configuration.SegmentOverlap <
            TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "SegmentOverlap no puede ser negativo.");
        }

        if (_configuration.SegmentOverlap >=
            _configuration.SegmentDuration)
        {
            throw new InvalidOperationException(
                "SegmentOverlap debe ser menor " +
                "que SegmentDuration.");
        }

        if (_configuration.HealthCheckInterval <=
            TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "HealthCheckInterval debe ser mayor que cero.");
        }

        if (_configuration.RecoveryRetryInterval <=
            TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "RecoveryRetryInterval debe ser mayor que cero.");
        }
    }

    private record RecordingWindow(
        DateTime Start,
        DateTime End,
        DateTime FileNameStart,
        DateTime FileNameEnd);

    private DateTime CalculateLogicalWindowStart(
    DateTime logicalScheduleStart,
    DateTime logicalNow)
    {
        /*
         * Si todavía estamos antes del horario,
         * el inicio lógico es el inicio del horario.
         */

        if (logicalNow <= logicalScheduleStart)
        {
            return logicalScheduleStart;
        }

        /*
         * Buscamos la siguiente hora par posterior
         * al inicio del horario.
         *
         * Ejemplo:
         *
         * 05:20 → 06:00
         * 05:00 → 06:00
         * 06:00 → 08:00
         */

        var firstBoundary =
            GetFirstEvenHourBoundary(
                logicalScheduleStart);

        /*
         * Primer segmento parcial.
         */

        if (logicalNow < firstBoundary)
        {
            return logicalNow;
        }

        /*
         * A partir de la primera hora par,
         * los segmentos se calculan con
         * SegmentDuration.
         */

        var elapsed =
            logicalNow - firstBoundary;

        var segmentCount =
            (long)Math.Floor(
                elapsed.TotalSeconds /
                _configuration.SegmentDuration.TotalSeconds);

        return firstBoundary +
               TimeSpan.FromTicks(
                   _configuration.SegmentDuration.Ticks *
                   segmentCount);
    }

    private DateTime CalculateLogicalWindowEnd(
    DateTime logicalScheduleStart,
    DateTime logicalWindowStart,
    DateTime logicalScheduleEnd)
    {
        /*
         * Si el segmento comienza antes de la
         * primera hora par, termina exactamente
         * en esa hora par.
         *
         * Ejemplo:
         *
         * 05:20 → 06:00
         */

        var firstBoundary =
            GetFirstEvenHourBoundary(
                logicalScheduleStart);

        if (logicalWindowStart <
            firstBoundary)
        {
            return Min(
                firstBoundary,
                logicalScheduleEnd);
        }

        /*
         * Después de la primera frontera,
         * utilizamos la duración configurada.
         */

        return Min(
            logicalWindowStart +
            _configuration.SegmentDuration,
            logicalScheduleEnd);
    }

    private static DateTime GetNextEvenHour(
    DateTime value)
    {
        var hour =
            value.Hour;

        /*
         * Si ya estamos exactamente en una hora par,
         * el primer bloque debe durar 2 horas.
         *
         * Ejemplo:
         *
         * 06:00 → 08:00
         */
        if (hour % 2 == 0 &&
            value.Minute == 0 &&
            value.Second == 0 &&
            value.Millisecond == 0)
        {
            return value.AddHours(2);
        }

        /*
         * Si estamos en una hora impar,
         * buscamos la siguiente hora par.
         *
         * 05:00 → 06:00
         * 05:30 → 06:00
         * 07:00 → 08:00
         */
        var nextEvenHour =
            hour % 2 == 0
                ? hour + 2
                : hour + 1;

        return value.Date.AddHours(
            nextEvenHour);
    }

    private RecordingWindow CalculateNextWindow(
    RadioStation station,
    RecordingWindow currentWindow)
    {
        /*
         * El siguiente bloque lógico comienza
         * exactamente donde termina el bloque actual.
         */

        var logicalStart =
            currentWindow.FileNameEnd;

        var logicalEnd =
            logicalStart +
            _configuration.SegmentDuration;

        /*
         * Horario lógico final de la estación.
         */

        var realScheduleEnd =
            GetScheduleEndForDate(
                station,
                currentWindow.End);

        var logicalScheduleEnd =
            realScheduleEnd.AddHours(
                station.RecordingFileTimeOffsetHours);

        /*
         * No permitimos superar el final lógico.
         */

        if (logicalEnd >
            logicalScheduleEnd)
        {
            logicalEnd =
                logicalScheduleEnd;
        }

        /*
         * Convertimos nuevamente a horario REAL.

         * IMPORTANTE:
         *
         * El offset solamente modifica las horas.
         * Los minutos y segundos permanecen iguales.
         */

        var realStart =
            logicalStart.AddHours(
                -station.RecordingFileTimeOffsetHours);

        var realEnd =
            logicalEnd.AddHours(
                -station.RecordingFileTimeOffsetHours);

        /*
         * No debemos grabar fuera del horario real.
         */

        if (realStart <
            currentWindow.End)
        {
            realStart =
                currentWindow.End;
        }

        if (realEnd >
            realScheduleEnd)
        {
            realEnd =
                realScheduleEnd;
        }

        return new RecordingWindow(
            realStart,
            realEnd,
            logicalStart,
            logicalEnd);
    }

    private static DateTime GetFirstEvenHourBoundary(
    DateTime value)
    {
        var hour = value.Hour;

        /*
         * Si el horario comienza exactamente
         * en una hora par, esa es nuestra primera
         * frontera.
         *
         * 06:00 → 06:00
         */

        if (hour % 2 == 0 &&
            value.Minute == 0 &&
            value.Second == 0 &&
            value.Millisecond == 0)
        {
            return value;
        }

        /*
         * Si comienza en una hora impar,
         * buscamos la siguiente hora par.
         *
         * 05:00 → 06:00
         * 05:30 → 06:00
         * 07:20 → 08:00
         */

        var nextEvenHour =
            hour % 2 == 0
                ? hour + 2
                : hour + 1;

        return value.Date.AddHours(
            nextEvenHour);
    }
}