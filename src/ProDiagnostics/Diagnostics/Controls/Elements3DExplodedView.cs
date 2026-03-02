using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Controls;

internal sealed class Elements3DExplodedView : Control
{
    public static readonly StyledProperty<IEnumerable<Elements3DNodeViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, IEnumerable<Elements3DNodeViewModel>?>(nameof(Items));

    public static readonly StyledProperty<Elements3DNodeViewModel?> SelectedItemProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, Elements3DNodeViewModel?>(
            nameof(SelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> DepthSpacingProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, double>(nameof(DepthSpacing), 24d);

    public static readonly StyledProperty<double> TiltProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, double>(nameof(Tilt), 0.55d);

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, double>(
            nameof(Zoom),
            1d,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> OrbitYawProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, double>(
            nameof(OrbitYaw),
            0d,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> OrbitPitchProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, double>(
            nameof(OrbitPitch),
            0d,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> OrbitRollProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, double>(
            nameof(OrbitRoll),
            0d,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowInvisibleProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, bool>(nameof(ShowInvisible), true);

    public static readonly StyledProperty<bool> FlattenTo2DProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, bool>(nameof(FlattenTo2D));

    public static readonly StyledProperty<int> Flat2DMaxLayersPerRowProperty =
        AvaloniaProperty.Register<Elements3DExplodedView, int>(nameof(Flat2DMaxLayersPerRow), 0);

    private const int SnapshotRefreshIntervalMs = 250;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 24;
    private const double Flat2DMinimumLayerGap = 12;
    private const double Flat2DMaximumLayerGap = 480;
    private const double Flat2DMinimumFitScale = 0.002;
    private const double KeyboardPanStep = 24;
    private const double OrbitSensitivity = 0.45;
    private const double OrbitControlMargin = 14;
    private const double OrbitControlStep = 12;
    private readonly List<ProjectedNode> _projectedNodes = new();
    private bool _isPanning;
    private bool _isOrbiting;
    private bool _isOrbitControlDragging;
    private Point _lastPointerPosition;
    private Point _pointerPosition;
    private Vector _panOffset;
    private bool _isCapturingSnapshot;
    private long _lastSnapshotTick;
    private RenderTargetBitmap? _rootSnapshot;
    private Visual? _snapshotRoot;
    private Rect _snapshotRootBounds;
    private PixelSize _snapshotPixelSize;
    private INotifyCollectionChanged? _observedItems;

    static Elements3DExplodedView()
    {
        ItemsProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.OnItemsChanged());
        SelectedItemProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        DepthSpacingProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        TiltProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        ZoomProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        OrbitYawProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        OrbitPitchProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        OrbitRollProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        ShowInvisibleProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        FlattenTo2DProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
        Flat2DMaxLayersPerRowProperty.Changed.AddClassHandler<Elements3DExplodedView>((view, _) => view.InvalidateVisual());
    }

    public Elements3DExplodedView()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public IEnumerable<Elements3DNodeViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public Elements3DNodeViewModel? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public double DepthSpacing
    {
        get => GetValue(DepthSpacingProperty);
        set => SetValue(DepthSpacingProperty, value);
    }

    public double Tilt
    {
        get => GetValue(TiltProperty);
        set => SetValue(TiltProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double OrbitYaw
    {
        get => GetValue(OrbitYawProperty);
        set => SetValue(OrbitYawProperty, value);
    }

    public double OrbitPitch
    {
        get => GetValue(OrbitPitchProperty);
        set => SetValue(OrbitPitchProperty, value);
    }

    public double OrbitRoll
    {
        get => GetValue(OrbitRollProperty);
        set => SetValue(OrbitRollProperty, value);
    }

    public bool ShowInvisible
    {
        get => GetValue(ShowInvisibleProperty);
        set => SetValue(ShowInvisibleProperty, value);
    }

    public bool FlattenTo2D
    {
        get => GetValue(FlattenTo2DProperty);
        set => SetValue(FlattenTo2DProperty, value);
    }

    public int Flat2DMaxLayersPerRow
    {
        get => GetValue(Flat2DMaxLayersPerRowProperty);
        set => SetValue(Flat2DMaxLayersPerRowProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var viewport = Bounds;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            _projectedNodes.Clear();
            return;
        }

        using var clip = context.PushClip(new Rect(0, 0, viewport.Width, viewport.Height));
        context.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(20, 120, 120, 120)),
            new Pen(new SolidColorBrush(Color.FromArgb(70, 120, 120, 120)), 1),
            new Rect(0, 0, viewport.Width, viewport.Height));

        var nodes = GetOrderedNodes();
        var isFlat2D = FlattenTo2D;
        LayoutMetrics layout;
        IReadOnlyDictionary<int, FlatLayerLayout>? flatLayers = null;
        if (isFlat2D)
        {
            if (!TryCreateFlatLayerLayouts(nodes, out flatLayers, out var flatContentWidth, out var flatContentHeight, out var maxDepth))
            {
                _projectedNodes.Clear();
                return;
            }

            layout = new LayoutMetrics(
                viewport,
                MinX: 0,
                MinY: 0,
                ContentWidth: flatContentWidth,
                ContentHeight: flatContentHeight,
                maxDepth,
                DepthOffsetX: 0,
                DepthOffsetY: 0);
        }
        else if (!TryCreateLayoutMetrics(nodes, viewport, out layout))
        {
            _projectedNodes.Clear();
            return;
        }

        var scale = CalculateScale(layout, Zoom, isFlat2D);
        var origin = CalculateOrigin(layout, scale, _panOffset);
        var orbit = isFlat2D
            ? CreateOrbitTransform(0, 0, 0)
            : CreateOrbitTransform(OrbitYaw, OrbitPitch, OrbitRoll);

        var sceneCenterX = origin.X + ((layout.ContentWidth * scale) * 0.5) + ((layout.MaxDepth * layout.DepthOffsetX) * 0.5);
        var sceneCenterY = origin.Y + ((layout.ContentHeight * scale) * 0.5) - ((layout.MaxDepth * layout.DepthOffsetY) * 0.5);
        var depthStep = isFlat2D ? 0 : Math.Max(1, DepthSpacing);
        var sceneCenterZ = (layout.MaxDepth * depthStep) * 0.5;

        _projectedNodes.Clear();
        foreach (var node in nodes)
        {
            var nodeLayoutRect = isFlat2D
                ? CreateFlatNodeLayoutRect(node, flatLayers!)
                : node.BoundsRect;
            var baseX = origin.X + ((nodeLayoutRect.X - layout.MinX) * scale);
            var baseY = origin.Y + ((nodeLayoutRect.Y - layout.MinY) * scale);
            var width = Math.Max(1, nodeLayoutRect.Width * scale);
            var height = Math.Max(1, nodeLayoutRect.Height * scale);
            var shiftX = node.Depth * layout.DepthOffsetX;
            var shiftY = -(node.Depth * layout.DepthOffsetY);
            var front = new Rect(baseX + shiftX, baseY + shiftY, width, height);
            var frontQuad = new QuadPoints(front.TopLeft, front.TopRight, front.BottomRight, front.BottomLeft);
            var frontZ = isFlat2D ? node.Depth : node.Depth * depthStep;
            var transformed = TransformFlatProjection(
                frontQuad,
                frontZ,
                sceneCenterX,
                sceneCenterY,
                sceneCenterZ,
                orbit);
            var hitRect = CreateHitRect(transformed.Front);
            _projectedNodes.Add(new ProjectedNode(
                node,
                nodeLayoutRect,
                front,
                transformed.Front,
                hitRect,
                transformed.Depth,
                node.IsVisible));
        }

        _projectedNodes.Sort(static (left, right) => right.Depth.CompareTo(left.Depth));

        var needsSnapshot = _projectedNodes.Any(x => x.UseSnapshot);
        Rect snapshotBounds = default;
        IImage? rootSnapshot = null;
        if (needsSnapshot)
        {
            rootSnapshot = TryGetRootSnapshot(nodes, out snapshotBounds);
        }

        double? selectedDepth = null;
        if (!isFlat2D && SelectedItem is not null)
        {
            for (var i = 0; i < _projectedNodes.Count; i++)
            {
                var projected = _projectedNodes[i];
                if (ReferenceEquals(projected.Node, SelectedItem))
                {
                    selectedDepth = projected.Depth;
                    break;
                }
            }
        }

        for (var i = 0; i < _projectedNodes.Count; i++)
        {
            var projection = _projectedNodes[i];
            var isSelected = ReferenceEquals(projection.Node, SelectedItem);
            var layerOpacityFactor = ComputeFrontLayerOpacityFactor(projection.Depth, selectedDepth, isSelected);
            DrawProjection(
                context,
                projection,
                isSelected,
                layerOpacityFactor,
                rootSnapshot,
                snapshotBounds);
        }

        if (!isFlat2D)
        {
            DrawOrbitControl(context, viewport, orbit);
        }
        DrawHud(context, viewport);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeFromItemsCollection();
        base.OnDetachedFromVisualTree(e);
        ClearSnapshotCache();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.ClickCount == 2)
        {
            ResetPanAndZoom();
            e.Handled = true;
            return;
        }

        var point = e.GetCurrentPoint(this);
        var isRightPressed = point.Properties.IsRightButtonPressed;
        var isLeftPressed = point.Properties.IsLeftButtonPressed;
        if (!isLeftPressed && !isRightPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        _pointerPosition = position;
        if (isLeftPressed && TryHandleOrbitControlPointerDown(position, e.Pointer))
        {
            e.Handled = true;
            return;
        }

        Focus();
        _isOrbiting = isRightPressed;
        _isPanning = !_isOrbiting && isLeftPressed;
        _lastPointerPosition = position;
        e.Pointer.Capture(this);
        if (_isPanning && TryHitTestNode(_lastPointerPosition, out var node))
        {
            SetCurrentValue(SelectedItemProperty, node);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerPosition = e.GetPosition(this);
        if (HitTestOrbitControl(Bounds, _pointerPosition) != OrbitControlAction.None && !_isPanning && !_isOrbiting && !_isOrbitControlDragging)
        {
            InvalidateVisual();
        }

        if (e.Pointer.Captured != this)
        {
            return;
        }

        var current = _pointerPosition;
        var delta = current - _lastPointerPosition;
        _lastPointerPosition = current;
        if (_isOrbitControlDragging)
        {
            SetCurrentValue(OrbitYawProperty, NormalizeAngle(OrbitYaw + (delta.X * OrbitSensitivity)));
            SetCurrentValue(OrbitPitchProperty, NormalizeAngle(OrbitPitch - (delta.Y * OrbitSensitivity)));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isOrbiting)
        {
            if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
            {
                SetCurrentValue(OrbitRollProperty, NormalizeAngle(OrbitRoll + (delta.X * OrbitSensitivity)));
                SetCurrentValue(OrbitPitchProperty, NormalizeAngle(OrbitPitch - (delta.Y * (OrbitSensitivity * 0.35))));
            }
            else
            {
                SetCurrentValue(OrbitYawProperty, NormalizeAngle(OrbitYaw + (delta.X * OrbitSensitivity)));
                SetCurrentValue(OrbitPitchProperty, NormalizeAngle(OrbitPitch - (delta.Y * OrbitSensitivity)));
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            _panOffset += delta;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning || _isOrbiting || _isOrbitControlDragging)
        {
            _isPanning = false;
            _isOrbiting = false;
            _isOrbitControlDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_isPanning || _isOrbiting || _isOrbitControlDragging)
        {
            return;
        }

        _pointerPosition = new Point(-10000, -10000);
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y == 0)
        {
            return;
        }

        var currentZoom = Math.Clamp(Zoom, MinZoom, MaxZoom);
        var factor = Math.Pow(1.1, e.Delta.Y);
        var newZoom = Math.Clamp(currentZoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - currentZoom) < 0.0001)
        {
            e.Handled = true;
            return;
        }

        var pointer = e.GetPosition(this);
        var nodes = GetOrderedNodes();
        if (!TryCreateLayoutMetrics(nodes, Bounds, out var layout))
        {
            SetCurrentValue(ZoomProperty, newZoom);
            e.Handled = true;
            return;
        }

        var isFlat2D = FlattenTo2D;
        var currentScale = CalculateScale(layout, currentZoom, isFlat2D);
        if (TryResolveWheelAnchor(pointer, layout, currentScale, out var anchorX, out var anchorY, out var anchorDepth))
        {
            var newScale = CalculateScale(layout, newZoom, isFlat2D);
            _panOffset = CalculatePanForAnchorWithOrbit(layout, newScale, pointer, anchorX, anchorY, anchorDepth);
        }

        SetCurrentValue(ZoomProperty, newZoom);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (IsResetShortcut(e.Key, e.KeyModifiers))
        {
            ResetPanAndZoom();
            e.Handled = true;
            return;
        }

        if (TryHandleKeyboardPan(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }

        if (TryHandleKeyboardOrbit(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }

        if (TryHandleKeyboardZoom(e.Key))
        {
            e.Handled = true;
        }
    }

    private bool TryHitTestNode(Point point, out Elements3DNodeViewModel node)
    {
        if (TryHitTestProjection(point, out var projected))
        {
            node = projected.Node;
            return true;
        }

        node = null!;
        return false;
    }

    private bool TryHandleOrbitControlPointerDown(Point position, IPointer pointer)
    {
        var action = HitTestOrbitControl(Bounds, position);
        if (action == OrbitControlAction.None)
        {
            return false;
        }

        Focus();
        if (action == OrbitControlAction.OrbitDrag)
        {
            _isOrbitControlDragging = true;
            _lastPointerPosition = position;
            pointer.Capture(this);
        }
        else
        {
            ApplyOrbitControlAction(action);
        }

        return true;
    }

    private void ApplyOrbitControlAction(OrbitControlAction action)
    {
        switch (action)
        {
            case OrbitControlAction.YawLeft:
                SetCurrentValue(OrbitYawProperty, NormalizeAngle(OrbitYaw - OrbitControlStep));
                break;
            case OrbitControlAction.YawRight:
                SetCurrentValue(OrbitYawProperty, NormalizeAngle(OrbitYaw + OrbitControlStep));
                break;
            case OrbitControlAction.PitchUp:
                SetCurrentValue(OrbitPitchProperty, NormalizeAngle(OrbitPitch + OrbitControlStep));
                break;
            case OrbitControlAction.PitchDown:
                SetCurrentValue(OrbitPitchProperty, NormalizeAngle(OrbitPitch - OrbitControlStep));
                break;
            case OrbitControlAction.RollLeft:
                SetCurrentValue(OrbitRollProperty, NormalizeAngle(OrbitRoll - OrbitControlStep));
                break;
            case OrbitControlAction.RollRight:
                SetCurrentValue(OrbitRollProperty, NormalizeAngle(OrbitRoll + OrbitControlStep));
                break;
            case OrbitControlAction.Reset:
                SetCurrentValue(OrbitYawProperty, 0d);
                SetCurrentValue(OrbitPitchProperty, 0d);
                SetCurrentValue(OrbitRollProperty, 0d);
                break;
        }

        InvalidateVisual();
    }

    private void DrawOrbitControl(DrawingContext context, Rect viewport, OrbitTransform orbit)
    {
        var layout = CreateOrbitControlLayout(viewport);
        if (layout.IsEmpty)
        {
            return;
        }

        var outerFill = new SolidColorBrush(Color.FromArgb(168, 30, 30, 30));
        var innerFill = new SolidColorBrush(Color.FromArgb(190, 44, 44, 44));
        var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(205, 215, 215, 215)), 1);
        context.DrawEllipse(outerFill, ringPen, layout.Center, layout.OuterRadius, layout.OuterRadius);
        context.DrawEllipse(innerFill, ringPen, layout.Center, layout.InnerRadius, layout.InnerRadius);

        DrawOrbitAxisTriad(context, layout, orbit);
        DrawOrbitActionButton(context, layout.UpButton, layout.ButtonRadius, "^", OrbitControlAction.PitchUp);
        DrawOrbitActionButton(context, layout.DownButton, layout.ButtonRadius, "v", OrbitControlAction.PitchDown);
        DrawOrbitActionButton(context, layout.LeftButton, layout.ButtonRadius, "<", OrbitControlAction.YawLeft);
        DrawOrbitActionButton(context, layout.RightButton, layout.ButtonRadius, ">", OrbitControlAction.YawRight);
        DrawOrbitActionButton(context, layout.RollLeftButton, layout.ButtonRadius, "L", OrbitControlAction.RollLeft);
        DrawOrbitActionButton(context, layout.RollRightButton, layout.ButtonRadius, "R", OrbitControlAction.RollRight);
        DrawOrbitActionButton(context, layout.Center, layout.InnerRadius, "0", OrbitControlAction.Reset);
    }

    private void DrawOrbitActionButton(
        DrawingContext context,
        Point center,
        double radius,
        string label,
        OrbitControlAction action)
    {
        var hoverAction = HitTestOrbitControl(Bounds, _pointerPosition);
        var isActive = action == hoverAction || (_isOrbitControlDragging && action == OrbitControlAction.OrbitDrag);
        var fill = new SolidColorBrush(isActive ? Color.FromArgb(220, 80, 120, 190) : Color.FromArgb(180, 56, 56, 56));
        var border = new Pen(new SolidColorBrush(Color.FromArgb(220, 228, 228, 228)), isActive ? 1.5 : 1);
        context.DrawEllipse(fill, border, center, radius, radius);

        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            10,
            Brushes.White);
        var textPoint = new Point(center.X - (text.Width * 0.5), center.Y - (text.Height * 0.5));
        context.DrawText(text, textPoint);
    }

    private static void DrawOrbitAxisTriad(DrawingContext context, OrbitControlLayout layout, OrbitTransform orbit)
    {
        const double axisScale = 0.56;
        var length = layout.OuterRadius * axisScale;
        DrawAxis(context, layout.Center, length, RotateUnitVector(1, 0, 0, orbit), "X", Color.FromRgb(232, 106, 106));
        DrawAxis(context, layout.Center, length, RotateUnitVector(0, -1, 0, orbit), "Y", Color.FromRgb(103, 196, 129));
        DrawAxis(context, layout.Center, length, RotateUnitVector(0, 0, 1, orbit), "Z", Color.FromRgb(111, 176, 232));
    }

    private static void DrawAxis(
        DrawingContext context,
        Point center,
        double length,
        RotatedVector axis,
        string label,
        Color color)
    {
        var end = new Point(center.X + (axis.X * length), center.Y + (axis.Y * length));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)), 2);
        context.DrawLine(pen, center, end);
        context.DrawEllipse(new SolidColorBrush(color), null, end, 3, 3);

        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            9,
            new SolidColorBrush(color));
        context.DrawText(text, new Point(end.X + 3, end.Y - (text.Height * 0.5)));
    }

