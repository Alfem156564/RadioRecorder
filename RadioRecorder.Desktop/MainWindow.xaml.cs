
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using RadioRecorder.Data;
using RadioRecorder.Desktop.Services;
using RadioRecorder.Interfaces;
using RadioRecorder.Services;
using RadioRecorder.Services.StationProviders;

namespace RadioRecorder.Desktop;

public partial class MainWindow : Window
{
    private RecordingApplicationService? _applicationService;

    private bool _isStartingOrStopping;

    private const string ConnectionString =
        "Server=localhost\\MSSQLSERVER01;" +
        "Database=test-radio-database;" +
        "Integrated Security=True;" +
        "TrustServerCertificate=True;";

    public MainWindow()
    {
        InitializeComponent();
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

            _applicationService = new RecordingApplicationService();

            await _applicationService.StartAsync(stationProvider);

            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;

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

            StopButton.Visibility = Visibility.Collapsed;
            StartButton.Visibility = Visibility.Visible;

            StatusText.Text = "Detenido";
            StatusIndicator.Fill = Brushes.SlateGray;

            ProviderComboBox.IsEnabled = true;
            StartButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
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
        var options = new DbContextOptionsBuilder<RadioDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        IDbContextFactory<RadioDbContext> factory =
            new PooledDbContextFactory<RadioDbContext>(options);

        return new SqlRadioStationProvider(factory);
    }

    private void Log(string message)
    {
        LogTextBox.AppendText(
            $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        LogTextBox.ScrollToEnd();
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_applicationService is not null)
        {
            try
            {
                await _applicationService.DisposeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error al cerrar Radio Recorder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        base.OnClosed(e);
    }
}
