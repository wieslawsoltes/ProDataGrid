using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Simple;
using Avalonia.VisualTree;
using Avalonia.Reactive;

namespace Avalonia.Diagnostics.Views
{
    partial class MainWindow : Window, IStyleHost
    {
        private readonly IDisposable? _inputSubscription;
        private readonly HashSet<Popup> _frozenPopupStates;
        private AvaloniaObject? _root;
        private PixelPoint _lastPointerPosition;
        private HotKeyConfiguration? _hotKeys;

        public MainWindow()
        {
            InitializeComponent();

            // Apply the SimpleTheme.Window theme; this must be done after the XAML is parsed as
            // the theme is included in the MainWindow's XAML.
            if (Theme is null && this.FindResource(typeof(Window)) is ControlTheme windowTheme)
                Theme = windowTheme;

            _inputSubscription = InputManager.Instance?.Process
                .Subscribe(x =>
                {
                    if (x is RawPointerEventArgs pointerEventArgs)
                    {
                        _lastPointerPosition = ((Visual)x.Root).PointToScreen(pointerEventArgs.Position);
                        RawPointerMoved(pointerEventArgs);
                    }
                    else if (x is RawKeyEventArgs keyEventArgs && keyEventArgs.Type == RawKeyEventType.KeyDown)
                    {
                        RawKeyDown(keyEventArgs);
                    }
                });
            
            _frozenPopupStates = new HashSet<Popup>();

            EventHandler? lh = default;
            lh = (s, e) =>
            {
                this.Opened -= lh;
                if ((DataContext as MainViewModel)?.StartupScreenIndex is { } index)
                {
                    var screens = this.Screens;
                    if (index > -1 && index < screens.ScreenCount)
                    {
                        var screen = screens.All[index];
                        this.Position = screen.Bounds.TopLeft;
                        this.WindowState = WindowState.Maximized;
                    }
                }
            };
            this.Opened += lh;
        }

        public AvaloniaObject? Root
        {
            get => _root;
            set
            {
                if (_root != value)
                {
                    if (_root is ICloseable oldClosable)
                    {
                        oldClosable.Closed -= RootClosed;
                    }

                    _root = value;

                    if (_root is  ICloseable newClosable)
                    {
                        newClosable.Closed += RootClosed;
                        DataContext = new MainViewModel(_root);
                    }
                    else
                    {
                        DataContext = null;
                    }
                }
            }
        }

        IStyleHost? IStyleHost.StylingParent => null;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _inputSubscription?.Dispose();

            foreach (var state in _frozenPopupStates)
            {
                state.Closing -= PopupOnClosing;
            }

            _frozenPopupStates.Clear();

            if (_root is ICloseable cloneable)
            {
                cloneable.Closed -= RootClosed;
                _root = null;
            }

            ((MainViewModel?)DataContext)?.Dispose();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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

                    return !(x is IInputElement ie) || ie.IsHitTestVisible;
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
                if (control is Popup p && p.Host is PopupRoot popupRoot)
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

