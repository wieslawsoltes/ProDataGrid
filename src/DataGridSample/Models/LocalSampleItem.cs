using System.Collections.ObjectModel;

namespace DataGridSample.Models
{
    public class LocalSampleItem
    {
        public LocalSampleItem()
        {
            Details = new ObservableCollection<LocalSampleDetail>();
        }

        public int Id { get; set; }

        public string? Name { get; set; }

        public string? Info { get; set; }

        public ObservableCollection<LocalSampleDetail> Details { get; }

        public override string ToString() => $"{Id}: {Name} - {Info}";
    }
}
