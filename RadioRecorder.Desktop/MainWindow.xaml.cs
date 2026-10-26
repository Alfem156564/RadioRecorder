
using RadioRecorder.Data;
using RadioRecorder.Desktop.Services;
using RadioRecorder.Interfaces;
using RadioRecorder.Services;
using RadioRecorder.Services.StationProviders;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RadioRecorder.Desktop;

public partial class MainWindow : Window
{
    private const int StationStatusRefreshIntervalSeconds = 5;

    private RecordingApplicationService? _applicationService;

    private readonly ObservableCollection<RecordingErrorItem>
        _recordingErrors = [];

    private readonly Dictionary<(string Station, string Error), int>
        _errorOccurrences = new();

    private readonly ObservableCollection<StationStatusItem> _stationStatuses = [];

    private readonly DispatcherTimer _stationStatusTimer;

    public ObservableCollection<RecordingErrorItem> RecordingErrors =>
        _recordingErrors;

    public ObservableCollection<StationStatusItem> StationStatuses =>
        _stationStatuses;

    private bool _isStartingOrStopping;

    public MainWindow()
    {
        InitializeComponent();

        var interval = StationStatusRefreshIntervalSeconds;

        var intervalText = interval % 60 == 0
            ? $"{interval / 60} minuto(s)"
            : $"{interval} segundo(s)";

        StationStatusRefreshText.Text =
            $"  •  Actualización automática cada {intervalText}";

        DataContext = this;

        _stationStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(StationStatusRefreshIntervalSeconds)
        };
        _stationStatusTimer.Tick += (_, _) => RefreshStationStatuses();
    }

    private async void StartButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isStartingOrStopping)
            return;

        if (ProviderComboBox.SelectedItem is not ComboBoxItem selectedItem)
        {
            MessageBox.Show(
                "Selecciona un origen de estaciones.",
                "Radio Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        _isStartingOrStopping = true;
        StartButton.IsEnabled = false;
        ProviderComboBox.IsEnabled = false;

        try
        {
            string providerType = selectedItem.Tag?.ToString()
                ?? throw new InvalidOperationException(
                    "No se pudo identificar el provider seleccionado.");

            Log($"Provider seleccionado: {selectedItem.Content}");
            Log("Preparando el sistema de grabación...");

            IRadioStationProvider stationProvider =
                CreateStationProvider(providerType);

            // Los errores se cuentan por sesión de grabación.
            _recordingErrors.Clear();
            _errorOccurrences.Clear();

            _applicationService = new RecordingApplicationService();
            _applicationService.RecordingErrorRegistered +=
                OnRecordingErrorRegistered;

            await _applicationService.StartAsync(stationProvider);

            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            RefreshStationsButton.IsEnabled = true;
            RefreshStationStatuses();
            _stationStatusTimer.Start();

            StatusText.Text = "Grabando";
            StatusIndicator.Fill = Brushes.Green;

            Log($"Estaciones cargadas: {_applicationService.Stations.Count}");

            foreach (var station in _applicationService.Stations)
            {
                Log($"Estación activa: {station.Name} ({station.Frequency})");
            }

            Log("Servicios de grabación y monitor iniciados.");
        }
        catch (Exception ex)
        {
            OnRecordingErrorRegistered(
                "Sistema",
                $"Error al iniciar las grabaciones: {ex.Message}",
                0);

            Log($"ERROR: {ex.Message}");

            MessageBox.Show(
                ex.Message,
                "No se pudo iniciar la grabación",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            if (_applicationService is not null)
            {
                await _applicationService.DisposeAsync();
                _applicationService = null;
            }

            ProviderComboBox.IsEnabled = true;
            StartButton.IsEnabled = true;
            RefreshStationsButton.IsEnabled = false;
        }
        finally
        {
            _isStartingOrStopping = false;
        }
    }

    private async void StopButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isStartingOrStopping)
            return;

        if (_applicationService is null)
            return;

        _isStartingOrStopping = true;
        StopButton.IsEnabled = false;

        try
        {
            Log("Deteniendo los servicios...");

            await _applicationService.StopAsync();
            await _applicationService.DisposeAsync();

            _applicationService = null;

            Log("Servicios detenidos correctamente.");
            _stationStatusTimer.Stop();
            _stationStatuses.Clear();

            StopButton.Visibility = Visibility.Collapsed;
            StartButton.Visibility = Visibility.Visible;
            RefreshStationsButton.IsEnabled = false;

            StatusText.Text = "Detenido";
            StatusIndicator.Fill = Brushes.SlateGray;

            ProviderComboBox.IsEnabled = true;
            StartButton.IsEnabled = true;
            RefreshStationsButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            OnRecordingErrorRegistered(
                "Sistema",
                $"Error al detener las grabaciones: {ex.Message}",
                0);

            Log($"ERROR al detener: {ex.Message}");

            MessageBox.Show(
                ex.Message,
                "Error al detener",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isStartingOrStopping = false;
            StopButton.IsEnabled = true;
        }
    }

    private async void RefreshStationsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_applicationService is null || _isStartingOrStopping)
            return;

        RefreshStationsButton.IsEnabled = false;
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        try
        {
            Log("Consultando nuevamente el origen de estaciones...");

            var result = await _applicationService.RefreshStationsAsync();
            RefreshStationStatuses();

            Log(
                $"Actualización terminada. Estaciones actualizadas: {result.Updated}; " +
                $"grabaciones activas protegidas: {result.SkippedRecording}; " +
                $"sin errores (sin cambios): {result.SkippedNoErrors}; " +
                $"sin coincidencia en el proveedor: {result.NotFound}.");

            MessageBox.Show(
                $"Actualización completada.\n\n" +
                $"Estaciones actualizadas: {result.Updated}\n" +
                $"Grabaciones activas sin cambios: {result.SkippedRecording}\n" +
                $"Estaciones sin errores, sin cambios: {result.SkippedNoErrors}\n" +
                $"Estaciones no encontradas en el origen: {result.NotFound}\n\n" +
                "Las estaciones actualizadas usarán sus nuevos datos en el siguiente intento de grabación.",
                "Actualizar estaciones",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log($"ERROR al actualizar estaciones: {ex.Message}");

            MessageBox.Show(
                ex.Message,
                "No se pudieron actualizar las estaciones",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            RefreshStationsButton.IsEnabled = _applicationService is not null;
        }
    }

    private void RefreshStationStatuses()
    {
        if (_applicationService is null)
            return;

        var currentStatuses = _applicationService.GetStationStatuses();
        var currentIds = currentStatuses.Select(item => item.Id).ToHashSet();

        for (var i = _stationStatuses.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(_stationStatuses[i].Id))
                _stationStatuses.RemoveAt(i);
        }

        foreach (var status in currentStatuses)
        {
            var existing = _stationStatuses.FirstOrDefault(item => item.Id == status.Id);
            if (existing is null)
            {
                existing = new StationStatusItem { Id = status.Id };
                _stationStatuses.Add(existing);
            }

            existing.Station = status.Name;
            existing.Frequency = status.Frequency;
            existing.City = status.City;
            existing.Status = status.Status;
            existing.StatusIcon = status.Status switch
            {
                "Grabando" => "🟢",
                "Error de grabación" => "🔴",
                _ => "⚪"
            };
        }

        // Mantener el orden alfabético para localizar estaciones rápidamente.
        var ordered = _stationStatuses.OrderBy(item => item.Station).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var currentIndex = _stationStatuses.IndexOf(ordered[i]);
            if (currentIndex != i)
            {
                _stationStatuses.Move(currentIndex, i);
            }
        }
    }

    private IRadioStationProvider CreateStationProvider(
        string providerType)
    {
        return providerType switch
        {
            "Hardcoded" =>
                new HardcodedRadioStationProvider(),

            "JsonFile" =>
                new JsonFileRadioStationProvider(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "Recordings",
                        "stations.txt")),

            "Sql" =>
                CreateSqlProvider(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(providerType),
                providerType,
                "Provider no reconocido.")
        };
    }

    private static IRadioStationProvider CreateSqlProvider()
    {
        return SqlRadioStationProvider.CreateFromEnvironment();
    }

    private void OnRecordingErrorRegistered(
        string stationName,
        string error,
        int count)
    {
        // Los eventos llegan desde distintos servicios y tareas de fondo.
        // La ventana centraliza los conteos de todas las fuentes de diagnóstico.
        _ = Dispatcher.InvokeAsync(() =>
        {
            var key = (stationName.Trim(), error.Trim());

            _errorOccurrences.TryGetValue(key, out var currentCount);
            currentCount++;
            _errorOccurrences[key] = currentCount;

            var existingIndex = -1;

            for (var i = 0; i < _recordingErrors.Count; i++)
            {
                if (string.Equals(
                        _recordingErrors[i].Station,
                        key.Item1,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        _recordingErrors[i].Error,
                        key.Item2,
                        StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            var updatedItem = new RecordingErrorItem(
                key.Item1,
                key.Item2,
                currentCount);

            if (existingIndex >= 0)
            {
                _recordingErrors[existingIndex] = updatedItem;
            }
            else
            {
                _recordingErrors.Add(updatedItem);
            }

            Log($"Error registrado [{key.Item1}] ({currentCount}): {key.Item2}");
        });
    }

    private void Log(string message)
    {
        LogTextBox.AppendText(
            $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        LogTextBox.ScrollToEnd();
    }

    protected override async void OnClosed(EventArgs e)
    {
        _stationStatusTimer.Stop();

        if (_applicationService is not null)
        {
            try
            {
                await _applicationService.DisposeAsync();
            }
            catch (Exception ex)
            {
                OnRecordingErrorRegistered(
                    "Sistema",
                    $"Error al cerrar Radio Recorder: {ex.Message}",
                    0);

                MessageBox.Show(
                    ex.Message,
                    "Error al cerrar Radio Recorder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        base.OnClosed(e);
    }


    public sealed class StationStatusItem : INotifyPropertyChanged
    {
        private string _station = string.Empty;
        private string _frequency = string.Empty;
        private string _city = string.Empty;
        private string _status = string.Empty;
        private string _statusIcon = "⚪";

        public Guid Id { get; set; }

        public string Station
        {
            get => _station;
            set => SetField(ref _station, value);
        }

        public string Frequency
        {
            get => _frequency;
            set => SetField(ref _frequency, value);
        }

        public string City
        {
            get => _city;
            set => SetField(ref _city, value);
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public string StatusIcon
        {
            get => _statusIcon;
            set => SetField(ref _statusIcon, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
