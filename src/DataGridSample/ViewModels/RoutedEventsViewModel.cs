using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.DataGridHierarchical;

namespace DataGridSample.ViewModels;

public class RoutedEventsViewModel
{
    public RoutedEventsViewModel()
    {
        Items = new ObservableCollection<SampleItem>(
            Enumerable.Range(1, 40).Select(i =>
            {
                var groupIndex = i % 3;
                var group = groupIndex == 0 ? "Group C" : groupIndex == 1 ? "Group A" : "Group B";

                return new SampleItem
                {
                    Id = i,
                    Name = $"Item {i}",
                    Group = group,
                    Category = i % 2 == 0 ? "Even" : "Odd",
                    Status = i % 5 == 0 ? "Blocked" : "Active",
                    Description = $"Description for item {i}",
                    LongText = $"Detail text for item {i} that is long enough to force horizontal scrolling."
                };
            }));

        ItemsView = new DataGridCollectionView(Items);
        ItemsView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(SampleItem.Group)));

        HierarchicalModel = CreateHierarchicalModel();
    }

    public ObservableCollection<string> Logs { get; } = new();

    public ObservableCollection<SampleItem> Items { get; }

    public DataGridCollectionView ItemsView { get; }

    public HierarchicalModel<HierarchicalItem> HierarchicalModel { get; }

    public void AddLog(string message)
    {
        Logs.Add(message);
        if (Logs.Count > 200)
        {
            Logs.RemoveAt(0);
        }
    }

    private static HierarchicalModel<HierarchicalItem> CreateHierarchicalModel()
    {
        var model = new HierarchicalModel<HierarchicalItem>(new HierarchicalOptions<HierarchicalItem>
        {
            ItemsSelector = item => item.Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1,
            VirtualizeChildren = false,
            IsLeafSelector = item => item.Children.Count == 0
        });

        var root = new HierarchicalItem(
            "Solutions",
            "Folder",
            new ObservableCollection<HierarchicalItem>
            {
                new("Client", "Folder", new ObservableCollection<HierarchicalItem>
                {
                    new("UI", "Folder", new ObservableCollection<HierarchicalItem>
                    {
                        new("Controls", "Folder", new ObservableCollection<HierarchicalItem>
                        {
                            new("DataGrid", "Folder", new ObservableCollection<HierarchicalItem>
                            {
                                new("Columns", "Folder", new()),
                                new("Rows", "Folder", new()),
                                new("Docs.md", "File", new())
                            })
                        }),
                        new("Themes", "Folder", new())
                    }),
                    new("Tests", "Folder", new ObservableCollection<HierarchicalItem>
                    {
                        new("RoutedEventsTests.cs", "File", new())
                    })
                }),
                new("Server", "Folder", new ObservableCollection<HierarchicalItem>
                {
                    new("Api", "Folder", new()),
                    new("Workers", "Folder", new())
                })
            });

        model.SetRoot(root);
        return model;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class SampleItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Group { get; set; }
        public string? Category { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? LongText { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public record HierarchicalItem(string Name, string Kind, ObservableCollection<HierarchicalItem> Children);
}
