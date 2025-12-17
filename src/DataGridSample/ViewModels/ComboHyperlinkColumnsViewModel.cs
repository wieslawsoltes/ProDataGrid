using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class ComboHyperlinkColumnsViewModel : ObservableObject
    {
        private readonly Random _random = new();

        public ComboHyperlinkColumnsViewModel()
        {
            Statuses = new ReadOnlyCollection<string>(new[]
            {
                "Active",
                "Evaluating",
                "Paused",
                "Blocked"
            });

            Contacts = new ObservableCollection<Contact>
            {
                new("Alisha Khan", "Product lead", "Active", new Uri("https://example.com/app"), "Alpha portal"),
                new("Diego Morales", "Design systems", "Evaluating", new Uri("https://example.com/design"), "Design kit"),
                new("Ivy Chen", "Data integrations", "Paused", new Uri("https://example.com/ingest"), "Ingestion docs"),
                new("Maria Silva", "Customer success", "Blocked", new Uri("https://example.com/status"), "Status board")
            };

            ShuffleStatusesCommand = new RelayCommand(_ => ShuffleStatuses());
            SetAllActiveCommand = new RelayCommand(_ => SetAll("Active"));
            SwapFirstLinkCommand = new RelayCommand(_ => SwapFirstLink());
        }

        public ReadOnlyCollection<string> Statuses { get; }

        public ObservableCollection<Contact> Contacts { get; }

        public RelayCommand ShuffleStatusesCommand { get; }

        public RelayCommand SetAllActiveCommand { get; }

        public RelayCommand SwapFirstLinkCommand { get; }

        private void ShuffleStatuses()
        {
            if (Statuses.Count == 0)
            {
                return;
            }

            foreach (var contact in Contacts)
            {
                var next = _random.Next(Statuses.Count);
                contact.Status = Statuses[next];
            }
        }

        private void SetAll(string status)
        {
            foreach (var contact in Contacts)
            {
                contact.Status = status;
            }
        }

        private void SwapFirstLink()
        {
            if (Contacts.Count == 0)
            {
                return;
            }

            Contacts[0].Link = new Uri("https://example.com/releases");
            Contacts[0].LinkText = "Latest release notes";
        }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class Contact : ObservableObject
    {
        private string _name;
        private string _title;
        private string _status;
        private Uri? _link;
        private string _linkText;

        public Contact(string name, string title, string status, Uri link, string linkText)
        {
            _name = name;
            _title = title;
            _status = status;
            _link = link;
            _linkText = linkText;
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public Uri? Link
        {
            get => _link;
            set => SetProperty(ref _link, value);
        }

        public string LinkText
        {
            get => _linkText;
            set => SetProperty(ref _linkText, value);
        }
    }
}
