// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;

namespace Avalonia.Controls.DataGridNavigation
{
    /// <summary>Identifies a normalized input event independently of a UI framework.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationInputKind
    {
        /// <summary>A key was pressed.</summary>
        KeyDown,
        /// <summary>A key was released.</summary>
        KeyUp,
        /// <summary>A pointer button was pressed.</summary>
        PointerPressed,
        /// <summary>A pointer button was released.</summary>
        PointerReleased,
        /// <summary>A pointer wheel changed.</summary>
        PointerWheel
    }

    /// <summary>Identifies a stable logical key used by a navigation input model.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationInputKey
    {
        /// <summary>No key.</summary>
        None,
        /// <summary>A platform key without a normalized representation.</summary>
        Unknown,
        /// <summary>The Up Arrow key.</summary>
        Up,
        /// <summary>The Down Arrow key.</summary>
        Down,
        /// <summary>The Left Arrow key.</summary>
        Left,
        /// <summary>The Right Arrow key.</summary>
        Right,
        /// <summary>The Page Up key.</summary>
        PageUp,
        /// <summary>The Page Down key.</summary>
        PageDown,
        /// <summary>The Home key.</summary>
        Home,
        /// <summary>The End key.</summary>
        End,
        /// <summary>The Tab key.</summary>
        Tab,
        /// <summary>The Enter or Return key.</summary>
        Enter,
        /// <summary>The Escape key.</summary>
        Escape,
        /// <summary>The Space key.</summary>
        Space,
        /// <summary>The Backspace key.</summary>
        Backspace,
        /// <summary>The Insert key.</summary>
        Insert,
        /// <summary>The Delete key.</summary>
        Delete,
        /// <summary>The add or plus key.</summary>
        Add,
        /// <summary>The subtract or minus key.</summary>
        Subtract,
        /// <summary>The multiply key.</summary>
        Multiply,
        /// <summary>The divide key.</summary>
        Divide,
        /// <summary>The decimal-point key.</summary>
        Decimal,
        /// <summary>The browser-history Back key.</summary>
        BrowserBack,
        /// <summary>The browser-history Forward key.</summary>
        BrowserForward,
        /// <summary>The A key.</summary>
        A,
        /// <summary>The B key.</summary>
        B,
        /// <summary>The C key.</summary>
        C,
        /// <summary>The D key.</summary>
        D,
        /// <summary>The E key.</summary>
        E,
        /// <summary>The F key.</summary>
        F,
        /// <summary>The G key.</summary>
        G,
        /// <summary>The H key.</summary>
        H,
        /// <summary>The I key.</summary>
        I,
        /// <summary>The J key.</summary>
        J,
        /// <summary>The K key.</summary>
        K,
        /// <summary>The L key.</summary>
        L,
        /// <summary>The M key.</summary>
        M,
        /// <summary>The N key.</summary>
        N,
        /// <summary>The O key.</summary>
        O,
        /// <summary>The P key.</summary>
        P,
        /// <summary>The Q key.</summary>
        Q,
        /// <summary>The R key.</summary>
        R,
        /// <summary>The S key.</summary>
        S,
        /// <summary>The T key.</summary>
        T,
        /// <summary>The U key.</summary>
        U,
        /// <summary>The V key.</summary>
        V,
        /// <summary>The W key.</summary>
        W,
        /// <summary>The X key.</summary>
        X,
        /// <summary>The Y key.</summary>
        Y,
        /// <summary>The Z key.</summary>
        Z,
        /// <summary>The 0 digit key.</summary>
        D0,
        /// <summary>The 1 digit key.</summary>
        D1,
        /// <summary>The 2 digit key.</summary>
        D2,
        /// <summary>The 3 digit key.</summary>
        D3,
        /// <summary>The 4 digit key.</summary>
        D4,
        /// <summary>The 5 digit key.</summary>
        D5,
        /// <summary>The 6 digit key.</summary>
        D6,
        /// <summary>The 7 digit key.</summary>
        D7,
        /// <summary>The 8 digit key.</summary>
        D8,
        /// <summary>The 9 digit key.</summary>
        D9,
        /// <summary>The F1 function key.</summary>
        F1,
        /// <summary>The F2 function key.</summary>
        F2,
        /// <summary>The F3 function key.</summary>
        F3,
        /// <summary>The F4 function key.</summary>
        F4,
        /// <summary>The F5 function key.</summary>
        F5,
        /// <summary>The F6 function key.</summary>
        F6,
        /// <summary>The F7 function key.</summary>
        F7,
        /// <summary>The F8 function key.</summary>
        F8,
        /// <summary>The F9 function key.</summary>
        F9,
        /// <summary>The F10 function key.</summary>
        F10,
        /// <summary>The F11 function key.</summary>
        F11,
        /// <summary>The F12 function key.</summary>
        F12
    }

