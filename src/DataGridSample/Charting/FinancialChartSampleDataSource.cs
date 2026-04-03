using System;
using System.Collections.Generic;
using DataGridSample.Models;
using ProCharts;

namespace DataGridSample.Charting
{
    internal sealed class FinancialChartSampleDataSource : IChartDataSource, IChartWindowInfoProvider
    {
        private readonly IReadOnlyList<FinancialCandleRecord> _candles;

        private readonly struct FinancialPoint
        {
            public FinancialPoint(string? category, double open, double high, double low, double close, double? size = null)
            {
                Category = category;
                Open = open;
                High = high;
                Low = low;
                Close = close;
                Size = size;
            }

            public string? Category { get; }

            public double Open { get; }

            public double High { get; }

            public double Low { get; }

            public double Close { get; }

            public double? Size { get; }
        }

        public FinancialChartSampleDataSource(IReadOnlyList<FinancialCandleRecord> candles, ChartSeriesKind seriesKind)
        {
            _candles = candles;
            SeriesKind = seriesKind;
        }

        public event EventHandler? DataInvalidated;

        public ChartSeriesKind SeriesKind { get; set; }

        public double BrickSize { get; set; } = 1.5d;

        public double RangeSize { get; set; } = 1.5d;

        public int LineBreakPeriod { get; set; } = 3;

        public double KagiReversalAmount { get; set; } = 1.8d;

        public double PointFigureBoxSize { get; set; } = 1.2d;

        public int PointFigureReversalBoxes { get; set; } = 3;

        public int? GetTotalCategoryCount()
        {
            return BuildPoints().Count;
        }

        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
        {
            var points = BuildPoints();
            var (start, count) = ResolveWindow(request, points.Count);
            var categories = new string?[count];
            var closeValues = new double?[count];
            double?[]? openValues = RequiresOpenValues(SeriesKind) ? new double?[count] : null;
            var highValues = new double?[count];
            var lowValues = new double?[count];
            double?[]? sizeValues = HasSizeValues(SeriesKind) ? new double?[count] : null;

            for (var i = 0; i < count; i++)
            {
                var point = points[start + i];
                categories[i] = point.Category;
                closeValues[i] = point.Close;
                if (openValues != null)
                {
                    openValues[i] = point.Open;
                }
                highValues[i] = point.High;
                lowValues[i] = point.Low;
                if (sizeValues != null)
                {
                    sizeValues[i] = point.Size;
                }
            }

            return new ChartDataSnapshot(
                categories,
                new[]
                {
                    new ChartSeriesSnapshot(
                        "Price",
                        SeriesKind,
                        closeValues,
                        sizeValues: sizeValues,
                        openValues: openValues,
                        highValues: highValues,
                        lowValues: lowValues)
                });
        }

