// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Avalonia.Controls.DataGridSorting
{
    internal interface IDataGridSortComparerWrapper
    {
        IComparer InnerComparer { get; }
    }

    internal sealed class DataGridInvertedComparer : IComparer, IDataGridSortComparerWrapper
    {
        private readonly IComparer _innerComparer;

        public DataGridInvertedComparer(IComparer innerComparer)
        {
            _innerComparer = innerComparer ?? throw new ArgumentNullException(nameof(innerComparer));
        }

        public IComparer InnerComparer => _innerComparer;

        public int Compare(object x, object y)
        {
            return -_innerComparer.Compare(x, y);
        }

        public override bool Equals(object obj)
        {
            return obj is DataGridInvertedComparer other && ReferenceEquals(_innerComparer, other._innerComparer);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(_innerComparer);
        }
    }

    internal static class DataGridSortComparerHelpers
    {
        public static IComparer Unwrap(IComparer comparer)
        {
            return comparer is IDataGridSortComparerWrapper wrapper ? wrapper.InnerComparer : comparer;
        }
    }
}