    /// <summary>Identifies the normalized device that produced a key event.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationKeyDeviceKind
    {
        /// <summary>A keyboard produced the event.</summary>
        Keyboard,
        /// <summary>A gamepad produced the event.</summary>
        Gamepad,
        /// <summary>A remote-control device produced the event.</summary>
        Remote,
        /// <summary>The input system did not identify the key device.</summary>
        Unknown
    }

    /// <summary>Identifies normalized input modifiers.</summary>
    [Flags]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationInputModifiers
    {
        /// <summary>No modifier is active.</summary>
        None = 0,
        /// <summary>The Shift modifier is active.</summary>
        Shift = 1 << 0,
        /// <summary>The Control modifier is active.</summary>
        Control = 1 << 1,
        /// <summary>The Alt modifier is active.</summary>
        Alt = 1 << 2,
        /// <summary>The platform Meta or Command modifier is active.</summary>
        Meta = 1 << 3
    }

    /// <summary>Identifies the normalized pointer device family.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationPointerDeviceKind
    {
        /// <summary>The pointer device is unknown.</summary>
        Unknown,
        /// <summary>A mouse produced the event.</summary>
        Mouse,
        /// <summary>A pen produced the event.</summary>
        Pen,
        /// <summary>A touch contact produced the event.</summary>
        Touch
    }

    /// <summary>Identifies a normalized pointer button.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationPointerButton
    {
        /// <summary>No pointer button applies.</summary>
        None,
        /// <summary>The primary pointer button.</summary>
        Primary,
        /// <summary>The secondary pointer button.</summary>
        Secondary,
        /// <summary>The middle pointer button.</summary>
        Middle,
        /// <summary>The first extended pointer button.</summary>
        XButton1,
        /// <summary>The second extended pointer button.</summary>
        XButton2
    }

    /// <summary>Identifies pointer-wheel movement for input matching.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationWheelDirection
    {
        /// <summary>No wheel movement applies.</summary>
        None,
        /// <summary>The wheel moved upward.</summary>
        Up,
        /// <summary>The wheel moved downward.</summary>
        Down,
        /// <summary>The wheel moved left.</summary>
        Left,
        /// <summary>The wheel moved right.</summary>
        Right
    }

    /// <summary>Identifies the semantic grid target under an input event.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationInputTargetKind
    {
        /// <summary>Matches every target kind in a binding.</summary>
        Any,
        /// <summary>The grid background or root.</summary>
        Grid,
        /// <summary>A data cell.</summary>
        Cell,
        /// <summary>A data row outside a cell.</summary>
        Row,
        /// <summary>A row header.</summary>
        RowHeader,
        /// <summary>A column header.</summary>
        ColumnHeader,
        /// <summary>A row-group header.</summary>
        GroupHeader,
        /// <summary>No semantic grid target was found.</summary>
        Empty
    }

