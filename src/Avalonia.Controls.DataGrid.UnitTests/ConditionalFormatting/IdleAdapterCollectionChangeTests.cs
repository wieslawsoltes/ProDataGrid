// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ConditionalFormatting;

public class IdleAdapterCollectionChangeTests
{
    [AvaloniaFact]
    public void Formatting_Adapter_Without_Rules_Ignores_Collection_Changes()
    {
        var items = new ObservableCollection<CountingItem> { new(1) };
        var adapter = new DataGridConditionalFormattingAdapter(new ConditionalFormattingModel(), () => Array.Empty<DataGridColumn>());
        adapter.AttachView(new DataGridCollectionView(items));
        var raised = 0;
        adapter.FormattingChanged += (_, _) => raised++;

        var added = new CountingItem(2);
        items.Add(added);
        items.RemoveAt(0);

        Assert.Equal(0, raised);
        Assert.Equal(0, added.Subscribers);
    }

    [AvaloniaFact]
    public void Formatting_Adapter_With_Rules_Still_Follows_Collection_Changes()
    {
        var items = new ObservableCollection<CountingItem> { new(1) };
        var column = new DataGridTextColumn();
        var model = new ConditionalFormattingModel();
        var adapter = new DataGridConditionalFormattingAdapter(model, () => new[] { column });
        adapter.AttachView(new DataGridCollectionView(items));
        model.Apply(new[]
        {
            new ConditionalFormattingDescriptor(
                ruleId: "positive",
                @operator: ConditionalFormattingOperator.GreaterThan,
                columnId: column,
                propertyPath: nameof(CountingItem.Value),
                value: 0)
        });
        var raised = 0;
        adapter.FormattingChanged += (_, _) => raised++;

        var added = new CountingItem(2);
        items.Add(added);

        Assert.Equal(1, raised);
        Assert.Equal(1, added.Subscribers);
    }

    [AvaloniaFact]
    public void Search_Adapter_Without_A_Search_Does_Not_Republish_Empty_Results()
    {
        var items = new ObservableCollection<CountingItem> { new(1) };
        var model = new SearchModel();
        var adapter = new DataGridSearchAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(new DataGridCollectionView(items));
        Dispatcher.UIThread.RunJobs();
        var raised = 0;
        model.ResultsChanged += (_, _) => raised++;

        items.Add(new CountingItem(2));
        Dispatcher.UIThread.RunJobs();
        items.RemoveAt(0);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, raised);
    }

    private sealed class CountingItem : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _propertyChanged;

        public CountingItem(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public int Subscribers { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                Subscribers++;
                _propertyChanged += value;
            }
            remove
            {
                Subscribers--;
                _propertyChanged -= value;
            }
        }
    }
}
