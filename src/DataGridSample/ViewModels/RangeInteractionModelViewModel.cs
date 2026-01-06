using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridInteractions;
using DataGridSample.InteractionModels;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class RangeInteractionModelViewModel : ObservableObject
    {
        private bool _useTopLeftAnchor;
        private IDataGridRangeInteractionModel _rangeInteractionModel;

        public ObservableCollection<FillHandleRow> Rows { get; } = new();

        public bool UseTopLeftAnchor
        {
            get => _useTopLeftAnchor;
            set
            {
                if (SetProperty(ref _useTopLeftAnchor, value))
                {
                    RangeInteractionModel = value ? new TopLeftRangeInteractionModel() : new DataGridRangeInteractionModel();
                }
            }
        }

        public IDataGridRangeInteractionModel RangeInteractionModel
        {
            get => _rangeInteractionModel;
            private set => SetProperty(ref _rangeInteractionModel, value);
        }

        public RangeInteractionModelViewModel()
        {
            _rangeInteractionModel = new DataGridRangeInteractionModel();

            Rows.Add(new FillHandleRow { Label = "Seed A", Value1 = 10, Value2 = 20, Value3 = 30, Value4 = 40 });
            Rows.Add(new FillHandleRow { Label = "Seed B", Value1 = 11, Value2 = 21, Value3 = 31, Value4 = 41 });
            Rows.Add(new FillHandleRow { Label = "Data", Value1 = 0, Value2 = 0, Value3 = 0, Value4 = 0 });
            Rows.Add(new FillHandleRow { Label = "Data", Value1 = 0, Value2 = 0, Value3 = 0, Value4 = 0 });
            Rows.Add(new FillHandleRow { Label = "Data", Value1 = 0, Value2 = 0, Value3 = 0, Value4 = 0 });
        }
    }
}
