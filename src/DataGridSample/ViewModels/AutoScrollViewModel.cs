using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Input;
using Avalonia;

namespace DataGridSample.ViewModels
{
    public class AutoScrollViewModel : AvaloniaObject
    {
        public AutoScrollViewModel()
        {
            Items = new ObservableCollection<AutoItem>(
                Enumerable.Range(1, 500).Select(i => new AutoItem
                {
                    Index = i,
                    Name = $"Item {i}",
                    Description = $"This is item number {i}. Use the commands to select and watch the grid auto-scroll."
                }));

            SelectLastCommand = new RelayCommand(_ => SelectedItem = Items.LastOrDefault());
            SelectFirstCommand = new RelayCommand(_ => SelectedItem = Items.FirstOrDefault());
            SelectRandomCommand = new RelayCommand(_ =>
            {
                if (Items.Count == 0)
                {
                    return;
                }

                var index = Random.Shared.Next(0, Items.Count);
                SelectedItem = Items[index];
            });
        }

        public ObservableCollection<AutoItem> Items { get; }

        public AutoItem? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly StyledProperty<AutoItem?> SelectedItemProperty =
            AvaloniaProperty.Register<AutoScrollViewModel, AutoItem?>(nameof(SelectedItem));

        public string Status
        {
            get => GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public static readonly StyledProperty<string> StatusProperty =
            AvaloniaProperty.Register<AutoScrollViewModel, string>(nameof(Status), "No selection");

        public ICommand SelectLastCommand { get; }
        public ICommand SelectFirstCommand { get; }
        public ICommand SelectRandomCommand { get; }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedItemProperty)
            {
                var item = change.GetNewValue<AutoItem?>();
                SetValue(StatusProperty, item is null ? "No selection" : $"Selected: {item.Name}");
            }
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
        public class AutoItem
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            public RelayCommand(Action<object?> execute) => _execute = execute;
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _execute(parameter);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}
