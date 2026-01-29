using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Diagnostics.Services;
using Avalonia.Reactive;
using Avalonia.Styling;

namespace Avalonia.Diagnostics.ViewModels
{
    internal sealed class ResourcesPageViewModel : ViewModelBase, IDisposable
    {
        private readonly IResourceNodeFormatter _formatter;
        private readonly AvaloniaList<ResourceEntryViewModel> _resourceEntries = new();
        private readonly IHierarchicalModel _hierarchicalModel;
        private readonly DataGridCollectionView _resourcesView;
        private IDisposable? _resourcesSubscription;
        private ResourceTreeNode? _selectedNode;
        private ResourceEntryViewModel? _selectedResource;
        private ResourceDetailsViewModel? _details;
        private int _resourceCount;
        private string _selectedScopePath = string.Empty;
        private bool _includeNested = true;
        private ResourceSortMode _sortMode = ResourceSortMode.Key;
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        public ResourcesPageViewModel(
            MainViewModel mainView,
            ResourceTreeNode[] nodes,
            IResourceHierarchyModelFactory modelFactory,
            IResourceNodeFormatter formatter)
        {
            MainView = mainView;
            Nodes = nodes;
            _hierarchicalModel = modelFactory.Create(nodes);
            _formatter = formatter;
            ResourcesFilter = new FilterViewModel();
            ResourcesFilter.RefreshFilter += (_, _) =>
            {
                _resourcesView.Refresh();
                UpdateResourceCount();
            };
            _resourcesView = new DataGridCollectionView(_resourceEntries)
            {
                Filter = FilterResource
            };

            SortModes = new[] { ResourceSortMode.Key, ResourceSortMode.Type };
            SortDirections = new[] { ListSortDirection.Ascending, ListSortDirection.Descending };
            ApplySort();
        }

        public MainViewModel MainView { get; }

        public ResourceTreeNode[] Nodes { get; }

        public IHierarchicalModel HierarchicalModel => _hierarchicalModel;

        public DataGridCollectionView ResourcesView => _resourcesView;

        public FilterViewModel ResourcesFilter { get; }

        public IReadOnlyList<ResourceSortMode> SortModes { get; }

        public IReadOnlyList<ListSortDirection> SortDirections { get; }

        public int ResourceCount
        {
            get => _resourceCount;
            private set => RaiseAndSetIfChanged(ref _resourceCount, value);
        }

        public string SelectedScopePath
        {
            get => _selectedScopePath;
            private set => RaiseAndSetIfChanged(ref _selectedScopePath, value);
        }

        public bool HasSelectedNode => _selectedNode != null;

        public bool ShowNoSelectionMessage => _selectedNode == null;

        public bool ShowNoResourcesMessage => _selectedNode != null && ResourceCount == 0;

