// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls.DataGridNavigation
{
    /// <summary>
    /// Identifies an application-route operation requested from a DataGrid workflow.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridRouteNavigationKind
    {
        /// <summary>Pushes or otherwise navigates to a route.</summary>
        Navigate,

        /// <summary>Replaces the active history entry with a route.</summary>
        Replace,

        /// <summary>Clears the active history and navigates to a route.</summary>
        Reset,

        /// <summary>Navigates to the previous history entry.</summary>
        Back,

        /// <summary>Navigates to the next history entry when supported.</summary>
        Forward
    }

    /// <summary>
    /// Identifies how a grid-associated application route was activated.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridRouteNavigationOrigin
    {
        /// <summary>The route was activated by a ViewModel command.</summary>
        Command,

        /// <summary>The route was activated by keyboard input.</summary>
        Keyboard,

        /// <summary>The route was activated by pointer input.</summary>
        Pointer,

        /// <summary>The route was requested through an application API.</summary>
        Programmatic,

        /// <summary>The route was applied while restoring application state or a deep link.</summary>
        RestoredState
    }

    /// <summary>
    /// Describes the route operations supported by an <see cref="IDataGridRouteNavigator"/>.
    /// </summary>
    [Flags]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridRouteNavigationCapabilities
    {
        /// <summary>No route operation is supported.</summary>
        None = 0,

        /// <summary>Navigation to a route is supported.</summary>
        Navigate = 1 << 0,

        /// <summary>Replacing the active history entry is supported.</summary>
        Replace = 1 << 1,

        /// <summary>Resetting history and navigating to a route is supported.</summary>
        Reset = 1 << 2,

        /// <summary>Backward history navigation is supported.</summary>
        Back = 1 << 3,

        /// <summary>Forward history navigation is supported.</summary>
        Forward = 1 << 4,

        /// <summary>All route operations are supported.</summary>
        All = Navigate | Replace | Reset | Back | Forward
    }

    /// <summary>
    /// Identifies the non-throwing outcome of an application-route request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridRouteNavigationStatus
    {
        /// <summary>The navigator completed the request successfully.</summary>
        Succeeded,

        /// <summary>The request was canceled before completion.</summary>
        Canceled,

        /// <summary>No route could be resolved from the supplied grid context.</summary>
        RouteNotFound,

        /// <summary>The navigator does not support the requested operation.</summary>
        NotSupported,

        /// <summary>The request or route was invalid.</summary>
        InvalidRequest,

        /// <summary>The navigator reported a failure.</summary>
        Failed
    }

    /// <summary>
    /// Represents a framework-neutral application route.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridRoute : IEquatable<DataGridRoute>
    {
        /// <summary>
        /// Initializes a route.
        /// </summary>
        /// <param name="path">The stable route path or route segment.</param>
        /// <param name="parameter">An optional immutable parameter or navigation state object.</param>
        /// <param name="target">An optional region, outlet, screen, or host identifier.</param>
        public DataGridRoute(string path, object parameter = null, string target = null)
        {
            Path = path ?? string.Empty;
            Parameter = parameter;
            Target = target ?? string.Empty;
        }

        /// <summary>Gets the stable route path or route segment.</summary>
        public string Path { get; }

        /// <summary>Gets an optional immutable parameter or navigation state object.</summary>
        public object Parameter { get; }

        /// <summary>Gets an optional region, outlet, screen, or host identifier.</summary>
        public string Target { get; }

        /// <summary>Gets whether the route contains a non-empty path.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Path);

        /// <summary>Gets an unset route.</summary>
        public static DataGridRoute Unset => default;

        /// <inheritdoc />
        public bool Equals(DataGridRoute other) =>
            string.Equals(Path, other.Path, StringComparison.Ordinal) &&
            ReferenceEquals(Parameter, other.Parameter) &&
            string.Equals(Target, other.Target, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridRoute other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(Path ?? string.Empty);
                hash = (hash * 397) ^ (Parameter?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Target ?? string.Empty);
                return hash;
            }
        }

        /// <summary>Compares two routes for equality.</summary>
        public static bool operator ==(DataGridRoute left, DataGridRoute right) => left.Equals(right);

        /// <summary>Compares two routes for inequality.</summary>
        public static bool operator !=(DataGridRoute left, DataGridRoute right) => !left.Equals(right);
    }

    /// <summary>
    /// Carries stable row and column identity from a grid workflow to a route resolver.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridRouteContext
    {
        /// <summary>
        /// Initializes a route context.
        /// </summary>
        /// <param name="item">The source item. It may be <see langword="null"/> when <paramref name="hasItem"/> is true.</param>
        /// <param name="itemKey">An optional stable key for restoring the item after filtering or reload.</param>
        /// <param name="columnKey">An optional stable column key.</param>
        /// <param name="position">The current row and display-column position.</param>
        /// <param name="origin">The activation source.</param>
        /// <param name="hasItem">Whether the context carries an item, including a null item.</param>
        public DataGridRouteContext(
            object item,
            object itemKey,
            object columnKey,
            DataGridNavigationPosition position,
            DataGridRouteNavigationOrigin origin,
            bool hasItem = true)
        {
            Item = item;
            ItemKey = itemKey;
            ColumnKey = columnKey;
            Position = position;
            Origin = origin;
            HasItem = hasItem;
        }

        /// <summary>Gets whether the context carries an item.</summary>
        public bool HasItem { get; }

        /// <summary>Gets the source item.</summary>
        public object Item { get; }

        /// <summary>Gets the optional stable row key.</summary>
        public object ItemKey { get; }

        /// <summary>Gets the optional stable column key.</summary>
        public object ColumnKey { get; }

        /// <summary>Gets the current row and display-column position.</summary>
        public DataGridNavigationPosition Position { get; }

        /// <summary>Gets the activation source.</summary>
        public DataGridRouteNavigationOrigin Origin { get; }

        /// <summary>Gets an empty route context for history-only operations.</summary>
        public static DataGridRouteContext Empty =>
            new(null, null, null, DataGridNavigationPosition.Unset, DataGridRouteNavigationOrigin.Programmatic, false);
    }

    /// <summary>
    /// Describes an application-route operation after grid context has been resolved.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridRouteNavigationRequest
    {
        /// <summary>
        /// Initializes an application-route request.
        /// </summary>
        /// <param name="kind">The history or navigation operation.</param>
        /// <param name="route">The destination route, or an unset route for Back and Forward.</param>
        /// <param name="context">The grid context that produced the request.</param>
        public DataGridRouteNavigationRequest(
            DataGridRouteNavigationKind kind,
            DataGridRoute route,
            DataGridRouteContext context)
        {
            Kind = kind;
            Route = route;
            Context = context;
        }

        /// <summary>Gets the requested history or navigation operation.</summary>
        public DataGridRouteNavigationKind Kind { get; }

        /// <summary>Gets the destination route.</summary>
        public DataGridRoute Route { get; }

        /// <summary>Gets the grid context that produced the request.</summary>
        public DataGridRouteContext Context { get; }

        /// <summary>Gets whether this request contains all values required for its operation.</summary>
        public bool IsValid =>
            Kind is DataGridRouteNavigationKind.Back or DataGridRouteNavigationKind.Forward || Route.IsValid;
    }

    /// <summary>
    /// Describes the completed outcome of an application-route request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridRouteNavigationResult
    {
        /// <summary>
        /// Initializes a route result.
        /// </summary>
        /// <param name="status">The non-throwing result status.</param>
        /// <param name="currentRoute">The route active after completion when known.</param>
        /// <param name="error">The exception reported by the host when navigation failed.</param>
        public DataGridRouteNavigationResult(
            DataGridRouteNavigationStatus status,
            DataGridRoute currentRoute = default,
            Exception error = null)
        {
            Status = status;
            CurrentRoute = currentRoute;
            Error = error;
        }

        /// <summary>Gets the non-throwing result status.</summary>
        public DataGridRouteNavigationStatus Status { get; }

        /// <summary>Gets whether the operation completed successfully.</summary>
        public bool Succeeded => Status == DataGridRouteNavigationStatus.Succeeded;

        /// <summary>Gets the route active after completion when known.</summary>
        public DataGridRoute CurrentRoute { get; }

        /// <summary>Gets the exception reported by the host when navigation failed.</summary>
        public Exception Error { get; }

        /// <summary>Creates a successful result.</summary>
        /// <param name="currentRoute">The route active after completion when known.</param>
        /// <returns>A successful result.</returns>
        public static DataGridRouteNavigationResult Success(DataGridRoute currentRoute = default) =>
            new(DataGridRouteNavigationStatus.Succeeded, currentRoute);

        /// <summary>Creates a result with the supplied status.</summary>
        /// <param name="status">A non-success result status.</param>
        /// <returns>A result containing the supplied status.</returns>
        public static DataGridRouteNavigationResult FromStatus(DataGridRouteNavigationStatus status) => new(status);

        /// <summary>Creates a failed result without throwing through the input pipeline.</summary>
        /// <param name="error">The exception reported by the route host.</param>
        /// <returns>A failed result containing <paramref name="error"/>.</returns>
        public static DataGridRouteNavigationResult Failure(Exception error) =>
            new(DataGridRouteNavigationStatus.Failed, default, error);
    }

    /// <summary>
    /// Resolves stable grid context into a framework-neutral application route.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridRouteResolver
    {
        /// <summary>Attempts to resolve a route for the supplied row and column context.</summary>
        /// <param name="context">The stable grid context.</param>
        /// <param name="route">The resolved route.</param>
        /// <returns><see langword="true"/> when a valid route was resolved.</returns>
        bool TryResolve(DataGridRouteContext context, out DataGridRoute route);
    }

    /// <summary>
    /// Executes framework-neutral route requests using an application navigation host.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridRouteNavigator
    {
        /// <summary>Gets the route operations supported by this navigator.</summary>
        DataGridRouteNavigationCapabilities Capabilities { get; }

        /// <summary>Executes a route request.</summary>
        /// <param name="request">The resolved route request.</param>
        /// <param name="cancellationToken">Cancels navigation or an activation guard.</param>
        /// <returns>A non-throwing navigation outcome.</returns>
        ValueTask<DataGridRouteNavigationResult> NavigateAsync(
            DataGridRouteNavigationRequest request,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Coordinates route resolution, cancelable preview, host execution, and completion telemetry.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridRouteNavigationModel : INotifyPropertyChanged
    {
        /// <summary>Raised before a resolved request is sent to the route host.</summary>
        event EventHandler<DataGridRouteNavigationChangingEventArgs> NavigationChanging;

        /// <summary>Raised after a route operation completes.</summary>
        event EventHandler<DataGridRouteNavigationChangedEventArgs> NavigationChanged;

        /// <summary>Gets whether a route operation is currently in progress.</summary>
        bool IsNavigating { get; }

        /// <summary>Gets the last route reported as active by the host.</summary>
        DataGridRoute CurrentRoute { get; }

        /// <summary>Gets the result of the last completed operation.</summary>
        DataGridRouteNavigationResult LastResult { get; }

        /// <summary>Determines whether the model can issue an operation for the supplied context.</summary>
        bool CanNavigate(DataGridRouteNavigationKind kind, DataGridRouteContext context);

        /// <summary>Resolves and executes an application-route operation.</summary>
        ValueTask<DataGridRouteNavigationResult> NavigateAsync(
            DataGridRouteNavigationKind kind,
            DataGridRouteContext context,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides cancelable preview data for a resolved application-route request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridRouteNavigationChangingEventArgs : CancelEventArgs
    {
        /// <summary>Initializes cancelable route preview data.</summary>
        /// <param name="request">The resolved route request.</param>
        public DataGridRouteNavigationChangingEventArgs(DataGridRouteNavigationRequest request)
        {
            Request = request;
        }

        /// <summary>Gets or sets the request sent to the route host when not canceled.</summary>
        public DataGridRouteNavigationRequest Request { get; set; }
    }

    /// <summary>
    /// Provides completion data for an application-route request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridRouteNavigationChangedEventArgs : EventArgs
    {
        /// <summary>Initializes route completion data.</summary>
        /// <param name="request">The request sent to the route host.</param>
        /// <param name="result">The non-throwing host result.</param>
        public DataGridRouteNavigationChangedEventArgs(
            DataGridRouteNavigationRequest request,
            DataGridRouteNavigationResult result)
        {
            Request = request;
            Result = result;
        }

        /// <summary>Gets the request sent to the route host.</summary>
        public DataGridRouteNavigationRequest Request { get; }

        /// <summary>Gets the non-throwing host result.</summary>
        public DataGridRouteNavigationResult Result { get; }
    }

    /// <summary>
    /// Default framework-neutral route model used by ReactiveUI, Prism, Toolkit-style, and custom adapters.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridRouteNavigationModel : IDataGridRouteNavigationModel
    {
        private readonly IDataGridRouteResolver _resolver;
        private readonly IDataGridRouteNavigator _navigator;
        private bool _isNavigating;
        private DataGridRoute _currentRoute;
        private DataGridRouteNavigationResult _lastResult;

        /// <summary>Initializes a route model from a resolver and application navigator.</summary>
        /// <param name="resolver">Maps grid row and column context to routes.</param>
        /// <param name="navigator">Executes routes using the application navigation host.</param>
        public DataGridRouteNavigationModel(
            IDataGridRouteResolver resolver,
            IDataGridRouteNavigator navigator)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _lastResult = DataGridRouteNavigationResult.FromStatus(DataGridRouteNavigationStatus.InvalidRequest);
        }

        /// <inheritdoc />
        public event EventHandler<DataGridRouteNavigationChangingEventArgs> NavigationChanging;

        /// <inheritdoc />
        public event EventHandler<DataGridRouteNavigationChangedEventArgs> NavigationChanged;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <inheritdoc />
        public bool IsNavigating => _isNavigating;

        /// <inheritdoc />
        public DataGridRoute CurrentRoute => _currentRoute;

        /// <inheritdoc />
        public DataGridRouteNavigationResult LastResult => _lastResult;

        /// <inheritdoc />
        public bool CanNavigate(DataGridRouteNavigationKind kind, DataGridRouteContext context)
        {
            if (!Supports(kind))
            {
                return false;
            }

            if (kind is DataGridRouteNavigationKind.Back or DataGridRouteNavigationKind.Forward)
            {
                return true;
            }

            return _resolver.TryResolve(context, out DataGridRoute route) && route.IsValid;
        }

        /// <inheritdoc />
        public async ValueTask<DataGridRouteNavigationResult> NavigateAsync(
            DataGridRouteNavigationKind kind,
            DataGridRouteContext context,
            CancellationToken cancellationToken = default)
        {
            DataGridRoute route = DataGridRoute.Unset;
            if (!Supports(kind))
            {
                return Complete(
                    new DataGridRouteNavigationRequest(kind, route, context),
                    DataGridRouteNavigationResult.FromStatus(DataGridRouteNavigationStatus.NotSupported));
            }

            if (kind is not DataGridRouteNavigationKind.Back and not DataGridRouteNavigationKind.Forward &&
                (!_resolver.TryResolve(context, out route) || !route.IsValid))
            {
                return Complete(
                    new DataGridRouteNavigationRequest(kind, route, context),
                    DataGridRouteNavigationResult.FromStatus(DataGridRouteNavigationStatus.RouteNotFound));
            }

            var request = new DataGridRouteNavigationRequest(kind, route, context);
            var changing = new DataGridRouteNavigationChangingEventArgs(request);
            NavigationChanging?.Invoke(this, changing);
            request = changing.Request;
            if (changing.Cancel)
            {
                return Complete(
                    request,
                    DataGridRouteNavigationResult.FromStatus(DataGridRouteNavigationStatus.Canceled));
            }

            if (!request.IsValid)
            {
                return Complete(
                    request,
                    DataGridRouteNavigationResult.FromStatus(DataGridRouteNavigationStatus.InvalidRequest));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(
                    request,
                    DataGridRouteNavigationResult.FromStatus(DataGridRouteNavigationStatus.Canceled));
            }

            SetIsNavigating(true);
            DataGridRouteNavigationResult result;
            try
            {
                result = await _navigator.NavigateAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = DataGridRouteNavigationResult.FromStatus(DataGridRouteNavigationStatus.Canceled);
            }
            catch (Exception error)
            {
                result = DataGridRouteNavigationResult.Failure(error);
            }
            finally
            {
                SetIsNavigating(false);
            }

            return Complete(request, result);
        }

        private bool Supports(DataGridRouteNavigationKind kind)
        {
            DataGridRouteNavigationCapabilities required = kind switch
            {
                DataGridRouteNavigationKind.Navigate => DataGridRouteNavigationCapabilities.Navigate,
                DataGridRouteNavigationKind.Replace => DataGridRouteNavigationCapabilities.Replace,
                DataGridRouteNavigationKind.Reset => DataGridRouteNavigationCapabilities.Reset,
                DataGridRouteNavigationKind.Back => DataGridRouteNavigationCapabilities.Back,
                DataGridRouteNavigationKind.Forward => DataGridRouteNavigationCapabilities.Forward,
                _ => DataGridRouteNavigationCapabilities.None
            };
            return required != DataGridRouteNavigationCapabilities.None &&
                (_navigator.Capabilities & required) == required;
        }

        private DataGridRouteNavigationResult Complete(
            DataGridRouteNavigationRequest request,
            DataGridRouteNavigationResult result)
        {
            DataGridRoute activeRoute = result.CurrentRoute;
            if (result.Succeeded && !activeRoute.IsValid &&
                request.Kind is DataGridRouteNavigationKind.Navigate or
                    DataGridRouteNavigationKind.Replace or
                    DataGridRouteNavigationKind.Reset)
            {
                activeRoute = request.Route;
                result = DataGridRouteNavigationResult.Success(activeRoute);
            }

            if (result.Succeeded && activeRoute.IsValid && activeRoute != _currentRoute)
            {
                _currentRoute = activeRoute;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentRoute)));
            }

            _lastResult = result;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastResult)));
            NavigationChanged?.Invoke(this, new DataGridRouteNavigationChangedEventArgs(request, result));
            return result;
        }

        private void SetIsNavigating(bool value)
        {
            if (_isNavigating == value)
            {
                return;
            }

            _isNavigating = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNavigating)));
        }
    }

    /// <summary>
    /// Resolves routes through an AOT-safe delegate supplied by application composition.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DelegateDataGridRouteResolver : IDataGridRouteResolver
    {
        private readonly Func<DataGridRouteContext, DataGridRoute?> _resolver;

        /// <summary>Initializes a delegate route resolver.</summary>
        /// <param name="resolver">Returns a route or null when the context is not routable.</param>
        public DelegateDataGridRouteResolver(Func<DataGridRouteContext, DataGridRoute?> resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <inheritdoc />
        public bool TryResolve(DataGridRouteContext context, out DataGridRoute route)
        {
            DataGridRoute? resolved = _resolver(context);
            route = resolved.GetValueOrDefault();
            return resolved.HasValue && route.IsValid;
        }
    }

    /// <summary>
    /// Executes route operations through an AOT-safe delegate supplied by a framework adapter.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DelegateDataGridRouteNavigator : IDataGridRouteNavigator
    {
        private readonly Func<DataGridRouteNavigationRequest, CancellationToken, ValueTask<DataGridRouteNavigationResult>> _navigate;

        /// <summary>Initializes a delegate route navigator.</summary>
        /// <param name="capabilities">The operations implemented by the delegate.</param>
        /// <param name="navigate">The async route operation.</param>
        public DelegateDataGridRouteNavigator(
            DataGridRouteNavigationCapabilities capabilities,
            Func<DataGridRouteNavigationRequest, CancellationToken, ValueTask<DataGridRouteNavigationResult>> navigate)
        {
            Capabilities = capabilities;
            _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        }

        /// <inheritdoc />
        public DataGridRouteNavigationCapabilities Capabilities { get; }

        /// <inheritdoc />
        public ValueTask<DataGridRouteNavigationResult> NavigateAsync(
            DataGridRouteNavigationRequest request,
            CancellationToken cancellationToken = default) =>
            _navigate(request, cancellationToken);
    }
}
