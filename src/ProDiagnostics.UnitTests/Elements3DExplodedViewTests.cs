using Avalonia.Diagnostics.Controls;
using Avalonia;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class Elements3DExplodedViewTests
{
    [Theory]
    [InlineData(Key.R, KeyModifiers.None, true)]
    [InlineData(Key.Home, KeyModifiers.None, true)]
    [InlineData(Key.D0, KeyModifiers.Control, true)]
    [InlineData(Key.NumPad0, KeyModifiers.Control, true)]
    [InlineData(Key.D0, KeyModifiers.None, false)]
    [InlineData(Key.Left, KeyModifiers.None, false)]
    public void IsResetShortcut_Matches_Expected_Keys(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, Elements3DExplodedView.IsResetShortcut(key, modifiers));
    }

    [Fact]
    public void CalculatePanForAnchor_Keeps_DepthZero_Point_Under_Cursor()
    {
        var layout = new Elements3DExplodedView.LayoutMetrics(
            new Rect(0, 0, 1200, 800),
            MinX: -50,
            MinY: 25,
            ContentWidth: 900,
            ContentHeight: 700,
            MaxDepth: 12,
            DepthOffsetX: 9,
            DepthOffsetY: 7);

        const double worldX = 220;
        const double worldY = 160;
        const int depth = 0;
        const double oldScale = 0.62;
        const double newScale = 0.87;
        var oldPan = new Vector(14, -11);
        var pointer = Elements3DExplodedView.ProjectWorldPoint(layout, oldScale, oldPan, worldX, worldY, depth);

        var newPan = Elements3DExplodedView.CalculatePanForAnchor(layout, newScale, pointer, worldX, worldY, depth);
        var projected = Elements3DExplodedView.ProjectWorldPoint(layout, newScale, newPan, worldX, worldY, depth);

        Assert.Equal(pointer.X, projected.X, 6);
        Assert.Equal(pointer.Y, projected.Y, 6);
    }

    [Fact]
    public void CalculatePanForAnchor_Keeps_DepthShifted_Point_Under_Cursor()
    {
        var layout = new Elements3DExplodedView.LayoutMetrics(
            new Rect(0, 0, 1000, 640),
            MinX: 0,
            MinY: 0,
            ContentWidth: 780,
            ContentHeight: 460,
            MaxDepth: 20,
            DepthOffsetX: 8,
            DepthOffsetY: 5);

        const double worldX = 320;
        const double worldY = 120;
        const int depth = 7;
        const double oldScale = 0.5;
        const double newScale = 1.05;
        var oldPan = new Vector(-20, 35);
        var pointer = Elements3DExplodedView.ProjectWorldPoint(layout, oldScale, oldPan, worldX, worldY, depth);

        var newPan = Elements3DExplodedView.CalculatePanForAnchor(layout, newScale, pointer, worldX, worldY, depth);
        var projected = Elements3DExplodedView.ProjectWorldPoint(layout, newScale, newPan, worldX, worldY, depth);

        Assert.Equal(pointer.X, projected.X, 6);
        Assert.Equal(pointer.Y, projected.Y, 6);
    }

    [Fact]
    public void HitTestOrbitControl_Maps_Center_And_Directional_Buttons()
    {
        var viewport = new Rect(0, 0, 1200, 800);
        var layout = Elements3DExplodedView.CreateOrbitControlLayout(viewport);

        var centerAction = Elements3DExplodedView.HitTestOrbitControl(viewport, layout.Center);
        var rightAction = Elements3DExplodedView.HitTestOrbitControl(viewport, layout.RightButton);
        var upAction = Elements3DExplodedView.HitTestOrbitControl(viewport, layout.UpButton);
        var rollRightAction = Elements3DExplodedView.HitTestOrbitControl(viewport, layout.RollRightButton);

        Assert.Equal(Elements3DExplodedView.OrbitControlAction.Reset, centerAction);
        Assert.Equal(Elements3DExplodedView.OrbitControlAction.YawRight, rightAction);
        Assert.Equal(Elements3DExplodedView.OrbitControlAction.PitchUp, upAction);
        Assert.Equal(Elements3DExplodedView.OrbitControlAction.RollRight, rollRightAction);
    }

    [Fact]
    public void HitTestOrbitControl_Maps_Ring_To_OrbitDrag()
    {
        var viewport = new Rect(0, 0, 1200, 800);
        var layout = Elements3DExplodedView.CreateOrbitControlLayout(viewport);
        var ringPoint = new Point(
            layout.Center.X + (layout.OuterRadius * 0.7),
            layout.Center.Y - (layout.OuterRadius * 0.5));

        var action = Elements3DExplodedView.HitTestOrbitControl(viewport, ringPoint);

        Assert.Equal(Elements3DExplodedView.OrbitControlAction.OrbitDrag, action);
    }

    [Theory]
    [InlineData(12.0, 25.0, false, 0.12)]
    [InlineData(30.0, 25.0, false, 1.0)]
    [InlineData(25.0, 25.0, true, 1.0)]
    public void ComputeFrontLayerOpacityFactor_Fades_Layers_In_Front_Of_Selection(
        double layerDepth,
        double selectedDepth,
        bool isSelected,
        double expected)
    {
        var actual = Elements3DExplodedView.ComputeFrontLayerOpacityFactor(layerDepth, selectedDepth, isSelected);
        Assert.Equal(expected, actual, 3);
    }

    [Fact]
    public void ComputeFrontLayerOpacityFactor_Returns_Full_Opacity_When_Selection_Missing()
    {
        var actual = Elements3DExplodedView.ComputeFrontLayerOpacityFactor(10, selectedLayerDepth: null, isSelected: false);
        Assert.Equal(1, actual, 3);
    }

    [Fact]
    public void PackFlatLayers_WithoutWrap_UsesSingleRowAndCentersByHeight()
    {
        var items = new[]
        {
            new Elements3DExplodedView.FlatLayerPackingItem(0, 100, 40),
            new Elements3DExplodedView.FlatLayerPackingItem(1, 60, 20),
            new Elements3DExplodedView.FlatLayerPackingItem(2, 80, 30)
        };

        var packed = Elements3DExplodedView.PackFlatLayers(items, layerGap: 10, maxLayersPerRow: 0);

        Assert.Equal(1, packed.RowCount);
        Assert.Equal(260, packed.ContentWidth, 3);
        Assert.Equal(40, packed.ContentHeight, 3);
        Assert.Equal(3, packed.Items.Count);
        Assert.All(packed.Items, item => Assert.Equal(0, item.Row));
        Assert.Equal(0, packed.Items[0].X, 3);
        Assert.Equal(110, packed.Items[1].X, 3);
        Assert.Equal(180, packed.Items[2].X, 3);
        Assert.Equal(0, packed.Items[0].Y, 3);
        Assert.Equal(10, packed.Items[1].Y, 3);
        Assert.Equal(5, packed.Items[2].Y, 3);
    }

    [Fact]
    public void PackFlatLayers_WithWrap_SplitsLayersIntoMultipleRows()
    {
        var items = new[]
        {
            new Elements3DExplodedView.FlatLayerPackingItem(0, 100, 40),
            new Elements3DExplodedView.FlatLayerPackingItem(1, 90, 35),
            new Elements3DExplodedView.FlatLayerPackingItem(2, 80, 30),
            new Elements3DExplodedView.FlatLayerPackingItem(3, 70, 25),
            new Elements3DExplodedView.FlatLayerPackingItem(4, 60, 20)
        };

        var packed = Elements3DExplodedView.PackFlatLayers(items, layerGap: 10, maxLayersPerRow: 2);

        Assert.Equal(3, packed.RowCount);
        Assert.Equal(200, packed.ContentWidth, 3);
        Assert.Equal(110, packed.ContentHeight, 3);
        Assert.Equal(5, packed.Items.Count);
        Assert.Equal(0, packed.Items[0].Row);
        Assert.Equal(0, packed.Items[1].Row);
        Assert.Equal(1, packed.Items[2].Row);
        Assert.Equal(1, packed.Items[3].Row);
        Assert.Equal(2, packed.Items[4].Row);
        Assert.Equal(70, packed.Items[4].X, 3);
        Assert.Equal(90, packed.Items[4].Y, 3);
    }

    [Fact]
    public void TryProjectPointToQuadUv_Resolves_OffCenter_Affine_Point()
    {
        var quadP1 = new Point(120, 80);
        var quadP2 = new Point(420, 120);
        var quadP4 = new Point(80, 280);
        const double expectedU = 0.73;
        const double expectedV = 0.41;
        var pointer = new Point(
            quadP1.X + ((quadP2.X - quadP1.X) * expectedU) + ((quadP4.X - quadP1.X) * expectedV),
            quadP1.Y + ((quadP2.Y - quadP1.Y) * expectedU) + ((quadP4.Y - quadP1.Y) * expectedV));

        var resolved = Elements3DExplodedView.TryProjectPointToQuadUv(pointer, quadP1, quadP2, quadP4, out var u, out var v);

        Assert.True(resolved);
        Assert.Equal(expectedU, u, 6);
        Assert.Equal(expectedV, v, 6);
    }

    [Fact]
    public void TryResolveWorldPointFromProjectedQuad_Maps_OffCenter_Pointer_To_NodeBounds()
    {
        var quadP1 = new Point(200, 160);
        var quadP2 = new Point(620, 210);
        var quadP4 = new Point(150, 430);
        var nodeBounds = new Rect(25, 40, 320, 180);
        const double expectedU = 0.66;
        const double expectedV = 0.27;
        var pointer = new Point(
            quadP1.X + ((quadP2.X - quadP1.X) * expectedU) + ((quadP4.X - quadP1.X) * expectedV),
            quadP1.Y + ((quadP2.Y - quadP1.Y) * expectedU) + ((quadP4.Y - quadP1.Y) * expectedV));

        var resolved = Elements3DExplodedView.TryResolveWorldPointFromProjectedQuad(
            pointer,
            quadP1,
            quadP2,
            quadP4,
            nodeBounds,
            out var worldX,
            out var worldY);

        Assert.True(resolved);
        Assert.Equal(nodeBounds.X + (expectedU * nodeBounds.Width), worldX, 6);
        Assert.Equal(nodeBounds.Y + (expectedV * nodeBounds.Height), worldY, 6);
    }

    [Fact]
    public void TryProjectPointToQuadUv_Returns_False_For_Degenerate_Basis()
    {
        var quadP1 = new Point(10, 10);
        var quadP2 = new Point(20, 20);
        var quadP4 = new Point(30, 30);

        var resolved = Elements3DExplodedView.TryProjectPointToQuadUv(new Point(15, 15), quadP1, quadP2, quadP4, out _, out _);

        Assert.False(resolved);
    }
}
