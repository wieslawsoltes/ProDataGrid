using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class PowerFxRow : ObservableObject, INotifyDataErrorInfo
    {
        private string _name = string.Empty;
        private double _quantity;
        private double _unitPrice;
        private double _discount;
        private double _tax;
        private string _formula = string.Empty;
        private string _resultText = string.Empty;
        private double? _resultNumber;
        private string? _errorMessage;
        private bool _hasError;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public double Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        public double UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value);
        }

        public double Discount
        {
            get => _discount;
            set => SetProperty(ref _discount, value);
        }

        public double Tax
        {
            get => _tax;
            set => SetProperty(ref _tax, value);
        }

        public string Formula
        {
            get => _formula;
            set => SetProperty(ref _formula, value);
        }

        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        public double? ResultNumber
        {
            get => _resultNumber;
            set => SetProperty(ref _resultNumber, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        private readonly Dictionary<string, List<string>> _errorLookup = new();

        public bool HasErrors => _errorLookup.Count > 0;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (propertyName is { } && _errorLookup.TryGetValue(propertyName, out var errorList))
            {
                return errorList;
            }

            return Array.Empty<object>();
        }

        public void SetError(string propertyName, string? error)
        {
            if (string.IsNullOrEmpty(error))
            {
                if (_errorLookup.Remove(propertyName))
                {
                    OnErrorsChanged(propertyName);
                }
            }
            else
            {
                if (_errorLookup.TryGetValue(propertyName, out var errors))
                {
                    errors.Clear();
                    errors.Add(error);
                }
                else
                {
                    _errorLookup[propertyName] = new List<string> { error };
                }

                OnErrorsChanged(propertyName);
            }
        }

        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }
}