    private static RotatedVector RotateUnitVector(double x, double y, double z, OrbitTransform orbit)
    {
        var yawX = (x * orbit.CosYaw) + (z * orbit.SinYaw);
        var yawZ = (-x * orbit.SinYaw) + (z * orbit.CosYaw);
        var pitchY = (y * orbit.CosPitch) - (yawZ * orbit.SinPitch);
        var pitchZ = (y * orbit.SinPitch) + (yawZ * orbit.CosPitch);
        var rollX = (yawX * orbit.CosRoll) - (pitchY * orbit.SinRoll);
        var rollY = (yawX * orbit.SinRoll) + (pitchY * orbit.CosRoll);
        return new RotatedVector(rollX, rollY, pitchZ);
    }

    private static void DrawProjection(
        DrawingContext context,
        ProjectedNode projection,
        bool isSelected,
        double layerOpacityFactor,
        IImage? rootSnapshot,
        Rect snapshotBounds)
    {
        IDisposable? opacityScope = null;
        if (!isSelected)
        {
            var clampedOpacity = Math.Clamp(layerOpacityFactor, 0.04, 1);
            if (clampedOpacity < 0.999)
            {
                opacityScope = context.PushOpacity(clampedOpacity);
            }
        }

        try
        {
            var front = projection.Front;
            var node = projection.Node;
            var color = ResolveNodeColor(node);
            var faceBrush = new SolidColorBrush(color);
            var frontFace = CreateQuadGeometry(front);

            var renderedFromSnapshot = projection.UseSnapshot
                && rootSnapshot is not null
                && TryDrawSnapshotFrontFace(context, front, node.BoundsRect, snapshotBounds, rootSnapshot);
            if (renderedFromSnapshot)
            {
                context.DrawGeometry(new SolidColorBrush(Color.FromArgb(24, color.R, color.G, color.B)), null, frontFace);
            }
            else
            {
                context.DrawGeometry(faceBrush, null, frontFace);
            }

            var stroke = isSelected
                ? new Pen(Brushes.Gold, 2)
                : new Pen(new SolidColorBrush(Color.FromArgb(148, 30, 30, 30)), 1);
            context.DrawGeometry(null, stroke, frontFace);
        }
        finally
        {
            opacityScope?.Dispose();
        }
    }

