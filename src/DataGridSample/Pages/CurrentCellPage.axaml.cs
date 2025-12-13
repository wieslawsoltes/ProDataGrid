using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DataGridSample.Models;

namespace DataGridSample.Pages
{
    public partial class CurrentCellPage : UserControl, INotifyPropertyChanged
    {
        private DataGrid? _grid;

        public CurrentCellPage()
        {
            Items = new ObservableCollection<Country>(Countries.All.Take(25).ToList());
            Log = new ObservableCollection<string>();
            _currentCell = DataGridCellInfo.Unset;

            InitializeComponent();
            _grid = this.FindControl<DataGrid>("CurrentCellGrid");
            DataContext = this;
        }

        public ObservableCollection<Country> Items { get; }

        public ObservableCollection<string> Log { get; }

        private DataGridCellInfo _currentCell;

        public DataGridCellInfo CurrentCell
        {
            get => _currentCell;
            set
            {
                if (_currentCell.Equals(value) &&
                    _currentCell.RowIndex == value.RowIndex &&
                    _currentCell.ColumnIndex == value.ColumnIndex &&
                    _currentCell.IsValid == value.IsValid)
                {
                    return;
                }

                _currentCell = value;
                OnPropertyChanged(nameof(CurrentCell));
                OnPropertyChanged(nameof(CurrentCellDescription));
            }
        }

        public string CurrentCellDescription =>
            CurrentCell.IsValid
                ? $"Row {CurrentCell.RowIndex}, Column {CurrentCell.ColumnIndex} ({CurrentCell.Column?.Header ?? "?"}) - item: {DescribeItem(CurrentCell.Item)}"
                : "No current cell";

        private void OnFirstCell(object? sender, RoutedEventArgs e)
        {
            MoveToCell(0, 0);
        }

        private void OnLastCell(object? sender, RoutedEventArgs e)
        {
            if (_grid == null)
            {
                return;
            }

            var visibleColumns = _grid.Columns.Where(c => c.IsVisible).ToList();
            if (Items.Count == 0 || visibleColumns.Count == 0)
            {
                return;
            }

            MoveToCell(Items.Count - 1, visibleColumns.Count - 1);
        }

        private void OnClearCell(object? sender, RoutedEventArgs e)
        {
            CurrentCell = DataGridCellInfo.Unset;
        }

        private void MoveToCell(int rowIndex, int columnIndex)
        {
            if (_grid == null)
            {
                return;
            }

            if (Items.Count == 0)
            {
                return;
            }

            var visibleColumns = _grid.Columns.Where(c => c.IsVisible).ToList();
            if (visibleColumns.Count == 0)
            {
                return;
            }

            if (rowIndex < 0 || rowIndex >= Items.Count)
            {
                return;
            }

            var targetColumn = columnIndex >= 0 && columnIndex < visibleColumns.Count
                ? visibleColumns[columnIndex]
                : visibleColumns.FirstOrDefault();
            if (targetColumn == null)
            {
                return;
            }

            var allColumns = _grid.Columns.ToList();
            var targetColumnIndex = allColumns.IndexOf(targetColumn);

            object? item = null;
            if (_grid.ItemsSource is IList list && rowIndex < list.Count)
            {
                item = list[rowIndex];
            }
            else if (rowIndex < Items.Count)
            {
                item = Items[rowIndex];
            }

            if (item == null || targetColumnIndex < 0)
            {
                return;
            }

            CurrentCell = new DataGridCellInfo(item, targetColumn, rowIndex, targetColumnIndex, isValid: true);
        }

        private void OnCurrentCellChanged(object? sender, DataGridCurrentCellChangedEventArgs e)
        {
            var message = CurrentCell.IsValid
                ? $"Row {CurrentCell.RowIndex}, Column {CurrentCell.ColumnIndex} ({CurrentCell.Column?.Header ?? "?"})"
                : "Cleared current cell";

            Log.Insert(0, message);
            if (Log.Count > 40)
            {
                Log.RemoveAt(Log.Count - 1);
            }
        }

        private static string DescribeItem(object? item) =>
            item switch
            {
                Country country => country.Name,
                null => "null",
                _ => item.ToString() ?? "null"
            };

        public new event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
