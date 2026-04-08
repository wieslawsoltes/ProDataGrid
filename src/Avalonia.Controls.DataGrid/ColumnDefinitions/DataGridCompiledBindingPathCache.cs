// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Runtime.CompilerServices;
using Avalonia.Data;
using Avalonia.Data.Core;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridCompiledBindingPathCache
    {
        private static ConditionalWeakTable<IPropertyInfo, CompiledBindingPath> s_cache = new();

        public static CompiledBindingPath GetOrCreate(IPropertyInfo property)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            return s_cache.GetValue(property, DataGridBindingDefinition.BuildPath);
        }

        public static bool TryGet(IPropertyInfo property, out CompiledBindingPath path)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            return s_cache.TryGetValue(property, out path);
        }

        public static void Clear()
        {
            s_cache = new ConditionalWeakTable<IPropertyInfo, CompiledBindingPath>();
        }
    }
}
