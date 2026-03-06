using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.Remote;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class TreePageViewModel : ViewModelBase, IDisposable
    {
        private static readonly bool TraceEnabled = string.Equals(
            Environment.GetEnvironmentVariable("PRODIAG_TRACE"),
            "1",
            StringComparison.Ordinal);
        private TreeNode? _selectedNode;
        private object? _selectedNodeItem;
        private ControlDetailsViewModel? _details;
        private readonly ISet<string> _pinnedProperties;
        private readonly IHierarchicalModel _hierarchicalModel;
        private readonly TreeNode[] _localNodes;
        private readonly Dictionary<string, TreeNode> _localNodesByPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AvaloniaObject> _localVisualByPath = new(StringComparer.Ordinal);
        private readonly Dictionary<AvaloniaObject, string> _localPathByVisual = new();
        private readonly Dictionary<string, TreeNode> _currentNodesByPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TreeNode> _currentNodesById = new(StringComparer.Ordinal);
        private readonly string _remoteScope;
        private IRemoteReadOnlyDiagnosticsDomainService? _remoteReadOnly;
        private IRemoteMutationDiagnosticsDomainService? _remoteMutation;
        private bool _isUpdatingSelectedNodeItem;
        private bool _suppressMainSelectionNotification;
        private bool _usingRemoteTree;
        private int _remoteTreeRefreshVersion;
        private bool _hasRemoteTreeSnapshot;
        private bool _disableLocalFallbackWhileRemoteDisconnected;

        public TreePageViewModel(
            MainViewModel mainView,
            TreeNode[] nodes,
            ITreeHierarchyModelFactory modelFactory,
            ISet<string> pinnedProperties,
            string remoteScope = "combined")
        {
            MainView = mainView;
            Nodes = nodes;
            _localNodes = nodes;
            _hierarchicalModel = modelFactory.Create(nodes);
            _pinnedProperties = pinnedProperties;
            _remoteScope = remoteScope;
            BuildLocalLookupMaps(nodes);
            RebuildCurrentNodePathMap();
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

                    RebuildDetails(value);

                    if (!_suppressMainSelectionNotification)
                    {
                        // Notify after details are rebuilt so the Properties tab receives
                        // the current selection's details rather than the previous node.
                        MainView.NotifyTreeSelectionChanged(value?.Visual, _remoteScope, GetNodePath(value), GetNodeId(value));
                    }
                }
            }
        }

        internal string? SelectedNodePath => GetNodePath(SelectedNode);

        internal string? SelectedNodeId => GetNodeId(SelectedNode);

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
            var disposed = new HashSet<TreeNode>(TreeNodeReferenceComparer.Instance);
            for (var i = 0; i < _localNodes.Length; i++)
            {
                if (disposed.Add(_localNodes[i]))
                {
                    _localNodes[i].Dispose();
                }
            }

            for (var i = 0; i < Nodes.Length; i++)
            {
                if (disposed.Add(Nodes[i]))
                {
                    Nodes[i].Dispose();
                }
            }

            _details?.Dispose();
        }

        internal void SetRemoteReadOnlySource(
            IRemoteReadOnlyDiagnosticsDomainService? readOnly,
            bool refreshTreeNow = true)
        {
            _remoteReadOnly = readOnly;
            _hasRemoteTreeSnapshot = false;
            if (TraceEnabled)
            {
                Console.WriteLine($"[TreePage:{_remoteScope}] SetRemoteReadOnlySource readOnly={(readOnly is null ? "(null)" : readOnly.GetType().Name)} refreshNow={refreshTreeNow}");
            }
            if (_remoteReadOnly is null)
            {
                if (_disableLocalFallbackWhileRemoteDisconnected)
                {
                    ShowRemoteDisconnectedTree();
                }
                else
                {
                    RestoreLocalTree();
                }
            }
            else if (refreshTreeNow)
            {
                _ = RefreshRemoteTreeAsync();
            }

            // Rebuild details immediately so Properties tab always uses the active source.
            RebuildDetails(SelectedNode);
        }

        internal void EnsureRemoteTreeLoaded()
        {
            if (_remoteReadOnly is null)
            {
                return;
            }

            if (_usingRemoteTree && _hasRemoteTreeSnapshot)
            {
                return;
            }

            _ = RefreshRemoteTreeAsync();
        }

        internal void ForceRefreshRemoteTree()
        {
            if (_remoteReadOnly is null)
            {
                return;
            }

            _ = RefreshRemoteTreeAsync();
        }

        internal Task RefreshRemoteTreeNowAsync()
        {
            return RefreshRemoteTreeAsync();
        }

        internal void ApplyPreloadedRemoteTreeSnapshot(RemoteTreeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (!string.Equals(snapshot.Scope, _remoteScope, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyRemoteTreeSnapshot(snapshot);
        }

        internal void SetRemoteMutationSource(IRemoteMutationDiagnosticsDomainService? mutation)
        {
            _remoteMutation = mutation;
            RebuildDetails(SelectedNode);
        }

        internal void SetRemoteDisconnectedFallbackDisabled(bool disabled)
        {
            _disableLocalFallbackWhileRemoteDisconnected = disabled;
            if (_remoteReadOnly is null)
            {
                if (_disableLocalFallbackWhileRemoteDisconnected)
                {
                    ShowRemoteDisconnectedTree();
                }
                else
                {
                    RestoreLocalTree();
                }
            }
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

            if (_usingRemoteTree &&
                _localPathByVisual.TryGetValue(control, out var nodePath) &&
                _currentNodesByPath.TryGetValue(nodePath, out var mappedRemoteNode))
            {
                return mappedRemoteNode;
            }

            return null;
        }

        public void SelectControl(Control control)
        {
            SelectObject(control, notifyMainSelection: true);
        }

        public void SelectControl(Control control, bool notifyMainSelection)
        {
            SelectObject(control, notifyMainSelection);
        }

        internal void SelectObject(AvaloniaObject? visual, bool notifyMainSelection)
        {
            var previousSuppression = _suppressMainSelectionNotification;
            _suppressMainSelectionNotification = !notifyMainSelection;

            try
            {
                if (visual is null)
                {
                    return;
                }

                var node = visual is Control control
                    ? FindControlNode(control)
                    : FindNodeByVisual(visual);

                if (node is not null)
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

        private TreeNode? FindControlNode(Control control)
        {
            var current = control;
            while (current is not null)
            {
                var node = FindNode(current);
                if (node is not null)
                {
                    return node;
                }

                current = current.GetVisualParent<Control>();
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

        internal string? GetNodePath(TreeNode? node)
        {
            if (node is null)
            {
                return null;
            }

            if (node is RemoteTreeNode remoteTreeNode)
            {
                return remoteTreeNode.Snapshot.NodePath;
            }

            var current = node;
            var indexes = new Stack<int>();
            while (current.Parent is { } parent)
            {
                var childIndex = GetChildIndex(parent, current);
                if (childIndex < 0)
                {
                    return null;
                }

                indexes.Push(childIndex);
                current = parent;
            }

            var rootIndex = Array.IndexOf(Nodes, current);
            if (rootIndex < 0)
            {
                return null;
            }

            var path = rootIndex.ToString();
            while (indexes.Count > 0)
            {
                path += "/" + indexes.Pop();
            }

            return path;
        }

        internal bool TrySelectNodeByPath(string? nodePath, bool notifyMainSelection = true)
        {
            if (!TryGetNodeByPath(nodePath, out var node) || node is null)
            {
                return false;
            }

            SelectNodeCore(node, notifyMainSelection);
            return true;
        }

        internal bool TryGetNodeByPath(string? nodePath, out TreeNode? node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(nodePath))
            {
                return false;
            }

            return _currentNodesByPath.TryGetValue(nodePath, out node);
        }

        internal bool TryGetNodeById(string? nodeId, out TreeNode? node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            return _currentNodesById.TryGetValue(nodeId, out node);
        }

        internal bool TrySelectNodeById(string? nodeId, bool notifyMainSelection = true)
        {
            if (!TryGetNodeById(nodeId, out var node) || node is null)
            {
                return false;
            }

            SelectNodeCore(node, notifyMainSelection);
            return true;
        }

        internal void ClearSelection(bool notifyMainSelection = false)
        {
            SelectNodeCore(null, notifyMainSelection);
        }

        internal bool EnsureSelection(bool notifyMainSelection = false)
        {
            if (SelectedNode is not null)
            {
                return true;
            }

            if (Nodes.Length == 0)
            {
                return false;
            }

            SelectNodeCore(Nodes[0], notifyMainSelection);
            return true;
        }

        internal bool TryGetNodePathById(string? nodeId, out string? nodePath)
        {
            nodePath = null;
            if (!TryGetNodeById(nodeId, out var node) || node is null)
            {
                return false;
            }

            nodePath = GetNodePath(node);
            return !string.IsNullOrWhiteSpace(nodePath);
        }

        internal bool TryGetNodePathForObject(AvaloniaObject? visual, out string? nodePath)
        {
            nodePath = null;
            if (visual is null)
            {
                return false;
            }

            var node = visual is Control control
                ? FindControlNode(control)
                : FindNodeByVisual(visual);
            if (node is null)
            {
                return false;
            }

            nodePath = GetNodePath(node);
            return !string.IsNullOrWhiteSpace(nodePath);
        }

        private static int GetChildIndex(TreeNode parent, TreeNode child)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                if (ReferenceEquals(parent.Children[i], child))
                {
                    return i;
                }
            }

            return -1;
        }

        private async Task RefreshRemoteTreeAsync()
        {
            if (_remoteReadOnly is null)
            {
                if (TraceEnabled)
                {
                    Console.WriteLine($"[TreePage:{_remoteScope}] RefreshRemoteTreeAsync skipped because remote source is null");
                }
                return;
            }

            var refreshVersion = Interlocked.Increment(ref _remoteTreeRefreshVersion);
            RemoteTreeSnapshot snapshot;
            try
            {
                if (TraceEnabled)
                {
                    Console.WriteLine($"[TreePage:{_remoteScope}] RefreshRemoteTreeAsync request version={refreshVersion}");
                }
                snapshot = await _remoteReadOnly.GetTreeSnapshotAsync(
                    new RemoteTreeSnapshotRequest
                    {
                        Scope = _remoteScope,
                        IncludeSourceLocations = false,
                        IncludeVisualDetails = false,
                    }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (TraceEnabled)
                {
                    Console.WriteLine($"[TreePage:{_remoteScope}] RefreshRemoteTreeAsync failed: {ex.GetType().Name}: {ex.Message}");
                }
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (TraceEnabled)
                {
                    Console.WriteLine($"[TreePage:{_remoteScope}] InvokeAsync apply version={refreshVersion} currentVersion={_remoteTreeRefreshVersion} remoteNull={_remoteReadOnly is null}");
                }
                if (_remoteReadOnly is null || refreshVersion != _remoteTreeRefreshVersion)
                {
                    if (TraceEnabled)
                    {
                        Console.WriteLine($"[TreePage:{_remoteScope}] InvokeAsync skipped");
                    }
                    return;
                }

                ApplyRemoteTreeSnapshot(snapshot);
            });
        }

        private void ApplyRemoteTreeSnapshot(RemoteTreeSnapshot snapshot)
        {
            var selectedPath = GetNodePath(SelectedNode);
            var (roots, map) = BuildRemoteNodes(snapshot.Nodes);
            if (TraceEnabled)
            {
                Console.WriteLine($"[TreePage:{_remoteScope}] snapshot nodes={snapshot.Nodes.Count} roots={roots.Length} selectedPath={selectedPath ?? "(null)"}");
            }
            if (roots.Length == 0)
            {
                if (_disableLocalFallbackWhileRemoteDisconnected)
                {
                    ShowRemoteDisconnectedTree();
                }
                else
                {
                    RestoreLocalTree();
                }
                return;
            }

            SwapTreeNodes(roots, map, useRemoteTree: true);
            if (selectedPath is not null && _currentNodesByPath.TryGetValue(selectedPath, out var selectedNode))
            {
                SelectNodeCore(selectedNode, notifyMainSelection: false);
            }
            else if (roots.Length > 0)
            {
                SelectNodeCore(roots[0], notifyMainSelection: false);
            }
            else
            {
                SelectNodeCore(null, notifyMainSelection: false);
            }

            if (TraceEnabled)
            {
                Console.WriteLine($"[TreePage:{_remoteScope}] selected={SelectedNode?.Type ?? "(null)"} details={(Details is null ? "(null)" : Details.GetType().Name)}");
            }

            _hasRemoteTreeSnapshot = true;
        }

        private void ShowRemoteDisconnectedTree()
        {
            SwapTreeNodes(
                Array.Empty<TreeNode>(),
                new Dictionary<string, TreeNode>(StringComparer.Ordinal),
                useRemoteTree: true);
            SelectNodeCore(null, notifyMainSelection: false);
            _hasRemoteTreeSnapshot = false;
        }

        private (TreeNode[] Roots, Dictionary<string, TreeNode> NodesByPath) BuildRemoteNodes(IReadOnlyList<RemoteTreeNodeSnapshot> snapshots)
        {
            var ordered = snapshots
                .OrderBy(static node => node.Depth)
                .ThenBy(static node => node.NodePath.Length)
                .ThenBy(static node => node.NodePath, StringComparer.Ordinal)
                .ToArray();
            var nodesByPath = new Dictionary<string, TreeNode>(StringComparer.Ordinal);

            for (var i = 0; i < ordered.Length; i++)
            {
                var snapshot = ordered[i];
                TreeNode? parent = null;
                if (snapshot.ParentNodePath is not null)
                {
                    nodesByPath.TryGetValue(snapshot.ParentNodePath, out parent);
                }

                var visual = _localVisualByPath.TryGetValue(snapshot.NodePath, out var localVisual)
                    ? localVisual
                    : RemoteTreeNode.CreateSnapshotVisual(snapshot);
                nodesByPath[snapshot.NodePath] = new RemoteTreeNode(snapshot, visual, parent);
            }

            var roots = new List<TreeNode>(capacity: Math.Max(1, snapshots.Count / 4));
            for (var i = 0; i < ordered.Length; i++)
            {
                var snapshot = ordered[i];
                var current = (RemoteTreeNode)nodesByPath[snapshot.NodePath];
                if (snapshot.ParentNodePath is not null &&
                    nodesByPath.TryGetValue(snapshot.ParentNodePath, out var parentNode) &&
                    parentNode is RemoteTreeNode remoteParentNode)
                {
                    remoteParentNode.AddChild(current);
                }
                else
                {
                    roots.Add(current);
                }
            }

            return (roots.ToArray(), nodesByPath);
        }

        private void RestoreLocalTree()
        {
            var selectedPath = GetNodePath(SelectedNode);
            SwapTreeNodes(_localNodes, _localNodesByPath, useRemoteTree: false);
            if (selectedPath is not null && _currentNodesByPath.TryGetValue(selectedPath, out var selectedNode))
            {
                SelectNodeCore(selectedNode, notifyMainSelection: false);
            }
            else
            {
                SelectNodeCore(null, notifyMainSelection: false);
            }

            _hasRemoteTreeSnapshot = false;
        }

        private void SwapTreeNodes(
            TreeNode[] nodes,
            IReadOnlyDictionary<string, TreeNode> nodesByPath,
            bool useRemoteTree)
        {
            if (_usingRemoteTree)
            {
                for (var i = 0; i < Nodes.Length; i++)
                {
                    Nodes[i].Dispose();
                }
            }

            Nodes = nodes;
            _usingRemoteTree = useRemoteTree;
            _currentNodesByPath.Clear();
            _currentNodesById.Clear();
            foreach (var (path, node) in nodesByPath)
            {
                _currentNodesByPath[path] = node;
                if (GetNodeId(node) is { Length: > 0 } nodeId)
                {
                    _currentNodesById[nodeId] = node;
                }
            }

            _hierarchicalModel.SetRoots(nodes);
            RaisePropertyChanged(nameof(Nodes));
        }

        private void SelectNodeCore(TreeNode? node, bool notifyMainSelection)
        {
            var previousSuppression = _suppressMainSelectionNotification;
            var previousSelectedItemUpdate = _isUpdatingSelectedNodeItem;
            _suppressMainSelectionNotification = !notifyMainSelection;
            _isUpdatingSelectedNodeItem = true;

            try
            {
                if (!ReferenceEquals(_selectedNodeItem, node))
                {
                    _selectedNodeItem = node;
                    RaisePropertyChanged(nameof(SelectedNodeItem));
                }

                SelectedNode = node;
            }
            finally
            {
                _isUpdatingSelectedNodeItem = previousSelectedItemUpdate;
                _suppressMainSelectionNotification = previousSuppression;
            }
        }

        private void BuildLocalLookupMaps(TreeNode[] roots)
        {
            _localNodesByPath.Clear();
            _localVisualByPath.Clear();
            _localPathByVisual.Clear();

            for (var i = 0; i < roots.Length; i++)
            {
                FillLookupMaps(roots[i], i.ToString(), _localNodesByPath, _localVisualByPath, _localPathByVisual);
            }
        }

        private void RebuildCurrentNodePathMap()
        {
            _currentNodesByPath.Clear();
            _currentNodesById.Clear();
            for (var i = 0; i < Nodes.Length; i++)
            {
                FillNodeMaps(Nodes[i], i.ToString(), _currentNodesByPath, _currentNodesById);
            }
        }

        private static void FillLookupMaps(
            TreeNode node,
            string path,
            IDictionary<string, TreeNode> nodesByPath,
            IDictionary<string, AvaloniaObject> visualsByPath,
            IDictionary<AvaloniaObject, string> pathsByVisual)
        {
            nodesByPath[path] = node;
            visualsByPath[path] = node.Visual;
            if (!pathsByVisual.ContainsKey(node.Visual))
            {
                pathsByVisual[node.Visual] = path;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                FillLookupMaps(
                    node.Children[i],
                    path + "/" + i,
                    nodesByPath,
                    visualsByPath,
                    pathsByVisual);
            }
        }

        private static void FillNodeMaps(
            TreeNode node,
            string path,
            IDictionary<string, TreeNode> nodesByPath,
            IDictionary<string, TreeNode> nodesById)
        {
            nodesByPath[path] = node;
            if (node is RemoteTreeNode remoteTreeNode &&
                !string.IsNullOrWhiteSpace(remoteTreeNode.Snapshot.NodeId))
            {
                nodesById[remoteTreeNode.Snapshot.NodeId] = node;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                FillNodeMaps(node.Children[i], path + "/" + i, nodesByPath, nodesById);
            }
        }

        private static string? GetNodeId(TreeNode? node)
        {
            return node is RemoteTreeNode remoteTreeNode ? remoteTreeNode.Snapshot.NodeId : null;
        }

        private sealed class TreeNodeReferenceComparer : IEqualityComparer<TreeNode>
        {
            public static TreeNodeReferenceComparer Instance { get; } = new();

            public bool Equals(TreeNode? x, TreeNode? y) => ReferenceEquals(x, y);

            public int GetHashCode(TreeNode obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        internal void UpdatePropertiesView()
        {
            Details?.UpdatePropertiesView(MainView?.ShowImplementedInterfaces ?? true);
        }

        private (string Scope, string? NodePath, string? ControlName) GetRemoteDetailsContext()
        {
            return (
                Scope: _remoteScope,
                NodePath: GetNodePath(SelectedNode),
                ControlName: (SelectedNode?.Visual as INamed)?.Name);
        }

        private void RebuildDetails(TreeNode? node)
        {
            Details = node is null
                ? null
                : new ControlDetailsViewModel(
                    this,
                    node.Visual,
                    _pinnedProperties,
                    sourceLocationService: null,
                    remoteReadOnly: _remoteReadOnly,
                    remoteMutation: _remoteMutation,
                    remoteContextAccessor: GetRemoteDetailsContext);

            Details?.UpdatePropertiesView(MainView.ShowImplementedInterfaces);
            Details?.UpdateStyleFilters();
        }
    }
}
