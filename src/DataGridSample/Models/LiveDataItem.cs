using System;
using System.Diagnostics.CodeAnalysis;
using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    /// <summary>
    /// Represents a live-updating row used to exercise scrolling, snap points, and row details.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class LiveDataItem : ObservableObject
    {
        private string _summary = string.Empty;
        private string _details = string.Empty;
        private string _source = string.Empty;
        private string _severity = "Info";
        private string _accent = "#4CAF50";
        private DateTime _timestamp = DateTime.Now;
        private bool _isPinned;

        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public string Details
        {
            get => _details;
            set => SetProperty(ref _details, value);
        }

        public string Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }

        public string Severity
        {
            get => _severity;
            set => SetProperty(ref _severity, value);
        }

        public string Accent
        {
            get => _accent;
            set => SetProperty(ref _accent, value);
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }
    }
}
