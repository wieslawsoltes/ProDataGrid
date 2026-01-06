using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class PowerFxSheetCell : ObservableObject
    {
        private string _input = string.Empty;
        private string _displayText = string.Empty;
        private double? _numericValue;
        private bool _hasError;
        private string? _errorMessage;
        private bool _isFormula;

        public string Input
        {
            get => _input;
            set => SetProperty(ref _input, value);
        }

        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }

        public double? NumericValue
        {
            get => _numericValue;
            set => SetProperty(ref _numericValue, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsFormula
        {
            get => _isFormula;
            set => SetProperty(ref _isFormula, value);
        }
    }
}
