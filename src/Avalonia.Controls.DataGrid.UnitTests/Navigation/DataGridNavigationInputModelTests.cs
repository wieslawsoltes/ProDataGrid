using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Navigation;

public class DataGridNavigationInputModelTests
{
    [Fact]
    public void Binding_Matches_Required_Modifiers_And_Allows_Additional_Modifiers()
    {
        var model = new DataGridNavigationInputModel(
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.J,
                DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Down),
                DataGridNavigationInputModifiers.Control));

        DataGridNavigationInputResult result = model.Resolve(CreateKeyRequest(
            DataGridNavigationInputKey.J,
            DataGridNavigationInputModifiers.Control | DataGridNavigationInputModifiers.Shift));

        Assert.Equal(DataGridNavigationInputDecision.Navigate, result.Decision);
        Assert.Equal(DataGridNavigationCommand.Down, result.Command);
    }

    [Fact]
    public void Exact_Modifiers_Reject_Additional_Modifiers()
    {
        var model = new DataGridNavigationInputModel(
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.J,
                DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Down),
                DataGridNavigationInputModifiers.Control,
                exactModifiers: true));

        DataGridNavigationInputResult result = model.Resolve(CreateKeyRequest(
            DataGridNavigationInputKey.J,
            DataGridNavigationInputModifiers.Control | DataGridNavigationInputModifiers.Shift));

        Assert.Equal(DataGridNavigationInputDecision.Ignore, result.Decision);
    }

    [Fact]
    public void Physical_Key_Binding_Is_Independent_Of_Logical_Keyboard_Layout()
    {
        var model = new DataGridNavigationInputModel(
            DataGridNavigationInputBinding.PhysicalKeyDown(
                DataGridNavigationInputKey.W,
                DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Up)));

        DataGridNavigationInputResult result = model.Resolve(CreateKeyRequest(
            DataGridNavigationInputKey.Z,
            physicalKey: DataGridNavigationInputKey.W));

        Assert.Equal(DataGridNavigationCommand.Up, result.Command);
    }

    [Fact]
    public void Pointer_Binding_Matches_Target_And_Click_Count()
    {
        var model = new DataGridNavigationInputModel(
            DataGridNavigationInputBinding.Pointer(
                DataGridNavigationInputKind.PointerPressed,
                DataGridNavigationPointerButton.Primary,
                DataGridNavigationInputResult.NavigateToTarget(),
                clickCount: 2,
                targetKind: DataGridNavigationInputTargetKind.Cell));
        DataGridNavigationInputRequest request = new(
            DataGridNavigationInputKind.PointerPressed,
            DataGridNavigationInputKey.None,
            DataGridNavigationInputKey.None,
            DataGridNavigationKeyDeviceKind.Unknown,
            DataGridNavigationInputModifiers.None,
            DataGridNavigationPointerDeviceKind.Mouse,
            DataGridNavigationPointerButton.Primary,
            DataGridNavigationWheelDirection.None,
            clickCount: 2,
            x: 10,
            y: 20,
            wheelDeltaX: 0,
            wheelDeltaY: 0,
            DataGridNavigationInputTargetKind.Cell,
            new DataGridNavigationPosition(3, 1),
            DataGridNavigationPosition.Unset,
            isEditing: false);

        Assert.Equal(DataGridNavigationInputDecision.NavigateToTarget, model.Resolve(request).Decision);
    }

    [Fact]
    public void Resolving_Event_Can_Replace_Table_Result_From_ViewModel()
    {
        var model = new DataGridNavigationInputModel();
        model.InputResolving += (_, args) =>
            args.Result = DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.GridEnd);

        DataGridNavigationInputResult result = model.Resolve(CreateKeyRequest(DataGridNavigationInputKey.G));

        Assert.Equal(DataGridNavigationCommand.GridEnd, result.Command);
    }

    [AvaloniaFact]
    public void Custom_Key_Is_Normalized_And_Executed_Through_Navigation_Model()
    {
        Window window = CreateWindow(out DataGrid grid);
        try
        {
            var navigation = new DataGridNavigationModel();
            DataGridNavigationRequest? observed = null;
            navigation.NavigationChanging += (_, args) => observed = args.Request;
            grid.NavigationModel = navigation;
            grid.NavigationInputModel = new DataGridNavigationInputModel(
                DataGridNavigationInputBinding.KeyDown(
                    DataGridNavigationInputKey.J,
                    DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Down)));
            SetCurrentCell(grid, 0, 0);

            KeyEventArgs args = RaiseKeyDown(grid, Key.J);

            Assert.True(args.Handled);
            Assert.Equal(1, grid.CurrentCell.RowIndex);
            Assert.Equal(DataGridNavigationCommand.Down, observed?.Command);
            Assert.Equal(DataGridNavigationOrigin.Keyboard, observed?.Origin);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ignored_Input_Falls_Through_To_Legacy_Keyboard_Navigation()
    {
        Window window = CreateWindow(out DataGrid grid);
        try
        {
            var input = new DataGridNavigationInputModel(
                DataGridNavigationInputBinding.KeyDown(
                    DataGridNavigationInputKey.J,
                    DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Down)));
            int resolveCount = 0;
            int navigationCount = 0;
            input.InputResolving += (_, _) => resolveCount++;
            grid.NavigationInputModel = input;
            ((DataGridNavigationModel)grid.NavigationModel).NavigationChanging += (_, _) => navigationCount++;
            SetCurrentCell(grid, 0, 0);

            KeyEventArgs args = RaiseKeyDown(grid, Key.Down);

            Assert.True(args.Handled);
            Assert.Equal(1, resolveCount);
            Assert.Equal(1, navigationCount);
            Assert.Equal(1, grid.CurrentCell.RowIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Consumed_Failed_Navigation_Prevents_Legacy_Fallback()
    {
        Window window = CreateWindow(out DataGrid grid);
        try
        {
            grid.NavigationInputModel = new DataGridNavigationInputModel(
                DataGridNavigationInputBinding.KeyDown(
                    DataGridNavigationInputKey.J,
                    DataGridNavigationInputResult.Navigate(
                        DataGridNavigationCommand.Up,
                        consumeWhenNavigationFails: true)));
            SetCurrentCell(grid, 0, 0);

            KeyEventArgs args = RaiseKeyDown(grid, Key.J);

            Assert.True(args.Handled);
            Assert.Equal(0, grid.CurrentCell.RowIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ViewModel_Sees_Tunneled_Key_Before_Descendant_Consumes_It()
    {
        Window window = CreateWindow(out DataGrid grid);
        try
        {
            var input = new DataGridNavigationInputModel();
            int resolveCount = 0;
            input.InputResolving += (_, _) => resolveCount++;
            grid.NavigationInputModel = input;
            SetCurrentCell(grid, 0, 0);
            DataGridCell source = grid.GetVisualDescendants()
                .OfType<DataGridCell>()
                .Single(cell => cell.RowIndex == 0 && cell.OwningColumn?.DisplayIndex == 0);
            source.KeyDown += (_, args) => args.Handled = true;
            var args = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Route = InputElement.KeyDownEvent.RoutingStrategies,
                Key = Key.J,
                Source = source,
                KeyDeviceType = KeyDeviceType.Keyboard
            };

            source.RaiseEvent(args);

            Assert.True(args.Handled);
            Assert.Equal(1, resolveCount);
            Assert.Equal(0, grid.CurrentCell.RowIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Target_Can_Establish_First_Current_Cell_Through_Model()
    {
        Window window = CreateWindow(out DataGrid grid);
        try
        {
            var navigation = new DataGridNavigationModel();
            DataGridNavigationRequest? observed = null;
            navigation.NavigationChanging += (_, args) => observed = args.Request;
            grid.NavigationModel = navigation;
            grid.NavigationInputModel = new DataGridNavigationInputModel(
                DataGridNavigationInputBinding.Pointer(
                    DataGridNavigationInputKind.PointerPressed,
                    DataGridNavigationPointerButton.Primary,
                    DataGridNavigationInputResult.NavigateToTarget(consumeWhenNavigationFails: true),
                    targetKind: DataGridNavigationInputTargetKind.Cell));
            grid.CurrentCell = default;
            DataGridCell target = grid.GetVisualDescendants()
                .OfType<DataGridCell>()
                .Single(cell => cell.RowIndex == 1 && cell.OwningColumn?.DisplayIndex == 1);
            Assert.False(grid.CurrentCell.IsValid);

            PointerPressedEventArgs args = CreatePointerPressedArgs(target, window);
            target.RaiseEvent(args);
            grid.UpdateLayout();

            Assert.True(args.Handled);
            Assert.Equal(1, grid.CurrentCell.RowIndex);
            Assert.Equal(1, grid.CurrentCell.Column.DisplayIndex);
            Assert.Equal(DataGridNavigationOrigin.Pointer, observed?.Origin);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Route_Uses_Clicked_Cell_Context_Instead_Of_Previous_Current_Cell()
    {
        Window window = CreateWindow(out DataGrid grid);
        try
        {
            DataGridRouteNavigationRequest? routed = null;
            var resolver = new DelegateDataGridRouteResolver(context =>
                context.Item is Row row ? new DataGridRoute($"rows/{row.Id}") : null);
            var navigator = new DelegateDataGridRouteNavigator(
                DataGridRouteNavigationCapabilities.Navigate,
                (request, _) =>
                {
                    routed = request;
                    return ValueTask.FromResult(DataGridRouteNavigationResult.Success(request.Route));
                });
            grid.RouteNavigationModel = new DataGridRouteNavigationModel(resolver, navigator);
            grid.RouteContextFactory = new DataGridRouteContextFactory(item => ((Row)item).Id);
            grid.NavigationInputModel = new DataGridNavigationInputModel(
                DataGridNavigationInputBinding.Pointer(
                    DataGridNavigationInputKind.PointerReleased,
                    DataGridNavigationPointerButton.Primary,
                    DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Navigate),
                    targetKind: DataGridNavigationInputTargetKind.Cell));
            SetCurrentCell(grid, 0, 0);
            DataGridCell target = grid.GetVisualDescendants()
                .OfType<DataGridCell>()
                .Single(cell => cell.RowIndex == 2 && cell.OwningColumn?.DisplayIndex == 1);

            PointerReleasedEventArgs args = CreatePointerReleasedArgs(target, window);
            target.RaiseEvent(args);

            Assert.True(args.Handled);
            Assert.NotNull(routed);
            Assert.Equal("rows/2", routed.Value.Route.Path);
            Assert.Equal(2, routed.Value.Context.ItemKey);
            Assert.Equal(new DataGridNavigationPosition(2, 1), routed.Value.Context.Position);
            Assert.Equal(DataGridRouteNavigationOrigin.Pointer, routed.Value.Context.Origin);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wheel_Input_Can_Trigger_Application_History_Without_View_Code()
    {
        Window window = CreateWindow(out DataGrid grid);
        try
        {
            DataGridRouteNavigationRequest? routed = null;
            var resolver = new DelegateDataGridRouteResolver(static _ => null);
            var navigator = new DelegateDataGridRouteNavigator(
                DataGridRouteNavigationCapabilities.Back,
                (request, _) =>
                {
                    routed = request;
                    return ValueTask.FromResult(DataGridRouteNavigationResult.Success());
                });
            grid.RouteNavigationModel = new DataGridRouteNavigationModel(resolver, navigator);
            DataGridNavigationInputRequest? observedInput = null;
            var input = new DataGridNavigationInputModel(
                DataGridNavigationInputBinding.Wheel(
                    DataGridNavigationWheelDirection.Down,
                    DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Back)));
            input.InputResolving += (_, args) => observedInput = args.Request;
            grid.NavigationInputModel = input;
            Point point = grid.TranslatePoint(
                new Point(grid.Bounds.Width / 2, grid.Bounds.Height / 2),
                window)!.Value;

            window.MouseWheel(point, new Vector(0, -3));

            Assert.Equal(DataGridNavigationWheelDirection.Down, observedInput?.WheelDirection);
            Assert.Equal(-3, observedInput?.WheelDeltaY);
            Assert.Equal(DataGridRouteNavigationKind.Back, routed?.Kind);
            Assert.Equal(DataGridRouteNavigationOrigin.Pointer, routed?.Context.Origin);
        }
        finally
        {
            window.Close();
        }
    }

    private static DataGridNavigationInputRequest CreateKeyRequest(
        DataGridNavigationInputKey key,
        DataGridNavigationInputModifiers modifiers = DataGridNavigationInputModifiers.None,
        DataGridNavigationInputKey? physicalKey = null) =>
        new(
            DataGridNavigationInputKind.KeyDown,
            key,
            physicalKey ?? key,
            DataGridNavigationKeyDeviceKind.Keyboard,
            modifiers,
            DataGridNavigationPointerDeviceKind.Unknown,
            DataGridNavigationPointerButton.None,
            DataGridNavigationWheelDirection.None,
            clickCount: 0,
            x: double.NaN,
            y: double.NaN,
            wheelDeltaX: 0,
            wheelDeltaY: 0,
            DataGridNavigationInputTargetKind.Grid,
            DataGridNavigationPosition.Unset,
            DataGridNavigationPosition.Unset,
            isEditing: false);

    private static Window CreateWindow(out DataGrid grid, int rowCount = 3)
    {
        var window = new Window { Width = 640, Height = 480 };
        window.SetThemeStyles();
        var items = Enumerable.Range(0, rowCount).Select(index => new Row(index, $"Row {index}")).ToArray();
        grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Id", Binding = new Binding(nameof(Row.Id)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Row.Name)) });
        window.Content = grid;
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void SetCurrentCell(DataGrid grid, int rowIndex, int columnDisplayIndex)
    {
        DataGridColumn column = grid.Columns.Single(candidate => candidate.DisplayIndex == columnDisplayIndex);
        object item = ((IList)grid.ItemsSource!)[rowIndex]!;
        grid.CurrentCell = new DataGridCellInfo(item, column, rowIndex, column.Index, isValid: true);
        grid.UpdateLayout();
    }

    private static KeyEventArgs RaiseKeyDown(DataGrid grid, Key key)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = key,
            Source = grid,
            KeyDeviceType = KeyDeviceType.Keyboard
        };
        grid.RaiseEvent(args);
        grid.UpdateLayout();
        return args;
    }

    private static PointerPressedEventArgs CreatePointerPressedArgs(Control source, Visual root)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(
            RawInputModifiers.LeftMouseButton,
            PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(
            source,
            pointer,
            root,
            new Point(source.Bounds.Width / 2, source.Bounds.Height / 2),
            0,
            properties,
            KeyModifiers.None,
            clickCount: 1);
    }

    private static PointerReleasedEventArgs CreatePointerReleasedArgs(Control source, Visual root)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(
            RawInputModifiers.None,
            PointerUpdateKind.LeftButtonReleased);
        return new PointerReleasedEventArgs(
            source,
            pointer,
            root,
            new Point(source.Bounds.Width / 2, source.Bounds.Height / 2),
            0,
            properties,
            KeyModifiers.None,
            MouseButton.Left);
    }

    private sealed record Row(int Id, string Name);
}
