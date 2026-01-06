using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridFilling;
using DataGridSample.FillModels;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class FillHandleModelViewModel : ObservableObject
    {
        private bool _useCopyOnlyFill;
        private IDataGridFillModel _fillModel;

        public ObservableCollection<FillHandleRow> Rows { get; } = new();

        public bool UseCopyOnlyFill
        {
            get => _useCopyOnlyFill;
            set
            {
                if (SetProperty(ref _useCopyOnlyFill, value))
                {
                    FillModel = value ? new CopyOnlyFillModel() : new DataGridFillModel();
                }
            }
        }

        public IDataGridFillModel FillModel
        {
            get => _fillModel;
            private set => SetProperty(ref _fillModel, value);
        }

        public FillHandleModelViewModel()
        {
            _fillModel = new DataGridFillModel();

            Rows.Add(new FillHandleRow { Label = "Seed A", Value1 = 1, Value2 = 10, Value3 = 100, Value4 = 1000 });
            Rows.Add(new FillHandleRow { Label = "Seed B", Value1 = 2, Value2 = 20, Value3 = 200, Value4 = 2000 });
            Rows.Add(new FillHandleRow { Label = "Fill", Value1 = 0, Value2 = 0, Value3 = 0, Value4 = 0 });
            Rows.Add(new FillHandleRow { Label = "Fill", Value1 = 0, Value2 = 0, Value3 = 0, Value4 = 0 });
            Rows.Add(new FillHandleRow { Label = "Fill", Value1 = 0, Value2 = 0, Value3 = 0, Value4 = 0 });
        }
    }
}
