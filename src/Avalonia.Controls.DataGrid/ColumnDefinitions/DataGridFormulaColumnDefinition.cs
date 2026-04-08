// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Controls.DataGridFormulas;
using Avalonia.Data;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridFormulaColumnDefinition : DataGridColumnDefinition
    {
        private string _formula;
        private string _formulaName;
        private bool _allowCellFormulas;

        public DataGridFormulaColumnDefinition()
        {
            IsReadOnly = true;
            ValueType = typeof(object);
        }

        public string Formula
        {
            get => _formula;
            set => SetProperty(ref _formula, value);
        }

        public string FormulaName
        {
            get => _formulaName;
            set => SetProperty(ref _formulaName, value);
        }

        public Type FormulaValueType
        {
            get => ValueType;
            set => ValueType = value;
        }

        public bool AllowCellFormulas
        {
            get => _allowCellFormulas;
            set
            {
                if (SetProperty(ref _allowCellFormulas, value))
                {
                    IsReadOnly = !value;
                }
            }
        }

        protected override DataGridColumn CreateColumnCore()
        {
            return new DataGridFormulaTextColumn();
        }

        protected override void ApplyColumnProperties(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            if (column is DataGridFormulaTextColumn textColumn)
            {
                var binding = CreateFormulaBinding(context?.Grid, this);
                textColumn.Binding = binding;
                textColumn.ClipboardContentBinding = binding;
                textColumn.FormulaDefinition = this;
            }

            if (context?.Grid != null)
            {
                var accessor = new DataGridFormulaValueAccessor(context.Grid, this);
                DataGridColumnMetadata.SetValueAccessor(column, accessor);
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

            if (column is not DataGridTextColumn textColumn)
            {
                return false;
            }

            switch (propertyName)
            {
                case nameof(Formula):
                case nameof(FormulaName):
                    var binding = CreateFormulaBinding(context?.Grid, this);
                    textColumn.Binding = binding;
                    textColumn.ClipboardContentBinding = binding;
                    if (context?.Grid?.FormulaModel is DataGridFormulaModel formulaModel)
                    {
                        formulaModel.NotifyColumnDefinitionChanged(this, propertyName);
                    }
                    else
                    {
                        context?.Grid?.FormulaModel?.Invalidate();
                    }
                    return true;
            }

            return false;
        }

        private static BindingBase CreateFormulaBinding(DataGrid grid, DataGridFormulaColumnDefinition definition)
        {
            if (grid == null)
            {
                return null;
            }

            var binding = new MultiBinding
            {
                Mode = BindingMode.OneWay,
                Converter = new DataGridFormulaMultiValueConverter(grid, definition)
            };

            binding.Bindings.Add(new Binding { Path = ".", Mode = BindingMode.OneWay });
            binding.Bindings.Add(new Binding { Source = grid, Path = "FormulaModel.FormulaVersion", Mode = BindingMode.OneWay });
            return binding;
        }
    }
}
