using System;
using Avalonia.Controls;
using Avalonia.Data.Core;

namespace ProDataGrid.ExcelSample.Helpers;

internal static class ColumnDefinitionBindingFactory
{
    public static IPropertyInfo CreateProperty<TItem, TValue>(
        string name,
        Func<TItem, TValue> getter,
        Action<TItem, TValue>? setter = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Property name is required.", nameof(name));
        }

        if (getter == null)
        {
            throw new ArgumentNullException(nameof(getter));
        }

        return new ClrPropertyInfo(
            name,
            target => TryGetValue(target, getter),
            setter == null
                ? null
                : (target, value) => TrySetValue(target, value, setter),
            typeof(TValue));
    }

    public static DataGridBindingDefinition CreateBinding<TItem, TValue>(
        string name,
        Func<TItem, TValue> getter,
        Action<TItem, TValue>? setter = null)
    {
        var propertyInfo = CreateProperty(name, getter, setter);
        return DataGridBindingDefinition.Create(propertyInfo, getter, setter);
    }

    private static TValue TryGetValue<TItem, TValue>(object target, Func<TItem, TValue> getter)
    {
        if (target is not TItem item)
        {
            return default!;
        }

        return getter(item);
    }

    private static void TrySetValue<TItem, TValue>(object target, object? value, Action<TItem, TValue> setter)
    {
        if (target is not TItem item)
        {
            return;
        }

        if (value is null)
        {
            setter(item, default!);
            return;
        }

        if (value is TValue typedValue)
        {
            setter(item, typedValue);
            return;
        }

        setter(item, (TValue)value);
    }
}