        public ResourceTreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (RaiseAndSetIfChanged(ref _selectedNode, value))
                {
                    if (value != null)
                    {
                        ExpandNode(value.Parent);
                    }

                    SelectedScopePath = value != null ? BuildScopePath(value) : string.Empty;
                    Details = value != null
                        ? new ResourceDetailsViewModel(value, MainView.ShowImplementedInterfaces, showProperties: false)
                        : null;
                    SelectedResource = null;
                    RefreshResources();
                    SubscribeToResourcesChanged(value);
                    RaisePropertyChanged(nameof(HasSelectedNode));
                    RaisePropertyChanged(nameof(ShowNoSelectionMessage));
                    RaisePropertyChanged(nameof(ShowNoResourcesMessage));
                }
            }
        }

        public ResourceDetailsViewModel? Details
        {
            get => _details;
            private set
            {
                var oldValue = _details;

                if (RaiseAndSetIfChanged(ref _details, value))
                {
                    oldValue?.Dispose();
                }
            }
        }

        public ResourceEntryViewModel? SelectedResource
        {
            get => _selectedResource;
            set => RaiseAndSetIfChanged(ref _selectedResource, value);
        }

        public bool IncludeNested
        {
            get => _includeNested;
            set
            {
                if (RaiseAndSetIfChanged(ref _includeNested, value))
                {
                    RefreshResources();
                }
            }
        }

        public ResourceSortMode SortMode
        {
            get => _sortMode;
            set
            {
                if (RaiseAndSetIfChanged(ref _sortMode, value))
                {
                    ApplySort();
                }
            }
        }

        public ListSortDirection SortDirection
        {
            get => _sortDirection;
            set
            {
                if (RaiseAndSetIfChanged(ref _sortDirection, value))
                {
                    ApplySort();
                }
            }
        }

        public void Dispose()
        {
            _resourcesSubscription?.Dispose();
            _details?.Dispose();

            foreach (var node in Nodes)
            {
                node.Dispose();
            }
        }

        public void SelectResourceHost(Control control)
        {
            var node = default(ResourceHostNode);
            IStyleHost? current = control;

            while (node == null && current != null)
            {
                if (current is IResourceHost host)
                {
                    node = FindHostNode(host);
                }

                current = current.StylingParent;
            }

            if (node != null)
            {
                SelectedNode = node;
            }
        }

        private void RefreshResources()
        {
            _resourceEntries.Clear();

            if (_selectedNode == null)
            {
                _resourcesView.Refresh();
                UpdateResourceCount();
                return;
            }

            var entries = new List<ResourceEntryViewModel>();
            if (_selectedNode is ResourceThemeVariantNode themeNode && !IncludeNested)
            {
                foreach (var child in themeNode.Children)
                {
                    AddDirectEntries(child, entries);
                }
            }
            else
            {
                CollectEntries(_selectedNode, IncludeNested, entries);
            }

            for (var i = 0; i < entries.Count; i++)
            {
                _resourceEntries.Add(entries[i]);
            }

            _resourcesView.Refresh();
            UpdateResourceCount();
        }

        public void UpdateDetailsView()
        {
            Details?.UpdatePropertiesView(MainView.ShowImplementedInterfaces);
        }

        private void CollectEntries(ResourceTreeNode node, bool includeNested, List<ResourceEntryViewModel> entries)
        {
            AddDirectEntries(node, entries);

            if (!includeNested)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                CollectEntries(child, includeNested, entries);
            }
        }

        private void AddDirectEntries(ResourceTreeNode node, List<ResourceEntryViewModel> entries)
        {
            var themeVariant = FindThemeVariant(node);

            if (TryGetDirectDictionary(node, out var dictionary))
            {
                var scopePath = BuildScopePath(node);
                var scopeName = node.Name;
                var declaringType = dictionary.GetType();

                foreach (var entry in dictionary)
                {
                    var key = entry.Key;
                    var value = entry.Value;
                    var keyDisplay = _formatter.FormatKey(key);
                    var keyTypeName = key?.GetType().Name ?? "null";
                    var valueDescriptor = _formatter.DescribeValue(value);
                    var propertyType = value?.GetType() ?? typeof(object);
                    object? currentValue = value;

                    var valueProperty = new ResourceEntryPropertyViewModel(
                        keyDisplay,
                        propertyType,
                        () => currentValue,
                        dictionary is IResourceDictionary
                            ? newValue =>
                            {
                                dictionary[key] = newValue;
                                currentValue = newValue;
                            }
                            : null,
                        declaringType);

                    entries.Add(new ResourceEntryViewModel(
                        key,
                        value,
                        keyDisplay,
                        keyTypeName,
                        valueDescriptor,
                        valueProperty,
                        scopeName,
                        scopePath,
                        themeVariant));
                }
            }
        }

        private bool TryGetDirectDictionary(ResourceTreeNode node, out IResourceDictionary dictionary)
        {
            dictionary = null!;

            if (node is ResourceThemeVariantNode)
            {
                return false;
            }

            if (node is ResourceHostNode hostNode)
            {
                return TryGetHostDictionary(hostNode.Host, out dictionary);
            }

            if (node.Source is IResourceDictionary directDictionary)
            {
                dictionary = directDictionary;
                return dictionary.Count > 0;
            }

            if (node.Source is Styles styles)
            {
                return TryGetResourcesDictionary(styles, out dictionary);
            }

            if (node.Source is StyleBase style)
            {
                return TryGetResourcesDictionary(style, out dictionary);
            }

            if (node.Source is IResourceProvider provider)
            {
                return TryGetResourcesDictionary(provider, out dictionary);
            }

            return false;
        }

        private static bool TryGetHostDictionary(IResourceHost host, out IResourceDictionary dictionary)
        {
            dictionary = null!;

            if (host is Application application)
            {
                dictionary = application.Resources;
                return dictionary.Count > 0;
            }

            if (host is StyledElement styledElement)
            {
                dictionary = styledElement.Resources;
                return dictionary.Count > 0;
            }

            return false;
        }

        private static bool TryGetResourcesDictionary(Styles styles, out IResourceDictionary dictionary)
        {
            dictionary = styles.Resources;
            return dictionary.Count > 0;
        }

        private static bool TryGetResourcesDictionary(StyleBase style, out IResourceDictionary dictionary)
        {
            dictionary = style.Resources;
            return dictionary.Count > 0;
        }

        private static bool TryGetResourcesDictionary(IResourceProvider provider, out IResourceDictionary dictionary)
        {
            dictionary = null!;

            var property = provider.GetType().GetProperty("Resources", BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(provider) is IResourceDictionary resources)
            {
                dictionary = resources;
                return dictionary.Count > 0
                    || dictionary.MergedDictionaries.Count > 0
                    || dictionary.ThemeDictionaries.Count > 0;
            }

            return false;
        }

        private void ApplySort()
        {
            _resourcesView.SortDescriptions.Clear();
            _resourcesView.GroupDescriptions.Clear();

            if (SortMode == ResourceSortMode.Type)
            {
                _resourcesView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(ResourceEntryViewModel.ValueTypeName)));
                _resourcesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
                    nameof(ResourceEntryViewModel.ValueTypeName),
                    SortDirection));
                _resourcesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
                    nameof(ResourceEntryViewModel.KeyDisplay),
                    SortDirection));
            }
            else
            {
                _resourcesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
                    nameof(ResourceEntryViewModel.KeyDisplay),
                    SortDirection));
            }

            _resourcesView.Refresh();
            UpdateResourceCount();
        }

        private void UpdateResourceCount()
        {
            ResourceCount = _resourcesView.Count;
            RaisePropertyChanged(nameof(ShowNoResourcesMessage));
        }

        private bool FilterResource(object obj)
        {
            if (obj is not ResourceEntryViewModel entry)
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.KeyDisplay))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.ValueTypeName))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.KeyTypeName))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.ValuePreview))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.ScopePath))
            {
                return true;
            }

            return entry.ThemeVariant != null && ResourcesFilter.Filter(entry.ThemeVariant);
        }

        private void SubscribeToResourcesChanged(ResourceTreeNode? node)
        {
            _resourcesSubscription?.Dispose();
            _resourcesSubscription = null;

            var host = FindOwnerHost(node);
            if (host == null)
            {
                return;
            }

            void Handler(object? sender, ResourcesChangedEventArgs e)
            {
                RefreshResources();
            }

            host.ResourcesChanged += Handler;
            _resourcesSubscription = Disposable.Create(() => host.ResourcesChanged -= Handler);
        }

        private static IResourceHost? FindOwnerHost(ResourceTreeNode? node)
        {
            var current = node;

            while (current != null)
            {
                if (current is ResourceHostNode hostNode)
                {
                    return hostNode.Host;
                }

                if (current.Source is IResourceProvider provider && provider.Owner != null)
                {
                    return provider.Owner;
                }

                current = current.Parent;
            }

            return null;
        }

        private ResourceHostNode? FindHostNode(IResourceHost host)
        {
            foreach (var node in Nodes)
            {
                var result = FindHostNode(node, host);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static ResourceHostNode? FindHostNode(ResourceTreeNode node, IResourceHost host)
        {
            if (node is ResourceHostNode hostNode && ReferenceEquals(hostNode.Host, host))
            {
                return hostNode;
            }

            foreach (var child in node.Children)
            {
                var result = FindHostNode(child, host);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void ExpandNode(ResourceTreeNode? node)
        {
            if (node != null)
            {
                node.IsExpanded = true;
                ExpandNode(node.Parent);
            }
        }

        private static string BuildScopePath(ResourceTreeNode node)
        {
            var stack = new Stack<string>();
            var current = node;

            while (current != null)
            {
                stack.Push(current.Name);
                current = current.Parent;
            }

            return string.Join(" / ", stack);
        }

        private static string? FindThemeVariant(ResourceTreeNode node)
        {
            var current = node;
            while (current != null)
            {
                if (current is ResourceThemeVariantNode themeNode)
                {
                    return themeNode.VariantDisplay;
                }

                current = current.Parent;
            }

            return null;
        }
    }
}
