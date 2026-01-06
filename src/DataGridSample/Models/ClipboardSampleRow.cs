using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class ClipboardSampleRow : ObservableObject
    {
        private string _a = string.Empty;
        private string _b = string.Empty;
        private string _c = string.Empty;
        private string _d = string.Empty;

        public string A
        {
            get => _a;
            set => SetProperty(ref _a, value);
        }

        public string B
        {
            get => _b;
            set => SetProperty(ref _b, value);
        }

        public string C
        {
            get => _c;
            set => SetProperty(ref _c, value);
        }

        public string D
        {
            get => _d;
            set => SetProperty(ref _d, value);
        }
    }
}
