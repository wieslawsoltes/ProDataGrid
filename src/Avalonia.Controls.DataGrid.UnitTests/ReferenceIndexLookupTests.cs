// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using Avalonia.Headless.XUnit;
using Xunit;
namespace Avalonia.Controls.DataGridTests;
public class ReferenceIndexLookupTests
{
    [AvaloniaFact]
    public void Sparse_Lookups_Do_Not_Allocate_A_Full_Source_Dictionary()
    {
        var items = new object[20000];
        for (int i = 0; i < items.Length; i++) items[i] = new object();
        var connection = new DataGridDataConnection(new DataGrid()) { DataSource = items };
        connection.IndexOf(items[0]); // warm JIT and the one-item cache
        connection.DataSource = items;
        long before = GC.GetAllocatedBytesForCurrentThread();
        int index = connection.IndexOf(items[10]);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(10, index);
        Assert.True(allocated < 4096, $"Sparse lookup allocated {allocated} bytes.");
    }

    [AvaloniaFact]
    public void Dense_Lookups_And_Source_Replacement_Preserve_First_Reference_Occurrence()
    {
        var items = new ArrayList();
        for (int i = 0; i < 40; i++) items.Add(new EqualItem());
        object duplicate = items[20]!;
        items.Add(duplicate);
        var connection = new DataGridDataConnection(new DataGrid()) { DataSource = items };
        for (int i = 0; i < 40; i++) Assert.Equal(i, connection.IndexOf(items[i]));
        Assert.Equal(20, connection.IndexOf(duplicate));
        items.Insert(0, new EqualItem());
        connection.DataSource = items;
        Assert.Equal(21, connection.IndexOf(duplicate));
        Assert.Equal(0, connection.IndexOf(new EqualItem())); // equality fallback remains available
    }
    private sealed class EqualItem
    {
        public override bool Equals(object? obj) => obj is EqualItem;
        public override int GetHashCode() => 1;
    }
}