    /// <summary>Describes normalized key or pointer input without routed-event or visual-tree objects.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridNavigationInputRequest
    {
        /// <summary>Initializes a complete normalized input request.</summary>
        /// <param name="kind">The normalized event kind.</param>
        /// <param name="key">The logical key, or <see cref="DataGridNavigationInputKey.None"/> for pointer input.</param>
        /// <param name="physicalKey">The layout-independent physical key, when available.</param>
        /// <param name="keyDevice">The device family for key input.</param>
        /// <param name="modifiers">The active normalized modifiers.</param>
        /// <param name="pointerDevice">The pointer device family.</param>
        /// <param name="pointerButton">The pointer button involved in the event.</param>
        /// <param name="wheelDirection">The dominant pointer-wheel direction.</param>
        /// <param name="clickCount">The pointer click count, or zero when not applicable.</param>
        /// <param name="x">The pointer X coordinate relative to the grid, or <see cref="double.NaN"/> for key input.</param>
        /// <param name="y">The pointer Y coordinate relative to the grid, or <see cref="double.NaN"/> for key input.</param>
        /// <param name="wheelDeltaX">The horizontal wheel delta.</param>
        /// <param name="wheelDeltaY">The vertical wheel delta.</param>
        /// <param name="targetKind">The semantic target under the event source.</param>
        /// <param name="targetPosition">The target data-cell position, when one can be resolved.</param>
        /// <param name="currentPosition">The current data-cell position.</param>
        /// <param name="isEditing">Whether the grid is editing a cell.</param>
        public DataGridNavigationInputRequest(
            DataGridNavigationInputKind kind,
            DataGridNavigationInputKey key,
            DataGridNavigationInputKey physicalKey,
            DataGridNavigationKeyDeviceKind keyDevice,
            DataGridNavigationInputModifiers modifiers,
            DataGridNavigationPointerDeviceKind pointerDevice,
            DataGridNavigationPointerButton pointerButton,
            DataGridNavigationWheelDirection wheelDirection,
            int clickCount,
            double x,
            double y,
            double wheelDeltaX,
            double wheelDeltaY,
            DataGridNavigationInputTargetKind targetKind,
            DataGridNavigationPosition targetPosition,
            DataGridNavigationPosition currentPosition,
            bool isEditing)
        {
            Kind = kind;
            Key = key;
            PhysicalKey = physicalKey;
            KeyDevice = keyDevice;
            Modifiers = modifiers;
            PointerDevice = pointerDevice;
            PointerButton = pointerButton;
            WheelDirection = wheelDirection;
            ClickCount = clickCount;
            X = x;
            Y = y;
            WheelDeltaX = wheelDeltaX;
            WheelDeltaY = wheelDeltaY;
            TargetKind = targetKind;
            TargetPosition = targetPosition;
            CurrentPosition = currentPosition;
            IsEditing = isEditing;
        }

        /// <summary>Gets the normalized event kind.</summary>
        public DataGridNavigationInputKind Kind { get; }
        /// <summary>Gets the normalized logical key.</summary>
        public DataGridNavigationInputKey Key { get; }
        /// <summary>Gets the normalized layout-independent physical key.</summary>
        public DataGridNavigationInputKey PhysicalKey { get; }
        /// <summary>Gets the normalized key-device family.</summary>
        public DataGridNavigationKeyDeviceKind KeyDevice { get; }
        /// <summary>Gets the active modifiers.</summary>
        public DataGridNavigationInputModifiers Modifiers { get; }
        /// <summary>Gets the pointer-device family.</summary>
        public DataGridNavigationPointerDeviceKind PointerDevice { get; }
        /// <summary>Gets the pointer button involved in the event.</summary>
        public DataGridNavigationPointerButton PointerButton { get; }
        /// <summary>Gets the dominant wheel direction.</summary>
        public DataGridNavigationWheelDirection WheelDirection { get; }
        /// <summary>Gets the pointer click count.</summary>
        public int ClickCount { get; }
        /// <summary>Gets the pointer X coordinate relative to the grid.</summary>
        public double X { get; }
        /// <summary>Gets the pointer Y coordinate relative to the grid.</summary>
        public double Y { get; }
        /// <summary>Gets the horizontal pointer-wheel delta.</summary>
        public double WheelDeltaX { get; }
        /// <summary>Gets the vertical pointer-wheel delta.</summary>
        public double WheelDeltaY { get; }
        /// <summary>Gets the semantic target kind.</summary>
        public DataGridNavigationInputTargetKind TargetKind { get; }
        /// <summary>Gets the data-cell target under the event, when available.</summary>
        public DataGridNavigationPosition TargetPosition { get; }
        /// <summary>Gets the current data-cell position.</summary>
        public DataGridNavigationPosition CurrentPosition { get; }
        /// <summary>Gets whether the grid is editing a cell.</summary>
        public bool IsEditing { get; }
    }

