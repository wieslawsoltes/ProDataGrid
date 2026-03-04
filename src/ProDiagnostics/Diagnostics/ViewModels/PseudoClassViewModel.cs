using Avalonia.Controls;
using System;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class PseudoClassViewModel : ViewModelBase
    {
        private readonly IPseudoClasses _pseudoClasses;
        private readonly StyledElement _source;
        private readonly Action<string, bool>? _setStateOverride;
        private bool _isActive;
        private bool _isUpdating;

        public PseudoClassViewModel(string name, StyledElement source, Action<string, bool>? setStateOverride = null)
        {
            Name = name;
            _source = source;
            _pseudoClasses = _source.Classes;
            _setStateOverride = setStateOverride;

            Update();
        }

        public string Name { get; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                RaiseAndSetIfChanged(ref _isActive, value);

                if (!_isUpdating)
                {
                    if (_setStateOverride is null)
                    {
                        _pseudoClasses.Set(Name, value);
                    }
                    else
                    {
                        _setStateOverride(Name, value);
                    }
                }
            }
        }

        public void Update()
        {
            try
            {
                _isUpdating = true;

                IsActive = _source.Classes.Contains(Name);
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
