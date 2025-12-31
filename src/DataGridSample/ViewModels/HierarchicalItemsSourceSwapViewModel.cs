using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalItemsSourceSwapViewModel : ObservableObject
    {
        private IEnumerable? _itemsSource;
        private HierarchicalModel<TreeNode>? _model;
        private string _status = string.Empty;
        private string _activeModel = string.Empty;

        public HierarchicalItemsSourceSwapViewModel()
        {
            ModelA = CreateModel("Alpha");
            ModelB = CreateModel("Beta");

            SelectionModel = new SelectionModel<HierarchicalNode> { SingleSelect = true };
            SelectionModel.SelectionChanged += (_, _) => UpdateStatus();
            SelectionModel.PropertyChanged += SelectionModelOnPropertyChanged;

            UseModelACommand = new RelayCommand(_ => UseModel(ModelA, "Model A"));
            UseModelBCommand = new RelayCommand(_ => UseModel(ModelB, "Model B"));
            ResetCommand = new RelayCommand(_ => ResetModels());

            UseModel(ModelA, "Model A");
        }

        public HierarchicalModel<TreeNode> ModelA { get; private set; }
        public HierarchicalModel<TreeNode> ModelB { get; private set; }

        public HierarchicalModel<TreeNode>? Model
        {
            get => _model;
            private set => SetProperty(ref _model, value);
        }

        public IEnumerable? ItemsSource
        {
            get => _itemsSource;
            private set => SetProperty(ref _itemsSource, value);
        }

        public SelectionModel<HierarchicalNode> SelectionModel { get; }

        public string ActiveModel
        {
            get => _activeModel;
            private set => SetProperty(ref _activeModel, value);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public RelayCommand UseModelACommand { get; }
        public RelayCommand UseModelBCommand { get; }
        public RelayCommand ResetCommand { get; }

        private void UseModel(HierarchicalModel<TreeNode> model, string label)
        {
            Model = model;
            ItemsSource = model.ObservableFlattened;
            ActiveModel = label;
            UpdateStatus();
        }

        private void ResetModels()
        {
            ModelA = CreateModel("Alpha");
            ModelB = CreateModel("Beta");

            OnPropertyChanged(nameof(ModelA));
            OnPropertyChanged(nameof(ModelB));

            UseModel(ModelA, "Model A");
        }

        private void SelectionModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectionModel<HierarchicalNode>.Source) ||
                e.PropertyName == nameof(SelectionModel<HierarchicalNode>.SelectedIndex) ||
                e.PropertyName == nameof(SelectionModel<HierarchicalNode>.SelectedItem))
            {
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            Status = $"Active: {ActiveModel} | ItemsSource: {DescribeSource(ItemsSource)} | SelectionModel.Source: {DescribeSource(SelectionModel.Source)}";
        }

        private static string DescribeSource(IEnumerable? source)
        {
            if (source == null)
            {
                return "null";
            }

            var count = source is ICollection collection ? collection.Count : -1;
            var typeName = source.GetType().Name;
            return count >= 0 ? $"{typeName} ({count})" : typeName;
        }

        private static HierarchicalModel<TreeNode> CreateModel(string prefix)
        {
            var root = new TreeNode($"{prefix} root");
            root.Children.Add(new TreeNode($"{prefix} child 1"));
            root.Children.Add(new TreeNode($"{prefix} child 2"));
            root.Children[1].Children.Add(new TreeNode($"{prefix} leaf 1"));
            root.Children[1].Children.Add(new TreeNode($"{prefix} leaf 2"));

            var options = new HierarchicalOptions<TreeNode>
            {
                ChildrenSelector = node => node.Children
            };

            var model = new HierarchicalModel<TreeNode>(options);
            model.SetRoot(root);
            return model;
        }

        public class TreeNode
        {
            public TreeNode(string name)
            {
                Name = name;
                Children = new ObservableCollection<TreeNode>();
            }

            public string Name { get; }
            public ObservableCollection<TreeNode> Children { get; }
        }
    }
}