    /// <summary>Identifies how a navigation input result is executed.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationInputDecision
    {
        /// <summary>Leaves the event available to controls and built-in grid input.</summary>
        Ignore,
        /// <summary>Consumes the event without navigating.</summary>
        Handle,
        /// <summary>Executes a semantic cell-navigation command.</summary>
        Navigate,
        /// <summary>Navigates to the cell or row resolved under the event source.</summary>
        NavigateToTarget,
        /// <summary>Navigates to an explicit data-cell position.</summary>
        NavigateToPosition,
        /// <summary>Executes an application-route operation.</summary>
        NavigateRoute
    }

    /// <summary>Contains the framework-neutral result of resolving normalized input.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridNavigationInputResult
    {
        private DataGridNavigationInputResult(
            DataGridNavigationInputDecision decision,
            DataGridNavigationCommand command,
            DataGridNavigationPosition target,
            DataGridRouteNavigationKind routeKind,
            bool consumeWhenNavigationFails)
        {
            Decision = decision;
            Command = command;
            Target = target;
            RouteKind = routeKind;
            ConsumeWhenNavigationFails = consumeWhenNavigationFails;
        }

        /// <summary>Gets how the resolved input is executed.</summary>
        public DataGridNavigationInputDecision Decision { get; }
        /// <summary>Gets the semantic cell-navigation command.</summary>
        public DataGridNavigationCommand Command { get; }
        /// <summary>Gets the explicit cell target.</summary>
        public DataGridNavigationPosition Target { get; }
        /// <summary>Gets the application-route operation.</summary>
        public DataGridRouteNavigationKind RouteKind { get; }
        /// <summary>Gets whether the routed input is consumed when navigation cannot be completed.</summary>
        public bool ConsumeWhenNavigationFails { get; }

        /// <summary>Creates a result that preserves existing input processing.</summary>
        public static DataGridNavigationInputResult Ignore() => default;
        /// <summary>Creates a result that consumes input without navigating.</summary>
        public static DataGridNavigationInputResult Handle() =>
            new(DataGridNavigationInputDecision.Handle, DataGridNavigationCommand.None, default, default, true);
        /// <summary>Creates a semantic cell-navigation result.</summary>
        /// <param name="command">The semantic command to execute.</param>
        /// <param name="consumeWhenNavigationFails">Whether to consume input if the command cannot be completed.</param>
        public static DataGridNavigationInputResult Navigate(
            DataGridNavigationCommand command,
            bool consumeWhenNavigationFails = false) =>
            new(DataGridNavigationInputDecision.Navigate, command, default, default, consumeWhenNavigationFails);
        /// <summary>Creates a result that navigates to the event's resolved cell or row target.</summary>
        /// <param name="consumeWhenNavigationFails">Whether to consume input if the target cannot be activated.</param>
        public static DataGridNavigationInputResult NavigateToTarget(bool consumeWhenNavigationFails = false) =>
            new(DataGridNavigationInputDecision.NavigateToTarget, DataGridNavigationCommand.None, default, default, consumeWhenNavigationFails);
        /// <summary>Creates an explicit cell-target result.</summary>
        /// <param name="target">The target row and column display indexes.</param>
        /// <param name="consumeWhenNavigationFails">Whether to consume input if the target cannot be activated.</param>
        public static DataGridNavigationInputResult NavigateTo(
            DataGridNavigationPosition target,
            bool consumeWhenNavigationFails = false) =>
            new(DataGridNavigationInputDecision.NavigateToPosition, DataGridNavigationCommand.None, target, default, consumeWhenNavigationFails);
        /// <summary>Creates an application-route navigation result.</summary>
        /// <param name="kind">The route operation to execute.</param>
        /// <param name="consumeWhenNavigationFails">Whether to consume input if route navigation cannot start.</param>
        public static DataGridNavigationInputResult NavigateRoute(
            DataGridRouteNavigationKind kind,
            bool consumeWhenNavigationFails = false) =>
            new(DataGridNavigationInputDecision.NavigateRoute, DataGridNavigationCommand.None, default, kind, consumeWhenNavigationFails);
    }

