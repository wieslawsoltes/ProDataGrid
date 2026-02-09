// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.DataGridFiltering;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Filtering;

public class FilteringModelTests
{
    [Fact]
    public void SetOrUpdate_Replaces_By_Column()
    {
        var model = new FilteringModel();
        var first = CreateDescriptor("col1", FilteringOperator.Equals, "Name", "A");
        var second = CreateDescriptor("col1", FilteringOperator.Equals, "Name", "B");
        FilteringChangedEventArgs? args = null;
        model.FilteringChanged += (_, e) => args = e;

        model.SetOrUpdate(first);
        model.SetOrUpdate(second);

        var active = Assert.Single(model.Descriptors);
        Assert.Equal("col1", active.ColumnId);
        Assert.Equal("B", active.Value);
        Assert.NotNull(args);
        Assert.Single(args!.OldDescriptors);
    }

    public class FilteringModelInteractionTests
    {
        [Fact]
        public void RequestShowFilterFlyout_Raises_Event()
        {
            var model = new FilteringModel();
            var raised = false;
            object? captured = null;

            model.ShowFilterFlyoutRequested += (_, args) =>
            {
                raised = true;
                captured = args.ColumnId;
            };

            model.RequestShowFilterFlyout("Name");

            Assert.True(raised);
            Assert.Equal("Name", captured);
        }
    }

    [Fact]
    public void Move_Reorders_Descriptors()
    {
        var model = new FilteringModel();
        model.Apply(new[]
        {
            CreateDescriptor("a", FilteringOperator.Equals, "NameA", "A"),
            CreateDescriptor("b", FilteringOperator.Equals, "NameB", "B"),
            CreateDescriptor("c", FilteringOperator.Equals, "NameC", "C")
        });
        FilteringChangedEventArgs? args = null;
        model.FilteringChanged += (_, e) => args = e;

        var moved = model.Move("c", 0);

        Assert.True(moved);
        Assert.Equal(new[] { "c", "a", "b" }, model.Descriptors.Select(x => x.ColumnId).ToArray());
        Assert.NotNull(args);
        Assert.Equal(new[] { "a", "b", "c" }, args!.OldDescriptors.Select(x => x.ColumnId).ToArray());
    }

    [Fact]
    public void Remove_Returns_False_When_Not_Found()
    {
        var model = new FilteringModel();
        var removed = model.Remove("missing");
        Assert.False(removed);
    }

    [Fact]
    public void Apply_Throws_On_Duplicate_Columns()
    {
        var model = new FilteringModel();
        var first = CreateDescriptor("col1", FilteringOperator.Equals, "Name", "A");
        var second = CreateDescriptor("col1", FilteringOperator.NotEquals, "Name", "B");

        Assert.Throws<ArgumentException>(() => model.Apply(new[] { first, second }));
    }

    [Fact]
    public void BeginUpdate_Coalesces_FilteringChanged()
    {
        var model = new FilteringModel();
        int changedCount = 0;
        model.FilteringChanged += (_, __) => changedCount++;

        model.BeginUpdate();
        model.SetOrUpdate(CreateDescriptor("col1", FilteringOperator.Equals, "Name", "A"));
        model.SetOrUpdate(CreateDescriptor("col1", FilteringOperator.Equals, "Name", "B"));
        model.Remove("col1");
        model.EndUpdate();

        Assert.Equal(1, changedCount);
        Assert.Empty(model.Descriptors);
    }

    [Fact]
    public void DeferRefresh_Disposes_Once()
    {
        var model = new FilteringModel();
        int changedCount = 0;
        model.FilteringChanged += (_, __) => changedCount++;

        using (model.DeferRefresh())
        {
            model.SetOrUpdate(CreateDescriptor("col1", FilteringOperator.Equals, "Name", "A"));
        }

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void FilteringChanging_Can_Cancel()
    {
        var model = new FilteringModel();
        model.SetOrUpdate(CreateDescriptor("col1", FilteringOperator.Equals, "Name", "A"));

        model.FilteringChanging += (_, e) =>
        {
            if (e.NewDescriptors.Count == 0)
            {
                e.Cancel = true;
            }
        };

        model.Remove("col1");

        Assert.Single(model.Descriptors);
    }

    [Fact]
    public void Defaults_Own_Filter()
    {
        var model = new FilteringModel();
        Assert.True(model.OwnsViewFilter);
    }

    [Fact]
    public void Descriptor_Allows_Custom_ColumnId_Without_Path()
    {
        var descriptor = new FilteringDescriptor(
            columnId: "custom-key",
            @operator: FilteringOperator.Contains,
            value: "A");

        Assert.Equal("custom-key", descriptor.ColumnId);
        Assert.Null(descriptor.PropertyPath);
    }

    private static FilteringDescriptor CreateDescriptor(
        object columnId,
        FilteringOperator @operator,
        string propertyPath,
        object? value = null,
        IReadOnlyList<object>? values = null,
        Func<object, bool>? predicate = null)
    {
        return new FilteringDescriptor(columnId, @operator, propertyPath, value, values, predicate);
    }
}
