// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedViewStateTests
{
    [Fact]
    public void Content_is_the_default_and_all_projection_values_are_stable()
    {
        Assert.Equal(DataGridGeneratedViewState.Content, default);
        Assert.Equal(0, (int)DataGridGeneratedViewState.Content);
        Assert.Equal(1, (int)DataGridGeneratedViewState.Loading);
        Assert.Equal(2, (int)DataGridGeneratedViewState.Empty);
        Assert.Equal(3, (int)DataGridGeneratedViewState.Error);
    }
}