    private void DrawHud(DrawingContext context, Rect viewport)
    {
        var isFlat2D = FlattenTo2D;
        var zoomText = Zoom.ToString("0.00", CultureInfo.InvariantCulture);
        var panTextX = _panOffset.X.ToString("0", CultureInfo.InvariantCulture);
        var panTextY = _panOffset.Y.ToString("0", CultureInfo.InvariantCulture);
        var yaw = OrbitYaw.ToString("0.0", CultureInfo.InvariantCulture);
        var pitch = OrbitPitch.ToString("0.0", CultureInfo.InvariantCulture);
        var roll = OrbitRoll.ToString("0.0", CultureInfo.InvariantCulture);
        string[] lines;
        if (isFlat2D)
        {
            var wrapText = Flat2DMaxLayersPerRow > 0
                ? "Rows: max " + Flat2DMaxLayersPerRow + " layers"
                : "Rows: single row";
            lines = new[]
            {
                "2D Layer View",
                "Zoom: " + zoomText + "x",
                "Pan: " + panTextX + ", " + panTextY,
                "Depth layers stacked left-to-right",
                wrapText,
                "R/Home/Ctrl+0 reset | Arrows pan | +/- zoom"
            };
        }
        else
        {
            lines = new[]
            {
                "3D View",
                "Zoom: " + zoomText + "x",
                "Pan: " + panTextX + ", " + panTextY,
                "Orbit Y/P/R: " + yaw + ", " + pitch + ", " + roll,
                "Right-drag orbit | Shift+right-drag roll",
                "Bottom-right gizmo: click/drag for orbit",
                "R/Home/Ctrl+0 reset | Arrows pan | +/- zoom"
            };
        }

        var maxWidth = 0d;
        var totalHeight = 0d;
        var formattedLines = new FormattedText[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var text = new FormattedText(
                lines[i],
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                i == 0 ? 11 : 10,
                Brushes.White);
            formattedLines[i] = text;
            maxWidth = Math.Max(maxWidth, text.Width);
            totalHeight += text.Height;
            if (i < lines.Length - 1)
            {
                totalHeight += 2;
            }
        }

        const double padding = 6;
        var hudBounds = new Rect(
            x: 8,
            y: 8,
            width: maxWidth + (padding * 2),
            height: totalHeight + (padding * 2));
        var hudBackground = new SolidColorBrush(Color.FromArgb(166, 22, 22, 22));
        var hudBorder = new Pen(new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)), 1);
        context.DrawRectangle(hudBackground, hudBorder, hudBounds, 4);

        var y = hudBounds.Y + padding;
        foreach (var text in formattedLines)
        {
            context.DrawText(text, new Point(hudBounds.X + padding, y));
            y += text.Height + 2;
        }

        if (!IsKeyboardFocusWithin)
        {
            var tip = new FormattedText(
                "Click view to enable keyboard shortcuts",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                10,
                Brushes.White);
            var tipBounds = new Rect(
                Math.Max(8, viewport.Width - tip.Width - 18),
                8,
                tip.Width + 10,
                tip.Height + 6);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(140, 32, 32, 32)), null, tipBounds, 3);
            context.DrawText(tip, new Point(tipBounds.X + 5, tipBounds.Y + 3));
        }
    }

    private static Geometry CreateQuadGeometry(QuadPoints quad)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(quad.P1, isFilled: true);
            ctx.LineTo(quad.P2);
            ctx.LineTo(quad.P3);
            ctx.LineTo(quad.P4);
            ctx.EndFigure(isClosed: true);
        }

        return geometry;
    }

    private static Rect CreateHitRect(QuadPoints front)
    {
        var left = Math.Min(Math.Min(front.P1.X, front.P2.X), Math.Min(front.P3.X, front.P4.X)) - 2;
        var top = Math.Min(Math.Min(front.P1.Y, front.P2.Y), Math.Min(front.P3.Y, front.P4.Y)) - 2;
        var right = Math.Max(Math.Max(front.P1.X, front.P2.X), Math.Max(front.P3.X, front.P4.X)) + 2;
        var bottom = Math.Max(Math.Max(front.P1.Y, front.P2.Y), Math.Max(front.P3.Y, front.P4.Y)) + 2;
        return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static Color ResolveNodeColor(Elements3DNodeViewModel node)
    {
        if (TryResolveVisualColor(node.Visual, out var visualColor))
        {
            return ApplyNodeOpacity(visualColor, node.IsVisible, node.Opacity);
        }

        return CreateFallbackNodeColor(node.Depth, node.IsVisible, node.Opacity);
    }

    private static bool TryResolveVisualColor(Visual visual, out Color color)
    {
        switch (visual)
        {
            case Border border when TryExtractBrushColor(border.Background, out color):
                return true;
            case Border border when TryExtractBrushColor(border.BorderBrush, out color):
                return true;
            case Panel panel when TryExtractBrushColor(panel.Background, out color):
                return true;
            case Shape shape when TryExtractBrushColor(shape.Fill, out color):
                return true;
            case Shape shape when TryExtractBrushColor(shape.Stroke, out color):
                return true;
            case TextBlock textBlock when TryExtractBrushColor(textBlock.Foreground, out color):
                return true;
            case TemplatedControl templatedControl when TryExtractBrushColor(templatedControl.Background, out color):
                return true;
            case TemplatedControl templatedControl when TryExtractBrushColor(templatedControl.Foreground, out color):
                return true;
            default:
                color = default;
                return false;
        }
    }

    private static bool TryExtractBrushColor(IBrush? brush, out Color color)
    {
        switch (brush)
        {
            case ISolidColorBrush solid:
                color = solid.Color;
                return true;
            case IGradientBrush gradient when gradient.GradientStops.Count > 0:
            {
                var totalWeight = 0d;
                var weightedA = 0d;
                var weightedR = 0d;
                var weightedG = 0d;
                var weightedB = 0d;
                for (var i = 0; i < gradient.GradientStops.Count; i++)
                {
                    var stop = gradient.GradientStops[i];
                    var weight = 1d;
                    totalWeight += weight;
                    weightedA += stop.Color.A * weight;
                    weightedR += stop.Color.R * weight;
                    weightedG += stop.Color.G * weight;
                    weightedB += stop.Color.B * weight;
                }

                color = Color.FromArgb(
                    (byte)Math.Clamp(weightedA / totalWeight, 0, 255),
                    (byte)Math.Clamp(weightedR / totalWeight, 0, 255),
                    (byte)Math.Clamp(weightedG / totalWeight, 0, 255),
                    (byte)Math.Clamp(weightedB / totalWeight, 0, 255));
                return true;
            }
            default:
                color = default;
                return false;
        }
    }

    private static Color ApplyNodeOpacity(Color source, bool isVisible, double opacity)
    {
        var visibilityFactor = isVisible ? 1d : 0.45d;
        var nodeOpacity = Math.Clamp(opacity, 0, 1);
        var sourceAlpha = source.A / 255d;
        var alpha = (byte)Math.Clamp(220d * sourceAlpha * nodeOpacity * visibilityFactor, 20, 220);
        return Color.FromArgb(alpha, source.R, source.G, source.B);
    }

    private static Color CreateFallbackNodeColor(int depth, bool isVisible, double opacity)
    {
        var baseChannel = 110 + Math.Min(80, depth * 8);
        var blue = Math.Clamp(baseChannel + 18, 0, 255);
        var channel = isVisible ? baseChannel : (int)(baseChannel * 0.75);
        var alpha = (byte)Math.Clamp((isVisible ? 165 : 95) * Math.Clamp(opacity, 0, 1), 20, 210);
        return Color.FromArgb(alpha, (byte)channel, (byte)channel, (byte)blue);
    }

    internal static double ComputeFrontLayerOpacityFactor(double layerDepth, double? selectedLayerDepth, bool isSelected)
    {
        if (isSelected || selectedLayerDepth is null)
        {
            return 1;
        }

        // Lower projected depth renders in front after painter ordering.
        return layerDepth < selectedLayerDepth.Value ? 0.12 : 1;
    }

    internal static OrbitControlAction HitTestOrbitControl(Rect viewport, Point point)
    {
        var layout = CreateOrbitControlLayout(viewport);
        if (layout.IsEmpty || !layout.Bounds.Contains(point))
        {
            return OrbitControlAction.None;
        }

        if (IsInsideCircle(point, layout.Center, layout.InnerRadius))
        {
            return OrbitControlAction.Reset;
        }

        if (IsInsideCircle(point, layout.LeftButton, layout.ButtonRadius))
        {
            return OrbitControlAction.YawLeft;
        }

        if (IsInsideCircle(point, layout.RightButton, layout.ButtonRadius))
        {
            return OrbitControlAction.YawRight;
        }

        if (IsInsideCircle(point, layout.UpButton, layout.ButtonRadius))
        {
            return OrbitControlAction.PitchUp;
        }

        if (IsInsideCircle(point, layout.DownButton, layout.ButtonRadius))
        {
            return OrbitControlAction.PitchDown;
        }

        if (IsInsideCircle(point, layout.RollLeftButton, layout.ButtonRadius))
        {
            return OrbitControlAction.RollLeft;
        }

        if (IsInsideCircle(point, layout.RollRightButton, layout.ButtonRadius))
        {
            return OrbitControlAction.RollRight;
        }

        var distance = Distance(point, layout.Center);
        if (distance <= layout.OuterRadius && distance >= layout.InnerRadius)
        {
            return OrbitControlAction.OrbitDrag;
        }

        return OrbitControlAction.None;
    }

    internal static OrbitControlLayout CreateOrbitControlLayout(Rect viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return default;
        }

        var radius = Math.Clamp(Math.Min(viewport.Width, viewport.Height) * 0.1, 32, 50);
        var center = new Point(
            viewport.Width - radius - OrbitControlMargin,
            viewport.Height - radius - OrbitControlMargin);
        if (center.X <= radius || center.Y <= radius)
        {
            return default;
        }

        var innerRadius = Math.Clamp(radius * 0.28, 10, 16);
        var buttonRadius = Math.Clamp(radius * 0.2, 8, 12);
        var buttonOffset = radius * 0.68;
        var rollOffsetX = radius * 0.52;
        var rollOffsetY = radius * 0.52;
        var bounds = new Rect(
            center.X - radius - buttonRadius - 2,
            center.Y - radius - buttonRadius - 2,
            (radius * 2) + (buttonRadius * 2) + 4,
            (radius * 2) + (buttonRadius * 2) + 4);
        return new OrbitControlLayout(
            Center: center,
            OuterRadius: radius,
            InnerRadius: innerRadius,
            ButtonRadius: buttonRadius,
            Bounds: bounds,
            LeftButton: new Point(center.X - buttonOffset, center.Y),
            RightButton: new Point(center.X + buttonOffset, center.Y),
            UpButton: new Point(center.X, center.Y - buttonOffset),
            DownButton: new Point(center.X, center.Y + buttonOffset),
            RollLeftButton: new Point(center.X - rollOffsetX, center.Y + rollOffsetY),
            RollRightButton: new Point(center.X + rollOffsetX, center.Y + rollOffsetY));
    }

    private static bool IsInsideCircle(Point point, Point center, double radius)
    {
        return Distance(point, center) <= radius;
    }

    private static double Distance(Point left, Point right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    internal static bool IsResetShortcut(Key key, KeyModifiers modifiers)
    {
        if (key == Key.R || key == Key.Home)
        {
            return true;
        }

        if ((modifiers & KeyModifiers.Control) == 0)
        {
            return false;
        }

        return key == Key.D0 || key == Key.NumPad0;
    }

    private bool TryHandleKeyboardZoom(Key key)
    {
        if (key != Key.OemPlus && key != Key.Add && key != Key.OemMinus && key != Key.Subtract)
        {
            return false;
        }

        var factor = key is Key.OemPlus or Key.Add ? 1.1 : 0.9;
        SetCurrentValue(ZoomProperty, Math.Clamp(Zoom * factor, MinZoom, MaxZoom));
        return true;
    }

    private bool TryHandleKeyboardPan(Key key, KeyModifiers modifiers)
    {
        var step = (modifiers & KeyModifiers.Shift) != 0 ? KeyboardPanStep * 2 : KeyboardPanStep;
        var delta = key switch
        {
            Key.Left => new Vector(-step, 0),
            Key.Right => new Vector(step, 0),
            Key.Up => new Vector(0, -step),
            Key.Down => new Vector(0, step),
            _ => default
        };

        if (delta == default)
        {
            return false;
        }

        _panOffset += delta;
        InvalidateVisual();
        return true;
    }

    private bool TryHandleKeyboardOrbit(Key key, KeyModifiers modifiers)
    {
        const double step = 5;
        var fineStep = (modifiers & KeyModifiers.Shift) != 0 ? 2 : step;
        var handled = true;
        switch (key)
        {
            case Key.A:
                SetCurrentValue(OrbitYawProperty, NormalizeAngle(OrbitYaw - fineStep));
                break;
            case Key.D:
                SetCurrentValue(OrbitYawProperty, NormalizeAngle(OrbitYaw + fineStep));
                break;
            case Key.W:
                SetCurrentValue(OrbitPitchProperty, NormalizeAngle(OrbitPitch + fineStep));
                break;
            case Key.S:
                SetCurrentValue(OrbitPitchProperty, NormalizeAngle(OrbitPitch - fineStep));
                break;
            case Key.Q:
                SetCurrentValue(OrbitRollProperty, NormalizeAngle(OrbitRoll - fineStep));
                break;
            case Key.E:
                SetCurrentValue(OrbitRollProperty, NormalizeAngle(OrbitRoll + fineStep));
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            InvalidateVisual();
        }

        return handled;
    }

    private void ResetPanAndZoom()
    {
        _panOffset = default;
        SetCurrentValue(ZoomProperty, 1d);
        SetCurrentValue(OrbitYawProperty, 0d);
        SetCurrentValue(OrbitPitchProperty, 0d);
        SetCurrentValue(OrbitRollProperty, 0d);
        InvalidateVisual();
    }

    private void OnItemsChanged()
    {
        SubscribeToItemsCollection();
        ClearSnapshotCache();
        InvalidateVisual();
    }

    private void SubscribeToItemsCollection()
    {
        UnsubscribeFromItemsCollection();
        _observedItems = Items as INotifyCollectionChanged;
        if (_observedItems is not null)
        {
            _observedItems.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void UnsubscribeFromItemsCollection()
    {
        if (_observedItems is not null)
        {
            _observedItems.CollectionChanged -= OnItemsCollectionChanged;
            _observedItems = null;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearSnapshotCache();
        InvalidateVisual();
    }

    private bool TryResolveWheelAnchor(
        Point pointer,
        in LayoutMetrics layout,
        double scale,
        out double worldX,
        out double worldY,
        out double depth)
    {
        if (scale <= 0)
        {
            worldX = default;
            worldY = default;
            depth = default;
            return false;
        }

        if (TryHitTestProjection(pointer, out var projected))
        {
            if (TryResolveWorldPointFromProjectedQuad(pointer, projected.Front, projected.LayoutRect, out worldX, out worldY))
            {
                depth = projected.Node.Depth;
                return true;
            }

            var front = projected.FrontRect;
            var clampedX = Math.Clamp(pointer.X, front.X, front.Right);
            var clampedY = Math.Clamp(pointer.Y, front.Y, front.Bottom);
            worldX = projected.LayoutRect.X + ((clampedX - front.X) / scale);
            worldY = projected.LayoutRect.Y + ((clampedY - front.Y) / scale);
            depth = projected.Node.Depth;
            return true;
        }

        if (FlattenTo2D)
        {
            var flatOrigin = CalculateOrigin(layout, scale, _panOffset);
            worldX = layout.MinX + ((pointer.X - flatOrigin.X) / scale);
            worldY = layout.MinY + ((pointer.Y - flatOrigin.Y) / scale);
            depth = 0;
            return true;
        }

        var depthLayer = ResolveFallbackDepth(layout);
        if (IsOrbitActive())
        {
            if (TryResolveOrbitAnchorOnDepthPlane(pointer, layout, scale, depthLayer, out worldX, out worldY, out depth))
            {
                return true;
            }
        }

        var origin = CalculateOrigin(layout, scale, _panOffset);
        worldX = layout.MinX + ((pointer.X - origin.X - (depthLayer * layout.DepthOffsetX)) / scale);
        worldY = layout.MinY + ((pointer.Y - origin.Y + (depthLayer * layout.DepthOffsetY)) / scale);
        depth = depthLayer;
        return true;
    }

    private bool TryResolveOrbitAnchorOnDepthPlane(
        Point pointer,
        in LayoutMetrics layout,
        double scale,
        double depthLayer,
        out double worldX,
        out double worldY,
        out double depth)
    {
        var origin = CalculateOrigin(layout, scale, _panOffset);
        var sceneCenterX = origin.X + ((layout.ContentWidth * scale) * 0.5) + ((layout.MaxDepth * layout.DepthOffsetX) * 0.5);
        var sceneCenterY = origin.Y + ((layout.ContentHeight * scale) * 0.5) - ((layout.MaxDepth * layout.DepthOffsetY) * 0.5);
        var depthStep = Math.Max(1, DepthSpacing);
        var sceneCenterZ = (layout.MaxDepth * depthStep) * 0.5;
        var planeZ = depthLayer * depthStep;
        var orbit = CreateOrbitTransform(OrbitYaw, OrbitPitch, OrbitRoll);
        if (!TryInverseOrbitPoint(pointer, sceneCenterX, sceneCenterY, sceneCenterZ, planeZ, orbit, out var planeX, out var planeY))
        {
            worldX = default;
            worldY = default;
            depth = default;
            return false;
        }

        worldX = layout.MinX + ((planeX - origin.X - (depthLayer * layout.DepthOffsetX)) / scale);
        worldY = layout.MinY + ((planeY - origin.Y + (depthLayer * layout.DepthOffsetY)) / scale);
        depth = depthLayer;
        return true;
    }

    private Vector CalculatePanForAnchorWithOrbit(
        in LayoutMetrics metrics,
        double scale,
        Point pointer,
        double worldX,
        double worldY,
        double depth)
    {
        if (FlattenTo2D)
        {
            return CalculatePanForAnchor(metrics, scale, pointer, worldX, worldY, depth: 0);
        }

        var origin = CalculateOrigin(metrics, scale, default);
        var sceneCenterX = origin.X + ((metrics.ContentWidth * scale) * 0.5) + ((metrics.MaxDepth * metrics.DepthOffsetX) * 0.5);
        var sceneCenterY = origin.Y + ((metrics.ContentHeight * scale) * 0.5) - ((metrics.MaxDepth * metrics.DepthOffsetY) * 0.5);
        var depthStep = Math.Max(1, DepthSpacing);
        var sceneCenterZ = (metrics.MaxDepth * depthStep) * 0.5;
        var planeZ = depth * depthStep;
        var projected = ProjectWorldPoint(metrics, scale, default, worldX, worldY, depth);
        var orbit = CreateOrbitTransform(OrbitYaw, OrbitPitch, OrbitRoll);
        var transformed = TransformPoint(projected, planeZ, sceneCenterX, sceneCenterY, sceneCenterZ, orbit).Point;
        return new Vector(pointer.X - transformed.X, pointer.Y - transformed.Y);
    }

    private double ResolveFallbackDepth(in LayoutMetrics layout)
    {
        if (SelectedItem is not null)
        {
            return Math.Clamp(SelectedItem.Depth, 0, layout.MaxDepth);
        }

        return layout.MaxDepth * 0.5;
    }

    internal static bool TryResolveWorldPointFromProjectedQuad(
        Point pointer,
        Point quadP1,
        Point quadP2,
        Point quadP4,
        Rect nodeBounds,
        out double worldX,
        out double worldY)
    {
        if (!TryProjectPointToQuadUv(pointer, quadP1, quadP2, quadP4, out var u, out var v))
        {
            worldX = default;
            worldY = default;
            return false;
        }

        var clampedU = Math.Clamp(u, 0, 1);
        var clampedV = Math.Clamp(v, 0, 1);
        worldX = nodeBounds.X + (clampedU * nodeBounds.Width);
        worldY = nodeBounds.Y + (clampedV * nodeBounds.Height);
        return true;
    }

    private static bool TryResolveWorldPointFromProjectedQuad(
        Point pointer,
        QuadPoints quad,
        Rect nodeBounds,
        out double worldX,
        out double worldY)
    {
        return TryResolveWorldPointFromProjectedQuad(pointer, quad.P1, quad.P2, quad.P4, nodeBounds, out worldX, out worldY);
    }

    internal static bool TryProjectPointToQuadUv(
        Point pointer,
        Point quadP1,
        Point quadP2,
        Point quadP4,
        out double u,
        out double v)
    {
        var basisX = quadP2 - quadP1;
        var basisY = quadP4 - quadP1;
        var point = pointer - quadP1;
        var determinant = (basisX.X * basisY.Y) - (basisX.Y * basisY.X);
        if (Math.Abs(determinant) < 0.0001)
        {
            u = default;
            v = default;
            return false;
        }

        u = ((point.X * basisY.Y) - (point.Y * basisY.X)) / determinant;
        v = ((basisX.X * point.Y) - (basisX.Y * point.X)) / determinant;
        return true;
    }

    private bool TryHitTestProjection(Point point, out ProjectedNode projectedNode)
    {
        for (var i = _projectedNodes.Count - 1; i >= 0; i--)
        {
            var projected = _projectedNodes[i];
            if (projected.HitRect.Contains(point)
                && IsPointInQuad(point, projected.Front))
            {
                projectedNode = projected;
                return true;
            }
        }

        projectedNode = default;
        return false;
    }

    private Elements3DNodeViewModel[] GetOrderedNodes()
    {
        var source = Items ?? Enumerable.Empty<Elements3DNodeViewModel>();
        return source
            .Where(node => ShowInvisible || node.IsVisible)
            .Where(node => node.BoundsRect.Width >= 0 && node.BoundsRect.Height >= 0)
            .OrderBy(node => node.Depth)
            // We draw in reverse depth order to keep near layers in front.
            // Using descending z-index here preserves z-index stacking after reversal.
            .ThenByDescending(node => node.ZIndex)
            .ToArray();
    }

    private bool TryCreateLayoutMetrics(
        IReadOnlyList<Elements3DNodeViewModel> nodes,
        Rect viewport,
        out LayoutMetrics metrics)
    {
        if (nodes.Count == 0 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            metrics = default;
            return false;
        }

        if (FlattenTo2D)
        {
            if (!TryCreateFlatLayerLayouts(nodes, out _, out var flatContentWidth, out var flatContentHeight, out var flatMaxDepth))
            {
                metrics = default;
                return false;
            }

            metrics = new LayoutMetrics(
                viewport,
                MinX: 0,
                MinY: 0,
                ContentWidth: flatContentWidth,
                ContentHeight: flatContentHeight,
                flatMaxDepth,
                DepthOffsetX: 0,
                DepthOffsetY: 0);
            return true;
        }

        var minX = nodes.Min(node => node.BoundsRect.X);
        var minY = nodes.Min(node => node.BoundsRect.Y);
        var maxX = nodes.Max(node => node.BoundsRect.Right);
        var maxY = nodes.Max(node => node.BoundsRect.Bottom);
        var maxDepth = Math.Max(1, nodes.Max(node => node.Depth));

        var contentWidth = Math.Max(1, maxX - minX);
        var contentHeight = Math.Max(1, maxY - minY);
        var spacing = Math.Max(0, DepthSpacing);
        var tilt = Math.Clamp(Tilt, 0, 1);
        var depthOffsetX = spacing * tilt;
        var depthOffsetY = spacing * (1 - tilt);

        metrics = new LayoutMetrics(
            viewport,
            minX,
            minY,
            contentWidth,
            contentHeight,
            maxDepth,
            depthOffsetX,
            depthOffsetY);
        return true;
    }

    private static double CalculateScale(in LayoutMetrics metrics, double zoom, bool isFlat2D)
    {
        const double padding = 16;
        var availableWidth = Math.Max(1, metrics.Viewport.Width - (padding * 2) - (metrics.MaxDepth * metrics.DepthOffsetX));
        var availableHeight = Math.Max(1, metrics.Viewport.Height - (padding * 2) - (metrics.MaxDepth * metrics.DepthOffsetY));
        var minimumFitScale = isFlat2D ? Flat2DMinimumFitScale : 0.05;
        var fitScale = Math.Max(minimumFitScale, Math.Min(availableWidth / metrics.ContentWidth, availableHeight / metrics.ContentHeight));
        return fitScale * Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    private bool TryCreateFlatLayerLayouts(
        IReadOnlyList<Elements3DNodeViewModel> nodes,
        out IReadOnlyDictionary<int, FlatLayerLayout> flatLayers,
        out double contentWidth,
        out double contentHeight,
        out int maxDepth)
    {
        if (nodes.Count == 0)
        {
            flatLayers = default!;
            contentWidth = default;
            contentHeight = default;
            maxDepth = default;
            return false;
        }

        var boundsByDepth = new Dictionary<int, FlatLayerBounds>();
        maxDepth = 1;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            maxDepth = Math.Max(maxDepth, node.Depth);
            var bounds = node.BoundsRect;
            if (boundsByDepth.TryGetValue(node.Depth, out var existing))
            {
                boundsByDepth[node.Depth] = new FlatLayerBounds(
                    Math.Min(existing.MinX, bounds.X),
                    Math.Min(existing.MinY, bounds.Y),
                    Math.Max(existing.MaxX, bounds.Right),
                    Math.Max(existing.MaxY, bounds.Bottom));
                continue;
            }

            boundsByDepth[node.Depth] = new FlatLayerBounds(bounds.X, bounds.Y, bounds.Right, bounds.Bottom);
        }

        if (boundsByDepth.Count == 0)
        {
            flatLayers = default!;
            contentWidth = default;
            contentHeight = default;
            maxDepth = default;
            return false;
        }

        var orderedDepths = boundsByDepth.Keys.OrderBy(x => x).ToArray();
        var layers = new FlatLayerPackingItem[orderedDepths.Length];
        for (var i = 0; i < orderedDepths.Length; i++)
        {
            var depth = orderedDepths[i];
            var bounds = boundsByDepth[depth];
            layers[i] = new FlatLayerPackingItem(
                depth,
                Math.Max(1, bounds.MaxX - bounds.MinX),
                Math.Max(1, bounds.MaxY - bounds.MinY));
        }

        var layerGap = ResolveFlatLayerGap(DepthSpacing);
        var wrapped = PackFlatLayers(layers, layerGap, Flat2DMaxLayersPerRow);
        var layoutByDepth = new Dictionary<int, FlatLayerLayout>(wrapped.Items.Count);
        for (var i = 0; i < wrapped.Items.Count; i++)
        {
            var item = wrapped.Items[i];
            var depth = item.Depth;
            var bounds = boundsByDepth[depth];
            layoutByDepth[depth] = new FlatLayerLayout(
                depth,
                bounds.MinX,
                bounds.MinY,
                item.Width,
                item.Height,
                item.X,
                item.Y);
        }

        contentWidth = wrapped.ContentWidth;
        contentHeight = wrapped.ContentHeight;
        flatLayers = layoutByDepth;
        return true;
    }

    private static Rect CreateFlatNodeLayoutRect(
        Elements3DNodeViewModel node,
        IReadOnlyDictionary<int, FlatLayerLayout> flatLayers)
    {
        if (!flatLayers.TryGetValue(node.Depth, out var layer))
        {
            return node.BoundsRect;
        }

        var width = Math.Max(1, node.BoundsRect.Width);
        var height = Math.Max(1, node.BoundsRect.Height);
        var x = layer.StartX + (node.BoundsRect.X - layer.MinX);
        var y = layer.StartY + (node.BoundsRect.Y - layer.MinY);
        return new Rect(x, y, width, height);
    }

    private static double ResolveFlatLayerGap(double spacing)
    {
        var requested = Math.Max(Flat2DMinimumLayerGap, spacing);
        return Math.Clamp(requested, Flat2DMinimumLayerGap, Flat2DMaximumLayerGap);
    }

    internal static FlatLayerPackingResult PackFlatLayers(
        IReadOnlyList<FlatLayerPackingItem> items,
        double layerGap,
        int maxLayersPerRow)
    {
        if (items.Count == 0)
        {
            return new FlatLayerPackingResult(Array.Empty<FlatLayerPackedItem>(), 1, 1, 0);
        }

        var clampedGap = Math.Max(0, layerGap);
        var wrapEnabled = maxLayersPerRow > 0;
        var layersPerRow = wrapEnabled ? maxLayersPerRow : int.MaxValue;

        var drafts = new FlatLayerPackedDraft[items.Count];
        var rowWidths = new List<double>();
        var rowHeights = new List<double>();
        var row = 0;
        var rowItemCount = 0;
        var rowWidth = 0d;
        var rowHeight = 1d;
        var draftCount = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (wrapEnabled && rowItemCount >= layersPerRow)
            {
                rowWidths.Add(Math.Max(1, rowWidth));
                rowHeights.Add(Math.Max(1, rowHeight));
                row++;
                rowItemCount = 0;
                rowWidth = 0;
                rowHeight = 1;
            }

            var width = Math.Max(1, item.Width);
            var height = Math.Max(1, item.Height);
            var startX = rowItemCount == 0 ? 0 : rowWidth + clampedGap;
            drafts[draftCount++] = new FlatLayerPackedDraft(item.Depth, row, startX, width, height);
            rowWidth = startX + width;
            rowHeight = Math.Max(rowHeight, height);
            rowItemCount++;
        }

        rowWidths.Add(Math.Max(1, rowWidth));
        rowHeights.Add(Math.Max(1, rowHeight));

        var rowCount = rowWidths.Count;
        var contentWidth = 1d;
        for (var i = 0; i < rowCount; i++)
        {
            contentWidth = Math.Max(contentWidth, rowWidths[i]);
        }

        var rowTops = new double[rowCount];
        var rowInsets = new double[rowCount];
        var y = 0d;
        for (var i = 0; i < rowCount; i++)
        {
            rowTops[i] = y;
            rowInsets[i] = (contentWidth - rowWidths[i]) * 0.5;
            y += rowHeights[i];
            if (i < rowCount - 1)
            {
                y += clampedGap;
            }
        }

        var contentHeight = Math.Max(1, y);
        var packed = new FlatLayerPackedItem[draftCount];
        for (var i = 0; i < draftCount; i++)
        {
            var draft = drafts[i];
            var rowTop = rowTops[draft.Row];
            var rowInset = rowInsets[draft.Row];
            var yOffset = rowTop + ((rowHeights[draft.Row] - draft.Height) * 0.5);
            packed[i] = new FlatLayerPackedItem(
                draft.Depth,
                draft.Row,
                rowInset + draft.X,
                yOffset,
                draft.Width,
                draft.Height);
        }

        return new FlatLayerPackingResult(packed, contentWidth, contentHeight, rowCount);
    }

    internal static Point CalculateOrigin(in LayoutMetrics metrics, double scale, Vector panOffset)
    {
        var projectedWidth = (metrics.ContentWidth * scale) + (metrics.MaxDepth * metrics.DepthOffsetX);
        var projectedHeight = (metrics.ContentHeight * scale) + (metrics.MaxDepth * metrics.DepthOffsetY);
        var originX = ((metrics.Viewport.Width - projectedWidth) * 0.5) + panOffset.X;
        var originY = ((metrics.Viewport.Height - projectedHeight) * 0.5) + panOffset.Y + (metrics.MaxDepth * metrics.DepthOffsetY);
        return new Point(originX, originY);
    }

    internal static Point ProjectWorldPoint(
        in LayoutMetrics metrics,
        double scale,
        Vector panOffset,
        double worldX,
        double worldY,
        double depth)
    {
        var origin = CalculateOrigin(metrics, scale, panOffset);
        var x = origin.X + ((worldX - metrics.MinX) * scale) + (depth * metrics.DepthOffsetX);
        var y = origin.Y + ((worldY - metrics.MinY) * scale) - (depth * metrics.DepthOffsetY);
        return new Point(x, y);
    }

    internal static Vector CalculatePanForAnchor(
        in LayoutMetrics metrics,
        double scale,
        Point pointer,
        double worldX,
        double worldY,
        double depth)
    {
        var projectedWidth = (metrics.ContentWidth * scale) + (metrics.MaxDepth * metrics.DepthOffsetX);
        var projectedHeight = (metrics.ContentHeight * scale) + (metrics.MaxDepth * metrics.DepthOffsetY);
        var centerX = (metrics.Viewport.Width - projectedWidth) * 0.5;
        var centerY = (metrics.Viewport.Height - projectedHeight) * 0.5;

        var panX = pointer.X - centerX - ((worldX - metrics.MinX) * scale) - (depth * metrics.DepthOffsetX);
        var panY = pointer.Y - centerY - (metrics.MaxDepth * metrics.DepthOffsetY) - ((worldY - metrics.MinY) * scale) + (depth * metrics.DepthOffsetY);
        return new Vector(panX, panY);
    }

    private OrbitTransform CreateOrbitTransform(double yawDegrees, double pitchDegrees, double rollDegrees)
    {
        var yawRadians = DegreesToRadians(NormalizeAngle(yawDegrees));
        var pitchRadians = DegreesToRadians(NormalizeAngle(pitchDegrees));
        var rollRadians = DegreesToRadians(NormalizeAngle(rollDegrees));
        return new OrbitTransform(
            Math.Sin(yawRadians),
            Math.Cos(yawRadians),
            Math.Sin(pitchRadians),
            Math.Cos(pitchRadians),
            Math.Sin(rollRadians),
            Math.Cos(rollRadians));
    }

    private bool IsOrbitActive()
    {
        const double epsilon = 0.001;
        return Math.Abs(OrbitYaw) > epsilon
               || Math.Abs(OrbitPitch) > epsilon
               || Math.Abs(OrbitRoll) > epsilon;
    }

    private static FlatProjectionTransformResult TransformFlatProjection(
        QuadPoints front,
        double frontZ,
        double centerX,
        double centerY,
        double centerZ,
        OrbitTransform orbit)
    {
        var tf1 = TransformPoint(front.P1, frontZ, centerX, centerY, centerZ, orbit);
        var tf2 = TransformPoint(front.P2, frontZ, centerX, centerY, centerZ, orbit);
        var tf3 = TransformPoint(front.P3, frontZ, centerX, centerY, centerZ, orbit);
        var tf4 = TransformPoint(front.P4, frontZ, centerX, centerY, centerZ, orbit);
        var depth = (tf1.Z + tf2.Z + tf3.Z + tf4.Z) / 4d;
        return new FlatProjectionTransformResult(
            new QuadPoints(tf1.Point, tf2.Point, tf3.Point, tf4.Point),
            depth);
    }

    private static ProjectedPoint3D TransformPoint(
        Point point,
        double z,
        double centerX,
        double centerY,
        double centerZ,
        OrbitTransform orbit)
    {
        var x = point.X - centerX;
        var y = point.Y - centerY;
        var depth = z - centerZ;

        var yawX = (x * orbit.CosYaw) + (depth * orbit.SinYaw);
        var yawZ = (-x * orbit.SinYaw) + (depth * orbit.CosYaw);

        var pitchY = (y * orbit.CosPitch) - (yawZ * orbit.SinPitch);
        var pitchZ = (y * orbit.SinPitch) + (yawZ * orbit.CosPitch);

        var rollX = (yawX * orbit.CosRoll) - (pitchY * orbit.SinRoll);
        var rollY = (yawX * orbit.SinRoll) + (pitchY * orbit.CosRoll);

        return new ProjectedPoint3D(
            new Point(centerX + rollX, centerY + rollY),
            centerZ + pitchZ);
    }

    private static bool TryInverseOrbitPoint(
        Point screenPoint,
        double centerX,
        double centerY,
        double centerZ,
        double planeZ,
        OrbitTransform orbit,
        out double x,
        out double y)
    {
        var depth = planeZ - centerZ;
        var m11 = (orbit.CosRoll * orbit.CosYaw) - (orbit.SinRoll * orbit.SinPitch * orbit.SinYaw);
        var m12 = -orbit.SinRoll * orbit.CosPitch;
        var m21 = (orbit.SinRoll * orbit.CosYaw) + (orbit.CosRoll * orbit.SinPitch * orbit.SinYaw);
        var m22 = orbit.CosRoll * orbit.CosPitch;
        var c1 = ((orbit.CosRoll * orbit.SinYaw) + (orbit.SinRoll * orbit.SinPitch * orbit.CosYaw)) * depth;
        var c2 = ((orbit.SinRoll * orbit.SinYaw) - (orbit.CosRoll * orbit.SinPitch * orbit.CosYaw)) * depth;

        var rx = screenPoint.X - centerX - c1;
        var ry = screenPoint.Y - centerY - c2;
        var determinant = (m11 * m22) - (m12 * m21);
        if (Math.Abs(determinant) < 0.0001)
        {
            x = default;
            y = default;
            return false;
        }

        var localX = ((rx * m22) - (ry * m12)) / determinant;
        var localY = ((ry * m11) - (rx * m21)) / determinant;
        x = centerX + localX;
        y = centerY + localY;
        return true;
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360;
        if (normalized > 180)
        {
            normalized -= 360;
        }
        else if (normalized < -180)
        {
            normalized += 360;
        }

        return normalized;
    }

    private static bool IsPointInQuad(Point point, QuadPoints quad)
    {
        return IsPointInTriangle(point, quad.P1, quad.P2, quad.P3)
               || IsPointInTriangle(point, quad.P1, quad.P3, quad.P4);
    }

    private static bool IsPointInTriangle(Point p, Point a, Point b, Point c)
    {
        var denominator = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
        if (Math.Abs(denominator) < 0.0001)
        {
            return false;
        }

        var w1 = (((b.Y - c.Y) * (p.X - c.X)) + ((c.X - b.X) * (p.Y - c.Y))) / denominator;
        var w2 = (((c.Y - a.Y) * (p.X - c.X)) + ((a.X - c.X) * (p.Y - c.Y))) / denominator;
        var w3 = 1 - w1 - w2;
        const double epsilon = -0.0001;
        return w1 >= epsilon && w2 >= epsilon && w3 >= epsilon;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private RenderTargetBitmap? TryGetRootSnapshot(
        IReadOnlyList<Elements3DNodeViewModel> orderedNodes,
        out Rect snapshotBounds)
    {
        snapshotBounds = default;
        if (orderedNodes.Count == 0)
        {
            return null;
        }

        var rootNode = orderedNodes[0];
        var rootVisual = rootNode.Visual;
        var rootBounds = rootNode.BoundsRect;
        if (rootBounds.Width <= 0 || rootBounds.Height <= 0)
        {
            return null;
        }

        // Avoid reentrant rendering when inspecting the diagnostics window itself.
        if (IsInVisualAncestry(rootVisual))
        {
            return null;
        }

        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(rootBounds.Width)),
            Math.Max(1, (int)Math.Ceiling(rootBounds.Height)));
        var now = Environment.TickCount64;
        var needsRefresh =
            _rootSnapshot is null
            || !ReferenceEquals(_snapshotRoot, rootVisual)
            || _snapshotPixelSize != pixelSize
            || !AreRectsClose(_snapshotRootBounds, rootBounds)
            || now - _lastSnapshotTick >= SnapshotRefreshIntervalMs;

        if (needsRefresh && !_isCapturingSnapshot)
        {
            try
            {
                _isCapturingSnapshot = true;
                var snapshot = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
                snapshot.Render(rootVisual);
                _rootSnapshot?.Dispose();
                _rootSnapshot = snapshot;
                _snapshotRoot = rootVisual;
                _snapshotRootBounds = rootBounds;
                _snapshotPixelSize = pixelSize;
                _lastSnapshotTick = now;
            }
            catch
            {
                ClearSnapshotCache();
            }
            finally
            {
                _isCapturingSnapshot = false;
            }
        }

        snapshotBounds = _snapshotRootBounds;
        return _rootSnapshot;
    }

    private static bool TryDrawSnapshotFrontFace(
        DrawingContext context,
        QuadPoints front,
        Rect nodeBounds,
        Rect snapshotBounds,
        IImage snapshot)
    {
        if (nodeBounds.Width <= 0 || nodeBounds.Height <= 0)
        {
            return false;
        }

        var source = new Rect(
            nodeBounds.X - snapshotBounds.X,
            nodeBounds.Y - snapshotBounds.Y,
            nodeBounds.Width,
            nodeBounds.Height);
        var imageWidth = snapshot.Size.Width;
        var imageHeight = snapshot.Size.Height;
        var clippedLeft = Math.Max(0, source.X);
        var clippedTop = Math.Max(0, source.Y);
        var clippedRight = Math.Min(imageWidth, source.Right);
        var clippedBottom = Math.Min(imageHeight, source.Bottom);
        var clippedWidth = clippedRight - clippedLeft;
        var clippedHeight = clippedBottom - clippedTop;
        if (clippedWidth <= 0 || clippedHeight <= 0)
        {
            return false;
        }

        var clippedSource = new Rect(clippedLeft, clippedTop, clippedWidth, clippedHeight);
        var normalizedX = (clippedLeft - source.X) / source.Width;
        var normalizedY = (clippedTop - source.Y) / source.Height;
        var normalizedWidth = clippedWidth / source.Width;
        var normalizedHeight = clippedHeight / source.Height;
        var destination = CreateSubQuad(front, normalizedX, normalizedY, normalizedWidth, normalizedHeight);
        if (Math.Abs(clippedSource.Width) <= 0.0001 || Math.Abs(clippedSource.Height) <= 0.0001)
        {
            return false;
        }

        var destinationWidth = clippedSource.Width;
        var destinationHeight = clippedSource.Height;
        var transform = CreateRectToQuadTransform(destination, destinationWidth, destinationHeight);
        using (context.PushTransform(transform))
        {
            context.DrawImage(snapshot, clippedSource, new Rect(0, 0, destinationWidth, destinationHeight));
        }

        return true;
    }

    private static QuadPoints CreateSubQuad(
        QuadPoints quad,
        double normalizedX,
        double normalizedY,
        double normalizedWidth,
        double normalizedHeight)
    {
        var left = Math.Clamp(normalizedX, 0, 1);
        var top = Math.Clamp(normalizedY, 0, 1);
        var right = Math.Clamp(normalizedX + normalizedWidth, 0, 1);
        var bottom = Math.Clamp(normalizedY + normalizedHeight, 0, 1);
        return new QuadPoints(
            InterpolateQuad(quad, left, top),
            InterpolateQuad(quad, right, top),
            InterpolateQuad(quad, right, bottom),
            InterpolateQuad(quad, left, bottom));
    }

    private static Point InterpolateQuad(QuadPoints quad, double u, double v)
    {
        var ux = quad.P1.X + ((quad.P2.X - quad.P1.X) * u);
        var uy = quad.P1.Y + ((quad.P2.Y - quad.P1.Y) * u);
        return new Point(
            ux + ((quad.P4.X - quad.P1.X) * v),
            uy + ((quad.P4.Y - quad.P1.Y) * v));
    }

    private static Matrix CreateRectToQuadTransform(QuadPoints quad, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return Matrix.Identity;
        }

        var basisX = quad.P2 - quad.P1;
        var basisY = quad.P4 - quad.P1;
        return new Matrix(
            basisX.X / width,
            basisX.Y / width,
            basisY.X / height,
            basisY.Y / height,
            quad.P1.X,
            quad.P1.Y);
    }

    private static bool AreRectsClose(Rect left, Rect right)
    {
        const double epsilon = 0.25;
        return Math.Abs(left.X - right.X) <= epsilon
               && Math.Abs(left.Y - right.Y) <= epsilon
               && Math.Abs(left.Width - right.Width) <= epsilon
               && Math.Abs(left.Height - right.Height) <= epsilon;
    }

    private bool IsInVisualAncestry(Visual visual)
    {
        for (Visual? current = this; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, visual))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearSnapshotCache()
    {
        _rootSnapshot?.Dispose();
        _rootSnapshot = null;
        _snapshotRoot = null;
        _snapshotRootBounds = default;
        _snapshotPixelSize = default;
        _lastSnapshotTick = 0;
    }

    internal readonly record struct LayoutMetrics(
        Rect Viewport,
        double MinX,
        double MinY,
        double ContentWidth,
        double ContentHeight,
        int MaxDepth,
        double DepthOffsetX,
        double DepthOffsetY);

    internal enum OrbitControlAction
    {
        None = 0,
        OrbitDrag = 1,
        YawLeft = 2,
        YawRight = 3,
        PitchUp = 4,
        PitchDown = 5,
        RollLeft = 6,
        RollRight = 7,
        Reset = 8
    }

    internal readonly record struct OrbitControlLayout(
        Point Center,
        double OuterRadius,
        double InnerRadius,
        double ButtonRadius,
        Rect Bounds,
        Point LeftButton,
        Point RightButton,
        Point UpButton,
        Point DownButton,
        Point RollLeftButton,
        Point RollRightButton)
    {
        public bool IsEmpty => OuterRadius <= 0 || ButtonRadius <= 0;
    }

    private readonly record struct FlatLayerBounds(
        double MinX,
        double MinY,
        double MaxX,
        double MaxY);

    private readonly record struct FlatLayerLayout(
        int Depth,
        double MinX,
        double MinY,
        double Width,
        double Height,
        double StartX,
        double StartY);

    internal readonly record struct FlatLayerPackingItem(
        int Depth,
        double Width,
        double Height);

    private readonly record struct FlatLayerPackedDraft(
        int Depth,
        int Row,
        double X,
        double Width,
        double Height);

    internal readonly record struct FlatLayerPackedItem(
        int Depth,
        int Row,
        double X,
        double Y,
        double Width,
        double Height);

    internal readonly record struct FlatLayerPackingResult(
        IReadOnlyList<FlatLayerPackedItem> Items,
        double ContentWidth,
        double ContentHeight,
        int RowCount);

    private readonly record struct ProjectedNode(
        Elements3DNodeViewModel Node,
        Rect LayoutRect,
        Rect FrontRect,
        QuadPoints Front,
        Rect HitRect,
        double Depth,
        bool UseSnapshot);

    private readonly record struct QuadPoints(
        Point P1,
        Point P2,
        Point P3,
        Point P4);

    private readonly record struct FlatProjectionTransformResult(
        QuadPoints Front,
        double Depth);

    private readonly record struct ProjectedPoint3D(
        Point Point,
        double Z);

    private readonly record struct RotatedVector(
        double X,
        double Y,
        double Z);

    private readonly record struct OrbitTransform(
        double SinYaw,
        double CosYaw,
        double SinPitch,
        double CosPitch,
        double SinRoll,
        double CosRoll);
}
