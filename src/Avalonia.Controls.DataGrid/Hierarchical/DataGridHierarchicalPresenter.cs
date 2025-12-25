// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Avalonia.Controls
{
    /// <summary>
    /// Presenter used by <see cref="DataGridHierarchicalColumn"/> to render an expander with indent.
    /// </summary>
    public class DataGridHierarchicalPresenter : ContentControl
    {
        private const string PartExpander = "PART_Expander";
        private ToggleButton? _expander;

        public static readonly StyledProperty<int> LevelProperty =
            AvaloniaProperty.Register<DataGridHierarchicalPresenter, int>(nameof(Level));

        public static readonly StyledProperty<double> IndentProperty =
            AvaloniaProperty.Register<DataGridHierarchicalPresenter, double>(nameof(Indent), 16d);

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<DataGridHierarchicalPresenter, bool>(nameof(IsExpanded));

        public static readonly StyledProperty<bool> IsExpandableProperty =
            AvaloniaProperty.Register<DataGridHierarchicalPresenter, bool>(nameof(IsExpandable));

        /// <summary>
        /// Identifies the <see cref="ToggleRequested"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<RoutedEventArgs> ToggleRequestedEvent =
            RoutedEvent.Register<DataGridHierarchicalPresenter, RoutedEventArgs>(nameof(ToggleRequested), RoutingStrategies.Bubble);

        /// <summary>
        /// Raised when the expander is activated.
        /// </summary>
        public event EventHandler<RoutedEventArgs>? ToggleRequested
        {
            add => AddHandler(ToggleRequestedEvent, value);
            remove => RemoveHandler(ToggleRequestedEvent, value);
        }

        static DataGridHierarchicalPresenter()
        {
            FocusableProperty.OverrideDefaultValue<DataGridHierarchicalPresenter>(false);
        }

        public DataGridHierarchicalPresenter()
        {
            UpdateIndentPadding();
        }

        /// <summary>
        /// Gets or sets the level of the current node.
        /// </summary>
        public int Level
        {
            get => GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        /// <summary>
        /// Gets or sets the per-level indent.
        /// </summary>
        public double Indent
        {
            get => GetValue(IndentProperty);
            set => SetValue(IndentProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the node is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the node can be expanded.
        /// </summary>
        public bool IsExpandable
        {
            get => GetValue(IsExpandableProperty);
            set => SetValue(IsExpandableProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == LevelProperty ||
                change.Property == IndentProperty ||
                change.Property == StyledElement.DataContextProperty)
            {
                UpdateIndentPadding();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_expander != null)
            {
                _expander.Click -= ExpanderOnClick;
                _expander.KeyDown -= ExpanderOnKeyDown;
            }

            _expander = e.NameScope.Find<ToggleButton>(PartExpander);
            if (_expander != null)
            {
                _expander.Click += ExpanderOnClick;
                _expander.KeyDown += ExpanderOnKeyDown;
            }
        }

        private void ExpanderOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ToggleRequestedEvent, this));
        }

        private void ExpanderOnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                RaiseEvent(new RoutedEventArgs(ToggleRequestedEvent, this));
                e.Handled = true;
            }
        }

        private void UpdateIndentPadding()
        {
            var indent = Math.Max(Indent, 0);
            Padding = new Thickness(Level * indent, 0, 0, 0);
        }
    }
}
