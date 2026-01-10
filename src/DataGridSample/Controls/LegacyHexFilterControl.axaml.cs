using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;

namespace DataGridSample.Controls
{
    public partial class LegacyHexFilterControl : UserControl
    {
        private IDisposable? _textSubscription;
        private TextBox? _textBox;

        public LegacyHexFilterControl()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _textSubscription?.Dispose();
            _textBox = e.NameScope.Find<TextBox>("textBox");
            if (_textBox is not null)
            {
                _textSubscription = _textBox.GetObservable(TextBox.TextProperty)
                    .Subscribe(UpdateColumnFilter);
                UpdateColumnFilter(_textBox.Text);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _textSubscription?.Dispose();
            _textSubscription = null;
        }

        private void UpdateColumnFilter(string? text)
        {
            if (DataContext is not DataGridColumn column)
            {
                return;
            }

            column.FilterValue = text;
            var normalized = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            column.ContentFilter = normalized is null ? null : new HexContentFilter(normalized);
        }

        private sealed class HexContentFilter
        {
            private readonly string _filter;

            public HexContentFilter(string filter)
            {
                _filter = filter;
            }

            public bool IsMatch(object value)
            {
                if (value == null)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(_filter))
                {
                    return true;
                }

                var text = GetHexText(value);
                if (text == null)
                {
                    return false;
                }

                return text.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static string? GetHexText(object value)
            {
                if (value is IFormattable formattable)
                {
                    return $"0x{formattable.ToString("X8", CultureInfo.InvariantCulture)}";
                }

                return value.ToString();
            }
        }
    }
}
