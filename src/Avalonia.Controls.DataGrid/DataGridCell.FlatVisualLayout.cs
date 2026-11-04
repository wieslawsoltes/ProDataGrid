// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace Avalonia.Controls;

partial class DataGridCell
{
    private bool _hasFlatDataContext;
    private bool _isFlatVisualHidden;
    private bool _flatVisualHitTestVisible;
    private double _flatVisualOpacity;

    internal bool IsFlatVisualHidden => _isFlatVisualHidden;

    internal void RestoreFlatLogicalContentParent()
    {
        if (Content is not StyledElement content || content.Parent != null)
        {
            return;
        }

        // A recycled ContentControl may retain the content in its logical-child list
        // after the content's parent was cleared during detach. Normalize the pair
        // before attaching the cell beneath its semantic row again.
        LogicalChildren.Remove(content);
        LogicalChildren.Add(content);
    }

    internal void SetFlatDataContext(object? dataContext)
    {
        if (_hasFlatDataContext && ReferenceEquals(DataContext, dataContext))
        {
            return;
        }

        DataContext = dataContext;
        _hasFlatDataContext = true;
    }

    internal void ClearFlatDataContext()
    {
        if (!_hasFlatDataContext)
        {
            return;
        }

        _hasFlatDataContext = false;
        ClearValue(StyledElement.DataContextProperty);
    }

    internal void HideFlatVisual()
    {
        if (_isFlatVisualHidden)
        {
            return;
        }

        _flatVisualOpacity = Opacity;
        _flatVisualHitTestVisible = IsHitTestVisible;
        _isFlatVisualHidden = true;
        Opacity = 0;
        IsHitTestVisible = false;
    }

    internal void ShowFlatVisual()
    {
        if (!_isFlatVisualHidden)
        {
            return;
        }

        Opacity = _flatVisualOpacity;
        IsHitTestVisible = _flatVisualHitTestVisible;
        _isFlatVisualHidden = false;
    }
}
