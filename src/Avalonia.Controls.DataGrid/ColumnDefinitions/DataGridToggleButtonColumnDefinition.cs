// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Windows.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridToggleButtonColumnDefinition : DataGridBoundColumnDefinition
    {
        private object _content;
        private object _checkedContent;
        private object _uncheckedContent;
        private bool? _isThreeState;
        private ClickMode? _clickMode;
        private ICommand _command;
        private object _commandParameter;
        private DataGridBindingDefinition _contentBinding;
        private DataGridBindingDefinition _checkedContentBinding;
        private DataGridBindingDefinition _uncheckedContentBinding;
        private DataGridBindingDefinition _commandBinding;
        private DataGridBindingDefinition _commandParameterBinding;

        [AssignBinding]
        public object Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve the default content.</summary>
        public DataGridBindingDefinition ContentBinding
        {
            get => _contentBinding;
            set => SetProperty(ref _contentBinding, value);
        }

        [AssignBinding]
        public object CheckedContent
        {
            get => _checkedContent;
            set => SetProperty(ref _checkedContent, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve checked content.</summary>
        public DataGridBindingDefinition CheckedContentBinding
        {
            get => _checkedContentBinding;
            set => SetProperty(ref _checkedContentBinding, value);
        }

        [AssignBinding]
        public object UncheckedContent
        {
            get => _uncheckedContent;
            set => SetProperty(ref _uncheckedContent, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve unchecked content.</summary>
        public DataGridBindingDefinition UncheckedContentBinding
        {
            get => _uncheckedContentBinding;
            set => SetProperty(ref _uncheckedContentBinding, value);
        }

        /// <summary>Gets or sets the fallback command used for every row.</summary>
        public ICommand Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve the command.</summary>
        public DataGridBindingDefinition CommandBinding
        {
            get => _commandBinding;
            set => SetProperty(ref _commandBinding, value);
        }

        /// <summary>Gets or sets the fallback command parameter. The row item is used when unset.</summary>
        [AssignBinding]
        public object CommandParameter
        {
            get => _commandParameter;
            set => SetProperty(ref _commandParameter, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve the command parameter.</summary>
        public DataGridBindingDefinition CommandParameterBinding
        {
            get => _commandParameterBinding;
            set => SetProperty(ref _commandParameterBinding, value);
        }

        public bool? IsThreeState
        {
            get => _isThreeState;
            set => SetProperty(ref _isThreeState, value);
        }

        public ClickMode? ClickMode
        {
            get => _clickMode;
            set => SetProperty(ref _clickMode, value);
        }

        protected override DataGridColumn CreateColumnCore()
        {
            return new DataGridToggleButtonColumn();
        }

        protected override void ApplyColumnProperties(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            base.ApplyColumnProperties(column, context);

            if (column is DataGridToggleButtonColumn toggleColumn)
            {
                if (ContentBinding != null)
                {
                    toggleColumn.Content = ContentBinding.CreateBinding();
                }
                else if (Content != null)
                {
                    toggleColumn.Content = Content;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleButtonColumn.ContentProperty);
                }

                if (CheckedContentBinding != null)
                {
                    toggleColumn.CheckedContent = CheckedContentBinding.CreateBinding();
                }
                else if (CheckedContent != null)
                {
                    toggleColumn.CheckedContent = CheckedContent;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleButtonColumn.CheckedContentProperty);
                }

                if (UncheckedContentBinding != null)
                {
                    toggleColumn.UncheckedContent = UncheckedContentBinding.CreateBinding();
                }
                else if (UncheckedContent != null)
                {
                    toggleColumn.UncheckedContent = UncheckedContent;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleButtonColumn.UncheckedContentProperty);
                }

                ApplyCommandProperties(toggleColumn);

                if (IsThreeState.HasValue)
                {
                    toggleColumn.IsThreeState = IsThreeState.Value;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleButtonColumn.IsThreeStateProperty);
                }

                if (ClickMode.HasValue)
                {
                    toggleColumn.ClickMode = ClickMode.Value;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleButtonColumn.ClickModeProperty);
                }
            }
        }

        protected override bool ApplyColumnPropertyChange(
            DataGridColumn column,
            DataGridColumnDefinitionContext context,
            string propertyName)
        {
            if (base.ApplyColumnPropertyChange(column, context, propertyName))
            {
                return true;
            }

            if (column is not DataGridToggleButtonColumn toggleColumn)
            {
                return false;
            }

            switch (propertyName)
            {
                case nameof(Content):
                case nameof(ContentBinding):
                    if (ContentBinding != null)
                    {
                        toggleColumn.Content = ContentBinding.CreateBinding();
                    }
                    else if (Content != null)
                    {
                        toggleColumn.Content = Content;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleButtonColumn.ContentProperty);
                    }
                    return true;
                case nameof(CheckedContent):
                case nameof(CheckedContentBinding):
                    if (CheckedContentBinding != null)
                    {
                        toggleColumn.CheckedContent = CheckedContentBinding.CreateBinding();
                    }
                    else if (CheckedContent != null)
                    {
                        toggleColumn.CheckedContent = CheckedContent;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleButtonColumn.CheckedContentProperty);
                    }
                    return true;
                case nameof(UncheckedContent):
                case nameof(UncheckedContentBinding):
                    if (UncheckedContentBinding != null)
                    {
                        toggleColumn.UncheckedContent = UncheckedContentBinding.CreateBinding();
                    }
                    else if (UncheckedContent != null)
                    {
                        toggleColumn.UncheckedContent = UncheckedContent;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleButtonColumn.UncheckedContentProperty);
                    }
                    return true;
                case nameof(Command):
                case nameof(CommandBinding):
                case nameof(CommandParameter):
                case nameof(CommandParameterBinding):
                    ApplyCommandProperties(toggleColumn);
                    return true;
                case nameof(IsThreeState):
                    if (IsThreeState.HasValue)
                    {
                        toggleColumn.IsThreeState = IsThreeState.Value;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleButtonColumn.IsThreeStateProperty);
                    }
                    return true;
                case nameof(ClickMode):
                    if (ClickMode.HasValue)
                    {
                        toggleColumn.ClickMode = ClickMode.Value;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleButtonColumn.ClickModeProperty);
                    }
                    return true;
            }

            return false;
        }

        private void ApplyCommandProperties(DataGridToggleButtonColumn toggleColumn)
        {
            if (CommandBinding != null)
            {
                toggleColumn.CommandBinding = CommandBinding.CreateBinding();
                toggleColumn.ClearValue(DataGridToggleButtonColumn.CommandProperty);
            }
            else if (Command != null)
            {
                toggleColumn.ClearValue(DataGridToggleButtonColumn.CommandBindingProperty);
                toggleColumn.Command = Command;
            }
            else
            {
                toggleColumn.ClearValue(DataGridToggleButtonColumn.CommandBindingProperty);
                toggleColumn.ClearValue(DataGridToggleButtonColumn.CommandProperty);
            }

            if (CommandParameterBinding != null)
            {
                toggleColumn.CommandParameter = CommandParameterBinding.CreateBinding();
            }
            else if (CommandParameter != null)
            {
                toggleColumn.CommandParameter = CommandParameter;
            }
            else
            {
                toggleColumn.ClearValue(DataGridToggleButtonColumn.CommandParameterProperty);
            }
        }
    }
}
