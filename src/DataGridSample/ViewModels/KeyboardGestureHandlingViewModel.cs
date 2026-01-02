using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class KeyboardGestureHandlingViewModel : ObservableObject
    {
        public KeyboardGestureHandlingViewModel()
        {
            People = new ObservableCollection<Person>
            {
                new Person { FirstName = "Ada", LastName = "Lovelace", Age = 36, IsBanned = false },
                new Person { FirstName = "Alan", LastName = "Turing", Age = 41, IsBanned = false },
                new Person { FirstName = "Grace", LastName = "Hopper", Age = 85, IsBanned = false },
                new Person { FirstName = "Linus", LastName = "Torvalds", Age = 54, IsBanned = false },
                new Person { FirstName = "Margaret", LastName = "Hamilton", Age = 86, IsBanned = false },
                new Person { FirstName = "Tim", LastName = "Berners-Lee", Age = 68, IsBanned = false }
            };

            _lastKey = "None";
            _useVimNavigation = false;
            _handleDirectionalKeys = false;
            UpdateGestureOverrides();
        }

        public ObservableCollection<Person> People { get; }

        private bool _useVimNavigation;

        public bool UseVimNavigation
        {
            get => _useVimNavigation;
            set
            {
                if (SetProperty(ref _useVimNavigation, value))
                {
                    UpdateGestureOverrides();
                }
            }
        }

        private DataGridKeyboardGestures? _keyboardGestureOverrides;

        public DataGridKeyboardGestures? KeyboardGestureOverrides
        {
            get => _keyboardGestureOverrides;
            set => SetProperty(ref _keyboardGestureOverrides, value);
        }

        private string _lastKey;

        public string LastKey
        {
            get => _lastKey;
            set => SetProperty(ref _lastKey, value);
        }

        private bool _handleDirectionalKeys;

        public bool HandleDirectionalKeys
        {
            get => _handleDirectionalKeys;
            set => SetProperty(ref _handleDirectionalKeys, value);
        }

        private void UpdateGestureOverrides()
        {
            if (_useVimNavigation)
            {
                KeyboardGestureOverrides = new DataGridKeyboardGestures
                {
                    MoveDown = new KeyGesture(Key.J),
                    MoveUp = new KeyGesture(Key.K)
                };
            }
            else
            {
                KeyboardGestureOverrides = null;
            }
        }
    }
}
