using System;
using System.Diagnostics.CodeAnalysis;

namespace Avalonia.Controls;

/// <summary>
/// Feature switch that gates every reflection and <see cref="System.ComponentModel.TypeDescriptor"/>
/// based code path the grid reaches without an explicit opt-in from the caller.
/// </summary>
/// <remarks>
/// The switch defaults to enabled. The ProDataGrid build targets turn it off when the consuming
/// application publishes trimmed or Native AOT (unless <c>ProDataGridIsReflectionEnabledByDefault</c>
/// is set), so the trimmer can remove the guarded branches and the app uses generated schemas,
/// accessors and compiled bindings instead.
/// </remarks>
internal static class DataGridReflectionFeature
{
    internal const string SwitchName = "ProDataGrid.IsReflectionEnabledByDefault";

    internal const string DisabledMessage =
        "Reflection-based data access is disabled because the application is trimmed or published with Native AOT. " +
        "Use generated column definitions (ProDataGrid.SourceGenerators), DataGridFastPathOptions accessors and compiled bindings, " +
        "or set the ProDataGridIsReflectionEnabledByDefault MSBuild property to true.";

    [FeatureSwitchDefinition(SwitchName)]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
    internal static bool IsEnabled { get; } = InitializeIsEnabled();

    private static bool InitializeIsEnabled()
        => !AppContext.TryGetSwitch(SwitchName, out var isEnabled) || isEnabled;
}
