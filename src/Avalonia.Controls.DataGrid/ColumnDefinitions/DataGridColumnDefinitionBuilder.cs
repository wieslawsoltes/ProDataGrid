// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Data;
using Avalonia.Data.Core;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridColumnDefinitionBuilder
    {
        public static DataGridColumnDefinitionBuilder<TItem> For<TItem>()
        {
            return new DataGridColumnDefinitionBuilder<TItem>();
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridColumnDefinitionBuilder<TItem>
    {
        public DataGridTextColumnDefinition Text<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridTextColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridTextColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridTextColumnDefinition Text<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridTextColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridTextColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridCheckBoxColumnDefinition CheckBox<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridCheckBoxColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridCheckBoxColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridCheckBoxColumnDefinition CheckBox<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridCheckBoxColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridCheckBoxColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridHyperlinkColumnDefinition Hyperlink<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridHyperlinkColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridHyperlinkColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridHyperlinkColumnDefinition Hyperlink<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridHyperlinkColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridHyperlinkColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridImageColumnDefinition Image<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridImageColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridImageColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridImageColumnDefinition Image<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridImageColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridImageColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridNumericColumnDefinition Numeric<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridNumericColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridNumericColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridNumericColumnDefinition Numeric<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridNumericColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridNumericColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridProgressBarColumnDefinition ProgressBar<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridProgressBarColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridProgressBarColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridProgressBarColumnDefinition ProgressBar<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridProgressBarColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridProgressBarColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridSliderColumnDefinition Slider<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridSliderColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridSliderColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridSliderColumnDefinition Slider<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridSliderColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridSliderColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridDatePickerColumnDefinition DatePicker<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridDatePickerColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridDatePickerColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridDatePickerColumnDefinition DatePicker<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridDatePickerColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridDatePickerColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridTimePickerColumnDefinition TimePicker<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridTimePickerColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridTimePickerColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridTimePickerColumnDefinition TimePicker<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridTimePickerColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridTimePickerColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridMaskedTextColumnDefinition MaskedText<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridMaskedTextColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridMaskedTextColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridMaskedTextColumnDefinition MaskedText<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridMaskedTextColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridMaskedTextColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridAutoCompleteColumnDefinition AutoComplete<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridAutoCompleteColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridAutoCompleteColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridAutoCompleteColumnDefinition AutoComplete<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridAutoCompleteColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridAutoCompleteColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridToggleButtonColumnDefinition ToggleButton<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridToggleButtonColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridToggleButtonColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridToggleButtonColumnDefinition ToggleButton<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridToggleButtonColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridToggleButtonColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridToggleSwitchColumnDefinition ToggleSwitch<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridToggleSwitchColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridToggleSwitchColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridToggleSwitchColumnDefinition ToggleSwitch<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridToggleSwitchColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridToggleSwitchColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridHierarchicalColumnDefinition Hierarchical<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridHierarchicalColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridHierarchicalColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridHierarchicalColumnDefinition Hierarchical<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridHierarchicalColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridHierarchicalColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridCustomDrawingColumnDefinition CustomDrawing<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridCustomDrawingColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridCustomDrawingColumnDefinition, TValue>(header, path, getter, setter, configure);
        }

        public DataGridCustomDrawingColumnDefinition CustomDrawing<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridCustomDrawingColumnDefinition> configure = null)
        {
            return CreateBoundDefinition<DataGridCustomDrawingColumnDefinition, TValue>(header, property, getter, setter, configure);
        }

        public DataGridComboBoxColumnDefinition ComboBoxSelectedItem<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridComboBoxColumnDefinition> configure = null)
        {
            var binding = DataGridBindingDefinition.Create(path, getter, setter);
            return CreateComboBoxDefinition(header, binding, ComboBoxBindingKind.SelectedItem, configure);
        }

        public DataGridComboBoxColumnDefinition ComboBoxSelectedItem<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridComboBoxColumnDefinition> configure = null)
        {
            var binding = DataGridBindingDefinition.Create(property, getter, setter);
            return CreateComboBoxDefinition(header, binding, ComboBoxBindingKind.SelectedItem, configure);
        }

        public DataGridComboBoxColumnDefinition ComboBoxSelectedValue<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridComboBoxColumnDefinition> configure = null)
        {
            var binding = DataGridBindingDefinition.Create(path, getter, setter);
            return CreateComboBoxDefinition(header, binding, ComboBoxBindingKind.SelectedValue, configure);
        }

        public DataGridComboBoxColumnDefinition ComboBoxSelectedValue<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridComboBoxColumnDefinition> configure = null)
        {
            var binding = DataGridBindingDefinition.Create(property, getter, setter);
            return CreateComboBoxDefinition(header, binding, ComboBoxBindingKind.SelectedValue, configure);
        }

        public DataGridComboBoxColumnDefinition ComboBoxText<TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridComboBoxColumnDefinition> configure = null)
        {
            var binding = DataGridBindingDefinition.Create(path, getter, setter);
            return CreateComboBoxDefinition(header, binding, ComboBoxBindingKind.Text, configure);
        }

        public DataGridComboBoxColumnDefinition ComboBoxText<TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null,
            Action<DataGridComboBoxColumnDefinition> configure = null)
        {
            var binding = DataGridBindingDefinition.Create(property, getter, setter);
            return CreateComboBoxDefinition(header, binding, ComboBoxBindingKind.Text, configure);
        }

        public DataGridTemplateColumnDefinition Template(
            object header,
            string cellTemplateKey,
            Action<DataGridTemplateColumnDefinition> configure = null)
        {
            var definition = new DataGridTemplateColumnDefinition
            {
                Header = header,
                CellTemplateKey = cellTemplateKey
            };

            configure?.Invoke(definition);
            return definition;
        }

        public DataGridButtonColumnDefinition Button(
            object header,
            object content = null,
            Action<DataGridButtonColumnDefinition> configure = null)
        {
            var definition = new DataGridButtonColumnDefinition
            {
                Header = header,
                Content = content
            };

            configure?.Invoke(definition);
            return definition;
        }

        public DataGridFormulaColumnDefinition Formula(
            object header,
            string formula,
            string formulaName = null,
            Action<DataGridFormulaColumnDefinition> configure = null)
        {
            var definition = new DataGridFormulaColumnDefinition
            {
                Header = header,
                Formula = formula,
                FormulaName = formulaName
            };

            configure?.Invoke(definition);
            return definition;
        }

        private static TDefinition CreateBoundDefinition<TDefinition, TValue>(
            object header,
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter,
            Action<TDefinition> configure)
            where TDefinition : DataGridBoundColumnDefinition, new()
        {
            var binding = DataGridBindingDefinition.Create(path, getter, setter);
            return CreateBoundDefinition(header, binding, configure);
        }

        private static TDefinition CreateBoundDefinition<TDefinition, TValue>(
            object header,
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter,
            Action<TDefinition> configure)
            where TDefinition : DataGridBoundColumnDefinition, new()
        {
            var binding = DataGridBindingDefinition.Create(property, getter, setter);
            return CreateBoundDefinition(header, binding, configure);
        }

        private static TDefinition CreateBoundDefinition<TDefinition>(
            object header,
            DataGridBindingDefinition binding,
            Action<TDefinition> configure)
            where TDefinition : DataGridBoundColumnDefinition, new()
        {
            var definition = new TDefinition
            {
                Header = header,
                Binding = binding
            };

            configure?.Invoke(definition);
            return definition;
        }

        private static DataGridComboBoxColumnDefinition CreateComboBoxDefinition(
            object header,
            DataGridBindingDefinition binding,
            ComboBoxBindingKind kind,
            Action<DataGridComboBoxColumnDefinition> configure)
        {
            var definition = new DataGridComboBoxColumnDefinition
            {
                Header = header
            };

            switch (kind)
            {
                case ComboBoxBindingKind.SelectedItem:
                    definition.SelectedItemBinding = binding;
                    break;
                case ComboBoxBindingKind.SelectedValue:
                    definition.SelectedValueBinding = binding;
                    break;
                case ComboBoxBindingKind.Text:
                    definition.TextBinding = binding;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown combo box binding kind.");
            }

            configure?.Invoke(definition);
            return definition;
        }

        private enum ComboBoxBindingKind
        {
            SelectedItem,
            SelectedValue,
            Text
        }
    }
}