    /// <summary>Resolves normalized input into cell or application-route navigation.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridNavigationInputModel
    {
        /// <summary>Resolves normalized key or pointer input into a navigation decision.</summary>
        /// <param name="request">The immutable, framework-neutral input context.</param>
        /// <returns>The decision to execute.</returns>
        DataGridNavigationInputResult Resolve(in DataGridNavigationInputRequest request);
    }

    /// <summary>Provides mutable framework-neutral preview data for a resolved input request.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridNavigationInputResolvingEventArgs : EventArgs
    {
        /// <summary>Initializes resolving event data.</summary>
        /// <param name="request">The normalized input request.</param>
        /// <param name="result">The binding table's initial result.</param>
        public DataGridNavigationInputResolvingEventArgs(
            DataGridNavigationInputRequest request,
            DataGridNavigationInputResult result)
        {
            Request = request;
            Result = result;
        }

        /// <summary>Gets the normalized input request.</summary>
        public DataGridNavigationInputRequest Request { get; }
        /// <summary>Gets or sets the decision that the grid will execute.</summary>
        public DataGridNavigationInputResult Result { get; set; }
    }

    /// <summary>Defines an immutable normalized input binding.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridNavigationInputBinding
    {
        private DataGridNavigationInputBinding(
            DataGridNavigationInputKind kind,
            DataGridNavigationInputKey key,
            DataGridNavigationInputKey physicalKey,
            DataGridNavigationPointerButton pointerButton,
            DataGridNavigationWheelDirection wheelDirection,
            DataGridNavigationInputModifiers modifiers,
            bool exactModifiers,
            int clickCount,
            DataGridNavigationInputTargetKind targetKind,
            DataGridNavigationInputResult result)
        {
            Kind = kind;
            Key = key;
            PhysicalKey = physicalKey;
            PointerButton = pointerButton;
            WheelDirection = wheelDirection;
            Modifiers = modifiers;
            ExactModifiers = exactModifiers;
            ClickCount = clickCount;
            TargetKind = targetKind;
            Result = result;
        }

        /// <summary>Gets the event kind matched by the binding.</summary>
        public DataGridNavigationInputKind Kind { get; }
        /// <summary>Gets the logical key matched by the binding.</summary>
        public DataGridNavigationInputKey Key { get; }
        /// <summary>Gets the layout-independent physical key matched by the binding.</summary>
        public DataGridNavigationInputKey PhysicalKey { get; }
        /// <summary>Gets the pointer button matched by the binding.</summary>
        public DataGridNavigationPointerButton PointerButton { get; }
        /// <summary>Gets the wheel direction matched by the binding.</summary>
        public DataGridNavigationWheelDirection WheelDirection { get; }
        /// <summary>Gets the required modifiers.</summary>
        public DataGridNavigationInputModifiers Modifiers { get; }
        /// <summary>Gets whether modifiers must match exactly instead of containing the required flags.</summary>
        public bool ExactModifiers { get; }
        /// <summary>Gets the required click count, or zero to match every count.</summary>
        public int ClickCount { get; }
        /// <summary>Gets the required semantic target, or <see cref="DataGridNavigationInputTargetKind.Any"/>.</summary>
        public DataGridNavigationInputTargetKind TargetKind { get; }
        /// <summary>Gets the result returned when the binding matches.</summary>
        public DataGridNavigationInputResult Result { get; }

        /// <summary>Creates a key-down binding.</summary>
        /// <param name="key">The logical key to match.</param>
        /// <param name="result">The result returned on a match.</param>
        /// <param name="modifiers">The required modifiers.</param>
        /// <param name="exactModifiers">Whether additional modifiers prevent a match.</param>
        public static DataGridNavigationInputBinding KeyDown(
            DataGridNavigationInputKey key,
            DataGridNavigationInputResult result,
            DataGridNavigationInputModifiers modifiers = DataGridNavigationInputModifiers.None,
            bool exactModifiers = false) =>
            new(DataGridNavigationInputKind.KeyDown, key, default, default, default, modifiers, exactModifiers, 0,
                DataGridNavigationInputTargetKind.Any, result);

        /// <summary>Creates a layout-independent physical key-down binding.</summary>
        /// <param name="key">The normalized physical key to match.</param>
        /// <param name="result">The result returned on a match.</param>
        /// <param name="modifiers">The required modifiers.</param>
        /// <param name="exactModifiers">Whether additional modifiers prevent a match.</param>
        public static DataGridNavigationInputBinding PhysicalKeyDown(
            DataGridNavigationInputKey key,
            DataGridNavigationInputResult result,
            DataGridNavigationInputModifiers modifiers = DataGridNavigationInputModifiers.None,
            bool exactModifiers = false) =>
            new(DataGridNavigationInputKind.KeyDown, default, key, default, default, modifiers, exactModifiers, 0,
                DataGridNavigationInputTargetKind.Any, result);

        /// <summary>Creates a key-up binding.</summary>
        /// <param name="key">The logical key to match.</param>
        /// <param name="result">The result returned on a match.</param>
        /// <param name="modifiers">The required modifiers.</param>
        /// <param name="exactModifiers">Whether additional modifiers prevent a match.</param>
        public static DataGridNavigationInputBinding KeyUp(
            DataGridNavigationInputKey key,
            DataGridNavigationInputResult result,
            DataGridNavigationInputModifiers modifiers = DataGridNavigationInputModifiers.None,
            bool exactModifiers = false) =>
            new(DataGridNavigationInputKind.KeyUp, key, default, default, default, modifiers, exactModifiers, 0,
                DataGridNavigationInputTargetKind.Any, result);

        /// <summary>Creates a layout-independent physical key-up binding.</summary>
        /// <param name="key">The normalized physical key to match.</param>
        /// <param name="result">The result returned on a match.</param>
        /// <param name="modifiers">The required modifiers.</param>
        /// <param name="exactModifiers">Whether additional modifiers prevent a match.</param>
        public static DataGridNavigationInputBinding PhysicalKeyUp(
            DataGridNavigationInputKey key,
            DataGridNavigationInputResult result,
            DataGridNavigationInputModifiers modifiers = DataGridNavigationInputModifiers.None,
            bool exactModifiers = false) =>
            new(DataGridNavigationInputKind.KeyUp, default, key, default, default, modifiers, exactModifiers, 0,
                DataGridNavigationInputTargetKind.Any, result);

        /// <summary>Creates a pointer press or release binding.</summary>
        /// <param name="kind">Either <see cref="DataGridNavigationInputKind.PointerPressed"/> or <see cref="DataGridNavigationInputKind.PointerReleased"/>.</param>
        /// <param name="button">The pointer button to match, or <see cref="DataGridNavigationPointerButton.None"/> for any button.</param>
        /// <param name="result">The result returned on a match.</param>
        /// <param name="clickCount">The click count to match, or zero for every count.</param>
        /// <param name="modifiers">The required modifiers.</param>
        /// <param name="exactModifiers">Whether additional modifiers prevent a match.</param>
        /// <param name="targetKind">The semantic target to match.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a pointer press or release, or <paramref name="clickCount"/> is negative.</exception>
        public static DataGridNavigationInputBinding Pointer(
            DataGridNavigationInputKind kind,
            DataGridNavigationPointerButton button,
            DataGridNavigationInputResult result,
            int clickCount = 0,
            DataGridNavigationInputModifiers modifiers = DataGridNavigationInputModifiers.None,
            bool exactModifiers = false,
            DataGridNavigationInputTargetKind targetKind = DataGridNavigationInputTargetKind.Any)
        {
            if (kind is not DataGridNavigationInputKind.PointerPressed and
                not DataGridNavigationInputKind.PointerReleased)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (clickCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clickCount));
            }

