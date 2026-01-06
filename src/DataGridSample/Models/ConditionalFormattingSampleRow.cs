using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class ConditionalFormattingSampleRow : ObservableObject
    {
        private string _region = string.Empty;
        private double _score;
        private double _change;
        private string _status = string.Empty;
        private double _target;

        public string Region
        {
            get => _region;
            set => SetProperty(ref _region, value);
        }

        public double Score
        {
            get => _score;
            set => SetProperty(ref _score, value);
        }

        public double Change
        {
            get => _change;
            set => SetProperty(ref _change, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public double Target
        {
            get => _target;
            set => SetProperty(ref _target, value);
        }
    }
}
