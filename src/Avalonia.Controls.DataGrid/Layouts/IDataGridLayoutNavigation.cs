// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Identifies a spatial navigation operation understood by a DataGrid layout algorithm.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
enum DataGridLayoutNavigationDirection
{
    /// <summary>Moves to the nearest item above the current item.</summary>
    Up,
    /// <summary>Moves to the nearest item below the current item.</summary>
    Down,
    /// <summary>Moves to the nearest item to the left of the current item.</summary>
    Left,
    /// <summary>Moves to the nearest item to the right of the current item.</summary>
    Right,
    /// <summary>Moves toward the start by approximately one viewport.</summary>
    PageUp,
    /// <summary>Moves toward the end by approximately one viewport.</summary>
    PageDown,
    /// <summary>Moves to the first item on the current layout line.</summary>
    LineStart,
    /// <summary>Moves to the last item on the current layout line.</summary>
    LineEnd,
    /// <summary>Moves to the first layout item.</summary>
    First,
    /// <summary>Moves to the last layout item.</summary>
    Last
}

/// <summary>
/// Describes a geometry-based navigation request for the active DataGrid layout.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
readonly struct DataGridLayoutNavigationRequest : IEquatable<DataGridLayoutNavigationRequest>
{
    /// <summary>
    /// Initializes a layout navigation request.
    /// </summary>
    /// <param name="currentItemIndex">The zero-based layout item index.</param>
    /// <param name="direction">The requested spatial direction.</param>
    /// <param name="viewport">The visible viewport in layout coordinates.</param>
    /// <param name="navigationAnchor">
    /// The layout-coordinate point whose cross-axis position should be preserved.
    /// </param>
    public DataGridLayoutNavigationRequest(
        int currentItemIndex,
        DataGridLayoutNavigationDirection direction,
        Rect viewport,
        Point navigationAnchor)
    {
        CurrentItemIndex = currentItemIndex;
        Direction = direction;
        Viewport = viewport;
        NavigationAnchor = navigationAnchor;
    }

    /// <summary>Gets the zero-based current layout item index.</summary>
    public int CurrentItemIndex { get; }

    /// <summary>Gets the requested spatial direction.</summary>
    public DataGridLayoutNavigationDirection Direction { get; }

    /// <summary>Gets the visible viewport in layout coordinates.</summary>
    public Rect Viewport { get; }

    /// <summary>Gets the layout-coordinate navigation anchor.</summary>
    public Point NavigationAnchor { get; }

    /// <inheritdoc/>
    public bool Equals(DataGridLayoutNavigationRequest other) =>
        CurrentItemIndex == other.CurrentItemIndex &&
        Direction == other.Direction &&
        Viewport == other.Viewport &&
        NavigationAnchor == other.NavigationAnchor;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DataGridLayoutNavigationRequest other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(CurrentItemIndex, Direction, Viewport, NavigationAnchor);

    /// <summary>Compares two requests for equality.</summary>
    public static bool operator ==(DataGridLayoutNavigationRequest left, DataGridLayoutNavigationRequest right) => left.Equals(right);

    /// <summary>Compares two requests for inequality.</summary>
    public static bool operator !=(DataGridLayoutNavigationRequest left, DataGridLayoutNavigationRequest right) => !left.Equals(right);
}

/// <summary>
/// Contains the target selected by a layout navigation algorithm.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
readonly struct DataGridLayoutNavigationResult : IEquatable<DataGridLayoutNavigationResult>
{
    /// <summary>Initializes a layout navigation result.</summary>
    /// <param name="itemIndex">The zero-based target layout item index.</param>
    /// <param name="estimatedBounds">The exact or estimated target bounds in layout coordinates.</param>
    public DataGridLayoutNavigationResult(int itemIndex, Rect estimatedBounds)
    {
        ItemIndex = itemIndex;
        EstimatedBounds = estimatedBounds;
    }

    /// <summary>Gets the zero-based target layout item index.</summary>
    public int ItemIndex { get; }

    /// <summary>
    /// Gets the exact or estimated target bounds. The item does not need to be realized.
    /// </summary>
    public Rect EstimatedBounds { get; }

    /// <inheritdoc/>
    public bool Equals(DataGridLayoutNavigationResult other) =>
        ItemIndex == other.ItemIndex && EstimatedBounds == other.EstimatedBounds;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DataGridLayoutNavigationResult other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ItemIndex, EstimatedBounds);

    /// <summary>Compares two results for equality.</summary>
    public static bool operator ==(DataGridLayoutNavigationResult left, DataGridLayoutNavigationResult right) => left.Equals(right);

    /// <summary>Compares two results for inequality.</summary>
    public static bool operator !=(DataGridLayoutNavigationResult left, DataGridLayoutNavigationResult right) => !left.Equals(right);
}

/// <summary>
/// Optionally supplies geometry-aware item navigation for a DataGrid layout algorithm.
/// </summary>
/// <remarks>
/// Implement this interface together with <see cref="IDataGridLayoutAlgorithm"/> to make a
/// custom layout participate in keyboard, controller, and programmatic spatial navigation.
/// Semantic policies such as wrapping, boundary exit, editing, and selection remain the
/// responsibility of the DataGrid navigation model.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
interface IDataGridLayoutNavigation
{
    /// <summary>Attempts to resolve a spatial target without forcing item realization.</summary>
    /// <param name="context">The active per-grid layout context.</param>
    /// <param name="request">The spatial request.</param>
    /// <param name="result">Receives the target index and its estimated bounds.</param>
    /// <returns><c>true</c> when the layout found a different target item.</returns>
    bool TryResolveNavigation(
        IDataGridLayoutContext context,
        in DataGridLayoutNavigationRequest request,
        out DataGridLayoutNavigationResult result);
}
