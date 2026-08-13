// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Controls.DataGridLayouts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Layouts;

public class DataGridLayoutModelBaseTests
{
    [Fact]
    public void Property_change_raises_property_and_layout_notifications()
    {
        var model = new TestLayoutModel();
        string? propertyName = null;
        DataGridLayoutInvalidationKind? invalidation = null;
        model.PropertyChanged += (_, e) => propertyName = e.PropertyName;
        model.LayoutInvalidated += (_, e) => invalidation = e.Kind;

        model.ItemSpacing = 12;

        Assert.Equal(nameof(TestLayoutModel.ItemSpacing), propertyName);
        Assert.Equal(DataGridLayoutInvalidationKind.Measure, invalidation);
    }

    [Fact]
    public void Unchanged_property_does_not_raise_notifications()
    {
        var model = new TestLayoutModel();
        var raised = false;
        model.PropertyChanged += (_, _) => raised = true;
        model.LayoutInvalidated += (_, _) => raised = true;

        model.ItemSpacing = model.ItemSpacing;

        Assert.False(raised);
    }

    [Fact]
    public void Deferred_changes_raise_one_strongest_invalidation()
    {
        var model = new TestLayoutModel();
        var raised = 0;
        DataGridLayoutInvalidationKind? invalidation = null;
        model.LayoutInvalidated += (_, e) =>
        {
            raised++;
            invalidation = e.Kind;
        };

        using (model.DeferInvalidation())
        {
            model.ItemSpacing = 8;
            model.ArrangementToken = 1;
            model.ResetToken = 1;
        }

        Assert.Equal(1, raised);
        Assert.Equal(DataGridLayoutInvalidationKind.Reset, invalidation);
    }

    [Fact]
    public void Nested_deferred_changes_flush_after_outer_scope()
    {
        var model = new TestLayoutModel();
        var raised = 0;
        model.LayoutInvalidated += (_, _) => raised++;

        using (model.DeferInvalidation())
        {
            model.ItemSpacing = 4;
            using (model.DeferInvalidation())
            {
                model.ItemSpacing = 6;
            }

            Assert.Equal(0, raised);
        }

        Assert.Equal(1, raised);
    }

    [Fact]
    public void CreateAlgorithm_returns_custom_algorithm()
    {
        var model = new TestLayoutModel();

        Assert.IsType<TestLayoutAlgorithm>(model.CreateAlgorithm());
    }

    [Fact]
    public void Presentation_properties_are_sanitized_and_raise_reset_invalidation()
    {
        var model = new TestLayoutModel();
        var invalidations = new List<DataGridLayoutInvalidationKind>();
        model.LayoutInvalidated += (_, e) => invalidations.Add(e.Kind);

        model.PresentationMode = DataGridLayoutPresentationMode.Items;
        model.ItemSizeEstimate = new Size(double.NaN, -12);

        Assert.Equal(DataGridLayoutPresentationMode.Items, model.PresentationMode);
        Assert.Equal(new Size(100, 1), model.ItemSizeEstimate);
        Assert.Equal(
            [DataGridLayoutInvalidationKind.Reset, DataGridLayoutInvalidationKind.Reset],
            invalidations);
    }

    private sealed class TestLayoutModel : DataGridLayoutModelBase
    {
        private double _itemSpacing;
        private int _arrangementToken;
        private int _resetToken;

        public double ItemSpacing
        {
            get => _itemSpacing;
            set => SetProperty(ref _itemSpacing, value);
        }

        public int ArrangementToken
        {
            get => _arrangementToken;
            set => SetProperty(ref _arrangementToken, value, DataGridLayoutInvalidationKind.Arrange);
        }

        public int ResetToken
        {
            get => _resetToken;
            set => SetProperty(ref _resetToken, value, DataGridLayoutInvalidationKind.Reset);
        }

        public override IDataGridLayoutAlgorithm CreateAlgorithm()
        {
            return new TestLayoutAlgorithm();
        }
    }

    private sealed class TestLayoutAlgorithm : IDataGridLayoutAlgorithm
    {
        public void Initialize(IDataGridLayoutContext context)
        {
        }

        public Size Measure(IDataGridLayoutContext context, Size availableSize)
        {
            return availableSize;
        }

        public Size Arrange(IDataGridLayoutContext context, Size finalSize)
        {
            return finalSize;
        }

        public void OnItemsChanged(IDataGridLayoutContext context, NotifyCollectionChangedEventArgs change)
        {
        }

        public void Uninitialize(IDataGridLayoutContext context)
        {
        }
    }
}
