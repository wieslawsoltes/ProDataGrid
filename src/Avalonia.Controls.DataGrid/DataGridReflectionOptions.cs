// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Avalonia.Controls
{
    /// <summary>Controls optional reflection-based DataGrid compatibility features.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridReflectionOptions
    {
        /// <summary>
        /// Gets whether reflection-based compatibility features are enabled.
        /// The default is true. Applications with compiled bindings and typed accessors can
        /// disable the ProDataGrid.Reflection.IsEnabled runtime host option with Trim=true.
        /// The .NET 10 assembly supports linker removal of the guarded compatibility paths.
        /// Disabling reflection does not preserve members for reflection-based bindings.
        /// </summary>
#if NET9_0_OR_GREATER
        [FeatureSwitchDefinition("ProDataGrid.Reflection.IsEnabled")]
#endif
        public static bool IsEnabled =>
            !AppContext.TryGetSwitch("ProDataGrid.Reflection.IsEnabled", out bool enabled) || enabled;

        internal static NotSupportedException CreateException(string operation) => new(
            operation + " requires reflection, which is disabled by ProDataGrid.Reflection.IsEnabled. " +
            "Use compiled bindings and typed column accessors instead.");
    }
}
