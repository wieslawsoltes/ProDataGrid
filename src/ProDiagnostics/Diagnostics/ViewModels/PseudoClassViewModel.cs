using Avalonia.Controls;
using System;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class PseudoClassViewModel : ViewModelBase
    {
        private readonly IPseudoClasses? _pseudoClasses;
        private readonly StyledElement? _source;
        private readonly Action<string, bool>? _setStateOverride;
        private bool _isActive;
        private bool _isUpdating;

        public PseudoClassViewModel(
            string name,
            StyledElement? source,
            Action<string, bool>? setStateOverride = null,
            bool initialState = false)
        {
            Name = name;
            _source = source;
            _pseudoClasses = _source?.Classes;
            _setStateOverride = setStateOverride;

            if (_source is null)
            {
                _isActive = initialState;
            }
            else
            {
                Update();
            }
        }

        public string Name { get; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                var changed = RaiseAndSetIfChanged(ref _isActive, value);

                if (_isUpdating || !changed)
                {
                    return;
                }

                try
                {
                    if (_setStateOverride is null)
                    {
                        _pseudoClasses?.Set(Name, value);
                    }
                    else
                    {
                        _setStateOverride(Name, value);
                    }
                }
                catch
                {
                    Update();
                }
            }
        }

        public void Update()
        {
            if (_source is null || _pseudoClasses is null)
            {
                return;
            }

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
