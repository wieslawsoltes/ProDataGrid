using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Navigation;

public class DataGridNavigationModelTests
{
    [Fact]
    public void Defaults_Preserve_Contained_Legacy_Policy()
    {
        var model = new DataGridNavigationModel();

        Assert.Equal(DataGridNavigationBoundaryMode.Contained, model.HorizontalBoundaryMode);
        Assert.Equal(DataGridNavigationBoundaryMode.Contained, model.VerticalBoundaryMode);
        Assert.Equal(DataGridNavigationBoundaryMode.Exit, model.TabBoundaryMode);
        Assert.Equal(DataGridTabNavigationMode.EditingOnly, model.TabNavigationMode);
        Assert.Equal(DataGridHorizontalNavigationMode.Physical, model.HorizontalNavigationMode);
    }

    [Fact]
    public void Resolve_Uses_Default_When_Grid_Has_Proposed_Target()
    {
        var model = new DataGridNavigationModel();
        DataGridNavigationRequest request = CreateRequest(
            DataGridNavigationCommand.Right,
            proposed: new DataGridNavigationPosition(1, 2));

        DataGridNavigationResult result = model.Resolve(request);

        Assert.Equal(DataGridNavigationDecision.Default, result.Decision);
    }

    [Theory]
    [InlineData(DataGridNavigationCommand.Left)]
    [InlineData(DataGridNavigationCommand.Right)]
    public void Resolve_Contains_Horizontal_Boundary(DataGridNavigationCommand command)
    {
        var model = new DataGridNavigationModel();

        DataGridNavigationResult result = model.Resolve(CreateRequest(command));

        Assert.Equal(DataGridNavigationDecision.Stay, result.Decision);
    }

    [Fact]
    public void Resolve_Wraps_Horizontal_Boundary()
    {
        var model = new DataGridNavigationModel
        {
            HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap
        };

        DataGridNavigationResult result = model.Resolve(CreateRequest(DataGridNavigationCommand.Right));

        Assert.Equal(DataGridNavigationDecision.Move, result.Decision);
        Assert.Equal(new DataGridNavigationPosition(1, 0), result.Target);
    }

    [Fact]
    public void Resolve_Wraps_Vertical_Boundary()
    {
        var model = new DataGridNavigationModel
        {
            VerticalBoundaryMode = DataGridNavigationBoundaryMode.Wrap
        };

        DataGridNavigationResult result = model.Resolve(CreateRequest(DataGridNavigationCommand.Down));

        Assert.Equal(DataGridNavigationDecision.Move, result.Decision);
        Assert.Equal(new DataGridNavigationPosition(0, 1), result.Target);
    }

    [Fact]
    public void Resolve_Tab_EditingOnly_Uses_Default_Outside_Edit_Mode()
    {
        var model = new DataGridNavigationModel();
        DataGridNavigationRequest request = CreateRequest(
            DataGridNavigationCommand.Next,
            proposed: new DataGridNavigationPosition(1, 2),
            isEditing: false);

        Assert.Equal(DataGridNavigationDecision.Default, model.Resolve(request).Decision);
    }

    [Fact]
    public void Resolve_Tab_Always_Uses_Proposed_Target_Outside_Edit_Mode()
    {
        var model = new DataGridNavigationModel
        {
            TabNavigationMode = DataGridTabNavigationMode.Always
        };
        var proposed = new DataGridNavigationPosition(1, 2);
        DataGridNavigationRequest request = CreateRequest(
            DataGridNavigationCommand.Next,
            proposed,
            isEditing: false);

        DataGridNavigationResult result = model.Resolve(request);

        Assert.Equal(DataGridNavigationDecision.Move, result.Decision);
        Assert.Equal(proposed, result.Target);
    }

    [Fact]
    public void Resolve_Tab_Exits_At_Boundary()
    {
        var model = new DataGridNavigationModel
        {
            TabNavigationMode = DataGridTabNavigationMode.Always
        };

        DataGridNavigationResult result = model.Resolve(CreateRequest(DataGridNavigationCommand.Next));

        Assert.Equal(DataGridNavigationDecision.LeaveGrid, result.Decision);
    }

