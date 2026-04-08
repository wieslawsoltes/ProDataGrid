using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace DataGridSample.Controls
{
    public sealed partial class KeyboardGestureHelpPopup : UserControl
    {
        public static readonly StyledProperty<DataGridKeyboardGestures?> OverridesProperty =
            AvaloniaProperty.Register<KeyboardGestureHelpPopup, DataGridKeyboardGestures?>(nameof(Overrides));

        private static readonly GestureDefinition[] Definitions =
        [
            new GestureDefinition("Tab", gestures => gestures.Tab),
            new GestureDefinition("MoveUp", gestures => gestures.MoveUp),
            new GestureDefinition("MoveDown", gestures => gestures.MoveDown),
            new GestureDefinition("MoveLeft", gestures => gestures.MoveLeft),
            new GestureDefinition("MoveRight", gestures => gestures.MoveRight),
            new GestureDefinition("MovePageUp", gestures => gestures.MovePageUp),
            new GestureDefinition("MovePageDown", gestures => gestures.MovePageDown),
            new GestureDefinition("MoveHome", gestures => gestures.MoveHome),
            new GestureDefinition("MoveEnd", gestures => gestures.MoveEnd),
            new GestureDefinition("Enter", gestures => gestures.Enter),
            new GestureDefinition("CancelEdit", gestures => gestures.CancelEdit),
            new GestureDefinition("BeginEdit", gestures => gestures.BeginEdit),
            new GestureDefinition("SelectAll", gestures => gestures.SelectAll),
            new GestureDefinition("Copy", gestures => gestures.Copy),
            new GestureDefinition("CopyAlternate", gestures => gestures.CopyAlternate),
            new GestureDefinition("Delete", gestures => gestures.Delete),
            new GestureDefinition("ExpandAll", gestures => gestures.ExpandAll)
        ];

        public KeyboardGestureHelpPopup()
        {
            InitializeComponent();
        }

        public DataGridKeyboardGestures? Overrides
        {
            get => GetValue(OverridesProperty);
            set => SetValue(OverridesProperty, value);
        }

        public ObservableCollection<KeyboardGestureHelpItem> Items { get; } = new();

        static KeyboardGestureHelpPopup()
        {
            OverridesProperty.Changed.AddClassHandler<KeyboardGestureHelpPopup>((control, _) => control.UpdateItems());
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateItems();
        }

        private void UpdateItems()
        {
            Items.Clear();

            var defaults = DataGridKeyboardGestures.CreateDefault(GetCommandModifiers());
            var overrides = Overrides;

            foreach (var definition in Definitions)
            {
                var defaultGesture = definition.GetGesture(defaults);
                var overrideGesture = overrides != null ? definition.GetGesture(overrides) : null;

                Items.Add(new KeyboardGestureHelpItem(
                    definition.Action,
                    FormatGesture(defaultGesture),
                    FormatOverride(overrideGesture)));
            }
        }

        private KeyModifiers GetCommandModifiers()
        {
            return this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        }

        private static string FormatOverride(KeyGesture? gesture)
        {
            if (gesture == null)
            {
                return "Default";
            }

            if (gesture.Key == Key.None)
            {
                return "Disabled";
            }

            return FormatGesture(gesture);
        }

        private static string FormatGesture(KeyGesture? gesture)
        {
            if (gesture == null || gesture.Key == Key.None)
            {
                return "None";
            }

            var parts = new List<string>();
            var modifiers = gesture.KeyModifiers;

            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (modifiers.HasFlag(KeyModifiers.Meta))
            {
                parts.Add("Cmd");
            }

            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (modifiers.HasFlag(KeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            parts.Add(gesture.Key.ToString());
            return string.Join("+", parts);
        }

        private sealed class GestureDefinition
        {
            public GestureDefinition(string action, Func<DataGridKeyboardGestures, KeyGesture?> getGesture)
            {
                Action = action;
                GetGesture = getGesture;
            }

            public string Action { get; }

            public Func<DataGridKeyboardGestures, KeyGesture?> GetGesture { get; }
        }
    }

    public sealed class KeyboardGestureHelpItem
    {
        public KeyboardGestureHelpItem(string action, string defaultGesture, string overrideGesture)
        {
            Action = action;
            DefaultGesture = defaultGesture;
            OverrideGesture = overrideGesture;
        }

        public string Action { get; }

        public string DefaultGesture { get; }

        public string OverrideGesture { get; }
    }
}
