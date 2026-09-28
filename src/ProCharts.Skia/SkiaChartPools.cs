// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using SkiaSharp;

namespace ProCharts.Skia
{
    internal static class SkiaChartPools
    {
        private const int MaxListCapacity = 16384;
        private const int MaxListPoolSize = 64;
        private const int MaxPathPoolSize = 64;

        public static List<T> RentList<T>(int capacity = 0)
        {
            var list = ListPool<T>.Rent(MaxListPoolSize);
            if (capacity > list.Capacity)
            {
                list.Capacity = capacity;
            }

            return list;
        }

        public static void ReturnList<T>(List<T> list)
        {
            // Do not allocate a replacement backing array on the return path.
            if (list.Capacity > MaxListCapacity)
            {
                return;
            }

            list.Clear();
            ListPool<T>.Return(list, MaxListPoolSize);
        }

        public static SKPath RentPath()
        {
            return PathPool.Rent(MaxPathPoolSize);
        }

        public static void ReturnPath(SKPath path)
        {
            path.Reset();
            PathPool.Return(path, MaxPathPoolSize);
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Items = new();
            private static readonly object Gate = new();

            public static List<T> Rent(int maxPoolSize)
            {
                lock (Gate)
                {
                    if (Items.Count > 0)
                    {
                        var list = Items.Pop();
                        list.Clear();
                        return list;
                    }
                }

                return new List<T>();
            }

            public static void Return(List<T> list, int maxPoolSize)
            {
                lock (Gate)
                {
                    if (Items.Count < maxPoolSize)
                    {
                        Items.Push(list);
                        return;
                    }
                }
            }
        }

        private static class PathPool
        {
            private static readonly Stack<SKPath> Items = new();
            private static readonly object Gate = new();

            public static SKPath Rent(int maxPoolSize)
            {
                lock (Gate)
                {
                    if (Items.Count > 0)
                    {
                        var path = Items.Pop();
                        path.Reset();
                        return path;
                    }
                }

                return new SKPath();
            }

            public static void Return(SKPath path, int maxPoolSize)
            {
                lock (Gate)
                {
                    if (Items.Count < maxPoolSize)
                    {
                        Items.Push(path);
                        return;
                    }
                }

                path.Dispose();
            }
        }
    }
}
