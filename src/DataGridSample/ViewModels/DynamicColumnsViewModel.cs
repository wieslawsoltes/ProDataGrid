using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System.Diagnostics.CodeAnalysis;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class DynamicColumnsViewModel : ObservableObject
    {
        private int _columnSeed = 1;

        public DynamicColumnsViewModel()
        {
            Items = new ObservableCollection<Person>(CreatePeople());
            Columns = new ObservableCollection<DataGridColumn>();
            BoundEvents = new ObservableCollection<string>();
            UnboundEvents = new ObservableCollection<string>();

            Columns.CollectionChanged += OnColumnsChanged;

            AddColumnCommand = new RelayCommand(_ => AddColumn());
            InsertColumnCommand = new RelayCommand(_ => InsertColumn());
            RemoveLastColumnCommand = new RelayCommand(_ => RemoveLastColumn());
            ReplaceFirstCommand = new RelayCommand(_ => ReplaceFirstColumn());
            MoveLastToFirstCommand = new RelayCommand(_ => MoveLastToFirst());
            SwapFirstLastCommand = new RelayCommand(_ => SwapFirstLast());
            ClearColumnsCommand = new RelayCommand(_ => ClearColumns());
            ResetColumnsCommand = new RelayCommand(_ => ResetColumns());

            ResetColumns();
        }

        public ObservableCollection<Person> Items { get; }

        public ObservableCollection<DataGridColumn> Columns { get; }

        public ObservableCollection<string> BoundEvents { get; }

        public ObservableCollection<string> UnboundEvents { get; }

        public RelayCommand AddColumnCommand { get; }

        public RelayCommand InsertColumnCommand { get; }

        public RelayCommand RemoveLastColumnCommand { get; }

        public RelayCommand ReplaceFirstCommand { get; }

        public RelayCommand MoveLastToFirstCommand { get; }

        public RelayCommand SwapFirstLastCommand { get; }

        public RelayCommand ClearColumnsCommand { get; }

        public RelayCommand ResetColumnsCommand { get; }

        private void AddColumn()
        {
            Columns.Add(CreateDynamicColumn());
        }

        private void InsertColumn()
        {
            Columns.Insert(0, CreateDynamicColumn(headerPrefix: "Inserted"));
        }

        private void RemoveLastColumn()
        {
            if (Columns.Count > 0)
            {
                Columns.RemoveAt(Columns.Count - 1);
            }
        }

        private void ReplaceFirstColumn()
        {
            if (Columns.Count == 0)
            {
                return;
            }

            Columns[0] = CreateDynamicColumn(headerPrefix: "Replaced");
        }

        private void MoveLastToFirst()
        {
            if (Columns.Count > 1)
            {
                Columns.Move(Columns.Count - 1, 0);
            }
        }

        private void SwapFirstLast()
        {
            if (Columns.Count > 1)
            {
                var lastIndex = Columns.Count - 1;
                Columns.Move(lastIndex, 0);
                Columns.Move(1, lastIndex);
            }
        }

        private void ClearColumns()
        {
            Columns.Clear();
        }

        private void ResetColumns()
        {
            Columns.Clear();
            _columnSeed = 1;

            Columns.Add(CreateTextColumn("First Name", p => p.FirstName, 1.2));
            Columns.Add(CreateTextColumn("Last Name", p => p.LastName, 1.2));
            Columns.Add(CreateTextColumn("Age", p => p.Age, 0.8));
        }

        private DataGridTemplateColumn CreateTextColumn(string header, Func<Person, object?> selector, double star = 1)
        {
            return new DataGridTemplateColumn
            {
                Header = header,
                Width = new DataGridLength(star, DataGridLengthUnitType.Star),
                CellTemplate = new FuncDataTemplate<Person>((item, _) =>
                    new TextBlock
                    {
                        Text = selector(item)?.ToString() ?? string.Empty
                    })
            };
        }

        private DataGridColumn CreateDynamicColumn(string? headerPrefix = null)
        {
            var index = _columnSeed++;
            var header = headerPrefix ?? "Dynamic";
            return new DataGridTemplateColumn
            {
                Header = $"{header} {index}",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                CellTemplate = new FuncDataTemplate<Person>((item, _) =>
                    new TextBlock
                    {
                        Text = item.Status.ToString()
                    })
            };
        }

        public void LogUnboundEvent(string message)
        {
            UnboundEvents.Insert(0, message);
            if (UnboundEvents.Count > 50)
            {
                UnboundEvents.RemoveAt(UnboundEvents.Count - 1);
            }
        }

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            string message = e.Action switch
            {
                NotifyCollectionChangedAction.Add => $"Add: {Describe(e.NewItems)} at {e.NewStartingIndex}",
                NotifyCollectionChangedAction.Remove => $"Remove: {Describe(e.OldItems)} from {e.OldStartingIndex}",
                NotifyCollectionChangedAction.Replace => $"Replace: {Describe(e.OldItems)} with {Describe(e.NewItems)} at {e.NewStartingIndex}",
                NotifyCollectionChangedAction.Move => $"Move: {Describe(e.OldItems)} from {e.OldStartingIndex} to {e.NewStartingIndex}",
                NotifyCollectionChangedAction.Reset => "Reset",
                _ => e.Action.ToString()
            };

            BoundEvents.Insert(0, message);
            if (BoundEvents.Count > 50)
            {
                BoundEvents.RemoveAt(BoundEvents.Count - 1);
            }
        }

        internal static string Describe(IList? list)
        {
            if (list == null || list.Count == 0)
            {
                return "(none)";
            }

            return string.Join(", ", list.Cast<DataGridColumn>().Select(c => c.Header?.ToString() ?? "(no header)"));
        }

        private static IEnumerable<Person> CreatePeople()
        {
            var random = new Random(1);
            var statuses = Enum.GetValues(typeof(PersonStatus)).Cast<PersonStatus>().ToArray();

            return Enumerable.Range(1, 8).Select(i => new Person
            {
                FirstName = $"First {i}",
                LastName = $"Last {i}",
                Age = 20 + i,
                Status = statuses[random.Next(statuses.Length)]
            });
        }
    }
}
