using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    /// <summary>
    /// Model for testing variable row heights in DataGrid smooth scrolling.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class VariableHeightItem : ObservableObject, IDataGridCellDrawOperationItemCache
    {
        private struct CellDrawCacheSlotEntry
        {
            public bool HasValue;
            public int CacheKey;
            public object? Value;
        }

        private int _id;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private int _lineCount;
        private double _rowHeight;
        private CellDrawCacheSlotEntry[]? _cellDrawCacheEntries;

        public int Id
        {
            get => _id;
            set
            {
                if (SetProperty(ref _id, value))
                {
                    ClearCellDrawCacheEntries();
                }
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value))
                {
                    ClearCellDrawCacheEntries();
                }
            }
        }

        /// <summary>
        /// Multi-line description that causes variable row height.
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    ClearCellDrawCacheEntries();
                }
            }
        }

        /// <summary>
        /// Number of lines in the description (for display purposes).
        /// </summary>
        public int LineCount
        {
            get => _lineCount;
            set
            {
                if (SetProperty(ref _lineCount, value))
                {
                    ClearCellDrawCacheEntries();
                }
            }
        }

        /// <summary>
        /// Expected row height based on line count (for debugging).
        /// </summary>
        public double ExpectedHeight
        {
            get => _rowHeight;
            set
            {
                if (SetProperty(ref _rowHeight, value))
                {
                    ClearCellDrawCacheEntries();
                }
            }
        }

        public bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value)
        {
            CellDrawCacheSlotEntry[]? entries = _cellDrawCacheEntries;
            if (entries is not null &&
                cacheSlot >= 0 &&
                cacheSlot < entries.Length)
            {
                CellDrawCacheSlotEntry entry = entries[cacheSlot];
                if (entry.HasValue &&
                    entry.CacheKey == cacheKey &&
                    entry.Value is not null)
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null!;
            return false;
        }

        public void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value)
        {
            if (cacheSlot < 0)
            {
                return;
            }

            CellDrawCacheSlotEntry[] entries = EnsureCellDrawCacheCapacity(cacheSlot + 1);
            entries[cacheSlot] = new CellDrawCacheSlotEntry
            {
                HasValue = true,
                CacheKey = cacheKey,
                Value = value
            };
        }

        private CellDrawCacheSlotEntry[] EnsureCellDrawCacheCapacity(int capacity)
        {
            CellDrawCacheSlotEntry[]? entries = _cellDrawCacheEntries;
            if (entries is null)
            {
                entries = new CellDrawCacheSlotEntry[Math.Max(1, capacity)];
                _cellDrawCacheEntries = entries;
                return entries;
            }

            if (entries.Length >= capacity)
            {
                return entries;
            }

            Array.Resize(ref entries, capacity);
            _cellDrawCacheEntries = entries;
            return entries;
        }

        private void ClearCellDrawCacheEntries()
        {
            CellDrawCacheSlotEntry[]? entries = _cellDrawCacheEntries;
            if (entries is null)
            {
                return;
            }

            Array.Clear(entries, 0, entries.Length);
        }

        /// <summary>
        /// Creates a collection of items with random variable heights.
        /// </summary>
        /// <param name="count">Number of items to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>Array of variable height items.</returns>
        public static VariableHeightItem[] GenerateItems(int count, int seed = 42)
        {
            var random = new Random(seed);
            var items = new VariableHeightItem[count];

            for (int i = 0; i < count; i++)
            {
                // Random line count between 1 and 10
                int lineCount = random.Next(1, 11);
                
                items[i] = new VariableHeightItem
                {
                    Id = i + 1,
                    Title = $"Item {i + 1}",
                    Description = GenerateMultiLineText(lineCount, i, random),
                    LineCount = lineCount,
                    ExpectedHeight = 20 + (lineCount * 16) // Approximate: base padding + lines * line height
                };
            }

            return items;
        }

        private static string GenerateMultiLineText(int lineCount, int itemIndex, Random random)
        {
            var lines = new string[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                int wordCount = random.Next(3, 12);
                lines[i] = $"Line {i + 1}: " + GenerateRandomWords(wordCount, random);
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static string GenerateRandomWords(int count, Random random)
        {
            string[] words = { "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", 
                "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", "incididunt",
                "labore", "dolore", "magna", "aliqua", "enim", "minim", "veniam" };
            
            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = words[random.Next(words.Length)];
            }
            return string.Join(" ", result);
        }
    }
}