        private void RawKeyDown(RawKeyEventArgs e)
        {
            if (_hotKeys is null ||
                DataContext is not MainViewModel vm)
            {
                return;
            }

            var root = vm.PointerOverRoot as TopLevel ?? TopLevel.GetTopLevel(this);
            if (root is null)
            {
                return;
            }

            if (root is PopupRoot pr && pr.ParentTopLevel != null)
            {
                root = pr.ParentTopLevel;
            }

            var modifiers = MergeModifiers(e.Key, e.Modifiers.ToKeyModifiers());
            var isTextInputFocused = IsEditableTextInputFocused();

            if (IsMatched(_hotKeys.ValueFramesFreeze, e.Key, modifiers))
            {
                FreezeValueFrames(vm);
            }
            else if (IsMatched(_hotKeys.ValueFramesUnfreeze, e.Key, modifiers))
            {
                UnfreezeValueFrames(vm);
            }
            else if (IsMatched(_hotKeys.TogglePopupFreeze, e.Key, modifiers))
            {
                ToggleFreezePopups(root, vm);
            }
            else if (IsMatched(_hotKeys.ScreenshotSelectedControl, e.Key, modifiers))
            {
                ScreenshotSelectedControl(vm);
            }
            else if (IsMatched(_hotKeys.InspectHoveredControl, e.Key, modifiers))
            {
                InspectHoveredControl(root, vm);
            }
            else if (IsMatched(_hotKeys.ToggleElementHighlight, e.Key, modifiers))
            {
                vm.ToggleHighlightElements();
            }
            else if (IsMatched(_hotKeys.ToggleFocusTracking, e.Key, modifiers))
            {
                vm.ToggleFocusTracking();
            }
            else if (IsMatched(_hotKeys.ToggleOverlayRulers, e.Key, modifiers))
            {
                vm.ToggleOverlayRulers();
            }
            else if (IsMatched(_hotKeys.ToggleOverlayInfo, e.Key, modifiers))
            {
                vm.ToggleOverlayInfo();
            }
            else if (IsMatched(_hotKeys.ToggleTopMost, e.Key, modifiers))
            {
                ToggleTopMost();
            }
            else if (IsMatched(_hotKeys.NextToolTab, e.Key, modifiers))
            {
                vm.SelectNextToolTab();
            }
            else if (IsMatched(_hotKeys.PreviousToolTab, e.Key, modifiers))
            {
                vm.SelectPreviousToolTab();
            }
            else if (IsMatched(_hotKeys.RefreshCurrentTool, e.Key, modifiers))
            {
                vm.RefreshCurrentTool();
            }
            else if (IsMatched(_hotKeys.ClearCurrentTool, e.Key, modifiers))
            {
                vm.ClearCurrentTool();
            }
            else if (IsMatched(_hotKeys.OpenSettings, e.Key, modifiers))
            {
                vm.ShowSettings();
            }
            else if (IsMatched(_hotKeys.SetBreakpoint, e.Key, modifiers))
            {
                vm.SetBreakpointFromContext();
            }
            else if (IsMatched(_hotKeys.FocusCurrentFilter, e.Key, modifiers))
            {
                FocusCurrentFilter();
            }
            else if (!isTextInputFocused && IsMatched(_hotKeys.NextSearchMatch, e.Key, modifiers))
            {
                vm.SelectNextSearchMatch();
            }
            else if (!isTextInputFocused && IsMatched(_hotKeys.PreviousSearchMatch, e.Key, modifiers))
            {
                vm.SelectPreviousSearchMatch();
            }
            else if (!isTextInputFocused && IsMatched(_hotKeys.ClearSelectionOrFilter, e.Key, modifiers))
            {
                vm.ClearSelectionOrFilter();
            }
            else if (!isTextInputFocused && IsMatched(_hotKeys.RemoveSelectedRecord, e.Key, modifiers))
            {
                vm.RemoveSelectedRecord();
            }

            static bool IsMatched(KeyGesture gesture, Key key, KeyModifiers modifiers)
            {
                return (gesture.Key == key || gesture.Key == Key.None) && modifiers == gesture.KeyModifiers;
            }

            // When Control, Shift, or Alt are initially pressed, they are the Key and not part of Modifiers
            // This merges so modifier keys alone can more easily trigger actions
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

        private void RawPointerMoved(RawPointerEventArgs e)
        {
            if (_hotKeys is null || DataContext is not MainViewModel vm)
            {
                return;
            }

            var inspectGesture = _hotKeys.InspectHoveredControl;
            if (inspectGesture.Key != Key.None || inspectGesture.KeyModifiers == KeyModifiers.None)
            {
                return;
            }

            var pointerModifiers = e.InputModifiers.ToKeyModifiers();
            if (pointerModifiers != inspectGesture.KeyModifiers)
            {
                return;
            }

            if (e.Root is not TopLevel root)
            {
                return;
            }

            if (root is PopupRoot popupRoot && popupRoot.ParentTopLevel is { } parentTopLevel)
            {
                root = parentTopLevel;
            }

            InspectHoveredControl(root, vm);
        }

        private void FreezeValueFrames(MainViewModel vm)
        {
            vm.EnableSnapshotStyles(true);
        }

        private void UnfreezeValueFrames(MainViewModel vm)
        {
            vm.EnableSnapshotStyles(false);
        }

        private void ToggleFreezePopups(TopLevel root, MainViewModel vm)
        {
            vm.FreezePopups = !vm.FreezePopups;

            foreach (var popupRoot in GetPopupRoots(root))
            {
                if (popupRoot.Parent is Popup popup)
                {
                    if (vm.FreezePopups)
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

        private void ScreenshotSelectedControl(MainViewModel vm)
        {
            vm.Shot(null);
        }

        private void InspectHoveredControl(TopLevel root, MainViewModel vm)
        {
            if (vm.HasRemoteMutation)
            {
                _ = vm.InspectHoveredViaRemoteAsync();
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

            if (control != null && !control.DoesBelongToDevTool())
            {
                vm.SelectControl(control);
            }
        }

        private void ToggleTopMost()
        {
            Topmost = !Topmost;
        }

        private bool FocusCurrentFilter()
        {
            foreach (var filterTextBox in this.GetVisualDescendants().OfType<FilterTextBox>())
            {
                if (!filterTextBox.IsVisible || !filterTextBox.IsEffectivelyEnabled)
                {
                    continue;
                }

                if (filterTextBox.Focus())
                {
                    filterTextBox.SelectAll();
                    return true;
                }
            }

            return false;
        }

        private bool IsEditableTextInputFocused()
        {
            var focusedElement = FocusManager?.GetFocusedElement() ?? KeyboardDevice.Instance?.FocusedElement;
            return focusedElement is TextBox { IsReadOnly: false };
        }

        private void PopupOnClosing(object? sender, CancelEventArgs e)
        {
            var vm = (MainViewModel?)DataContext;
            if (vm?.FreezePopups == true)
            {
                e.Cancel = true;
            }
        }
        
        private void RootClosed(object? sender, EventArgs e) => Close();

        public void SetOptions(DevToolsOptions options)
        {
            _hotKeys = options.HotKeys;
            if (!string.IsNullOrWhiteSpace(options.ApplicationName))
            {
                Title = options.ApplicationName + " - ProDevTools for Avalonia";
            }

            (DataContext as MainViewModel)?.SetOptions(options);
            if (options.ThemeVariant is { } themeVariant)
            {
                RequestedThemeVariant = themeVariant;
            }
        }

        internal void SelectedControl(Control? control)
        {
            if (control is { })
            {
                (DataContext as MainViewModel)?.SelectControl(control);
            }
        }
    }
}
