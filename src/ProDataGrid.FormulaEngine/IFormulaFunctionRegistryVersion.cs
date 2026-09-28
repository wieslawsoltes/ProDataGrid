// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProDataGrid.FormulaEngine
{
    /// <summary>
    /// Optional revision contract for mutable function registries. Implement this alongside
    /// <see cref="IFormulaFunctionRegistry"/> to invalidate cached eager/lazy compilation plans.
    /// </summary>
    public interface IFormulaFunctionRegistryVersion
    {
        /// <summary>
        /// Gets a revision that changes whenever a function is registered, replaced or removed.
        /// Registry mutation must be synchronized by the host with formula evaluation.
        /// </summary>
        long Version { get; }
    }
}