        public void Invalidate()
        {
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private List<FinancialPoint> BuildPoints()
        {
            return SeriesKind switch
            {
                ChartSeriesKind.HeikinAshi => BuildHeikinAshiPoints(),
                ChartSeriesKind.Renko => BuildRenkoPoints(),
                ChartSeriesKind.Range => BuildRangePoints(),
                ChartSeriesKind.LineBreak => BuildLineBreakPoints(),
                ChartSeriesKind.Kagi => BuildKagiPoints(),
                ChartSeriesKind.PointFigure => BuildPointFigurePoints(),
                _ => BuildRawPoints()
            };
        }

        private List<FinancialPoint> BuildRawPoints()
        {
            var points = new List<FinancialPoint>(_candles.Count);
            for (var i = 0; i < _candles.Count; i++)
            {
                var candle = _candles[i];
                points.Add(new FinancialPoint(
                    candle.Timestamp.ToString("HH:mm"),
                    candle.Open,
                    candle.High,
                    candle.Low,
                    candle.Close));
            }

            return points;
        }

        private List<FinancialPoint> BuildHeikinAshiPoints()
        {
            var points = new List<FinancialPoint>(_candles.Count);
            if (_candles.Count == 0)
            {
                return points;
            }

            var previousOpen = (_candles[0].Open + _candles[0].Close) / 2d;
            var previousClose = (_candles[0].Open + _candles[0].High + _candles[0].Low + _candles[0].Close) / 4d;

            for (var i = 0; i < _candles.Count; i++)
            {
                var candle = _candles[i];
                var close = (candle.Open + candle.High + candle.Low + candle.Close) / 4d;
                var open = i == 0 ? previousOpen : (previousOpen + previousClose) / 2d;
                var high = Math.Max(candle.High, Math.Max(open, close));
                var low = Math.Min(candle.Low, Math.Min(open, close));

                points.Add(new FinancialPoint(
                    candle.Timestamp.ToString("HH:mm"),
                    Math.Round(open, 2),
                    Math.Round(high, 2),
                    Math.Round(low, 2),
                    Math.Round(close, 2)));

                previousOpen = open;
                previousClose = close;
            }

            return points;
        }

        private List<FinancialPoint> BuildRenkoPoints()
        {
            var points = new List<FinancialPoint>();
            if (_candles.Count == 0)
            {
                return points;
            }

            var brickSize = Math.Max(0.01d, BrickSize);
            var brickClose = _candles[0].Close;

            for (var i = 1; i < _candles.Count; i++)
            {
                var candle = _candles[i];
                while (candle.Close >= brickClose + brickSize)
                {
                    var open = brickClose;
                    brickClose += brickSize;
                    points.Add(CreateBoxPoint(candle.Timestamp, open, brickClose));
                }

                while (candle.Close <= brickClose - brickSize)
                {
                    var open = brickClose;
                    brickClose -= brickSize;
                    points.Add(CreateBoxPoint(candle.Timestamp, open, brickClose));
                }
            }

            if (points.Count == 0)
            {
                points.Add(CreateBoxPoint(_candles[^1].Timestamp, _candles[0].Open, _candles[^1].Close));
            }

            return points;
        }

        private List<FinancialPoint> BuildLineBreakPoints()
        {
            var points = new List<FinancialPoint>();
            if (_candles.Count == 0)
            {
                return points;
            }

            var period = Math.Max(2, LineBreakPeriod);
            points.Add(CreateBoxPoint(_candles[0].Timestamp, _candles[0].Open, _candles[0].Close));

            for (var i = 1; i < _candles.Count; i++)
            {
                var candle = _candles[i];
                var price = candle.Close;
                var last = points[^1];
                var lastDirection = Math.Sign(last.Close - last.Open);
                if (lastDirection == 0)
                {
                    lastDirection = price >= last.Close ? 1 : -1;
                }

                var comparisonCount = Math.Min(period, points.Count);
                var highThreshold = double.MinValue;
                var lowThreshold = double.MaxValue;
                for (var offset = 0; offset < comparisonCount; offset++)
                {
                    var point = points[points.Count - 1 - offset];
                    highThreshold = Math.Max(highThreshold, Math.Max(point.Open, point.Close));
                    lowThreshold = Math.Min(lowThreshold, Math.Min(point.Open, point.Close));
                }

                var shouldAddUp = lastDirection >= 0
                    ? price > Math.Max(last.Open, last.Close)
                    : price > highThreshold;
                var shouldAddDown = lastDirection <= 0
                    ? price < Math.Min(last.Open, last.Close)
                    : price < lowThreshold;

                if (!shouldAddUp && !shouldAddDown)
                {
                    continue;
                }

                points.Add(CreateBoxPoint(candle.Timestamp, last.Close, price));
            }

            return points;
        }

        private List<FinancialPoint> BuildRangePoints()
        {
            var points = new List<FinancialPoint>();
            if (_candles.Count == 0)
            {
                return points;
            }

            var rangeSize = Math.Max(0.1d, RangeSize);
            var anchorClose = _candles[0].Close;

            for (var i = 1; i < _candles.Count; i++)
            {
                var candle = _candles[i];
                while (candle.Close >= anchorClose + rangeSize)
                {
                    var nextClose = anchorClose + rangeSize;
                    var isTerminalStep = candle.Close < nextClose + rangeSize;
                    var high = isTerminalStep ? Math.Max(candle.High, nextClose) : nextClose;
                    var low = Math.Min(anchorClose, nextClose);
                    points.Add(CreateCandlePoint(candle.Timestamp, anchorClose, high, low, nextClose));
                    anchorClose = nextClose;
                }

                while (candle.Close <= anchorClose - rangeSize)
                {
                    var nextClose = anchorClose - rangeSize;
                    var isTerminalStep = candle.Close > nextClose - rangeSize;
                    var high = Math.Max(anchorClose, nextClose);
                    var low = isTerminalStep ? Math.Min(candle.Low, nextClose) : nextClose;
                    points.Add(CreateCandlePoint(candle.Timestamp, anchorClose, high, low, nextClose));
                    anchorClose = nextClose;
                }
            }

            if (points.Count == 0)
            {
                points.Add(CreateCandlePoint(
                    _candles[^1].Timestamp,
                    _candles[0].Open,
                    Math.Max(_candles[0].Open, _candles[^1].Close),
                    Math.Min(_candles[0].Open, _candles[^1].Close),
                    _candles[^1].Close));
            }

            return points;
        }

        private List<FinancialPoint> BuildKagiPoints()
        {
            var points = new List<FinancialPoint>();
            if (_candles.Count == 0)
            {
                return points;
            }

            var reversal = Math.Max(0.01d, KagiReversalAmount);
            var segmentOpen = _candles[0].Close;
            var segmentClose = segmentOpen;
            var segmentTimestamp = _candles[0].Timestamp;
            var direction = 0;

            for (var i = 1; i < _candles.Count; i++)
            {
                var candle = _candles[i];
                var price = candle.Close;

                if (direction == 0)
                {
                    if (price >= segmentOpen + reversal)
                    {
                        segmentClose = price;
                        segmentTimestamp = candle.Timestamp;
                        direction = 1;
                        continue;
                    }

                    if (price <= segmentOpen - reversal)
                    {
                        segmentClose = price;
                        segmentTimestamp = candle.Timestamp;
                        direction = -1;
                    }

                    continue;
                }

                if (direction > 0)
                {
                    if (price >= segmentClose)
                    {
                        segmentClose = price;
                        segmentTimestamp = candle.Timestamp;
                        continue;
                    }

                    if (segmentClose - price < reversal)
                    {
                        continue;
                    }
                }
                else
                {
                    if (price <= segmentClose)
                    {
                        segmentClose = price;
                        segmentTimestamp = candle.Timestamp;
                        continue;
                    }

                    if (price - segmentClose < reversal)
                    {
                        continue;
                    }
                }

                if (Math.Abs(segmentClose - segmentOpen) >= 0.0001d)
                {
                    points.Add(CreateSegmentPoint(segmentTimestamp, segmentOpen, segmentClose));
                }

                segmentOpen = segmentClose;
                segmentClose = price;
                segmentTimestamp = candle.Timestamp;
                direction = direction > 0 ? -1 : 1;
            }

            if (direction == 0)
            {
                segmentClose = _candles[^1].Close;
                segmentTimestamp = _candles[^1].Timestamp;
            }

            if (points.Count == 0 || Math.Abs(segmentClose - segmentOpen) >= 0.0001d)
            {
                points.Add(CreateSegmentPoint(segmentTimestamp, segmentOpen, segmentClose));
            }

            return points;
        }

        private List<FinancialPoint> BuildPointFigurePoints()
        {
            var points = new List<FinancialPoint>();
            if (_candles.Count == 0)
            {
                return points;
            }

            var boxSize = Math.Max(0.1d, PointFigureBoxSize);
            var reversalBoxes = Math.Max(1, PointFigureReversalBoxes);
            var anchor = NormalizeToBox(_candles[0].Close, boxSize);
            var columnHigh = anchor;
            var columnLow = anchor;
            var columnTimestamp = _candles[0].Timestamp;
            var direction = 0;

            for (var i = 1; i < _candles.Count; i++)
            {
                var candle = _candles[i];
                var price = NormalizeToBox(candle.Close, boxSize);

                if (direction == 0)
                {
                    if (price >= columnHigh + boxSize)
                    {
                        direction = 1;
                        while (price >= columnHigh + boxSize)
                        {
                            columnHigh += boxSize;
                        }

                        columnTimestamp = candle.Timestamp;
                    }
                    else if (price <= columnLow - boxSize)
                    {
                        direction = -1;
                        while (price <= columnLow - boxSize)
                        {
                            columnLow -= boxSize;
                        }

                        columnTimestamp = candle.Timestamp;
                    }

                    continue;
                }

                if (direction > 0)
                {
                    if (price >= columnHigh + boxSize)
                    {
                        while (price >= columnHigh + boxSize)
                        {
                            columnHigh += boxSize;
                        }

                        columnTimestamp = candle.Timestamp;
                        continue;
                    }

                    if (price > columnHigh - (reversalBoxes * boxSize))
                    {
                        continue;
                    }

                    points.Add(CreatePointFigureColumnPoint(columnTimestamp, columnLow, columnHigh, isBullish: true, boxSize));
                    direction = -1;
                    columnLow = columnHigh - boxSize;
                    while (price <= columnLow - boxSize)
                    {
                        columnLow -= boxSize;
                    }

                    columnTimestamp = candle.Timestamp;
                }
                else
                {
                    if (price <= columnLow - boxSize)
                    {
                        while (price <= columnLow - boxSize)
                        {
                            columnLow -= boxSize;
                        }

                        columnTimestamp = candle.Timestamp;
                        continue;
                    }

                    if (price < columnLow + (reversalBoxes * boxSize))
                    {
                        continue;
                    }

                    points.Add(CreatePointFigureColumnPoint(columnTimestamp, columnLow, columnHigh, isBullish: false, boxSize));
                    direction = 1;
                    columnHigh = columnLow + boxSize;
                    while (price >= columnHigh + boxSize)
                    {
                        columnHigh += boxSize;
                    }

                    columnTimestamp = candle.Timestamp;
                }
            }

            if (direction == 0)
            {
                points.Add(CreateSegmentPoint(_candles[^1].Timestamp, _candles[0].Close, _candles[^1].Close));
                return points;
            }

            points.Add(CreatePointFigureColumnPoint(columnTimestamp, columnLow, columnHigh, direction > 0, boxSize));
            return points;
        }

        private static FinancialPoint CreateBoxPoint(DateTime timestamp, double open, double close)
        {
            var high = Math.Max(open, close);
            var low = Math.Min(open, close);
            return new FinancialPoint(
                timestamp.ToString("HH:mm"),
                Math.Round(open, 2),
                Math.Round(high, 2),
                Math.Round(low, 2),
                Math.Round(close, 2));
        }

        private static FinancialPoint CreateSegmentPoint(DateTime timestamp, double open, double close)
        {
            return new FinancialPoint(
                timestamp.ToString("HH:mm"),
                Math.Round(open, 2),
                Math.Round(Math.Max(open, close), 2),
                Math.Round(Math.Min(open, close), 2),
                Math.Round(close, 2));
        }

        private static FinancialPoint CreateCandlePoint(DateTime timestamp, double open, double high, double low, double close)
        {
            return new FinancialPoint(
                timestamp.ToString("HH:mm"),
                Math.Round(open, 2),
                Math.Round(Math.Max(high, Math.Max(open, close)), 2),
                Math.Round(Math.Min(low, Math.Min(open, close)), 2),
                Math.Round(close, 2));
        }

        private static FinancialPoint CreatePointFigureColumnPoint(DateTime timestamp, double low, double high, bool isBullish, double boxSize)
        {
            var boxCount = Math.Max(1, (int)Math.Round(Math.Abs(high - low) / boxSize) + 1);
            var open = isBullish ? low : high;
            var close = isBullish ? high : low;
            return new FinancialPoint(
                timestamp.ToString("HH:mm"),
                Math.Round(open, 2),
                Math.Round(high, 2),
                Math.Round(low, 2),
                Math.Round(close, 2),
                boxCount);
        }

        private static double NormalizeToBox(double price, double boxSize)
        {
            if (boxSize <= 0d)
            {
                return price;
            }

            return Math.Round(price / boxSize, MidpointRounding.AwayFromZero) * boxSize;
        }

        private static bool RequiresOpenValues(ChartSeriesKind kind)
        {
            return kind != ChartSeriesKind.Hlc;
        }

        private static bool HasSizeValues(ChartSeriesKind kind)
        {
            return kind == ChartSeriesKind.PointFigure;
        }

        private static (int Start, int Count) ResolveWindow(ChartDataRequest request, int totalCount)
        {
            if (totalCount <= 0)
            {
                return (0, 0);
            }

            var count = request.WindowCount ?? totalCount;
            if (count <= 0 || count > totalCount)
            {
                count = totalCount;
            }

            var start = request.WindowStart ?? Math.Max(0, totalCount - count);
            if (start < 0)
            {
                start = 0;
            }

            if (start + count > totalCount)
            {
                start = Math.Max(0, totalCount - count);
            }

            return (start, count);
        }
    }
}
