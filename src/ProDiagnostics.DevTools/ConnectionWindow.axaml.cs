using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ProDiagnostics.DevTools;

public partial class ConnectionWindow : Window, IDevToolsHostSurface
{
    private static readonly bool TraceEnabled = string.Equals(
        Environment.GetEnvironmentVariable("PRODIAG_TRACE"),
        "1",
        StringComparison.Ordinal);
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromArgb(255, 191, 45, 45));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.FromArgb(255, 38, 92, 34));

    private readonly TextBox _endpointTextBox;
    private readonly TextBox _applicationNameTextBox;
    private readonly ComboBox _launchViewComboBox;
    private readonly TextBox _widthTextBox;
    private readonly TextBox _heightTextBox;
    private readonly TextBlock _statusTextBlock;
    private readonly Button _connectButton;
    private readonly Button _cancelButton;
    private bool _connectOnStartup;
    private bool _isConnecting;
    private bool _launchedMainWindow;

    public ConnectionWindow()
        : this(connectOnStartup: false)
    {
    }

    public ConnectionWindow(bool connectOnStartup)
    {
        InitializeComponent();
        _endpointTextBox = GetControl<TextBox>("EndpointTextBox");
        _applicationNameTextBox = GetControl<TextBox>("ApplicationNameTextBox");
        _launchViewComboBox = GetControl<ComboBox>("LaunchViewComboBox");
        _widthTextBox = GetControl<TextBox>("WidthTextBox");
        _heightTextBox = GetControl<TextBox>("HeightTextBox");
        _statusTextBlock = GetControl<TextBlock>("StatusTextBlock");
        _connectButton = GetControl<Button>("ConnectButton");
        _cancelButton = GetControl<Button>("CancelButton");
        _connectOnStartup = connectOnStartup;

        _launchViewComboBox.ItemsSource = Enum.GetValues<DevToolsViewKind>();
        _launchViewComboBox.SelectedItem = DevToolsStartupSettings.LaunchView;
        _endpointTextBox.Text = DevToolsStartupSettings.RemoteEndpoint.ToString();
        _applicationNameTextBox.Text = DevToolsStartupSettings.ApplicationName ?? string.Empty;

        if (DevToolsStartupSettings.StartupWidth is > 0)
        {
            _widthTextBox.Text = DevToolsStartupSettings.StartupWidth.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (DevToolsStartupSettings.StartupHeight is > 0)
        {
            _heightTextBox.Text = DevToolsStartupSettings.StartupHeight.Value.ToString(CultureInfo.InvariantCulture);
        }

        SetStatus("Enter remote endpoint and connect.", isError: false);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_connectOnStartup)
        {
            _connectOnStartup = false;
            _ = ConnectAsync();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void ConnectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ConnectAsync().ConfigureAwait(false);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_launchedMainWindow)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private async Task ConnectAsync()
    {
        if (_isConnecting)
        {
            return;
        }

        if (!DevToolsStartupSettings.TryParseEndpointText(_endpointTextBox.Text, out var endpoint))
        {
            SetStatus("Endpoint is invalid. Expected ws:// or wss:// URL.", isError: true);
            return;
        }

        var launchView = _launchViewComboBox.SelectedItem is DevToolsViewKind selectedView
            ? selectedView
            : DevToolsViewKind.CombinedTree;
        var appName = NormalizeText(_applicationNameTextBox.Text);
        var width = ParsePositiveDoubleOrNull(_widthTextBox.Text);
        var height = ParsePositiveDoubleOrNull(_heightTextBox.Text);

        _isConnecting = true;
        UpdateUiState();
        SetStatus("Connecting to remote diagnostics endpoint...", isError: false);
        DevToolsRemoteClientSession? remoteSession = null;
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                SetStatus("Desktop application lifetime is not available.", isError: true);
                return;
            }

            var connectOptions = DevToolsStartupSettings.CreateOptions(
                endpoint,
                appName,
                launchView,
                width,
                height,
                connectOnStartup: true);

            remoteSession = await DevToolsRemoteClientSession.ConnectAsync(
                endpoint,
                connectOptions.RemoteRuntimeClientOptions).ConfigureAwait(true);

            SetStatus("Opening ProDiagnostics...", isError: false);
            var mainWindow = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
                remoteSession,
                connectOptions);
            remoteSession = null;
            if (TraceEnabled)
            {
                Console.WriteLine(
                    $"[ConnectionWindow] created standalone window endpoint={endpoint} preconnected=True");
            }
            _launchedMainWindow = true;
            desktop.MainWindow = mainWindow;
            mainWindow.Closed += (_, _) => desktop.Shutdown();
            mainWindow.Show();
            Close();
        }
        catch (Exception ex)
        {
            _launchedMainWindow = false;
            SetStatus($"Failed to connect: {ex.Message}", isError: true);
            if (remoteSession is not null)
            {
                await remoteSession.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            _isConnecting = false;
            UpdateUiState();
        }
    }

    private void UpdateUiState()
    {
        _endpointTextBox.IsEnabled = !_isConnecting;
        _applicationNameTextBox.IsEnabled = !_isConnecting;
        _launchViewComboBox.IsEnabled = !_isConnecting;
        _widthTextBox.IsEnabled = !_isConnecting;
        _heightTextBox.IsEnabled = !_isConnecting;
        _connectButton.IsEnabled = !_isConnecting;
        _cancelButton.IsEnabled = !_isConnecting;
    }

    private void SetStatus(string message, bool isError)
    {
        _statusTextBlock.Text = message;
        _statusTextBlock.Foreground = isError ? ErrorBrush : InfoBrush;
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static double? ParsePositiveDoubleOrNull(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    private T GetControl<T>(string name) where T : Control
    {
        return this.FindControl<T>(name)
            ?? throw new InvalidOperationException($"Missing required control '{name}'.");
    }
}
