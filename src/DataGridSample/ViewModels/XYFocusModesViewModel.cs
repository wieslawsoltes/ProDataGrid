using System.Collections.ObjectModel;
using Avalonia.Input;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class XYFocusModesViewModel : ObservableObject
    {
        private bool _keyboardEnabled = true;
        private bool _gamepadEnabled;
        private bool _remoteEnabled;
        private XYFocusNavigationModes _navigationModes;

        public XYFocusModesViewModel()
        {
            Items = new ObservableCollection<Person>
            {
                new Person { FirstName = "Ada", LastName = "Lovelace", Age = 36, IsBanned = false },
                new Person { FirstName = "Alan", LastName = "Turing", Age = 41, IsBanned = false },
                new Person { FirstName = "Grace", LastName = "Hopper", Age = 85, IsBanned = false },
                new Person { FirstName = "Linus", LastName = "Torvalds", Age = 54, IsBanned = false },
                new Person { FirstName = "Margaret", LastName = "Hamilton", Age = 86, IsBanned = false },
                new Person { FirstName = "Tim", LastName = "Berners-Lee", Age = 68, IsBanned = false }
            };

            UpdateNavigationModes();
        }

        public ObservableCollection<Person> Items { get; }

        public bool KeyboardEnabled
        {
            get => _keyboardEnabled;
            set
            {
                if (SetProperty(ref _keyboardEnabled, value))
                {
                    UpdateNavigationModes();
                }
            }
        }

        public bool GamepadEnabled
        {
            get => _gamepadEnabled;
            set
            {
                if (SetProperty(ref _gamepadEnabled, value))
                {
                    UpdateNavigationModes();
                }
            }
        }

        public bool RemoteEnabled
        {
            get => _remoteEnabled;
            set
            {
                if (SetProperty(ref _remoteEnabled, value))
                {
                    UpdateNavigationModes();
                }
            }
        }

        public XYFocusNavigationModes NavigationModes
        {
            get => _navigationModes;
            private set
            {
                if (SetProperty(ref _navigationModes, value))
                {
                    OnPropertyChanged(nameof(ModesLabel));
                }
            }
        }

        public string ModesLabel => $"Current: {NavigationModes}";

        private void UpdateNavigationModes()
        {
            var modes = XYFocusNavigationModes.Disabled;
            if (_keyboardEnabled)
            {
                modes |= XYFocusNavigationModes.Keyboard;
            }

            if (_gamepadEnabled)
            {
                modes |= XYFocusNavigationModes.Gamepad;
            }

            if (_remoteEnabled)
            {
                modes |= XYFocusNavigationModes.Remote;
            }

            NavigationModes = modes;
        }
    }
}
