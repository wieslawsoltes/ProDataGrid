// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;

namespace ProDataGrid.Charting
{
    internal static class ChartListPool<T>
    {
        private const int MaxPoolSize = 64;
        private const int MaxCapacity = 16384;
        private static readonly Stack<List<T>> Items = new();
        private static readonly object Gate = new();

        public static List<T> Rent(int capacity = 0)
        {
            List<T> list;
            lock (Gate)
            {
                list = Items.Count > 0 ? Items.Pop() : new List<T>();
            }

            if (capacity > list.Capacity)
            {
                list.Capacity = capacity;
            }

            return list;
        }

        public static void Return(List<T> list)
        {
            // Discard oversized storage. Shrinking it would allocate a replacement array
            // merely to return the list, even when the pool is already full.
            if (list.Capacity > MaxCapacity)
            {
                return;
            }

            list.Clear();
            lock (Gate)
            {
                if (Items.Count < MaxPoolSize)
                {
                    Items.Push(list);
                }
            }
        }
    }
}