    [Fact]
    public void Resolve_Logical_Rtl_Redirects_Horizontal_Command()
    {
        var model = new DataGridNavigationModel
        {
            HorizontalNavigationMode = DataGridHorizontalNavigationMode.Logical
        };

        DataGridNavigationResult result = model.Resolve(CreateRequest(
            DataGridNavigationCommand.Left,
            flowDirection: FlowDirection.RightToLeft));

        Assert.Equal(DataGridNavigationDecision.Redirect, result.Decision);
        Assert.Equal(DataGridNavigationCommand.Right, result.RedirectedCommand);
    }

    [Fact]
    public void NavigationChanging_Can_Redirect_Result()
    {
        var model = new DataGridNavigationModel();
        model.NavigationChanging += (_, e) =>
            e.Result = DataGridNavigationResult.MoveTo(new DataGridNavigationPosition(2, 2));

        DataGridNavigationResult result = model.Resolve(CreateRequest(DataGridNavigationCommand.Down));

        Assert.Equal(new DataGridNavigationPosition(2, 2), result.Target);
    }

    [Fact]
    public void NavigationChanging_Can_Cancel_Result()
    {
        var model = new DataGridNavigationModel();
        model.NavigationChanging += (_, e) => e.Cancel = true;

        DataGridNavigationResult result = model.Resolve(CreateRequest(DataGridNavigationCommand.Down));

        Assert.Equal(DataGridNavigationDecision.Stay, result.Decision);
        Assert.Equal(DataGridNavigationFailureReason.Canceled, result.FailureReason);
    }

    [Fact]
    public void Query_Is_SideEffect_Free()
    {
        var model = new DataGridNavigationModel();
        int previewCount = 0;
        model.NavigationChanging += (_, _) => previewCount++;

        DataGridNavigationResult result = model.Query(CreateRequest(DataGridNavigationCommand.Down));

        Assert.Equal(DataGridNavigationDecision.Stay, result.Decision);
        Assert.Equal(0, previewCount);
    }

    [Fact]
    public void RequestNavigate_Returns_False_Without_Bound_Grid()
    {
        var model = new DataGridNavigationModel();

        Assert.False(model.RequestNavigate(DataGridNavigationCommand.Down));
    }

    [Fact]
    public void Settings_Raise_PropertyChanged_Only_For_Real_Changes()
    {
        var model = new DataGridNavigationModel();
        var changes = new List<string?>();
        model.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        model.HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Contained;
        model.HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap;

        Assert.Equal(new[] { nameof(model.HorizontalBoundaryMode) }, changes);
    }

    [Fact]
    public void NotifyCompleted_Raises_Compact_Completion_Event()
    {
        var model = new DataGridNavigationModel();
        DataGridNavigationCompleted? observed = null;
        model.NavigationChanged += (_, e) => observed = e.Completed;
        DataGridNavigationRequest request = CreateRequest(DataGridNavigationCommand.Down);
        var completed = new DataGridNavigationCompleted(
            request,
            DataGridNavigationResult.UseDefault(),
            request.CurrentPosition,
            new DataGridNavigationPosition(2, 1),
            handled: true,
            DataGridNavigationFailureReason.None);

        model.NotifyCompleted(completed);

        Assert.True(observed.HasValue);
        Assert.True(observed.Value.Moved);
        Assert.True(observed.Value.Handled);
    }

    [AvaloniaFact]
    public void Grid_Creates_Default_Model_And_Replaces_Null()
    {
        var grid = new DataGrid();

        Assert.IsType<DataGridNavigationModel>(grid.NavigationModel);

        IDataGridNavigationModel first = grid.NavigationModel;
        grid.NavigationModel = null!;

        Assert.NotNull(grid.NavigationModel);
        Assert.NotSame(first, grid.NavigationModel);
    }

