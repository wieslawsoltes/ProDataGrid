using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Navigation;

public class DataGridRouteNavigationModelTests
{
    [Fact]
    public void Route_Preserves_Path_Parameter_And_Target()
    {
        object parameter = new();
        var route = new DataGridRoute("orders/42", parameter, "details");

        Assert.True(route.IsValid);
        Assert.Equal("orders/42", route.Path);
        Assert.Same(parameter, route.Parameter);
        Assert.Equal("details", route.Target);
    }

    [Fact]
    public void Delegate_Resolver_Returns_Route_Without_Reflection()
    {
        var resolver = new DelegateDataGridRouteResolver(context =>
            context.ItemKey is int id ? new DataGridRoute($"orders/{id}") : null);
        DataGridRouteContext context = CreateContext(itemKey: 42);

        bool resolved = resolver.TryResolve(context, out DataGridRoute route);

        Assert.True(resolved);
        Assert.Equal("orders/42", route.Path);
    }

    [Fact]
    public void CanNavigate_Requires_Capability_And_Resolvable_Route()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.Navigate,
            out _);

        Assert.True(model.CanNavigate(DataGridRouteNavigationKind.Navigate, CreateContext()));
        Assert.False(model.CanNavigate(DataGridRouteNavigationKind.Back, CreateContext()));
        Assert.False(model.CanNavigate(
            DataGridRouteNavigationKind.Navigate,
            new DataGridRouteContext(null, null, null, DataGridNavigationPosition.Unset,
                DataGridRouteNavigationOrigin.Command, hasItem: false)));
    }

    [Fact]
    public async Task NavigateAsync_Maps_Context_And_Updates_State()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out RecordingNavigator navigator);
        var changes = new List<string?>();
        model.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            CreateContext(itemKey: 42));

        Assert.True(result.Succeeded);
        Assert.Equal("orders/42", result.CurrentRoute.Path);
        Assert.Equal("orders/42", model.CurrentRoute.Path);
        Assert.Equal(DataGridRouteNavigationStatus.Succeeded, model.LastResult.Status);
        Assert.Equal(DataGridRouteNavigationKind.Navigate, navigator.LastRequest.Kind);
        Assert.Contains(nameof(model.IsNavigating), changes);
        Assert.Contains(nameof(model.CurrentRoute), changes);
        Assert.Contains(nameof(model.LastResult), changes);
    }

    [Theory]
    [InlineData(DataGridRouteNavigationKind.Replace)]
    [InlineData(DataGridRouteNavigationKind.Reset)]
    [InlineData(DataGridRouteNavigationKind.Back)]
    [InlineData(DataGridRouteNavigationKind.Forward)]
    public async Task NavigateAsync_Preserves_History_Operation(DataGridRouteNavigationKind kind)
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out RecordingNavigator navigator);

        DataGridRouteNavigationResult result = await model.NavigateAsync(kind, CreateContext());

        Assert.True(result.Succeeded);
        Assert.Equal(kind, navigator.LastRequest.Kind);
        if (kind is DataGridRouteNavigationKind.Back or DataGridRouteNavigationKind.Forward)
        {
            Assert.False(navigator.LastRequest.Route.IsValid);
        }
    }

    [Fact]
    public async Task NavigateAsync_Returns_NotSupported_Without_Calling_Host()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.Navigate,
            out RecordingNavigator navigator);

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Back,
            DataGridRouteContext.Empty);

        Assert.Equal(DataGridRouteNavigationStatus.NotSupported, result.Status);
        Assert.Equal(0, navigator.CallCount);
    }

    [Fact]
    public async Task NavigateAsync_Returns_RouteNotFound_Without_Calling_Host()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out RecordingNavigator navigator);

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            new DataGridRouteContext(null, null, null, DataGridNavigationPosition.Unset,
                DataGridRouteNavigationOrigin.Command, hasItem: false));

        Assert.Equal(DataGridRouteNavigationStatus.RouteNotFound, result.Status);
        Assert.Equal(0, navigator.CallCount);
    }

    [Fact]
    public async Task NavigationChanging_Can_Redirect_Request()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out RecordingNavigator navigator);
        model.NavigationChanging += (_, e) =>
            e.Request = new DataGridRouteNavigationRequest(
                DataGridRouteNavigationKind.Replace,
                new DataGridRoute("orders/redirected"),
                e.Request.Context);

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            CreateContext());

        Assert.True(result.Succeeded);
        Assert.Equal(DataGridRouteNavigationKind.Replace, navigator.LastRequest.Kind);
        Assert.Equal("orders/redirected", navigator.LastRequest.Route.Path);
    }

    [Fact]
    public async Task NavigationChanging_Can_Cancel_Request()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out RecordingNavigator navigator);
        model.NavigationChanging += (_, e) => e.Cancel = true;

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            CreateContext());

        Assert.Equal(DataGridRouteNavigationStatus.Canceled, result.Status);
        Assert.Equal(0, navigator.CallCount);
    }

    [Fact]
    public async Task NavigationChanged_Reports_Typed_Completion()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out _);
        DataGridRouteNavigationChangedEventArgs? observed = null;
        model.NavigationChanged += (_, e) => observed = e;

        await model.NavigateAsync(DataGridRouteNavigationKind.Navigate, CreateContext(itemKey: 7));

        Assert.NotNull(observed);
        Assert.Equal("orders/7", observed.Request.Route.Path);
        Assert.True(observed.Result.Succeeded);
    }

    [Fact]
    public async Task Host_Exception_Becomes_Failed_Result()
    {
        var resolver = CreateResolver();
        var navigator = new DelegateDataGridRouteNavigator(
            DataGridRouteNavigationCapabilities.All,
            (_, _) => throw new InvalidOperationException("router failed"));
        var model = new DataGridRouteNavigationModel(resolver, navigator);

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            CreateContext());

        Assert.Equal(DataGridRouteNavigationStatus.Failed, result.Status);
        Assert.IsType<InvalidOperationException>(result.Error);
    }

    [Fact]
    public async Task Canceled_Token_Does_Not_Call_Host()
    {
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out RecordingNavigator navigator);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            CreateContext(),
            cancellation.Token);

        Assert.Equal(DataGridRouteNavigationStatus.Canceled, result.Status);
        Assert.Equal(0, navigator.CallCount);
    }

    [Fact]
    public async Task Host_Cancellation_Becomes_Canceled_Result()
    {
        var resolver = CreateResolver();
        var navigator = new DelegateDataGridRouteNavigator(
            DataGridRouteNavigationCapabilities.All,
            (_, token) => throw new OperationCanceledException(token));
        var model = new DataGridRouteNavigationModel(resolver, navigator);

        DataGridRouteNavigationResult result = await model.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            CreateContext());

        Assert.Equal(DataGridRouteNavigationStatus.Canceled, result.Status);
    }

    [AvaloniaFact]
    public void Grid_Creates_Current_Route_Context_With_Stable_Column_Key()
    {
        (DataGrid grid, Row row) = CreateGrid();

        DataGridRouteContext context = grid.GetCurrentRouteContext(DataGridRouteNavigationOrigin.Command);

        Assert.True(context.HasItem);
        Assert.Same(row, context.Item);
        Assert.Equal("name", context.ColumnKey);
        Assert.Equal(new DataGridNavigationPosition(0, 0), context.Position);
        Assert.Equal(DataGridRouteNavigationOrigin.Command, context.Origin);
    }

    [AvaloniaFact]
    public async Task Grid_NavigateRouteAsync_Uses_Bound_Model()
    {
        (DataGrid grid, _) = CreateGrid();
        DataGridRouteNavigationModel model = CreateModel(
            DataGridRouteNavigationCapabilities.All,
            out RecordingNavigator navigator);
        grid.RouteNavigationModel = model;

        DataGridRouteNavigationResult result = await grid.NavigateRouteAsync(
            DataGridRouteNavigationKind.Navigate,
            DataGridRouteNavigationOrigin.Keyboard,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(DataGridRouteNavigationOrigin.Keyboard, navigator.LastRequest.Context.Origin);
        Assert.Same(grid.CurrentCell.Item, navigator.LastRequest.Context.Item);
    }

    [AvaloniaFact]
    public async Task Grid_NavigateRouteAsync_Returns_NotSupported_When_Unconfigured()
    {
        (DataGrid grid, _) = CreateGrid();

        DataGridRouteNavigationResult result = await grid.NavigateRouteAsync(
            DataGridRouteNavigationKind.Navigate,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(DataGridRouteNavigationStatus.NotSupported, result.Status);
    }

    private static DataGridRouteNavigationModel CreateModel(
        DataGridRouteNavigationCapabilities capabilities,
        out RecordingNavigator navigator)
    {
        navigator = new RecordingNavigator(capabilities);
        return new DataGridRouteNavigationModel(CreateResolver(), navigator);
    }

    private static DelegateDataGridRouteResolver CreateResolver() =>
        new(context => context.HasItem
            ? new DataGridRoute($"orders/{context.ItemKey}", context.Item, "details")
            : null);

    private static DataGridRouteContext CreateContext(int itemKey = 1) =>
        new(
            new { Id = itemKey },
            itemKey,
            "status",
            new DataGridNavigationPosition(0, 2),
            DataGridRouteNavigationOrigin.Command);

    private static (DataGrid Grid, Row Row) CreateGrid()
    {
        var row = new Row(1, "One");
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            ItemsSource = new[] { row }
        };
        var column = new DataGridTextColumn
        {
            ColumnKey = "name",
            Binding = new Binding(nameof(Row.Name))
        };
        grid.Columns.Add(column);
        grid.CurrentCell = new DataGridCellInfo(row, column, 0, 0);
        return (grid, row);
    }

    private sealed record Row(int Id, string Name);

    private sealed class RecordingNavigator : IDataGridRouteNavigator
    {
        public RecordingNavigator(DataGridRouteNavigationCapabilities capabilities)
        {
            Capabilities = capabilities;
        }

        public DataGridRouteNavigationCapabilities Capabilities { get; }

        public DataGridRouteNavigationRequest LastRequest { get; private set; }

        public int CallCount { get; private set; }

        public ValueTask<DataGridRouteNavigationResult> NavigateAsync(
            DataGridRouteNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            CallCount++;
            return ValueTask.FromResult(DataGridRouteNavigationResult.Success(request.Route));
        }
    }
}
