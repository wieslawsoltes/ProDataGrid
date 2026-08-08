// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Windows.Input;
using Avalonia.Data;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridButtonColumnDefinition : DataGridColumnDefinition
    {
        private object _content;
        private string _contentTemplateKey;
        private ICommand _command;
        private object _commandParameter;
        private DataGridBindingDefinition _contentBinding;
        private DataGridBindingDefinition _commandBinding;
        private DataGridBindingDefinition _commandParameterBinding;
        private ClickMode? _clickMode;
        private KeyGesture _hotKey;

        [AssignBinding]
        public object Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve button content.</summary>
        public DataGridBindingDefinition ContentBinding
        {
            get => _contentBinding;
            set => SetProperty(ref _contentBinding, value);
        }

        public string ContentTemplateKey
        {
            get => _contentTemplateKey;
            set => SetProperty(ref _contentTemplateKey, value);
        }

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

        public ClickMode? ClickMode
        {
            get => _clickMode;
            set => SetProperty(ref _clickMode, value);
        }

        public KeyGesture HotKey
        {
            get => _hotKey;
            set => SetProperty(ref _hotKey, value);
        }

        protected override DataGridColumn CreateColumnCore()
        {
            return new DataGridButtonColumn();
        }

        protected override void ApplyColumnProperties(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            if (column is DataGridButtonColumn buttonColumn)
            {
                if (ContentBinding != null)
                {
                    buttonColumn.Content = ContentBinding.CreateBinding();
                }
                else if (Content != null)
                {
                    buttonColumn.Content = Content;
                }
                else
                {
                    buttonColumn.ClearValue(DataGridButtonColumn.ContentProperty);
                }

                if (ContentTemplateKey != null)
                {
                    buttonColumn.ContentTemplate = context?.ResolveResource<IDataTemplate>(ContentTemplateKey);
                }
                else
                {
                    buttonColumn.ClearValue(DataGridButtonColumn.ContentTemplateProperty);
                }

                if (CommandBinding != null)
                {
                    buttonColumn.CommandBinding = CommandBinding.CreateBinding();
                    buttonColumn.ClearValue(DataGridButtonColumn.CommandProperty);
                }
                else if (Command != null)
                {
                    buttonColumn.ClearValue(DataGridButtonColumn.CommandBindingProperty);
                    buttonColumn.Command = Command;
                }
                else
                {
                    buttonColumn.ClearValue(DataGridButtonColumn.CommandBindingProperty);
                    buttonColumn.ClearValue(DataGridButtonColumn.CommandProperty);
                }

                if (CommandParameterBinding != null)
                {
                    buttonColumn.CommandParameter = CommandParameterBinding.CreateBinding();
                }
                else if (CommandParameter != null)
                {
                    buttonColumn.CommandParameter = CommandParameter;
                }
                else
                {
                    buttonColumn.ClearValue(DataGridButtonColumn.CommandParameterProperty);
                }

                if (ClickMode.HasValue)
                {
                    buttonColumn.ClickMode = ClickMode.Value;
                }
                else
                {
                    buttonColumn.ClearValue(DataGridButtonColumn.ClickModeProperty);
                }

                if (HotKey != null)
                {
                    buttonColumn.HotKey = HotKey;
                }
                else
                {
                    buttonColumn.ClearValue(DataGridButtonColumn.HotKeyProperty);
                }
            }
        }

        protected override bool ApplyColumnPropertyChange(
            DataGridColumn column,
            DataGridColumnDefinitionContext context,
            string propertyName)
        {
            if (column is not DataGridButtonColumn buttonColumn)
            {
                return false;
            }

            switch (propertyName)
            {
                case nameof(Content):
                case nameof(ContentBinding):
                    if (ContentBinding != null)
                    {
                        buttonColumn.Content = ContentBinding.CreateBinding();
                    }
                    else if (Content != null)
                    {
                        buttonColumn.Content = Content;
                    }
                    else
                    {
                        buttonColumn.ClearValue(DataGridButtonColumn.ContentProperty);
                    }
                    return true;
                case nameof(ContentTemplateKey):
                    if (ContentTemplateKey != null)
                    {
                        buttonColumn.ContentTemplate = context?.ResolveResource<IDataTemplate>(ContentTemplateKey);
                    }
                    else
                    {
                        buttonColumn.ClearValue(DataGridButtonColumn.ContentTemplateProperty);
                    }
                    return true;
                case nameof(Command):
                case nameof(CommandBinding):
                    if (CommandBinding != null)
                    {
                        buttonColumn.CommandBinding = CommandBinding.CreateBinding();
                        buttonColumn.ClearValue(DataGridButtonColumn.CommandProperty);
                    }
                    else if (Command != null)
                    {
                        buttonColumn.ClearValue(DataGridButtonColumn.CommandBindingProperty);
                        buttonColumn.Command = Command;
                    }
                    else
                    {
                        buttonColumn.ClearValue(DataGridButtonColumn.CommandBindingProperty);
                        buttonColumn.ClearValue(DataGridButtonColumn.CommandProperty);
                    }
                    return true;
                case nameof(CommandParameter):
                case nameof(CommandParameterBinding):
                    if (CommandParameterBinding != null)
                    {
                        buttonColumn.CommandParameter = CommandParameterBinding.CreateBinding();
                    }
                    else if (CommandParameter != null)
                    {
                        buttonColumn.CommandParameter = CommandParameter;
                    }
                    else
                    {
                        buttonColumn.ClearValue(DataGridButtonColumn.CommandParameterProperty);
                    }
                    return true;
                case nameof(ClickMode):
                    if (ClickMode.HasValue)
                    {
                        buttonColumn.ClickMode = ClickMode.Value;
                    }
                    else
                    {
                        buttonColumn.ClearValue(DataGridButtonColumn.ClickModeProperty);
                    }
                    return true;
                case nameof(HotKey):
                    if (HotKey != null)
                    {
                        buttonColumn.HotKey = HotKey;
                    }
                    else
                    {
                        buttonColumn.ClearValue(DataGridButtonColumn.HotKeyProperty);
                    }
                    return true;
            }

            return false;
        }
    }
}
