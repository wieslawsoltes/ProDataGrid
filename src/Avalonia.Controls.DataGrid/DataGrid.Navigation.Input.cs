// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Controls.DataGridNavigation;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Avalonia.Controls
{
    partial class DataGrid
    {
        private KeyEventArgs _lastNavigationInputKeyDown;
        private KeyEventArgs _lastNavigationInputKeyUp;

        private void DataGrid_NavigationKeyDown(object sender, KeyEventArgs e)
        {
            ProcessKeyNavigationInputOnTunnel(e, DataGridNavigationInputKind.KeyDown);
        }

        private void DataGrid_NavigationKeyUp(object sender, KeyEventArgs e)
        {
            ProcessKeyNavigationInputOnTunnel(e, DataGridNavigationInputKind.KeyUp);
        }

        private void ProcessKeyNavigationInputOnTunnel(KeyEventArgs e, DataGridNavigationInputKind kind)
        {
            if (e.Handled || NavigationInputModel == null)
            {
                return;
            }

            if (kind == DataGridNavigationInputKind.KeyDown)
            {
                _lastNavigationInputKeyDown = e;
            }
            else
            {
                _lastNavigationInputKeyUp = e;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Keyboard, e);
            if (TryProcessKeyNavigationInput(e, kind, out bool handled))
            {
                e.Handled = handled;
            }
        }

        private void DataGrid_NavigationPointerPressed(object sender, PointerPressedEventArgs e)
        {
            ProcessPointerNavigationInput(e, DataGridNavigationInputKind.PointerPressed);
        }

        private void DataGrid_NavigationPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            ProcessPointerNavigationInput(e, DataGridNavigationInputKind.PointerReleased);
        }

        private void DataGrid_NavigationPointerWheel(object sender, PointerWheelEventArgs e)
        {
            if (e.Handled || NavigationInputModel == null)
            {
                return;
            }

            DataGridNavigationWheelDirection direction = GetWheelDirection(e.Delta.X, e.Delta.Y);
            DataGridNavigationInputRequest request = CreatePointerNavigationInputRequest(
                e,
                DataGridNavigationInputKind.PointerWheel,
                DataGridNavigationPointerButton.None,
                direction,
                clickCount: 0);
            if (TryProcessNavigationInput(request, null, out bool handled))
            {
                e.Handled = handled;
            }
        }

        private void ProcessPointerNavigationInput(PointerEventArgs e, DataGridNavigationInputKind kind)
        {
            if (e.Handled || NavigationInputModel == null)
            {
                return;
            }

            DataGridNavigationPointerButton button = GetPointerButton(e, kind);
            int clickCount = e is PointerPressedEventArgs pressed ? pressed.ClickCount : 0;
            DataGridNavigationInputRequest request = CreatePointerNavigationInputRequest(
                e,
                kind,
                button,
                DataGridNavigationWheelDirection.None,
                clickCount);
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Pointer, e);
            if (TryProcessNavigationInput(request, null, out bool handled))
            {
                e.Handled = handled;
            }
        }

        private bool TryProcessKeyNavigationInput(
            KeyEventArgs e,
            DataGridNavigationInputKind kind,
            out bool handled)
        {
            handled = false;
            if (NavigationInputModel == null)
            {
                return false;
            }

            ResolveNavigationInputTarget(
                e.Source as Visual,
                out DataGridNavigationInputTargetKind targetKind,
                out DataGridNavigationPosition targetPosition);
            DataGridNavigationInputRequest request = new(
                kind,
                NormalizeNavigationInputKey(e.Key),
                NormalizeNavigationInputPhysicalKey(e.PhysicalKey),
                e.KeyDeviceType switch
                {
                    KeyDeviceType.Keyboard => DataGridNavigationKeyDeviceKind.Keyboard,
                    KeyDeviceType.Gamepad => DataGridNavigationKeyDeviceKind.Gamepad,
                    KeyDeviceType.Remote => DataGridNavigationKeyDeviceKind.Remote,
                    _ => DataGridNavigationKeyDeviceKind.Unknown
                },
                NormalizeNavigationInputModifiers(e.KeyModifiers),
                DataGridNavigationPointerDeviceKind.Unknown,
                DataGridNavigationPointerButton.None,
                DataGridNavigationWheelDirection.None,
                clickCount: 0,
                x: double.NaN,
                y: double.NaN,
                wheelDeltaX: 0,
                wheelDeltaY: 0,
                targetKind,
                targetPosition,
                CreateNavigationPosition(),
                _editingColumnIndex != -1);
            return TryProcessNavigationInput(request, e, out handled);
        }

        private bool TakeNavigationKeyInputResolved(KeyEventArgs e, DataGridNavigationInputKind kind)
        {
            if (kind == DataGridNavigationInputKind.KeyDown)
            {
                bool resolved = ReferenceEquals(_lastNavigationInputKeyDown, e);
                if (resolved)
                {
                    _lastNavigationInputKeyDown = null;
                }
                return resolved;
            }

            bool keyUpResolved = ReferenceEquals(_lastNavigationInputKeyUp, e);
            if (keyUpResolved)
            {
                _lastNavigationInputKeyUp = null;
            }
            return keyUpResolved;
        }

        private bool WasNavigationKeyInputResolved(KeyEventArgs e, DataGridNavigationInputKind kind) =>
            ReferenceEquals(
                kind == DataGridNavigationInputKind.KeyDown
                    ? _lastNavigationInputKeyDown
                    : _lastNavigationInputKeyUp,
                e);

        private bool TryProcessNavigationInput(
            in DataGridNavigationInputRequest request,
            KeyEventArgs keyEventArgs,
            out bool handled)
        {
            handled = false;
            IDataGridNavigationInputModel model = NavigationInputModel;
            if (model == null)
            {
                return false;
            }

            DataGridNavigationInputResult result = model.Resolve(request);
            KeyModifiers modifiers = DenormalizeNavigationInputModifiers(request.Modifiers);
            bool navigationHandled;
            switch (result.Decision)
            {
                case DataGridNavigationInputDecision.Ignore:
                    return false;
                case DataGridNavigationInputDecision.Handle:
                    handled = true;
                    return true;
                case DataGridNavigationInputDecision.Navigate:
                    navigationHandled = result.Command != DataGridNavigationCommand.None &&
                        ProcessNavigationCommand(
                            result.Command,
                            keyEventArgs,
                            GetNavigationOrigin(request.Kind),
                            modifiers,
                            allowCtrlForTab: false);
                    break;
                case DataGridNavigationInputDecision.NavigateToTarget:
                    navigationHandled = request.TargetPosition.IsValid &&
                        ProcessNavigationCommand(
                            DataGridNavigationCommand.GoTo,
                            keyEventArgs,
                            GetNavigationOrigin(request.Kind),
                            modifiers,
                            allowCtrlForTab: false,
                            request.TargetPosition);
                    break;
                case DataGridNavigationInputDecision.NavigateToPosition:
                    navigationHandled = result.Target.IsValid &&
                        ProcessNavigationCommand(
                            DataGridNavigationCommand.GoTo,
                            keyEventArgs,
                            GetNavigationOrigin(request.Kind),
                            modifiers,
                            allowCtrlForTab: false,
                            result.Target);
                    break;
                case DataGridNavigationInputDecision.NavigateRoute:
                    navigationHandled = TryNavigateRouteFromInput(result.RouteKind, request.Kind);
                    break;
                default:
                    navigationHandled = false;
                    break;
            }

            handled = navigationHandled || result.ConsumeWhenNavigationFails;
            return true;
        }

        private static DataGridNavigationOrigin GetNavigationOrigin(DataGridNavigationInputKind kind) =>
            kind is DataGridNavigationInputKind.KeyDown or DataGridNavigationInputKind.KeyUp
                ? DataGridNavigationOrigin.Keyboard
                : DataGridNavigationOrigin.Pointer;

        private bool TryNavigateRouteFromInput(
            DataGridRouteNavigationKind kind,
            DataGridNavigationInputKind inputKind)
        {
            IDataGridRouteNavigationModel model = RouteNavigationModel;
            if (model == null)
            {
                return false;
            }

            DataGridRouteNavigationOrigin origin = inputKind is DataGridNavigationInputKind.KeyDown or
                DataGridNavigationInputKind.KeyUp
                    ? DataGridRouteNavigationOrigin.Keyboard
                    : DataGridRouteNavigationOrigin.Pointer;
            DataGridRouteContext context = kind is DataGridRouteNavigationKind.Back or DataGridRouteNavigationKind.Forward
                ? DataGridRouteContext.Empty
                : GetCurrentRouteContext(origin);
            if (!model.CanNavigate(kind, context))
            {
                return false;
            }

            _ = NavigateRouteAsync(kind, origin);
            return true;
        }

        private DataGridNavigationInputRequest CreatePointerNavigationInputRequest(
            PointerEventArgs e,
            DataGridNavigationInputKind kind,
            DataGridNavigationPointerButton button,
            DataGridNavigationWheelDirection wheelDirection,
            int clickCount)
        {
            Point position = e.GetPosition(this);
            ResolveNavigationInputTarget(
                e.Source as Visual,
                out DataGridNavigationInputTargetKind targetKind,
                out DataGridNavigationPosition targetPosition);
            return new DataGridNavigationInputRequest(
                kind,
                DataGridNavigationInputKey.None,
                DataGridNavigationInputKey.None,
                DataGridNavigationKeyDeviceKind.Unknown,
                NormalizeNavigationInputModifiers(e.KeyModifiers),
                e.Pointer.Type switch
                {
                    PointerType.Mouse => DataGridNavigationPointerDeviceKind.Mouse,
                    PointerType.Pen => DataGridNavigationPointerDeviceKind.Pen,
                    PointerType.Touch => DataGridNavigationPointerDeviceKind.Touch,
                    _ => DataGridNavigationPointerDeviceKind.Unknown
                },
                button,
                wheelDirection,
                clickCount,
                position.X,
                position.Y,
                e is PointerWheelEventArgs wheel ? wheel.Delta.X : 0,
                e is PointerWheelEventArgs wheelY ? wheelY.Delta.Y : 0,
                targetKind,
                targetPosition,
                CreateNavigationPosition(),
                _editingColumnIndex != -1);
        }

        private void ResolveNavigationInputTarget(
            Visual source,
            out DataGridNavigationInputTargetKind targetKind,
            out DataGridNavigationPosition targetPosition)
        {
            targetPosition = DataGridNavigationPosition.Unset;
            if (source == null)
            {
                targetKind = DataGridNavigationInputTargetKind.Empty;
                return;
            }

            DataGridCell cell = source as DataGridCell ?? source.FindAncestorOfType<DataGridCell>();
            if (cell?.OwningGrid == this && cell.OwningRow != null && cell.OwningColumn != null)
            {
                targetKind = DataGridNavigationInputTargetKind.Cell;
                targetPosition = new DataGridNavigationPosition(cell.RowIndex, cell.OwningColumn.DisplayIndex);
                return;
            }

            if (source is DataGridRowHeader || source.FindAncestorOfType<DataGridRowHeader>() != null)
            {
                targetKind = DataGridNavigationInputTargetKind.RowHeader;
                return;
            }

            if (source is DataGridColumnHeader || source.FindAncestorOfType<DataGridColumnHeader>() != null)
            {
                targetKind = DataGridNavigationInputTargetKind.ColumnHeader;
                return;
            }

            if (source is DataGridRowGroupHeader || source.FindAncestorOfType<DataGridRowGroupHeader>() != null)
            {
                targetKind = DataGridNavigationInputTargetKind.GroupHeader;
                return;
            }

            DataGridRow row = source as DataGridRow ?? source.FindAncestorOfType<DataGridRow>();
            if (row?.OwningGrid == this)
            {
                targetKind = DataGridNavigationInputTargetKind.Row;
                DataGridNavigationPosition current = CreateNavigationPosition();
                if (current.ColumnDisplayIndex >= 0)
                {
                    targetPosition = new DataGridNavigationPosition(row.Index, current.ColumnDisplayIndex);
                }
                return;
            }

            targetKind = ReferenceEquals(source, this) || source.FindAncestorOfType<DataGrid>() == this
                ? DataGridNavigationInputTargetKind.Grid
                : DataGridNavigationInputTargetKind.Empty;
        }

        private static DataGridNavigationPointerButton GetPointerButton(
            PointerEventArgs e,
            DataGridNavigationInputKind kind)
        {
            PointerPointProperties properties = e.GetCurrentPoint(null).Properties;
            if (kind == DataGridNavigationInputKind.PointerPressed)
            {
                if (properties.IsLeftButtonPressed) return DataGridNavigationPointerButton.Primary;
                if (properties.IsRightButtonPressed) return DataGridNavigationPointerButton.Secondary;
                if (properties.IsMiddleButtonPressed) return DataGridNavigationPointerButton.Middle;
                if (properties.IsXButton1Pressed) return DataGridNavigationPointerButton.XButton1;
                if (properties.IsXButton2Pressed) return DataGridNavigationPointerButton.XButton2;
            }

            return properties.PointerUpdateKind switch
            {
                PointerUpdateKind.LeftButtonReleased => DataGridNavigationPointerButton.Primary,
                PointerUpdateKind.RightButtonReleased => DataGridNavigationPointerButton.Secondary,
                PointerUpdateKind.MiddleButtonReleased => DataGridNavigationPointerButton.Middle,
                PointerUpdateKind.XButton1Released => DataGridNavigationPointerButton.XButton1,
                PointerUpdateKind.XButton2Released => DataGridNavigationPointerButton.XButton2,
                _ => DataGridNavigationPointerButton.None
            };
        }

        private static DataGridNavigationWheelDirection GetWheelDirection(double x, double y)
        {
            if (System.Math.Abs(y) >= System.Math.Abs(x) && y != 0)
            {
                return y > 0 ? DataGridNavigationWheelDirection.Up : DataGridNavigationWheelDirection.Down;
            }

            if (x != 0)
            {
                return x > 0 ? DataGridNavigationWheelDirection.Right : DataGridNavigationWheelDirection.Left;
            }

            return DataGridNavigationWheelDirection.None;
        }

        private static DataGridNavigationInputModifiers NormalizeNavigationInputModifiers(KeyModifiers modifiers)
        {
            DataGridNavigationInputModifiers result = DataGridNavigationInputModifiers.None;
            if ((modifiers & KeyModifiers.Shift) != 0) result |= DataGridNavigationInputModifiers.Shift;
            if ((modifiers & KeyModifiers.Control) != 0) result |= DataGridNavigationInputModifiers.Control;
            if ((modifiers & KeyModifiers.Alt) != 0) result |= DataGridNavigationInputModifiers.Alt;
            if ((modifiers & KeyModifiers.Meta) != 0) result |= DataGridNavigationInputModifiers.Meta;
            return result;
        }

        private static KeyModifiers DenormalizeNavigationInputModifiers(DataGridNavigationInputModifiers modifiers)
        {
            KeyModifiers result = KeyModifiers.None;
            if ((modifiers & DataGridNavigationInputModifiers.Shift) != 0) result |= KeyModifiers.Shift;
            if ((modifiers & DataGridNavigationInputModifiers.Control) != 0) result |= KeyModifiers.Control;
            if ((modifiers & DataGridNavigationInputModifiers.Alt) != 0) result |= KeyModifiers.Alt;
            if ((modifiers & DataGridNavigationInputModifiers.Meta) != 0) result |= KeyModifiers.Meta;
            return result;
        }

        private static DataGridNavigationInputKey NormalizeNavigationInputKey(Key key) => key switch
        {
            Key.Up => DataGridNavigationInputKey.Up,
            Key.Down => DataGridNavigationInputKey.Down,
            Key.Left => DataGridNavigationInputKey.Left,
            Key.Right => DataGridNavigationInputKey.Right,
            Key.PageUp => DataGridNavigationInputKey.PageUp,
            Key.PageDown => DataGridNavigationInputKey.PageDown,
            Key.Home => DataGridNavigationInputKey.Home,
            Key.End => DataGridNavigationInputKey.End,
            Key.Tab => DataGridNavigationInputKey.Tab,
            Key.Enter => DataGridNavigationInputKey.Enter,
            Key.Escape => DataGridNavigationInputKey.Escape,
            Key.Space => DataGridNavigationInputKey.Space,
            Key.Back => DataGridNavigationInputKey.Backspace,
            Key.Insert => DataGridNavigationInputKey.Insert,
            Key.Delete => DataGridNavigationInputKey.Delete,
            Key.Add or Key.OemPlus => DataGridNavigationInputKey.Add,
            Key.Subtract or Key.OemMinus => DataGridNavigationInputKey.Subtract,
            Key.Multiply => DataGridNavigationInputKey.Multiply,
            Key.Divide => DataGridNavigationInputKey.Divide,
            Key.Decimal or Key.OemPeriod => DataGridNavigationInputKey.Decimal,
            Key.BrowserBack => DataGridNavigationInputKey.BrowserBack,
            Key.BrowserForward => DataGridNavigationInputKey.BrowserForward,
            >= Key.A and <= Key.Z => (DataGridNavigationInputKey)((int)DataGridNavigationInputKey.A + (key - Key.A)),
            >= Key.D0 and <= Key.D9 => (DataGridNavigationInputKey)((int)DataGridNavigationInputKey.D0 + (key - Key.D0)),
            >= Key.F1 and <= Key.F12 => (DataGridNavigationInputKey)((int)DataGridNavigationInputKey.F1 + (key - Key.F1)),
            Key.None => DataGridNavigationInputKey.None,
            _ => DataGridNavigationInputKey.Unknown
        };

        private static DataGridNavigationInputKey NormalizeNavigationInputPhysicalKey(PhysicalKey key) => key switch
        {
            PhysicalKey.ArrowUp => DataGridNavigationInputKey.Up,
            PhysicalKey.ArrowDown => DataGridNavigationInputKey.Down,
            PhysicalKey.ArrowLeft => DataGridNavigationInputKey.Left,
            PhysicalKey.ArrowRight => DataGridNavigationInputKey.Right,
            PhysicalKey.PageUp => DataGridNavigationInputKey.PageUp,
            PhysicalKey.PageDown => DataGridNavigationInputKey.PageDown,
            PhysicalKey.Home => DataGridNavigationInputKey.Home,
            PhysicalKey.End => DataGridNavigationInputKey.End,
            PhysicalKey.Tab => DataGridNavigationInputKey.Tab,
            PhysicalKey.Enter or PhysicalKey.NumPadEnter => DataGridNavigationInputKey.Enter,
            PhysicalKey.Escape => DataGridNavigationInputKey.Escape,
            PhysicalKey.Space => DataGridNavigationInputKey.Space,
            PhysicalKey.Backspace => DataGridNavigationInputKey.Backspace,
            PhysicalKey.Insert => DataGridNavigationInputKey.Insert,
            PhysicalKey.Delete => DataGridNavigationInputKey.Delete,
            PhysicalKey.NumPadAdd or PhysicalKey.Equal => DataGridNavigationInputKey.Add,
            PhysicalKey.NumPadSubtract or PhysicalKey.Minus => DataGridNavigationInputKey.Subtract,
            PhysicalKey.NumPadMultiply => DataGridNavigationInputKey.Multiply,
            PhysicalKey.NumPadDivide or PhysicalKey.Slash => DataGridNavigationInputKey.Divide,
            PhysicalKey.NumPadDecimal or PhysicalKey.Period => DataGridNavigationInputKey.Decimal,
            >= PhysicalKey.A and <= PhysicalKey.Z =>
                (DataGridNavigationInputKey)((int)DataGridNavigationInputKey.A + ((int)key - (int)PhysicalKey.A)),
            >= PhysicalKey.Digit0 and <= PhysicalKey.Digit9 =>
                (DataGridNavigationInputKey)((int)DataGridNavigationInputKey.D0 + ((int)key - (int)PhysicalKey.Digit0)),
            >= PhysicalKey.F1 and <= PhysicalKey.F12 =>
                (DataGridNavigationInputKey)((int)DataGridNavigationInputKey.F1 + ((int)key - (int)PhysicalKey.F1)),
            PhysicalKey.None => DataGridNavigationInputKey.None,
            _ => DataGridNavigationInputKey.Unknown
        };
    }
}
