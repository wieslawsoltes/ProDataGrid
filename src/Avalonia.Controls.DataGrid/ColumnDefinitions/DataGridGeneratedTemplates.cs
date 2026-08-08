// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Controls.Templates;

namespace Avalonia.Controls
{
    /// <summary>Builds and recycles typed generated template controls through a validated direct factory.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedFuncDataTemplate<TItem> : IRecyclingDataTemplate
    {
        private readonly Func<TItem, Control, Control> _factory;

        /// <summary>Initializes a typed recycling template.</summary>
        public DataGridGeneratedFuncDataTemplate(Func<TItem, Control, Control> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc />
        public bool Match(object data) => data is TItem;

        /// <inheritdoc />
        public Control Build(object data) => Build(data, null);

        /// <inheritdoc />
        public Control Build(object data, Control existing)
        {
            // DataGrid may probe a row-details template before a data row is available.
            // Returning no control keeps that measurement pass allocation-free and lets
            // the first realized typed row establish the actual details height.
            if (data is null)
            {
                return null;
            }

            if (data is not TItem item)
            {
                throw new InvalidOperationException("Generated template expected item type '" + typeof(TItem) + "'.");
            }
            return _factory(item, existing) ??
                throw new InvalidOperationException("Generated template factory returned null.");
        }
    }
}
