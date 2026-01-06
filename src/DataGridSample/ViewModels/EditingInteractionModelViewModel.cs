using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridEditing;
using DataGridSample.EditingInteractionModels;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class EditingInteractionModelViewModel : ObservableObject
    {
        private bool _requireAltClick;
        private IDataGridEditingInteractionModel _editingInteractionModel;
        private DataGridEditTriggers _editTriggers;

        public ObservableCollection<SpreadsheetRow> Rows { get; } = new();

        public bool RequireAltClick
        {
            get => _requireAltClick;
            set
            {
                if (SetProperty(ref _requireAltClick, value))
                {
                    EditingInteractionModel = value ? new AltClickEditingInteractionModel() : new DataGridEditingInteractionModel();
                    EditTriggers = value
                        ? DataGridEditTriggers.CellClick
                        : DataGridEditTriggers.CellClick | DataGridEditTriggers.TextInput;
                }
            }
        }

        public IDataGridEditingInteractionModel EditingInteractionModel
        {
            get => _editingInteractionModel;
            private set => SetProperty(ref _editingInteractionModel, value);
        }

        public DataGridEditTriggers EditTriggers
        {
            get => _editTriggers;
            private set => SetProperty(ref _editTriggers, value);
        }

        public EditingInteractionModelViewModel()
        {
            _editingInteractionModel = new DataGridEditingInteractionModel();
            _editTriggers = DataGridEditTriggers.CellClick | DataGridEditTriggers.TextInput;

            Rows.Add(new SpreadsheetRow { Account = "North", Q1 = 12, Q2 = 14, Q3 = 16, Q4 = 18, Notes = "Draft" });
            Rows.Add(new SpreadsheetRow { Account = "South", Q1 = 20, Q2 = 22, Q3 = 19, Q4 = 24, Notes = "Review" });
            Rows.Add(new SpreadsheetRow { Account = "East", Q1 = 8, Q2 = 9, Q3 = 11, Q4 = 13, Notes = "Approved" });
            Rows.Add(new SpreadsheetRow { Account = "West", Q1 = 15, Q2 = 17, Q3 = 18, Q4 = 21, Notes = "Draft" });
        }
    }
}