            return new DataGridNavigationInputBinding(
                kind,
                default,
                default,
                button,
                default,
                modifiers,
                exactModifiers,
                clickCount,
                targetKind,
                result);
        }

        /// <summary>Creates a pointer-wheel binding.</summary>
        /// <param name="direction">The dominant wheel direction to match.</param>
        /// <param name="result">The result returned on a match.</param>
        /// <param name="modifiers">The required modifiers.</param>
        /// <param name="exactModifiers">Whether additional modifiers prevent a match.</param>
        /// <param name="targetKind">The semantic target to match.</param>
        public static DataGridNavigationInputBinding Wheel(
            DataGridNavigationWheelDirection direction,
            DataGridNavigationInputResult result,
            DataGridNavigationInputModifiers modifiers = DataGridNavigationInputModifiers.None,
            bool exactModifiers = false,
            DataGridNavigationInputTargetKind targetKind = DataGridNavigationInputTargetKind.Any) =>
            new(DataGridNavigationInputKind.PointerWheel, default, default, default, direction, modifiers, exactModifiers, 0,
                targetKind, result);

        internal bool Matches(in DataGridNavigationInputRequest request)
        {
            if (Kind != request.Kind ||
                Key != DataGridNavigationInputKey.None && Key != request.Key ||
                PhysicalKey != DataGridNavigationInputKey.None && PhysicalKey != request.PhysicalKey ||
                PointerButton != DataGridNavigationPointerButton.None && PointerButton != request.PointerButton ||
                WheelDirection != DataGridNavigationWheelDirection.None && WheelDirection != request.WheelDirection ||
                ClickCount > 0 && ClickCount != request.ClickCount ||
                TargetKind != DataGridNavigationInputTargetKind.Any && TargetKind != request.TargetKind)
            {
                return false;
            }

            return ExactModifiers
                ? request.Modifiers == Modifiers
                : (request.Modifiers & Modifiers) == Modifiers;
        }
    }

    /// <summary>
    /// Provides an allocation-free binding table and an optional ViewModel-owned resolving event.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridNavigationInputModel : IDataGridNavigationInputModel
    {
        private DataGridNavigationInputBinding[] _bindings;

        /// <summary>Initializes a model with an ordered first-match-wins binding table.</summary>
        /// <param name="bindings">The bindings to evaluate.</param>
        public DataGridNavigationInputModel(params DataGridNavigationInputBinding[] bindings)
        {
            _bindings = bindings ?? Array.Empty<DataGridNavigationInputBinding>();
        }

        /// <summary>
        /// Occurs after the static table is evaluated and permits a ViewModel to replace the decision.
        /// </summary>
        public event EventHandler<DataGridNavigationInputResolvingEventArgs> InputResolving;

        /// <summary>Gets the ordered binding table without allocating a copy.</summary>
        public ReadOnlySpan<DataGridNavigationInputBinding> Bindings => _bindings;

        /// <summary>Replaces the ordered binding table.</summary>
        /// <param name="bindings">The bindings to evaluate.</param>
        public void SetBindings(params DataGridNavigationInputBinding[] bindings)
        {
            _bindings = bindings ?? Array.Empty<DataGridNavigationInputBinding>();
        }

        /// <inheritdoc />
        public DataGridNavigationInputResult Resolve(in DataGridNavigationInputRequest request)
        {
            DataGridNavigationInputResult result = ResolveCore(request);
            EventHandler<DataGridNavigationInputResolvingEventArgs> handler = InputResolving;
            if (handler == null)
            {
                return result;
            }

            var args = new DataGridNavigationInputResolvingEventArgs(request, result);
            handler(this, args);
            return args.Result;
        }

        /// <summary>Resolves the binding table before <see cref="InputResolving"/> observers run.</summary>
        /// <param name="request">The normalized input request.</param>
        /// <returns>The first matching result, or <see cref="DataGridNavigationInputResult.Ignore"/>.</returns>
        protected virtual DataGridNavigationInputResult ResolveCore(in DataGridNavigationInputRequest request)
        {
            DataGridNavigationInputBinding[] bindings = _bindings;
            for (int index = 0; index < bindings.Length; index++)
            {
                if (bindings[index].Matches(request))
                {
                    return bindings[index].Result;
                }
            }

            return DataGridNavigationInputResult.Ignore();
        }
    }
}
