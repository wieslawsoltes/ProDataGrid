using Avalonia.Collections;
using DataGridSample.Models;

namespace DataGridSample.ViewModels
{
    public class StylingShowcaseViewModel
    {
        public StylingShowcaseViewModel()
        {
            Countries = new DataGridCollectionView(Models.Countries.All);
            Countries.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Country.Region)));
        }

        public DataGridCollectionView Countries { get; }
    }
}
