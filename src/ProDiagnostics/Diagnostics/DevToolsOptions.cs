using System;
using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace Avalonia.Diagnostics
{
    /// <summary>
    /// Describes options used to customize DevTools.
    /// </summary>
    public class DevToolsOptions
    {
        private int _maxRecordedEvents = 100;

        /// <summary>
        /// Gets or sets the key gesture used to open DevTools.
        /// </summary>
        public KeyGesture Gesture { get; set; } = new KeyGesture(Key.F12);

        /// <summary>
        /// Gets or sets the key gesture used to open DevTools with remote-enabled mode.
        /// </summary>
        public KeyGesture RemoteGesture { get; set; } = new KeyGesture(Key.F11);

        /// <summary>
        /// Gets or sets whether the remote launch gesture is enabled.
        /// </summary>
        public bool EnableRemoteGesture { get; set; } = true;

        /// <summary>
        /// Optional display name of the inspected application.
        /// </summary>
        public string? ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets whether DevTools should connect automatically on startup.
        /// </summary>
        public bool ConnectOnStartup { get; set; }

        /// <summary>
        /// Gets or sets whether auto-connect is allowed while running in design mode.
        /// </summary>
        public bool AutoConnectFromDesignMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether DevTools should be displayed as a child window
        /// of the window being inspected. The default value is true.
        /// </summary>
        /// <remarks>This setting is ignored if DevTools is attached to <see cref="Application"/></remarks>
        public bool ShowAsChildWindow { get; set; } = true;

        /// <summary>
        /// Gets or sets the initial size of the DevTools window. The default value is 1280x720.
        /// </summary>
        public Size Size { get; set; } = new Size(1280, 720);

        /// <summary>
        /// Get or set the startup screen index where the DevTools window will be displayed.
        /// </summary>
        public int? StartupScreenIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether DevTools should be displayed implemented interfaces on Control details. The default value is true.
        /// </summary>
        public bool ShowImplementedInterfaces { get; set; } = true;
        
        /// <summary>
        /// Allow to customize SreenshotHandler
        /// </summary>
        /// <remarks>Default handler is <see cref="Screenshots.FilePickerHandler"/></remarks>
        public IScreenshotHandler ScreenshotHandler { get; set; }
          = Conventions.DefaultScreenshotHandler;

        /// <summary>
        /// Gets or sets whether DevTools theme.
        /// </summary>
        public ThemeVariant? ThemeVariant { get; set; }

        /// <summary>
        /// Get or set Focus Highlighter <see cref="Brush"/>
        /// </summary>
        public IBrush? FocusHighlighterBrush { get; set; }

        /// <summary>
        /// Set the <see cref="DevToolsViewKind">kind</see> of diagnostic view that show at launch of DevTools
        /// </summary>
        public DevToolsViewKind LaunchView { get; init; } = DevToolsViewKind.CombinedTree;

        /// <summary>
        /// Gets or inits the <see cref="HotKeyConfiguration" /> used to activate DevTools features
        /// </summary>
        public HotKeyConfiguration HotKeys { get; init; } = new();

        /// <summary>
        /// Gets or sets whether hover overlays should visualize margin and padding frames.
        /// </summary>
        public bool VisualizeMarginPaddingOverlay { get; set; } = true;

        /// <summary>
        /// Gets or sets whether hover overlays should show an information tooltip.
        /// </summary>
        public bool ShowOverlayInfo { get; set; }

        /// <summary>
        /// Gets or sets whether hover overlays should show ruler lines.
        /// </summary>
        public bool ShowOverlayRulers { get; set; }

        /// <summary>
        /// Gets or sets whether hover overlays should show extension guide lines.
        /// </summary>
        public bool ShowOverlayExtensionLines { get; set; }

        /// <summary>
        /// Gets or sets whether hovered controls should be highlighted in inspection lists.
        /// </summary>
        public bool HighlightElements { get; set; } = true;

        /// <summary>
        /// Gets or sets whether overlay highlighting should follow the live pointer hover target.
        /// When disabled, overlays stay on the current inspected/selected element.
        /// </summary>
        public bool LiveHoverOverlay { get; set; } = true;

        /// <summary>
        /// Gets or sets whether focus tracking is enabled for the focused control indicator.
        /// </summary>
        public bool TrackFocusedControl { get; set; } = true;

        /// <summary>
        /// Gets or sets the default routed events that are enabled when opening the Events tool.
        /// When not set, the built-in defaults are used.
        /// </summary>
        public IReadOnlyCollection<RoutedEvent>? DefaultRoutedEvents { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of recorded events retained in the Events tool.
        /// Values less than 1 are clamped to 1.
        /// </summary>
        public int MaxRecordedEvents
        {
            get => _maxRecordedEvents;
            set => _maxRecordedEvents = value > 0 ? value : 1;
        }

        /// <summary>
        /// Gets or sets whether the Events tool should automatically scroll to newly recorded events.
        /// </summary>
        public bool AutoScrollEvents { get; set; } = true;

        /// <summary>
        /// Gets or sets whether diagnostics UDP export should be started automatically when DevTools opens.
        /// </summary>
        public bool EnableTransportAtStartup { get; set; }

        /// <summary>
        /// Gets or sets whether the external diagnostics viewer should be launched automatically when DevTools opens.
        /// </summary>
        public bool LaunchViewerOnStartup { get; set; }

        /// <summary>
        /// Gets or sets the external diagnostics viewer command or executable path.
        /// Default value is <c>prodiagnostics-viewer</c>.
        /// </summary>
        public string ViewerCommand { get; set; } = "prodiagnostics-viewer";

        /// <summary>
        /// Gets or sets optional command-line arguments passed to the external diagnostics viewer.
        /// </summary>
        public string ViewerArguments { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional target application name filter passed to the external diagnostics viewer.
        /// </summary>
        public string? ViewerTargetAppName { get; set; }

        /// <summary>
        /// Gets or sets an optional target process name filter passed to the external diagnostics viewer.
        /// </summary>
        public string? ViewerTargetProcessName { get; set; }

        /// <summary>
        /// Gets or sets an optional target process id filter passed to the external diagnostics viewer.
        /// </summary>
        public int? ViewerTargetProcessId { get; set; }

        /// <summary>
        /// Gets or sets an optional log collector used by the Logs tool.
        /// When not set, the built-in in-process collector is used.
        /// </summary>
        public IDevToolsLogCollector? LogCollector { get; set; }

        /// <summary>
        /// Gets or sets the target host used by diagnostics UDP export.
        /// </summary>
        public string TransportHost { get; set; } = "127.0.0.1";

        /// <summary>
        /// Gets or sets the transport protocol used by diagnostics export.
        /// Supported values currently include <c>udp</c>.
        /// </summary>
        public string TransportProtocol { get; set; } = "udp";

        /// <summary>
        /// Gets or sets the target port used by diagnostics UDP export.
        /// </summary>
        public int TransportPort { get; set; } = 54831;

        /// <summary>
        /// Gets or sets the maximum number of tags emitted per telemetry message.
        /// </summary>
        public int TransportMaxTagsPerMessage { get; set; } = 32;

        /// <summary>
        /// Gets or sets the diagnostics UDP hello packet interval.
        /// </summary>
        public TimeSpan TransportHelloInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets whether activity tags are included in export payloads.
        /// </summary>
        public bool TransportIncludeActivityTags { get; set; } = true;

        /// <summary>
        /// Gets or sets whether metric tags are included in export payloads.
        /// </summary>
        public bool TransportIncludeMetricTags { get; set; } = true;

        /// <summary>
        /// Gets or sets the activity source name patterns included in export.
        /// </summary>
        public IReadOnlyList<string>? TransportActivitySourceNames { get; set; } = new[] { "*" };

        /// <summary>
        /// Gets or sets the meter name patterns included in export.
        /// </summary>
        public IReadOnlyList<string>? TransportMeterNames { get; set; } = new[] { "*" };

        /// <summary>
        /// Gets or sets local loopback host/client options used when <see cref="UseRemoteRuntime"/> is enabled.
        /// </summary>
        public DevToolsRemoteLoopbackOptions RemoteLoopbackOptions { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this DevTools instance should run against the local remote loopback runtime.
        /// When false, DevTools uses direct in-process diagnostics services.
        /// </summary>
        public bool UseRemoteRuntime { get; set; }

        internal DevToolsOptions Clone() => (DevToolsOptions)MemberwiseClone();
    }
}
