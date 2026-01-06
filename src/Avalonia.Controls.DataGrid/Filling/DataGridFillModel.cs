// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls;
using Avalonia.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Avalonia.Controls.DataGridFilling
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    interface IDataGridFillModel
    {
        void ApplyFill(DataGridFillContext context);
    }

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    interface IDataGridFillModelFactory
    {
        IDataGridFillModel Create();
    }

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    enum DataGridFillDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4
    }

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    sealed class DataGridFillContext
    {
        private readonly DataGrid _grid;

        public DataGridFillContext(DataGrid grid, DataGridCellRange sourceRange, DataGridCellRange targetRange)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            SourceRange = sourceRange;
            TargetRange = targetRange;
            Direction = GetDirection(sourceRange, targetRange);
        }

        public DataGrid Grid => _grid;

        public DataGridCellRange SourceRange { get; }

        public DataGridCellRange TargetRange { get; }

        public DataGridFillDirection Direction { get; }

        public bool IsVerticalFill => Direction == DataGridFillDirection.Up || Direction == DataGridFillDirection.Down;

        public bool IsHorizontalFill => Direction == DataGridFillDirection.Left || Direction == DataGridFillDirection.Right;

        public int RowCount => _grid.DataConnection?.Count ?? 0;

        public bool TryGetRowItem(int rowIndex, out object item)
        {
            item = null;

            var connection = _grid.DataConnection;
            if (connection == null || rowIndex < 0 || rowIndex >= connection.Count)
            {
                return false;
            }

            item = connection.GetDataItem(rowIndex);
            if (item == null || ReferenceEquals(item, global::Avalonia.Collections.DataGridCollectionView.NewItemPlaceholder))
            {
                return false;
            }

            return true;
        }

        public IDisposable BeginRowEdit(int rowIndex, out object item)
        {
            item = null;

            if (!TryGetRowItem(rowIndex, out item))
            {
                return NoopDisposable.Instance;
            }

            _grid.DataConnection.BeginEdit(item);
            return new RowEditScope(_grid, item);
        }

        public bool TryGetCellValue(int rowIndex, int columnIndex, out object value)
        {
            return _grid.TryGetFillCellValue(rowIndex, columnIndex, out value);
        }

        public bool TryGetCellText(int rowIndex, int columnIndex, out string text)
        {
            return _grid.TryGetFillCellText(rowIndex, columnIndex, out text);
        }

        public bool TrySetCellText(int rowIndex, int columnIndex, string text)
        {
            return _grid.TrySetFillCellText(rowIndex, columnIndex, text);
        }

        public bool TrySetCellText(object item, int columnIndex, string text)
        {
            return _grid.TrySetFillCellText(item, columnIndex, text);
        }

        private static DataGridFillDirection GetDirection(DataGridCellRange source, DataGridCellRange target)
        {
            if (target.StartRow < source.StartRow)
            {
                return DataGridFillDirection.Up;
            }

            if (target.EndRow > source.EndRow)
            {
                return DataGridFillDirection.Down;
            }

            if (target.StartColumn < source.StartColumn)
            {
                return DataGridFillDirection.Left;
            }

            if (target.EndColumn > source.EndColumn)
            {
                return DataGridFillDirection.Right;
            }

            return DataGridFillDirection.None;
        }

        private sealed class RowEditScope : IDisposable
        {
            private DataGrid _grid;
            private object _item;

            public RowEditScope(DataGrid grid, object item)
            {
                _grid = grid;
                _item = item;
            }

            public void Dispose()
            {
                var grid = _grid;
                var item = _item;
                if (grid == null || item == null)
                {
                    return;
                }

                _grid = null;
                _item = null;
                grid.DataConnection.EndEdit(item);
            }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();

            public void Dispose()
            {
            }
        }
    }

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridFillModel : IDataGridFillModel
    {
        public virtual void ApplyFill(DataGridFillContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!CanApplyFill(context))
            {
                return;
            }

            var source = context.SourceRange;
            var target = context.TargetRange;

            if (source == target)
            {
                return;
            }

            var rowCount = source.RowCount;
            var colCount = source.ColumnCount;

            if (rowCount <= 0 || colCount <= 0)
            {
                return;
            }

            var isVerticalFill = target.StartColumn == source.StartColumn
                && target.EndColumn == source.EndColumn
                && (target.StartRow != source.StartRow || target.EndRow != source.EndRow);
            var isHorizontalFill = target.StartRow == source.StartRow
                && target.EndRow == source.EndRow
                && (target.StartColumn != source.StartColumn || target.EndColumn != source.EndColumn);

            Dictionary<int, FillSeries> seriesByColumn = null;
            Dictionary<int, FillSeries> seriesByRow = null;

            if (isVerticalFill)
            {
                seriesByColumn = BuildFillSeriesByColumn(context, source);
            }
            else if (isHorizontalFill)
            {
                seriesByRow = BuildFillSeriesByRow(context, source);
            }

            ApplyFillCore(context, source, target, rowCount, colCount, isVerticalFill, isHorizontalFill, seriesByColumn, seriesByRow);
        }

        protected virtual bool CanApplyFill(DataGridFillContext context)
        {
            var grid = context.Grid;
            return grid?.DataConnection != null && !grid.IsReadOnly;
        }

        protected virtual void ApplyFillCore(
            DataGridFillContext context,
            DataGridCellRange source,
            DataGridCellRange target,
            int rowCount,
            int colCount,
            bool isVerticalFill,
            bool isHorizontalFill,
            Dictionary<int, FillSeries> seriesByColumn,
            Dictionary<int, FillSeries> seriesByRow)
        {
            var grid = context.Grid;

            for (var rowIndex = target.StartRow; rowIndex <= target.EndRow; rowIndex++)
            {
                using var editScope = context.BeginRowEdit(rowIndex, out var item);
                if (item == null)
                {
                    continue;
                }

                for (var columnIndex = target.StartColumn; columnIndex <= target.EndColumn; columnIndex++)
                {
                    if (columnIndex < 0 || columnIndex >= grid.ColumnsItemsInternal.Count)
                    {
                        continue;
                    }

                    if (source.Contains(rowIndex, columnIndex))
                    {
                        continue;
                    }

                    var hasText = false;
                    var text = string.Empty;

                    if (isVerticalFill && seriesByColumn != null && seriesByColumn.TryGetValue(columnIndex, out var columnSeries))
                    {
                        hasText = TryGetSeriesFillText(columnSeries, rowIndex - source.StartRow, out text);
                    }
                    else if (isHorizontalFill && seriesByRow != null && seriesByRow.TryGetValue(rowIndex, out var rowSeries))
                    {
                        hasText = TryGetSeriesFillText(rowSeries, columnIndex - source.StartColumn, out text);
                    }

                    if (!hasText)
                    {
                        var sourceRow = source.StartRow + Mod(rowIndex - source.StartRow, rowCount);
                        var sourceColumn = source.StartColumn + Mod(columnIndex - source.StartColumn, colCount);
                        hasText = TryGetFillText(context, sourceRow, sourceColumn, out text);
                    }

                    if (hasText)
                    {
                        context.TrySetCellText(item, columnIndex, text);
                    }
                }
            }
        }

        protected virtual bool TryGetFillText(DataGridFillContext context, int rowIndex, int columnIndex, out string text)
        {
            return context.TryGetCellText(rowIndex, columnIndex, out text);
        }

        protected virtual Dictionary<int, FillSeries> BuildFillSeriesByColumn(DataGridFillContext context, DataGridCellRange source)
        {
            var result = new Dictionary<int, FillSeries>();
            for (var columnIndex = source.StartColumn; columnIndex <= source.EndColumn; columnIndex++)
            {
                if (TryBuildFillSeriesForColumn(context, source, columnIndex, out var series))
                {
                    result[columnIndex] = series;
                }
            }
            return result;
        }

        protected virtual Dictionary<int, FillSeries> BuildFillSeriesByRow(DataGridFillContext context, DataGridCellRange source)
        {
            var result = new Dictionary<int, FillSeries>();
            for (var rowIndex = source.StartRow; rowIndex <= source.EndRow; rowIndex++)
            {
                if (TryBuildFillSeriesForRow(context, source, rowIndex, out var series))
                {
                    result[rowIndex] = series;
                }
            }
            return result;
        }

        protected virtual bool TryBuildFillSeriesForColumn(DataGridFillContext context, DataGridCellRange source, int columnIndex, out FillSeries series)
        {
            series = default;
            var values = new List<object>();
            for (var rowIndex = source.StartRow; rowIndex <= source.EndRow; rowIndex++)
            {
                if (!context.TryGetCellValue(rowIndex, columnIndex, out var value) || value == null)
                {
                    return false;
                }
                values.Add(value);
            }

            return TryBuildFillSeries(values, out series);
        }

        protected virtual bool TryBuildFillSeriesForRow(DataGridFillContext context, DataGridCellRange source, int rowIndex, out FillSeries series)
        {
            series = default;
            var values = new List<object>();
            for (var columnIndex = source.StartColumn; columnIndex <= source.EndColumn; columnIndex++)
            {
                if (!context.TryGetCellValue(rowIndex, columnIndex, out var value) || value == null)
                {
                    return false;
                }
                values.Add(value);
            }

            return TryBuildFillSeries(values, out series);
        }

        protected virtual bool TryBuildFillSeries(IReadOnlyList<object> values, out FillSeries series)
        {
            series = default;

            if (values.Count == 0)
            {
                return false;
            }

            if (TryBuildNumberSeries(values, out series))
            {
                return true;
            }

            if (TryBuildTimeSeries(values, out series))
            {
                return true;
            }

            if (TryBuildDateSeries(values, out series))
            {
                return true;
            }

            return false;
        }

        protected virtual bool TryBuildNumberSeries(IReadOnlyList<object> values, out FillSeries series)
        {
            series = default;
            var numbers = new List<double>(values.Count);
            foreach (var value in values)
            {
                if (!TryConvertToDouble(value, out var number))
                {
                    return false;
                }
                numbers.Add(number);
            }

            var step = numbers.Count == 1 ? 1d : numbers[1] - numbers[0];
            if (numbers.Count > 1)
            {
                for (var i = 1; i < numbers.Count; i++)
                {
                    var diff = numbers[i] - numbers[i - 1];
                    if (!MathUtilities.AreClose(diff, step))
                    {
                        return false;
                    }
                }
            }

            series = FillSeries.Number(numbers[0], step);
            return true;
        }

        protected virtual bool TryBuildDateSeries(IReadOnlyList<object> values, out FillSeries series)
        {
            series = default;
            var dates = new List<DateTime>(values.Count);
            foreach (var value in values)
            {
                if (!TryConvertToDateTime(value, out var date))
                {
                    return false;
                }
                dates.Add(date);
            }

            var step = dates.Count == 1 ? TimeSpan.FromDays(1) : dates[1] - dates[0];
            if (dates.Count > 1)
            {
                for (var i = 1; i < dates.Count; i++)
                {
                    var diff = dates[i] - dates[i - 1];
                    if (diff != step)
                    {
                        return false;
                    }
                }
            }

            series = FillSeries.DateTime(dates[0], step);
            return true;
        }

        protected virtual bool TryBuildTimeSeries(IReadOnlyList<object> values, out FillSeries series)
        {
            series = default;
            var times = new List<TimeSpan>(values.Count);
            foreach (var value in values)
            {
                if (!TryConvertToTimeSpan(value, out var time))
                {
                    return false;
                }
                times.Add(time);
            }

            var step = times.Count == 1 ? TimeSpan.FromHours(1) : times[1] - times[0];
            if (times.Count > 1)
            {
                for (var i = 1; i < times.Count; i++)
                {
                    var diff = times[i] - times[i - 1];
                    if (diff != step)
                    {
                        return false;
                    }
                }
            }

            series = FillSeries.TimeSpan(times[0], step);
            return true;
        }

        protected virtual bool TryConvertToDouble(object value, out double number)
        {
            number = 0d;

            switch (value)
            {
                case byte b:
                    number = b;
                    return true;
                case short s:
                    number = s;
                    return true;
                case int i:
                    number = i;
                    return true;
                case long l:
                    number = l;
                    return true;
                case float f:
                    number = f;
                    return true;
                case double d:
                    number = d;
                    return true;
                case decimal dec:
                    number = (double)dec;
                    return true;
                case string text:
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
                default:
                    return false;
            }
        }

        protected virtual bool TryConvertToDateTime(object value, out DateTime date)
        {
            date = default;

            switch (value)
            {
                case DateTime dt:
                    date = dt;
                    return true;
                case DateTimeOffset dto:
                    date = dto.DateTime;
                    return true;
#if NET6_0_OR_GREATER
                case DateOnly dateOnly:
                    date = dateOnly.ToDateTime(TimeOnly.MinValue);
                    return true;
#endif
                case string text when !IsTimeOnlyString(text):
                    return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
                default:
                    return false;
            }
        }

        protected virtual bool TryConvertToTimeSpan(object value, out TimeSpan time)
        {
            time = default;

            switch (value)
            {
                case TimeSpan span:
                    time = span;
                    return true;
#if NET6_0_OR_GREATER
                case TimeOnly timeOnly:
                    time = timeOnly.ToTimeSpan();
                    return true;
#endif
                case string text when IsTimeOnlyString(text):
                    return TimeSpan.TryParse(text, CultureInfo.CurrentCulture, out time);
                default:
                    return false;
            }
        }

        protected virtual bool TryGetSeriesFillText(FillSeries series, int offset, out string text)
        {
            text = string.Empty;

            try
            {
                switch (series.Kind)
                {
                    case FillSeriesKind.Number:
                        text = FormatFillValue(series.NumberStart + series.NumberStep * offset);
                        return true;
                    case FillSeriesKind.DateTime:
                        text = FormatFillValue(series.DateStart + TimeSpan.FromTicks(series.DateStep.Ticks * offset));
                        return true;
                    case FillSeriesKind.TimeSpan:
                        text = FormatFillValue(series.TimeStart + TimeSpan.FromTicks(series.TimeStep.Ticks * offset));
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        protected virtual string FormatFillValue(object value)
        {
            var converted = DataGridValueConverter.Instance.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);
            return converted?.ToString() ?? string.Empty;
        }

        protected virtual bool IsTimeOnlyString(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (!trimmed.Contains(":"))
            {
                return false;
            }

            return trimmed.IndexOfAny(new[] { '/', '-', '.' }) < 0;
        }

        protected readonly struct FillSeries
        {
            private FillSeries(
                FillSeriesKind kind,
                double numberStart,
                double numberStep,
                DateTime dateStart,
                TimeSpan dateStep,
                TimeSpan timeStart,
                TimeSpan timeStep)
            {
                Kind = kind;
                NumberStart = numberStart;
                NumberStep = numberStep;
                DateStart = dateStart;
                DateStep = dateStep;
                TimeStart = timeStart;
                TimeStep = timeStep;
            }

            public FillSeriesKind Kind { get; }

            public double NumberStart { get; }

            public double NumberStep { get; }

            public DateTime DateStart { get; }

            public TimeSpan DateStep { get; }

            public TimeSpan TimeStart { get; }

            public TimeSpan TimeStep { get; }

            public static FillSeries Number(double start, double step)
            {
                return new FillSeries(FillSeriesKind.Number, start, step, default, default, default, default);
            }

            public static FillSeries DateTime(DateTime start, TimeSpan step)
            {
                return new FillSeries(FillSeriesKind.DateTime, 0, 0, start, step, default, default);
            }

            public static FillSeries TimeSpan(TimeSpan start, TimeSpan step)
            {
                return new FillSeries(FillSeriesKind.TimeSpan, 0, 0, default, default, start, step);
            }
        }

        protected enum FillSeriesKind
        {
            None = 0,
            Number = 1,
            DateTime = 2,
            TimeSpan = 3
        }

        protected static int Mod(int value, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }

            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }
    }
}
