// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.ComponentModel;
using Avalonia;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridColumnChooserItem : INotifyPropertyChanged, IDisposable
    {
        private readonly DataGridColumn _column;
        private readonly DataGridColumnDefinition _definition;
        private bool _isVisible;
        private object _header;
        private bool _updating;

        public DataGridColumnChooserItem(DataGridColumn column)
        {
            _column = column ?? throw new ArgumentNullException(nameof(column));
            _definition = DataGridColumnMetadata.GetDefinition(column);
            _isVisible = column.IsVisible;
            _header = column.Header;
            _column.PropertyChanged += OnColumnPropertyChanged;
            if (_definition != null)
            {
                _definition.PropertyChanged += OnDefinitionPropertyChanged;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DataGridColumn Column => _column;

        public object Header
        {
            get => _header;
            private set
            {
                if (!Equals(_header, value))
                {
                    _header = value;
                    OnPropertyChanged(nameof(Header));
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value)
                {
                    return;
                }

                if (!_column.CanUserHide)
                {
                    return;
                }

                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
                UpdateVisibility(value);
            }
        }

        public bool CanUserHide => _column.CanUserHide;

        private void UpdateVisibility(bool isVisible)
        {
            if (_updating)
            {
                return;
            }

            try
            {
                _updating = true;
                if (_definition != null)
                {
                    _definition.IsVisible = isVisible;
                }
                else
                {
                    _column.IsVisible = isVisible;
                }
            }
            finally
            {
                _updating = false;
            }
        }

        private void OnColumnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataGridColumn.IsVisibleProperty)
            {
                var newValue = (bool)e.NewValue;
                if (_isVisible != newValue)
                {
                    _isVisible = newValue;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
            else if (e.Property == DataGridColumn.HeaderProperty)
            {
                Header = e.NewValue;
            }
        }

        private void OnDefinitionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(DataGridColumnDefinition.CanUserHide), StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(CanUserHide));
            }
            else if (string.Equals(e.PropertyName, nameof(DataGridColumnDefinition.IsVisible), StringComparison.Ordinal))
            {
                var resolvedVisible = _definition?.IsVisible ?? _column.IsVisible;
                if (_isVisible != resolvedVisible)
                {
                    _isVisible = resolvedVisible;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }

        public void Dispose()
        {
            _column.PropertyChanged -= OnColumnPropertyChanged;
            if (_definition != null)
            {
                _definition.PropertyChanged -= OnDefinitionPropertyChanged;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
