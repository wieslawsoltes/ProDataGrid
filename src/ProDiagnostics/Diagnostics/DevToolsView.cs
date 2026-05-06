using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Reactive;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics
{
    /// <summary>
    /// Hosts the Avalonia diagnostics tools inside an existing view.
    /// </summary>
    public partial class DevToolsView : UserControl
    {
        public static readonly StyledProperty<AvaloniaObject?> RootProperty =
            AvaloniaProperty.Register<DevToolsView, AvaloniaObject?>(nameof(Root));

        public static readonly StyledProperty<DevToolsOptions?> OptionsProperty =
            AvaloniaProperty.Register<DevToolsView, DevToolsOptions?>(nameof(Options));

        private readonly HashSet<Popup> _frozenPopupStates = new();
        private readonly ContentControl _content;
        private readonly TextBlock _emptyView;
        private MainViewModel? _viewModel;
        private MainView? _mainView;
        private IDisposable? _inputSubscription;
        private PixelPoint _lastPointerPosition;
        private HotKeyConfiguration? _hotKeys;

        public DevToolsView()
        {
            InitializeComponent();

            _content = this.GetControl<ContentControl>("PART_Content");
            _emptyView = new TextBlock
            {
                Text = "Run a sample to inspect diagnostics.",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Foreground = Avalonia.Media.Brushes.Gray
            };

            _content.Content = _emptyView;
        }

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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _inputSubscription ??= InputManager.Instance?.Process.Subscribe(ProcessRawInput);

            if (_viewModel is null && Root is not null)
            {
                UpdateRoot(Root);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _inputSubscription?.Dispose();
            _inputSubscription = null;
            DisposeDiagnostics();
        }

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

        private void UpdateRoot(AvaloniaObject? root)
        {
            DisposeDiagnostics();

            if (root is null)
            {
                _content.Content = _emptyView;
                return;
            }

            _viewModel = new MainViewModel(root);
            _mainView = new MainView
            {
                DataContext = _viewModel
            };

            ApplyOptions();
            _content.Content = _mainView;
        }

        private void ApplyOptions()
        {
            var options = Options ?? new DevToolsOptions();
            _hotKeys = options.HotKeys;
            _viewModel?.SetOptions(options);
        }

        private void DisposeDiagnostics()
        {
            foreach (var popup in _frozenPopupStates)
            {
                popup.Closing -= PopupOnClosing;
            }

            _frozenPopupStates.Clear();

            if (_mainView is not null)
            {
                _mainView.DataContext = null;
                _mainView = null;
            }

            _viewModel?.Dispose();
            _viewModel = null;
            _content.Content = _emptyView;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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

                ProcessProperty(control, ContextFlyoutProperty);
                ProcessProperty(control, ContextMenuProperty);
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
