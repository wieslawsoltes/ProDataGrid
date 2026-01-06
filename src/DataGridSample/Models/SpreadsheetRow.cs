using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class SpreadsheetRow : ObservableObject
    {
        private string _account = string.Empty;
        private double _q1;
        private double _q2;
        private double _q3;
        private double _q4;
        private string _notes = string.Empty;

        public string Account
        {
            get => _account;
            set => SetProperty(ref _account, value);
        }

        public double Q1
        {
            get => _q1;
            set
            {
                if (SetProperty(ref _q1, value))
                {
                    NotifyTotalsChanged();
                }
            }
        }

        public double Q2
        {
            get => _q2;
            set
            {
                if (SetProperty(ref _q2, value))
                {
                    NotifyTotalsChanged();
                }
            }
        }

        public double Q3
        {
            get => _q3;
            set
            {
                if (SetProperty(ref _q3, value))
                {
                    NotifyTotalsChanged();
                }
            }
        }

        public double Q4
        {
            get => _q4;
            set
            {
                if (SetProperty(ref _q4, value))
                {
                    NotifyTotalsChanged();
                }
            }
        }

        public double Delta => Q4 - Q1;

        public double Total => Q1 + Q2 + Q3 + Q4;

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private void NotifyTotalsChanged()
        {
            OnPropertyChanged(nameof(Delta));
            OnPropertyChanged(nameof(Total));
        }
    }
}
