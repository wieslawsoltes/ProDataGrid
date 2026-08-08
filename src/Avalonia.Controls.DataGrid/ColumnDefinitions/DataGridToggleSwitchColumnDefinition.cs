// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Windows.Input;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridToggleSwitchColumnDefinition : DataGridBoundColumnDefinition
    {
        private object _onContent;
        private object _offContent;
        private string _onContentTemplateKey;
        private string _offContentTemplateKey;
        private bool? _isThreeState;
        private ICommand _command;
        private object _commandParameter;
        private DataGridBindingDefinition _onContentBinding;
        private DataGridBindingDefinition _offContentBinding;
        private DataGridBindingDefinition _commandBinding;
        private DataGridBindingDefinition _commandParameterBinding;

        [AssignBinding]
        public object OnContent
        {
            get => _onContent;
            set => SetProperty(ref _onContent, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve on content.</summary>
        public DataGridBindingDefinition OnContentBinding
        {
            get => _onContentBinding;
            set => SetProperty(ref _onContentBinding, value);
        }

        [AssignBinding]
        public object OffContent
        {
            get => _offContent;
            set => SetProperty(ref _offContent, value);
        }

        /// <summary>Gets or sets the compiled row binding used to resolve off content.</summary>
        public DataGridBindingDefinition OffContentBinding
        {
            get => _offContentBinding;
            set => SetProperty(ref _offContentBinding, value);
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

        public string OnContentTemplateKey
        {
            get => _onContentTemplateKey;
            set => SetProperty(ref _onContentTemplateKey, value);
        }

        public string OffContentTemplateKey
        {
            get => _offContentTemplateKey;
            set => SetProperty(ref _offContentTemplateKey, value);
        }

        public bool? IsThreeState
        {
            get => _isThreeState;
            set => SetProperty(ref _isThreeState, value);
        }

        protected override DataGridColumn CreateColumnCore()
        {
            return new DataGridToggleSwitchColumn();
        }

        protected override void ApplyColumnProperties(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            base.ApplyColumnProperties(column, context);

            if (column is DataGridToggleSwitchColumn toggleColumn)
            {
                if (OnContentBinding != null)
                {
                    toggleColumn.OnContent = OnContentBinding.CreateBinding();
                }
                else if (OnContent != null)
                {
                    toggleColumn.OnContent = OnContent;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleSwitchColumn.OnContentProperty);
                }

                if (OffContentBinding != null)
                {
                    toggleColumn.OffContent = OffContentBinding.CreateBinding();
                }
                else if (OffContent != null)
                {
                    toggleColumn.OffContent = OffContent;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleSwitchColumn.OffContentProperty);
                }

                ApplyCommandProperties(toggleColumn);

                if (OnContentTemplateKey != null)
                {
                    toggleColumn.OnContentTemplate = context?.ResolveResource<IDataTemplate>(OnContentTemplateKey);
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleSwitchColumn.OnContentTemplateProperty);
                }

                if (OffContentTemplateKey != null)
                {
                    toggleColumn.OffContentTemplate = context?.ResolveResource<IDataTemplate>(OffContentTemplateKey);
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleSwitchColumn.OffContentTemplateProperty);
                }

                if (IsThreeState.HasValue)
                {
                    toggleColumn.IsThreeState = IsThreeState.Value;
                }
                else
                {
                    toggleColumn.ClearValue(DataGridToggleSwitchColumn.IsThreeStateProperty);
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

            if (column is not DataGridToggleSwitchColumn toggleColumn)
            {
                return false;
            }

            switch (propertyName)
            {
                case nameof(OnContent):
                case nameof(OnContentBinding):
                    if (OnContentBinding != null)
                    {
                        toggleColumn.OnContent = OnContentBinding.CreateBinding();
                    }
                    else if (OnContent != null)
                    {
                        toggleColumn.OnContent = OnContent;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleSwitchColumn.OnContentProperty);
                    }
                    return true;
                case nameof(OffContent):
                case nameof(OffContentBinding):
                    if (OffContentBinding != null)
                    {
                        toggleColumn.OffContent = OffContentBinding.CreateBinding();
                    }
                    else if (OffContent != null)
                    {
                        toggleColumn.OffContent = OffContent;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleSwitchColumn.OffContentProperty);
                    }
                    return true;
                case nameof(Command):
                case nameof(CommandBinding):
                case nameof(CommandParameter):
                case nameof(CommandParameterBinding):
                    ApplyCommandProperties(toggleColumn);
                    return true;
                case nameof(OnContentTemplateKey):
                    if (OnContentTemplateKey != null)
                    {
                        toggleColumn.OnContentTemplate = context?.ResolveResource<IDataTemplate>(OnContentTemplateKey);
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleSwitchColumn.OnContentTemplateProperty);
                    }
                    return true;
                case nameof(OffContentTemplateKey):
                    if (OffContentTemplateKey != null)
                    {
                        toggleColumn.OffContentTemplate = context?.ResolveResource<IDataTemplate>(OffContentTemplateKey);
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleSwitchColumn.OffContentTemplateProperty);
                    }
                    return true;
                case nameof(IsThreeState):
                    if (IsThreeState.HasValue)
                    {
                        toggleColumn.IsThreeState = IsThreeState.Value;
                    }
                    else
                    {
                        toggleColumn.ClearValue(DataGridToggleSwitchColumn.IsThreeStateProperty);
                    }
                    return true;
            }

            return false;
        }

        private void ApplyCommandProperties(DataGridToggleSwitchColumn toggleColumn)
        {
            if (CommandBinding != null)
            {
                toggleColumn.CommandBinding = CommandBinding.CreateBinding();
                toggleColumn.ClearValue(DataGridToggleSwitchColumn.CommandProperty);
            }
            else if (Command != null)
            {
                toggleColumn.ClearValue(DataGridToggleSwitchColumn.CommandBindingProperty);
                toggleColumn.Command = Command;
            }
            else
            {
                toggleColumn.ClearValue(DataGridToggleSwitchColumn.CommandBindingProperty);
                toggleColumn.ClearValue(DataGridToggleSwitchColumn.CommandProperty);
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
                toggleColumn.ClearValue(DataGridToggleSwitchColumn.CommandParameterProperty);
            }
        }
    }
}
