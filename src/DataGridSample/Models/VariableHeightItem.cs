using System;
using System.ComponentModel;

namespace DataGridSample.Models
{
    /// <summary>
    /// Model for testing variable row heights in DataGrid smooth scrolling.
    /// </summary>
    public class VariableHeightItem : INotifyPropertyChanged
    {
        private int _id;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private int _lineCount;
        private double _rowHeight;

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
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
                _description = value;
                OnPropertyChanged(nameof(Description));
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
                _lineCount = value;
                OnPropertyChanged(nameof(LineCount));
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
                _rowHeight = value;
                OnPropertyChanged(nameof(ExpectedHeight));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
