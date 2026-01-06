using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridClipboard;
using DataGridSample.ClipboardImportModels;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class ClipboardImportModelViewModel : ObservableObject
    {
        private bool _useUppercasePaste;
        private IDataGridClipboardImportModel _clipboardImportModel;

        public ObservableCollection<ClipboardSampleRow> Rows { get; } = new();

        public bool UseUppercasePaste
        {
            get => _useUppercasePaste;
            set
            {
                if (SetProperty(ref _useUppercasePaste, value))
                {
                    ClipboardImportModel = value ? new UppercaseClipboardImportModel() : new DataGridClipboardImportModel();
                }
            }
        }

        public IDataGridClipboardImportModel ClipboardImportModel
        {
            get => _clipboardImportModel;
            private set => SetProperty(ref _clipboardImportModel, value);
        }

        public ClipboardImportModelViewModel()
        {
            _clipboardImportModel = new DataGridClipboardImportModel();

            Rows.Add(new ClipboardSampleRow { A = "Alpha", B = "Bravo", C = "Charlie", D = "Delta" });
            Rows.Add(new ClipboardSampleRow { A = "Echo", B = "Foxtrot", C = "Golf", D = "Hotel" });
            Rows.Add(new ClipboardSampleRow { A = "India", B = "Juliet", C = "Kilo", D = "Lima" });
        }
    }
}
