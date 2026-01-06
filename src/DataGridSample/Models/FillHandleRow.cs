using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class FillHandleRow : ObservableObject
    {
        private string _label = string.Empty;
        private int _value1;
        private int _value2;
        private int _value3;
        private int _value4;

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public int Value1
        {
            get => _value1;
            set => SetProperty(ref _value1, value);
        }

        public int Value2
        {
            get => _value2;
            set => SetProperty(ref _value2, value);
        }

        public int Value3
        {
            get => _value3;
            set => SetProperty(ref _value3, value);
        }

        public int Value4
        {
            get => _value4;
            set => SetProperty(ref _value4, value);
        }
    }
}
