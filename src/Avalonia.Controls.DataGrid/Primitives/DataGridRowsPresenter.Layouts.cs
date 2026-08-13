// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia.Controls.DataGridLayouts;
using Avalonia.Media;

namespace Avalonia.Controls.Primitives
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed partial class DataGridRowsPresenter
    {
        private readonly ConditionalWeakTable<IDataGridLayoutModel, LayoutSession> _layoutSessions = new();
        private LayoutSession? _activeLayoutSession;
        private int _requestedLayoutAnchorIndex = -1;

        internal bool UsesLayoutModel => OwningGrid?.LayoutModel != null;

        internal void OnLayoutModelChanged(IDataGridLayoutModel? oldModel, IDataGridLayoutModel? newModel)
        {
            if (_activeLayoutSession != null && ReferenceEquals(_activeLayoutSession.Model, oldModel))
            {
                _activeLayoutSession.Algorithm.Uninitialize(_activeLayoutSession.Context);
                _activeLayoutSession.IsInitialized = false;
                _activeLayoutSession = null;
            }

            CancelPrefetch();
            InvalidateMeasure();
            InvalidateArrange();
        }

        internal void OnLayoutModelInvalidated(IDataGridLayoutModel model, DataGridLayoutInvalidationKind kind)
        {
            if (kind == DataGridLayoutInvalidationKind.Reset && _layoutSessions.TryGetValue(model, out LayoutSession? session))
            {
                session.State = null;
                session.Bounds.Clear();
                session.LastItemCount = -1;
            }
        }

        internal bool ScrollLayoutIndexIntoView(int layoutIndex)
        {
            if (!UsesLayoutModel || OwningGrid == null || layoutIndex < 0 || layoutIndex >= OwningGrid.LayoutItemCount)
            {
                return false;
            }

            _requestedLayoutAnchorIndex = layoutIndex;
            InvalidateMeasure();
            return true;
        }

        private Size MeasureLayoutModel(Size availableSize)
        {
            DataGrid grid = OwningGrid!;
            IDataGridLayoutModel model = grid.LayoutModel!;
            LayoutSession session = GetLayoutSession(model);
            Size viewport = NormalizeViewport(availableSize);

            session.Context.BeginMeasure(viewport, _offset);
            if (session.LastItemCount != grid.LayoutItemCount)
            {
                session.Algorithm.OnItemsChanged(
                    session.Context,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                session.LastItemCount = grid.LayoutItemCount;
            }

            Size extent = session.Algorithm.Measure(session.Context, viewport);
            session.Context.CompleteMeasure();
            if (_requestedLayoutAnchorIndex >= 0 &&
                session.Bounds.TryGetValue(_requestedLayoutAnchorIndex, out Rect anchorBounds))
            {
                _offset = BringBoundsIntoViewport(_offset, viewport, anchorBounds);
                session.Context.SetViewport(viewport, _offset);
                _requestedLayoutAnchorIndex = -1;
            }
            UpdateMeasuredLayoutMetadata(grid);

            extent = new Size(
                NormalizeExtent(extent.Width, viewport.Width),
                NormalizeExtent(extent.Height, viewport.Height));
            UpdateScrollInfo(extent, viewport);

            grid.AvailableSlotElementRoom = Math.Max(0, viewport.Height - GetRealizedDesiredHeight(grid));
            return new Size(
                Math.Min(extent.Width, viewport.Width),
                Math.Min(extent.Height, viewport.Height));
        }

        private static Vector BringBoundsIntoViewport(Vector offset, Size viewport, Rect bounds)
        {
            double x = offset.X;
            double y = offset.Y;
            if (bounds.X < x)
            {
                x = bounds.X;
            }
            else if (bounds.Right > x + viewport.Width)
            {
                x = bounds.Right - viewport.Width;
            }
            if (bounds.Y < y)
            {
                y = bounds.Y;
            }
            else if (bounds.Bottom > y + viewport.Height)
            {
                y = bounds.Bottom - viewport.Height;
            }
            return new Vector(Math.Max(0, x), Math.Max(0, y));
        }

        private Size ArrangeLayoutModel(Size finalSize)
        {
            DataGrid grid = OwningGrid!;
            LayoutSession session = GetLayoutSession(grid.LayoutModel!);
            Size viewport = NormalizeViewport(finalSize);
            if (!AreClose(_viewport, viewport))
            {
                UpdateScrollInfo(_extent, viewport);
            }

            foreach (Control element in grid.DisplayData.GetScrollingElements())
            {
                if (element is DataGridRow row)
                {
                    row.EnsureFillerVisibility();
                }
            }

            session.Context.SetViewport(viewport, _offset);
            Size arranged = session.Algorithm.Arrange(session.Context, viewport);
            _lastArrangeHeight = viewport.Height;
            _lastArrangeMatchesDesired = true;

            Rect clipRect = new(0, 0, viewport.Width, viewport.Height);
            if (!AreClose(_clipRectGeometry.Rect, clipRect))
            {
                _clipRectGeometry.Rect = clipRect;
            }
            if (!ReferenceEquals(Clip, _clipRectGeometry))
            {
                Clip = _clipRectGeometry;
            }

            return new Size(
                Math.Max(viewport.Width, arranged.Width),
                Math.Max(viewport.Height, arranged.Height));
        }

        private LayoutSession GetLayoutSession(IDataGridLayoutModel model)
        {
            LayoutSession session = _layoutSessions.GetValue(model, key => new LayoutSession(this, key));
            if (!ReferenceEquals(_activeLayoutSession, session))
            {
                if (_activeLayoutSession != null)
                {
                    _activeLayoutSession.Algorithm.Uninitialize(_activeLayoutSession.Context);
                    _activeLayoutSession.IsInitialized = false;
                }
                _activeLayoutSession = session;
            }

            if (!session.IsInitialized)
            {
                session.Algorithm.Initialize(session.Context);
                session.IsInitialized = true;
            }
            return session;
        }

        private static Size NormalizeViewport(Size size)
        {
            double width = double.IsNaN(size.Width) || double.IsInfinity(size.Width) ? 0 : Math.Max(0, size.Width);
            double height = double.IsNaN(size.Height) || double.IsInfinity(size.Height) ? 0 : Math.Max(0, size.Height);
            return new Size(width, height);
        }

        private static double NormalizeExtent(double extent, double viewport)
        {
            return double.IsNaN(extent) || double.IsInfinity(extent) ? viewport : Math.Max(viewport, extent);
        }

        private static double GetRealizedDesiredHeight(DataGrid grid)
        {
            double height = 0;
            foreach (Control element in grid.DisplayData.GetScrollingElements())
            {
                height += element.DesiredSize.Height;
            }
            return height;
        }

        private static void UpdateMeasuredLayoutMetadata(DataGrid grid)
        {
            double headerWidth = 0;
            foreach (Control element in grid.DisplayData.GetScrollingElements())
            {
                int layoutIndex = grid.GetLayoutIndex(element);
                int slot = grid.GetLayoutSlot(layoutIndex);
                if (slot >= 0)
                {
                    grid.UpdateScrollHeightEstimate(slot, element.DesiredSize.Height);
                }

                if (element is DataGridRow { HeaderCell: { } rowHeader })
                {
                    headerWidth = Math.Max(headerWidth, rowHeader.DesiredSize.Width);
                }
                else if (element is DataGridRowGroupHeader { HeaderCell: { } groupHeader })
                {
                    headerWidth = Math.Max(headerWidth, groupHeader.DesiredSize.Width);
                }
            }
            grid.RowHeadersDesiredWidth = headerWidth;
        }

        private sealed class LayoutSession
        {
            public LayoutSession(DataGridRowsPresenter presenter, IDataGridLayoutModel model)
            {
                Model = model;
                Algorithm = model.CreateAlgorithm() ??
                    throw new InvalidOperationException("A DataGrid layout model returned a null algorithm.");
                Context = new LayoutContext(presenter, this);
            }

            public IDataGridLayoutModel Model { get; }
            public IDataGridLayoutAlgorithm Algorithm { get; }
            public LayoutContext Context { get; }
            public Dictionary<int, Rect> Bounds { get; } = new();
            public object? State { get; set; }
            public Point Origin { get; set; }
            public int LastItemCount { get; set; } = -1;
            public bool IsInitialized { get; set; }
        }

        private sealed class LayoutContext : IDataGridLayoutContext
        {
            private readonly DataGridRowsPresenter _presenter;
            private readonly LayoutSession _session;
            private readonly RealizedElementList _realizedElements;
            private int _firstRequestedIndex;
            private int _lastRequestedIndex;

            public LayoutContext(DataGridRowsPresenter presenter, LayoutSession session)
            {
                _presenter = presenter;
                _session = session;
                _realizedElements = new RealizedElementList(presenter);
            }

            private DataGrid Grid => _presenter.OwningGrid!;

            public int ItemCount => Grid.LayoutItemCount;
            public Rect RealizationRect { get; private set; }
            public Vector ScrollOffset { get; private set; }
            public int RecommendedAnchorIndex => _presenter._requestedLayoutAnchorIndex;
            public Point LayoutOrigin
            {
                get => _session.Origin;
                set => _session.Origin = value;
            }
            public object? LayoutState
            {
                get => _session.State;
                set => _session.State = value;
            }
            public IReadOnlyList<Control> RealizedElements => _realizedElements;

            public void BeginMeasure(Size viewport, Vector offset)
            {
                _firstRequestedIndex = int.MaxValue;
                _lastRequestedIndex = -1;
                _session.Bounds.Clear();
                SetViewport(viewport, offset);
            }

            public void SetViewport(Size viewport, Vector offset)
            {
                ScrollOffset = offset;
                RealizationRect = new Rect(offset.X, offset.Y, viewport.Width, viewport.Height);
            }

            public void CompleteMeasure()
            {
                Grid.CompleteLayoutRealization(
                    _lastRequestedIndex < 0 ? -1 : _firstRequestedIndex,
                    _lastRequestedIndex);
            }

            public Control GetOrCreateElementAt(int index)
            {
                if (index < 0 || index >= ItemCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                _firstRequestedIndex = Math.Min(_firstRequestedIndex, index);
                _lastRequestedIndex = Math.Max(_lastRequestedIndex, index);
                return Grid.GetOrCreateLayoutElement(index);
            }

            public void RecycleElement(Control element)
            {
                int index = GetElementIndex(element);
                if (index >= 0)
                {
                    _session.Bounds.Remove(index);
                }
            }

            public int GetElementIndex(Control element) => Grid.GetLayoutIndex(element);
            public Size GetEstimatedItemSize(int index) => Grid.GetEstimatedLayoutItemSize(index);
            public double GetEstimatedItemOffset(int index, DataGridLayoutOrientation orientation) =>
                Grid.GetEstimatedLayoutItemOffset(index, orientation);
            public void SetLayoutBounds(int index, Rect bounds) => _session.Bounds[index] = bounds;
            public bool TryGetLayoutBounds(int index, out Rect bounds) => _session.Bounds.TryGetValue(index, out bounds);
        }

        private sealed class RealizedElementList : IReadOnlyList<Control>
        {
            private readonly DataGridRowsPresenter _presenter;

            public RealizedElementList(DataGridRowsPresenter presenter)
            {
                _presenter = presenter;
            }

            public int Count => _presenter.OwningGrid?.DisplayData.ScrollingElementCount ?? 0;
            public Control this[int index] => _presenter.OwningGrid!.DisplayData.GetLogicalScrollingElement(index);

            public IEnumerator<Control> GetEnumerator()
            {
                for (int index = 0; index < Count; index++)
                {
                    yield return this[index];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
