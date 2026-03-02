namespace Avalonia.Diagnostics;

/// <summary>
/// Kinds of diagnostic views available in DevTools
/// </summary>
public enum DevToolsViewKind
{
    /// <summary>
    /// The Logical Tree diagnostic view
    /// </summary>
    LogicalTree = 0,
    /// <summary>
    /// The Visual Tree diagnostic view
    /// </summary>
    VisualTree = 1,
    /// <summary>
    /// Events diagnostic view
    /// </summary>
    Events = 2,
    /// <summary>
    /// The Combined Tree diagnostic view
    /// </summary>
    CombinedTree = 3,
    /// <summary>
    /// Resources diagnostic view
    /// </summary>
    Resources = 4,
    /// <summary>
    /// Assets diagnostic view
    /// </summary>
    Assets = 5,
    /// <summary>
    /// Logs diagnostic view
    /// </summary>
    Logs = 6,
    /// <summary>
    /// Metrics diagnostic view
    /// </summary>
    Metrics = 7,
    /// <summary>
    /// ViewModels and bindings diagnostic view
    /// </summary>
    ViewModelsBindings = 8,
    /// <summary>
    /// Transport and process export settings view
    /// </summary>
    TransportSettings = 9,
    /// <summary>
    /// Breakpoints diagnostic view
    /// </summary>
    Breakpoints = 10,
    /// <summary>
    /// Elements 3D diagnostic view
    /// </summary>
    Elements3D = 11,
    /// <summary>
    /// Settings diagnostic view
    /// </summary>
    Settings = 12,
    /// <summary>
    /// Profiler diagnostic view
    /// </summary>
    Profiler = 13,
    /// <summary>
    /// Styles diagnostic view
    /// </summary>
    Styles = 14,
    /// <summary>
    /// Source code and XAML diagnostics view.
    /// </summary>
    Code = 15,
}
