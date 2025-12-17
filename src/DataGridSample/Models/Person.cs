using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class Person : ObservableObject, INotifyDataErrorInfo
    {
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private bool _isBanned;
        private int _age;
        private PersonStatus _status;
        private PersonStatus? _optionalStatus;
        private Uri? _profileLink;

        public string FirstName
        {
            get => _firstName;
            set
            {
                _firstName = value;
                if (string.IsNullOrWhiteSpace(value))
                    SetError(nameof(FirstName), "First Name Required");
                else
                    SetError(nameof(FirstName), null);

                OnPropertyChanged(nameof(FirstName));
            }

        }

        public string LastName
        {
            get => _lastName;
            set
            {
                _lastName = value;
                if (string.IsNullOrWhiteSpace(value))
                    SetError(nameof(LastName), "Last Name Required");
                else
                    SetError(nameof(LastName), null);

                OnPropertyChanged(nameof(LastName));
            }
        }

        public bool IsBanned
        {
            get => _isBanned;
            set
            {
                _isBanned = value;

                OnPropertyChanged(nameof(_isBanned));
            }
        }

        
        /// <summary>
        ///    Gets or sets the age of the person
        /// </summary>
        public int Age
        {
            get => _age;
            set
            {
                _age = value;
                OnPropertyChanged(nameof(Age));
            }
        }

        public PersonStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value, nameof(Status));
        }

        public PersonStatus? OptionalStatus
        {
            get => _optionalStatus;
            set => SetProperty(ref _optionalStatus, value, nameof(OptionalStatus));
        }

        public Uri? ProfileLink
        {
            get => _profileLink;
            set => SetProperty(ref _profileLink, value, nameof(ProfileLink));
        }

        private Dictionary<string, List<string>> _errorLookup = new Dictionary<string, List<string>>();

        private void SetError(string propertyName, string? error)
        {
            if (string.IsNullOrEmpty(error))
            {
                if (_errorLookup.Remove(propertyName))
                    OnErrorsChanged(propertyName);
            }
            else
            {
                if (_errorLookup.TryGetValue(propertyName, out var errorList))
                {
                    errorList.Clear();
                    errorList.Add(error!);
                }
                else
                {
                    var errors = new List<string> { error! };
                    _errorLookup.Add(propertyName, errors);
                }

                OnErrorsChanged(propertyName);
            }
        }

        public bool HasErrors => _errorLookup.Count > 0;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        public IEnumerable GetErrors(string? propertyName)
        {
            if (propertyName is { } && _errorLookup.TryGetValue(propertyName, out var errorList))
                return errorList;
            else
                return Array.Empty<object>();
        }
    }

    public enum PersonStatus
    {
        New,
        Active,
        Suspended,
        Disabled
    }
}
