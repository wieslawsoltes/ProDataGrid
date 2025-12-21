// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Linq;
using Avalonia.Controls.DataGridSearching;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Searching;

public class SearchModelTests
{
    [Fact]
    public void SetOrUpdate_Replaces_Descriptor_For_Same_Scope()
    {
        var model = new SearchModel();

        model.SetOrUpdate(new SearchDescriptor("one", scope: SearchScope.AllColumns));
        model.SetOrUpdate(new SearchDescriptor("two", scope: SearchScope.AllColumns));

        var descriptor = Assert.Single(model.Descriptors);
        Assert.Equal("two", descriptor.Query);
    }

    [Fact]
    public void SetOrUpdate_Replaces_Descriptor_For_Same_Explicit_Columns()
    {
        var model = new SearchModel();

        model.SetOrUpdate(new SearchDescriptor("one", scope: SearchScope.ExplicitColumns, columnIds: new object[] { "Name" }));
        model.SetOrUpdate(new SearchDescriptor("two", scope: SearchScope.ExplicitColumns, columnIds: new object[] { "Name" }));

        var descriptor = Assert.Single(model.Descriptors);
        Assert.Equal("two", descriptor.Query);
    }

    [Fact]
    public void SetOrUpdate_Ignores_Explicit_Column_Order()
    {
        var model = new SearchModel();

        model.SetOrUpdate(new SearchDescriptor("one", scope: SearchScope.ExplicitColumns, columnIds: new object[] { "Name", "Region" }));
        model.SetOrUpdate(new SearchDescriptor("two", scope: SearchScope.ExplicitColumns, columnIds: new object[] { "Region", "Name" }));

        var descriptor = Assert.Single(model.Descriptors);
        Assert.Equal("two", descriptor.Query);
    }

    [Fact]
    public void UpdateResults_Resets_CurrentIndex_When_Empty()
    {
        var model = new SearchModel();
        model.UpdateResults(new[]
        {
            new SearchResult(new object(), 0, "Name", 0, "Alpha", new[] { new SearchMatch(0, 1) })
        });

        Assert.Equal(0, model.CurrentIndex);

        model.UpdateResults(Array.Empty<SearchResult>());

        Assert.Equal(-1, model.CurrentIndex);
        Assert.Null(model.CurrentResult);
    }

    [Fact]
    public void DeferRefresh_Defers_SearchChanged_Event()
    {
        var model = new SearchModel();
        int eventCount = 0;

        model.SearchChanged += (_, _) => eventCount++;

        using (model.DeferRefresh())
        {
            model.Apply(new[] { new SearchDescriptor("alpha") });
            model.Apply(new[] { new SearchDescriptor("beta") });
            Assert.Equal(0, eventCount);
        }

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Navigation_Wraps_When_Enabled()
    {
        var model = new SearchModel { WrapNavigation = true };
        model.UpdateResults(new[]
        {
            new SearchResult(new object(), 0, "Name", 0, "Alpha", new[] { new SearchMatch(0, 1) }),
            new SearchResult(new object(), 1, "Name", 0, "Beta", new[] { new SearchMatch(0, 1) })
        });

        Assert.Equal(0, model.CurrentIndex);

        Assert.True(model.MoveNext());
        Assert.Equal(1, model.CurrentIndex);

        Assert.True(model.MoveNext());
        Assert.Equal(0, model.CurrentIndex);

        Assert.True(model.MovePrevious());
        Assert.Equal(1, model.CurrentIndex);
    }
}