    [AvaloniaFact]
    public void Programmatic_Navigate_Moves_Current_Cell()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);

        bool handled = grid.Navigate(DataGridNavigationCommand.Down);

        Assert.True(handled);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
    }

    [AvaloniaFact]
    public void Model_Controller_Request_Moves_Bound_Grid()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);
        var model = new DataGridNavigationModel();
        grid.NavigationModel = model;

        bool handled = model.RequestNavigate(DataGridNavigationCommand.Down);

        Assert.True(handled);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
    }

    [AvaloniaFact]
    public void Replacing_Model_Detaches_Controller_Request()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);
        var oldModel = new DataGridNavigationModel();
        grid.NavigationModel = oldModel;
        grid.NavigationModel = new DataGridNavigationModel();

        bool handled = oldModel.RequestNavigate(DataGridNavigationCommand.Down);

        Assert.False(handled);
        Assert.Equal(0, grid.CurrentCell.RowIndex);
    }

    [AvaloniaFact]
    public void Programmatic_Navigate_Uses_Explicit_Custom_Target()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);
        grid.NavigationModel = new RedirectingNavigationModel(
            DataGridNavigationResult.MoveTo(new DataGridNavigationPosition(2, 2)));

        bool handled = grid.Navigate(DataGridNavigationCommand.Down);

        Assert.True(handled);
        Assert.Equal(2, grid.CurrentCell.RowIndex);
        Assert.Equal(2, grid.CurrentCell.Column.DisplayIndex);
    }

    [AvaloniaFact]
    public void Programmatic_Navigate_Rejects_Hidden_Target()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);
        grid.Columns[1].IsVisible = false;
        var model = new RedirectingNavigationModel(
            DataGridNavigationResult.MoveTo(new DataGridNavigationPosition(1, 1)));
        grid.NavigationModel = model;
        DataGridNavigationCompleted? completed = null;
        model.NavigationChanged += (_, e) => completed = e.Completed;

        bool handled = grid.Navigate(DataGridNavigationCommand.Down);

        Assert.False(handled);
        Assert.Equal(0, grid.CurrentCell.RowIndex);
        Assert.Equal(DataGridNavigationFailureReason.InvalidTarget, completed?.FailureReason);
    }

    [AvaloniaFact]
    public void Programmatic_Navigate_Can_Be_Canceled()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);
        var model = new DataGridNavigationModel();
        model.NavigationChanging += (_, e) => e.Cancel = true;
        grid.NavigationModel = model;
        DataGridNavigationCompleted? completed = null;
        model.NavigationChanged += (_, e) => completed = e.Completed;

        bool handled = grid.Navigate(DataGridNavigationCommand.Down);

        Assert.True(handled);
        Assert.Equal(0, grid.CurrentCell.RowIndex);
        Assert.Equal(DataGridNavigationFailureReason.Canceled, completed?.FailureReason);
    }

    [AvaloniaFact]
    public void Programmatic_Redirect_Uses_Default_Engine()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);
        grid.NavigationModel = new RedirectingNavigationModel(
            DataGridNavigationResult.RedirectTo(DataGridNavigationCommand.Right));

        Assert.True(grid.Navigate(DataGridNavigationCommand.Down));
        Assert.Equal(0, grid.CurrentCell.RowIndex);
        Assert.Equal(1, grid.CurrentCell.Column.DisplayIndex);
    }

    [AvaloniaFact]
    public void Horizontal_Wrap_Uses_Visible_Edge_Columns()
    {
        DataGrid grid = CreateGrid();
        grid.Columns[1].IsVisible = false;
        grid.Columns[2].DisplayIndex = 0;
        grid.Columns[0].DisplayIndex = 2;
        var model = new DataGridNavigationModel
        {
            HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap
        };
        grid.NavigationModel = model;
        SetCurrentCell(grid, rowIndex: 1, columnDisplayIndex: 2);

        Assert.True(grid.Navigate(DataGridNavigationCommand.Right));
        Assert.Equal(0, grid.CurrentCell.Column.DisplayIndex);
    }

    [AvaloniaFact]
    public void Vertical_Wrap_Preserves_Column()
    {
        DataGrid grid = CreateGrid();
        grid.NavigationModel = new DataGridNavigationModel
        {
            VerticalBoundaryMode = DataGridNavigationBoundaryMode.Wrap
        };
        SetCurrentCell(grid, rowIndex: 2, columnDisplayIndex: 1);

        Assert.True(grid.Navigate(DataGridNavigationCommand.Down));
        Assert.Equal(0, grid.CurrentCell.RowIndex);
        Assert.Equal(1, grid.CurrentCell.Column.DisplayIndex);
    }

    [AvaloniaFact]
    public void Tab_Always_Navigates_When_Not_Editing()
    {
        DataGrid grid = CreateGrid();
        grid.NavigationModel = new DataGridNavigationModel
        {
            TabNavigationMode = DataGridTabNavigationMode.Always
        };
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);

        Assert.True(grid.Navigate(DataGridNavigationCommand.Next));
        Assert.Equal(1, grid.CurrentCell.Column.DisplayIndex);
    }

    [AvaloniaFact]
    public void Shift_Navigation_Extends_Row_Selection()
    {
        DataGrid grid = CreateGrid(selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);

        Assert.True(grid.Navigate(DataGridNavigationCommand.Down, KeyModifiers.Shift));
        Assert.Equal(2, grid.SelectedItems.Count);
    }

    [AvaloniaFact]
    public void Logical_Rtl_Horizontal_Navigation_Uses_Layout_Direction()
    {
        DataGrid grid = CreateGrid();
        grid.FlowDirection = FlowDirection.RightToLeft;
        grid.NavigationModel = new DataGridNavigationModel
        {
            HorizontalNavigationMode = DataGridHorizontalNavigationMode.Logical
        };
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 1);

        Assert.True(grid.Navigate(DataGridNavigationCommand.Left));
        Assert.Equal(2, grid.CurrentCell.Column.DisplayIndex);
    }

    [AvaloniaFact]
    public void Hidden_And_Frozen_Columns_Use_Existing_Default_Mechanics()
    {
        DataGrid grid = CreateGrid();
        grid.FrozenColumnCount = 1;
        grid.Columns[1].IsVisible = false;
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);

        Assert.True(grid.Navigate(DataGridNavigationCommand.Right));
        Assert.Same(grid.Columns[2], grid.CurrentCell.Column);
    }

    [AvaloniaFact]
    public void Keyboard_Request_Flows_Through_Model()
    {
        DataGrid grid = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnDisplayIndex: 0);
        var model = new RedirectingNavigationModel(DataGridNavigationResult.Stay());
        grid.NavigationModel = model;

        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = Key.Down,
            Source = grid,
            KeyDeviceType = KeyDeviceType.Keyboard
        };
        grid.RaiseEvent(args);

        Assert.True(args.Handled);
        Assert.Equal(DataGridNavigationCommand.Down, model.LastRequest?.Command);
        Assert.Equal(DataGridNavigationOrigin.Keyboard, model.LastRequest?.Origin);
        Assert.Equal(0, grid.CurrentCell.RowIndex);
    }

    private static DataGridNavigationRequest CreateRequest(
        DataGridNavigationCommand command,
        DataGridNavigationPosition? proposed = null,
        bool isEditing = true,
        FlowDirection flowDirection = FlowDirection.LeftToRight)
    {
        return new DataGridNavigationRequest(
            command,
            DataGridNavigationOrigin.Keyboard,
            new DataGridNavigationPosition(1, 1),
            proposed,
            KeyModifiers.None,
            isEditing,
            DataGridSelectionMode.Extended,
            DataGridSelectionUnit.Cell,
            flowDirection,
            firstRowIndex: 0,
            lastRowIndex: 2,
            firstColumnDisplayIndex: 0,
            lastColumnDisplayIndex: 2);
    }

    private static DataGrid CreateGrid(DataGridSelectionMode selectionMode = DataGridSelectionMode.Single)
    {
        var window = new Window { Width = 640, Height = 480 };
        window.SetThemeStyles();
        var items = new[]
        {
            new Row(1, "One", true),
            new Row(2, "Two", false),
            new Row(3, "Three", true)
        };
        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            SelectionMode = selectionMode,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Id", Binding = new Binding(nameof(Row.Id)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Row.Name)) });
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Active", Binding = new Binding(nameof(Row.Active)) });
        window.Content = grid;
        window.Show();
        grid.UpdateLayout();
        return grid;
    }

    private static void SetCurrentCell(DataGrid grid, int rowIndex, int columnDisplayIndex)
    {
        DataGridColumn column = grid.Columns[columnDisplayIndex];
        foreach (DataGridColumn candidate in grid.Columns)
        {
            if (candidate.DisplayIndex == columnDisplayIndex)
            {
                column = candidate;
                break;
            }
        }

        object item = ((System.Collections.IList)grid.ItemsSource!)[rowIndex]!;
        grid.CurrentCell = new DataGridCellInfo(item, column, rowIndex, column.Index, isValid: true);
        grid.UpdateLayout();
    }

    private sealed record Row(int Id, string Name, bool Active);

    private sealed class RedirectingNavigationModel : DataGridNavigationModel
    {
        private readonly DataGridNavigationResult _result;

        public RedirectingNavigationModel(DataGridNavigationResult result)
        {
            _result = result;
        }

        public DataGridNavigationRequest? LastRequest { get; private set; }

        protected override DataGridNavigationResult ResolveCore(DataGridNavigationRequest request)
        {
            LastRequest = request;
            return _result;
        }
    }
}
