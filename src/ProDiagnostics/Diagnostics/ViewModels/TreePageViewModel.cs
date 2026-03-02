using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Diagnostics.Services;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Reflection;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class TreePageViewModel : ViewModelBase, IDisposable
    {
        private TreeNode? _selectedNode;
        private object? _selectedNodeItem;
        private ControlDetailsViewModel? _details;
        private readonly ISet<string> _pinnedProperties;
        private readonly IHierarchicalModel _hierarchicalModel;
        private bool _isUpdatingSelectedNodeItem;
        private bool _suppressMainSelectionNotification;

        public TreePageViewModel(MainViewModel mainView, TreeNode[] nodes, ITreeHierarchyModelFactory modelFactory, ISet<string> pinnedProperties)
        {
            MainView = mainView;
            Nodes = nodes;
            _hierarchicalModel = modelFactory.Create(nodes);
            _pinnedProperties = pinnedProperties;
            PropertiesFilter = new FilterViewModel();
            PropertiesFilter.RefreshFilter += (s, e) => Details?.PropertiesView?.Refresh();

            SettersFilter = new FilterViewModel();
            SettersFilter.RefreshFilter += (s, e) => Details?.UpdateStyleFilters();
        }

        public event EventHandler<string>? ClipboardCopyRequested;

        public MainViewModel MainView { get; }

        public FilterViewModel PropertiesFilter { get; }

        public FilterViewModel SettersFilter { get; }

        public TreeNode[] Nodes { get; protected set; }

        public IHierarchicalModel HierarchicalModel => _hierarchicalModel;

        public TreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (RaiseAndSetIfChanged(ref _selectedNode, value))
                {
                    if (!_isUpdatingSelectedNodeItem &&
                        !ReferenceEquals(_selectedNodeItem, value))
                    {
                        _selectedNodeItem = value;
                        RaisePropertyChanged(nameof(SelectedNodeItem));
                    }

                    if (value != null)
                    {
                        ExpandNode(value.Parent);
                    }

                    Details = value != null ?
                        new ControlDetailsViewModel(this, value.Visual, _pinnedProperties) :
                        null;
                    Details?.UpdatePropertiesView(MainView.ShowImplementedInterfaces);
                    Details?.UpdateStyleFilters();

                    if (!_suppressMainSelectionNotification)
                    {
                        // Notify after details are rebuilt so the Properties tab receives
                        // the current selection's details rather than the previous node.
                        MainView.NotifyTreeSelectionChanged(value?.Visual);
                    }
                }
            }
        }

        public object? SelectedNodeItem
        {
            get => _selectedNodeItem;
            set
            {
                var resolvedNode = ResolveNodeFromSelectionItem(value);

                // DataGrid may transiently clear SelectedItem when the view is detached during tab switch.
                // Keep the current selection so cross-page diagnostics scope remains stable.
                if (value is null && _selectedNode is not null)
                {
                    return;
                }

                // Some virtualization wrappers can briefly surface selection tokens that do not map to a tree node.
                // Ignore these transients to avoid clearing cross-page inspection state.
                if (value is not null && resolvedNode is null && _selectedNode is not null)
                {
                    if (!ReferenceEquals(_selectedNodeItem, _selectedNode))
                    {
                        _selectedNodeItem = _selectedNode;
                        RaisePropertyChanged(nameof(SelectedNodeItem));
                    }

                    return;
                }

                if (!RaiseAndSetIfChanged(ref _selectedNodeItem, value))
                {
                    return;
                }

                _isUpdatingSelectedNodeItem = true;
                try
                {
                    SelectedNode = resolvedNode;
                }
                finally
                {
                    _isUpdatingSelectedNodeItem = false;
                }
            }
        }

        public ControlDetailsViewModel? Details
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

        public void Dispose()
        {
            foreach (var node in Nodes)
            {
                node.Dispose();
            }

            _details?.Dispose();
        }

        public TreeNode? FindNode(Control control)
        {
            foreach (var node in Nodes)
            {
                var result = FindNode(node, control);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public void SelectControl(Control control)
        {
            SelectControl(control, notifyMainSelection: true);
        }

        public void SelectControl(Control control, bool notifyMainSelection)
        {
            var previousSuppression = _suppressMainSelectionNotification;
            _suppressMainSelectionNotification = !notifyMainSelection;

            try
            {
            var node = default(TreeNode);
            Control? c = control;

            while (node == null && c != null)
            {
                node = FindNode(c);

                if (node == null)
                {
                    c = c.GetVisualParent<Control>();
                }
            }

            if (node != null)
            {
                SelectedNode = node;
            }
            }
            finally
            {
                _suppressMainSelectionNotification = previousSuppression;
            }
        }

        public void CopySelector()
        {
            var currentVisual = SelectedNode?.Visual as Visual;
            if (currentVisual is not null)
            {
                var selector = GetVisualSelector(currentVisual);

                ClipboardCopyRequested?.Invoke(this, selector);
            }
        }

        public void CopySelectorFromTemplateParent()
        {
            var parts = new List<string>();

            var currentVisual = SelectedNode?.Visual as Visual;
            while (currentVisual is not null)
            {
                parts.Add(GetVisualSelector(currentVisual));

                currentVisual = currentVisual.TemplatedParent as Visual;
            }

            if (parts.Any())
            {
                parts.Reverse();
                var selector = string.Join(" /template/ ", parts);

                ClipboardCopyRequested?.Invoke(this, selector);
            }
        }

        public void ExpandRecursively()
        {
            if (SelectedNode is { } selectedNode)
            {
                ExpandNode(selectedNode);

                var stack = new Stack<TreeNode>();
                stack.Push(selectedNode);

                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    item.IsExpanded = true;
                    foreach (var child in item.Children)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        public void CollapseChildren()
        {
            if (SelectedNode is { } selectedNode)
            {
                var stack = new Stack<TreeNode>();
                stack.Push(selectedNode);

                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    item.IsExpanded = false;
                    foreach (var child in item.Children)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        public void CaptureNodeScreenshot()
        {
            MainView.Shot(null);
        }

        public void BringIntoView()
        {
            (SelectedNode?.Visual as Control)?.BringIntoView();
        }


        public void Focus()
        {
            (SelectedNode?.Visual as Control)?.Focus();
        }

        private static string GetVisualSelector(Visual visual)
        {
            var name = string.IsNullOrEmpty(visual.Name) ? "" : $"#{visual.Name}";
            var classes = string.Concat(visual.Classes
                .Where(c => !c.StartsWith(":"))
                .Select(c => '.' + c));
            var pseudo = string.Concat(visual.Classes.Where(c => c[0] == ':').Select(c => c));
            var type = StyledElement.GetStyleKey(visual);
            return $$"""{{{type.Assembly.FullName}}}{{type.Namespace}}|{{type.Name}}{{name}}{{classes}}{{pseudo}}""";
        }

        private void ExpandNode(TreeNode? node)
        {
            if (node != null)
            {
                node.IsExpanded = true;
                ExpandNode(node.Parent);
            }
        }

        private TreeNode? FindNode(TreeNode node, Control control)
        {
            if (node.Visual == control)
            {
                return node;
            }
            else
            {
                foreach (var child in node.Children)
                {
                    var result = FindNode(child, control);

                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private TreeNode? ResolveNodeFromSelectionItem(object? item)
        {
            switch (item)
            {
                case TreeNode treeNode:
                    return treeNode;
                case HierarchicalNode node:
                    return ResolveWrappedItem(node.Item);
                default:
                {
                    var wrappedItem = TryGetWrappedItem(item);
                    if (wrappedItem is not null)
                    {
                        var resolvedWrappedItem = ResolveWrappedItem(wrappedItem);
                        if (resolvedWrappedItem is not null)
                        {
                            return resolvedWrappedItem;
                        }

                        var nestedWrappedItem = TryGetWrappedItem(wrappedItem);
                        if (nestedWrappedItem is not null && !ReferenceEquals(nestedWrappedItem, wrappedItem))
                        {
                            return ResolveWrappedItem(nestedWrappedItem);
                        }
                    }

                    return ResolveWrappedItem(item);
                }
            }
        }

        private TreeNode? ResolveWrappedItem(object? wrappedItem)
        {
            switch (wrappedItem)
            {
                case null:
                    return null;
                case TreeNode treeNode:
                    return treeNode;
                case Control control:
                    return FindNode(control);
                case AvaloniaObject avaloniaObject:
                    return FindNodeByVisual(avaloniaObject);
                default:
                    return null;
            }
        }

        private TreeNode? FindNodeByVisual(AvaloniaObject visual)
        {
            foreach (var node in Nodes)
            {
                var result = FindNodeByVisual(node, visual);
                if (result is not null)
                {
                    return result;
                }
            }

            return null;
        }

        private static TreeNode? FindNodeByVisual(TreeNode node, AvaloniaObject visual)
        {
            if (ReferenceEquals(node.Visual, visual))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var result = FindNodeByVisual(child, visual);
                if (result is not null)
                {
                    return result;
                }
            }

            return null;
        }

        private static object? TryGetWrappedItem(object? selectionItem)
        {
            if (selectionItem is null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = selectionItem.GetType();

            if (TryReadProperty(type.GetProperty("Item", flags), selectionItem, out var value))
            {
                return value;
            }

            if (TryReadProperty(type.GetProperty("Node", flags), selectionItem, out value))
            {
                return value;
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (!property.Name.EndsWith(".Item", StringComparison.Ordinal) &&
                    !property.Name.EndsWith(".Node", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryReadProperty(property, selectionItem, out value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool TryReadProperty(PropertyInfo? property, object target, out object? value)
        {
            value = null;
            if (property is null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return false;
            }

            try
            {
                value = property.GetValue(target);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal void UpdatePropertiesView()
        {
            Details?.UpdatePropertiesView(MainView?.ShowImplementedInterfaces ?? true);
        }
    }
}
