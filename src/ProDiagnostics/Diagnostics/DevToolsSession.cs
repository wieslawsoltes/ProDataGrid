using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics
{
    /// <summary>
    /// Owns diagnostics state that can be shared by multiple standalone diagnostics segment views.
    /// </summary>
    public sealed class DevToolsSession : AvaloniaObject, System.IDisposable
    {
        public static readonly StyledProperty<AvaloniaObject?> RootProperty =
            AvaloniaProperty.Register<DevToolsSession, AvaloniaObject?>(nameof(Root));

        public static readonly StyledProperty<DevToolsOptions?> OptionsProperty =
            AvaloniaProperty.Register<DevToolsSession, DevToolsOptions?>(nameof(Options));

        private readonly HashSet<Popup> _frozenPopupStates = new();
        private MainViewModel? _viewModel;
        private System.IDisposable? _inputSubscription;
        private PixelPoint _lastPointerPosition;
        private HotKeyConfiguration? _hotKeys;
        private int _inputAttachCount;

        public event System.EventHandler? ViewModelChanged;

        public AvaloniaObject? Root
        {
            get => GetValue(RootProperty);
            set => SetValue(RootProperty, value);
        }

        public DevToolsOptions? Options
        {
            get => GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        internal MainViewModel? ViewModel => _viewModel;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == RootProperty)
            {
                UpdateRoot(change.GetNewValue<AvaloniaObject?>());
            }
            else if (change.Property == OptionsProperty)
            {
                ApplyOptions();
            }
        }

        public void Dispose()
        {
            while (_inputAttachCount > 0)
            {
                DetachInput();
            }

            SetViewModel(null);
        }

        public void AttachInput()
        {
            _inputAttachCount++;
            _inputSubscription ??= InputManager.Instance?.Process.Subscribe(ProcessRawInput);
        }

        public void DetachInput()
        {
            if (_inputAttachCount <= 0)
            {
                return;
            }

            _inputAttachCount--;
            if (_inputAttachCount == 0)
            {
                _inputSubscription?.Dispose();
                _inputSubscription = null;
            }
        }

        private void UpdateRoot(AvaloniaObject? root)
        {
            SetViewModel(root is null ? null : new MainViewModel(root));
            ApplyOptions();
        }

        private void ApplyOptions()
        {
            var options = Options ?? new DevToolsOptions();
            _hotKeys = options.HotKeys;
            _viewModel?.SetOptions(options);
        }

        private void SetViewModel(MainViewModel? viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            _viewModel?.Dispose();
            _viewModel = viewModel;
            ViewModelChanged?.Invoke(this, System.EventArgs.Empty);
        }

        private void ProcessRawInput(RawInputEventArgs e)
        {
            if (e is RawPointerEventArgs pointerEventArgs)
            {
                if (pointerEventArgs.Root.GetScreenPoint(pointerEventArgs.Position) is { } screenPoint)
                {
                    _lastPointerPosition = screenPoint;
                }
            }
            else if (e is RawKeyEventArgs keyEventArgs && keyEventArgs.Type == RawKeyEventType.KeyDown)
            {
                RawKeyDown(keyEventArgs);
            }
        }

        private void RawKeyDown(RawKeyEventArgs e)
        {
            if (_hotKeys is null ||
                _viewModel is null ||
                _viewModel.PointerOverRoot is not TopLevel root)
            {
                return;
            }

            if (root is PopupRoot pr && pr.ParentTopLevel != null)
            {
                root = pr.ParentTopLevel;
            }

            var modifiers = MergeModifiers(e.Key, e.Modifiers.ToKeyModifiers());

            if (IsMatched(_hotKeys.ValueFramesFreeze, e.Key, modifiers))
            {
                _viewModel.EnableSnapshotStyles(true);
            }
            else if (IsMatched(_hotKeys.ValueFramesUnfreeze, e.Key, modifiers))
            {
                _viewModel.EnableSnapshotStyles(false);
            }
            else if (IsMatched(_hotKeys.TogglePopupFreeze, e.Key, modifiers))
            {
                ToggleFreezePopups(root);
            }
            else if (IsMatched(_hotKeys.ScreenshotSelectedControl, e.Key, modifiers))
            {
                _viewModel.Shot(null);
            }
            else if (IsMatched(_hotKeys.InspectHoveredControl, e.Key, modifiers))
            {
                InspectHoveredControl(root);
            }

            static bool IsMatched(KeyGesture gesture, Key key, KeyModifiers modifiers)
            {
                return (gesture.Key == key || gesture.Key == Key.None) && modifiers.HasAllFlags(gesture.KeyModifiers);
            }

            static KeyModifiers MergeModifiers(Key key, KeyModifiers modifiers)
            {
                return key switch
                {
                    Key.LeftCtrl or Key.RightCtrl => modifiers | KeyModifiers.Control,
                    Key.LeftShift or Key.RightShift => modifiers | KeyModifiers.Shift,
                    Key.LeftAlt or Key.RightAlt => modifiers | KeyModifiers.Alt,
                    _ => modifiers
                };
            }
        }

        private void ToggleFreezePopups(TopLevel root)
        {
            if (_viewModel is null)
            {
                return;
            }

            _viewModel.FreezePopups = !_viewModel.FreezePopups;

            foreach (var popupRoot in GetPopupRoots(root))
            {
                if (popupRoot.Parent is Popup popup)
                {
                    if (_viewModel.FreezePopups)
                    {
                        popup.Closing += PopupOnClosing;
                        _frozenPopupStates.Add(popup);
                    }
                    else
                    {
                        popup.Closing -= PopupOnClosing;
                        _frozenPopupStates.Remove(popup);
                    }
                }
            }
        }

        private void InspectHoveredControl(TopLevel root)
        {
            if (_viewModel is null)
            {
                return;
            }

            Control? control = null;

            foreach (var popupRoot in GetPopupRoots(root))
            {
                control = GetHoveredControl(popupRoot);

                if (control != null)
                {
                    break;
                }
            }

            control ??= GetHoveredControl(root);

            if (control != null)
            {
                _viewModel.SelectControl(control);
            }
        }

        private Control? GetHoveredControl(TopLevel topLevel)
        {
            var point = topLevel.PointToClient(_lastPointerPosition);

            return (Control?)topLevel.GetVisualsAt(point, x =>
                {
                    if (x is AdornerLayer || !x.IsVisible)
                    {
                        return false;
                    }

                    return x is not IInputElement inputElement || inputElement.IsHitTestVisible;
                })
                .FirstOrDefault();
        }

        private static List<PopupRoot> GetPopupRoots(TopLevel root)
        {
            var popupRoots = new List<PopupRoot>();

            void ProcessProperty<T>(Control control, AvaloniaProperty<T> property)
            {
                if (control.GetValue(property) is IPopupHostProvider popupProvider
                    && popupProvider.PopupHost is PopupRoot popupRoot)
                {
                    popupRoots.Add(popupRoot);
                }
            }

            foreach (var control in root.GetVisualDescendants().OfType<Control>())
            {
                if (control is Popup popup && popup.Host is PopupRoot popupRoot)
                {
                    popupRoots.Add(popupRoot);
                }

                ProcessProperty(control, Control.ContextFlyoutProperty);
                ProcessProperty(control, Control.ContextMenuProperty);
                ProcessProperty(control, FlyoutBase.AttachedFlyoutProperty);
                ProcessProperty(control, ToolTipDiagnostics.ToolTipProperty);
                ProcessProperty(control, Button.FlyoutProperty);
            }

            return popupRoots;
        }

        private void PopupOnClosing(object? sender, CancelEventArgs e)
        {
            if (_viewModel?.FreezePopups == true)
            {
                e.Cancel = true;
            }
        }
    }
}
