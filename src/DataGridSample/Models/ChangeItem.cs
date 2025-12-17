using System.Diagnostics.CodeAnalysis;
using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class ChangeItem : ObservableObject
    {
        private int _id;
        private string _name = string.Empty;
        private int _value;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
