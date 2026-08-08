using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Core;

namespace DataGridSample.Models;

public sealed class GeneratedCustomImplementationSchema : IDataGridGeneratedSchema<GeneratedCustomRow>
{
    private static readonly DataGridColumnValueAccessor<GeneratedCustomRow, int> s_idAccessor =
        new(static row => row.Id, static (row, value) => row.Id = value);

    private static readonly DataGridColumnValueAccessor<GeneratedCustomRow, string> s_labelAccessor =
        new(static row => row.Label, static (row, value) => row.Label = value);

    private static readonly DataGridColumnValueAccessor<GeneratedCustomRow, int> s_priorityAccessor =
        new(static row => row.Priority, static (row, value) => row.Priority = value);

    private static readonly DataGridGeneratedDataOperations<GeneratedCustomRow> s_operations = new(
        new[]
        {
            new DataGridColumnAccessorRegistration("custom-id", nameof(GeneratedCustomRow.Id), s_idAccessor),
            new DataGridColumnAccessorRegistration("custom-label", nameof(GeneratedCustomRow.Label), s_labelAccessor),
            new DataGridColumnAccessorRegistration("custom-priority", nameof(GeneratedCustomRow.Priority), s_priorityAccessor)
        });

    public DataGridColumnDefinitionList CreateColumnDefinitions()
    {
        DataGridColumnDefinitionBuilder<GeneratedCustomRow> builder = DataGridColumnDefinitionBuilder.For<GeneratedCustomRow>();
        var columns = new DataGridColumnDefinitionList
        {
            builder.Numeric("ID", Property(nameof(GeneratedCustomRow.Id), typeof(int), static target => ((GeneratedCustomRow)target).Id, static (target, value) => ((GeneratedCustomRow)target).Id = (int)value!), static row => row.Id, static (row, value) => row.Id = value, column => column.ColumnKey = "custom-id"),
            builder.Text("Label", Property(nameof(GeneratedCustomRow.Label), typeof(string), static target => ((GeneratedCustomRow)target).Label, static (target, value) => ((GeneratedCustomRow)target).Label = (string)value!), static row => row.Label, static (row, value) => row.Label = value, column =>
            {
                column.ColumnKey = "custom-label";
                GeneratedCustomRow.ConfigureLabel(column);
            }),
            builder.Numeric("Priority", Property(nameof(GeneratedCustomRow.Priority), typeof(int), static target => ((GeneratedCustomRow)target).Priority, static (target, value) => ((GeneratedCustomRow)target).Priority = (int)value!), static row => row.Priority, static (row, value) => row.Priority = value, column =>
            {
                column.ColumnKey = "custom-priority";
                column.Minimum = 0;
                column.Maximum = 5;
            })
        };
        return columns;
    }

    public IComparer<GeneratedCustomRow> CreateSortComparer(IReadOnlyList<Avalonia.Controls.DataGridSorting.SortingDescriptor> descriptors) =>
        s_operations.CreateSortComparer(descriptors);

    public Func<GeneratedCustomRow, bool> CreateFilterPredicate(IReadOnlyList<Avalonia.Controls.DataGridFiltering.FilteringDescriptor> descriptors) =>
        s_operations.CreateFilterPredicate(descriptors);

    public Func<GeneratedCustomRow, bool> CreateSearchPredicate(IReadOnlyList<Avalonia.Controls.DataGridSearching.SearchDescriptor> descriptors) =>
        s_operations.CreateSearchPredicate(descriptors);

    public DataGridFastPathOptions CreateFastPathOptions() => new()
    {
        UseAccessorsOnly = true,
        ThrowOnMissingAccessor = true,
        EnableHighPerformanceSearching = true
    };

    private static IPropertyInfo Property(string name, Type type, Func<object, object?> getter, Action<object, object?> setter) =>
        new ClrPropertyInfo(name, getter, setter, type);
}
