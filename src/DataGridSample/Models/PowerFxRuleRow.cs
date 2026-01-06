using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class PowerFxRuleRow : ObservableObject
    {
        private string _item = string.Empty;
        private double _units;
        private double _revenue;
        private double _cost;
        private double _target;
        private string _resultText = string.Empty;
        private double? _resultNumber;
        private bool _ruleHit;
        private bool _hasError;
        private string? _errorMessage;
        private string _status = string.Empty;

        public string Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }

        public double Units
        {
            get => _units;
            set => SetProperty(ref _units, value);
        }

        public double Revenue
        {
            get => _revenue;
            set => SetProperty(ref _revenue, value);
        }

        public double Cost
        {
            get => _cost;
            set => SetProperty(ref _cost, value);
        }

        public double Target
        {
            get => _target;
            set => SetProperty(ref _target, value);
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

        public bool RuleHit
        {
            get => _ruleHit;
            set => SetProperty(ref _ruleHit, value);
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

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }
}
