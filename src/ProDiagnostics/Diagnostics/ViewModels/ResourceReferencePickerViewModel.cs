using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels
{
    internal sealed class ResourceReferencePickerViewModel : ViewModelBase
    {
        private readonly AvaloniaList<ResourceReferenceEntryViewModel> _entries = new();
        private readonly DataGridCollectionView _resourcesView;
        private ResourceReferenceScopeViewModel? _selectedScope;
        private ResourceReferenceEntryViewModel? _selectedResource;
        private int _resourceCount;

        public ResourceReferencePickerViewModel(
            PropertyViewModel property,
            IReadOnlyList<ResourceReferenceCandidate> candidates,
            IResourceNodeFormatter formatter)
        {
            Title = $"Select Resource - {property.Name}";
            TargetProperty = property.Name;
            TargetType = property.PropertyType.GetTypeName();
            ResourcesFilter = new FilterViewModel();
            ResourcesFilter.RefreshFilter += (_, _) => RefreshResources();
            _resourcesView = new DataGridCollectionView(_entries)
            {
                Filter = FilterResource
            };
            _resourcesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
                nameof(ResourceReferenceEntryViewModel.KeyDisplay),
                ListSortDirection.Ascending));

            foreach (var entry in CreateEntries(candidates, formatter))
            {
                _entries.Add(entry);
            }

            Scopes = BuildScopes(_entries);
            SelectedScope = Scopes.Count > 0 ? Scopes[0] : null;
            RefreshResources();
        }

        public string Title { get; }

        public string TargetProperty { get; }

        public string TargetType { get; }

        public IReadOnlyList<ResourceReferenceScopeViewModel> Scopes { get; }

        public DataGridCollectionView ResourcesView => _resourcesView;

        public FilterViewModel ResourcesFilter { get; }

        public int ResourceCount
        {
            get => _resourceCount;
            private set => RaiseAndSetIfChanged(ref _resourceCount, value);
        }

        public bool ShowNoResourcesMessage => ResourceCount == 0;

        public ResourceReferenceScopeViewModel? SelectedScope
        {
            get => _selectedScope;
            set
            {
                if (RaiseAndSetIfChanged(ref _selectedScope, value))
                {
                    RefreshResources();
                }
            }
        }

        public ResourceReferenceEntryViewModel? SelectedResource
        {
            get => _selectedResource;
            set
            {
                if (RaiseAndSetIfChanged(ref _selectedResource, value))
                {
                    RaisePropertyChanged(nameof(CanUseStatic));
                    RaisePropertyChanged(nameof(CanUseDynamic));
                }
            }
        }

        public bool CanUseStatic => SelectedResource?.StaticCandidate != null;

        public bool CanUseDynamic => SelectedResource?.DynamicCandidate != null;

        public ResourceReferenceCandidate? GetSelectedCandidate(DevToolsResourceReferenceKind kind)
        {
            return kind == DevToolsResourceReferenceKind.Dynamic
                ? SelectedResource?.DynamicCandidate
                : SelectedResource?.StaticCandidate;
        }

        private void RefreshResources()
        {
            _resourcesView.Refresh();
            ResourceCount = _resourcesView.Count;
            RaisePropertyChanged(nameof(ShowNoResourcesMessage));

            if (SelectedResource != null && !_resourcesView.Contains(SelectedResource))
            {
                SelectedResource = null;
            }
        }

        private bool FilterResource(object obj)
        {
            if (obj is not ResourceReferenceEntryViewModel entry)
            {
                return true;
            }

            if (SelectedScope != null && !SelectedScope.Contains(entry.ScopePath))
            {
                return false;
            }

            if (ResourcesFilter.Filter(entry.KeyDisplay))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.ValueTypeName))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.ValuePreview))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.ReferenceKinds))
            {
                return true;
            }

            if (ResourcesFilter.Filter(entry.ScopePath))
            {
                return true;
            }

            return entry.ThemeVariant != null && ResourcesFilter.Filter(entry.ThemeVariant);
        }

        private static IReadOnlyList<ResourceReferenceEntryViewModel> CreateEntries(
            IEnumerable<ResourceReferenceCandidate> candidates,
            IResourceNodeFormatter formatter)
        {
            return candidates
                .GroupBy(static candidate => new ResourceCandidateGroupKey(
                    candidate.KeyText,
                    candidate.ScopePath,
                    candidate.ThemeVariant))
                .Select(group =>
                {
                    var staticCandidate = group.FirstOrDefault(static candidate => candidate.Kind == DevToolsResourceReferenceKind.Static);
                    var dynamicCandidate = group.FirstOrDefault(static candidate => candidate.Kind == DevToolsResourceReferenceKind.Dynamic);
                    var candidate = staticCandidate ?? dynamicCandidate!;
                    var descriptor = formatter.DescribeValue(candidate.Value);
                    var valueProperty = new ResourceEntryPropertyViewModel(
                        candidate.KeyText,
                        candidate.ValueType,
                        () => candidate.Value,
                        setter: null,
                        candidate.ValueType);
                    return new ResourceReferenceEntryViewModel(
                        candidate.Key,
                        candidate.KeyText,
                        descriptor,
                        valueProperty,
                        candidate.ScopePath,
                        candidate.ThemeVariant,
                        staticCandidate,
                        dynamicCandidate);
                })
                .ToArray();
        }

        private static IReadOnlyList<ResourceReferenceScopeViewModel> BuildScopes(IEnumerable<ResourceReferenceEntryViewModel> entries)
        {
            var root = new ResourceReferenceScopeViewModel("All resources", string.Empty);

            foreach (var entry in entries)
            {
                root.IncrementResourceCount();
                var current = root;
                var path = string.Empty;
                foreach (var part in entry.ScopePath.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    path = path.Length == 0 ? part : $"{path} / {part}";
                    current = current.GetOrAddChild(part, path);
                    current.IncrementResourceCount();
                }
            }

            root.IsExpanded = true;
            return new[] { root };
        }

        private readonly record struct ResourceCandidateGroupKey(string KeyText, string ScopePath, string? ThemeVariant);
    }

    internal sealed class ResourceReferenceEntryViewModel
    {
        public ResourceReferenceEntryViewModel(
            object key,
            string keyDisplay,
            ResourceValueDescriptor valueDescriptor,
            ResourceEntryPropertyViewModel valueProperty,
            string scopePath,
            string? themeVariant,
            ResourceReferenceCandidate? staticCandidate,
            ResourceReferenceCandidate? dynamicCandidate)
        {
            Key = key;
            KeyDisplay = keyDisplay;
            ValueTypeName = valueDescriptor.TypeName;
            ValuePreview = valueDescriptor.Preview;
            ValueProperty = valueProperty;
            ScopePath = scopePath;
            ThemeVariant = themeVariant;
            StaticCandidate = staticCandidate;
            DynamicCandidate = dynamicCandidate;
            ReferenceKinds = dynamicCandidate != null
                ? "StaticResource, DynamicResource"
                : "StaticResource";
        }

        public object Key { get; }

        public string KeyDisplay { get; }

        public string ValueTypeName { get; }

        public string ValuePreview { get; }

        public ResourceEntryPropertyViewModel ValueProperty { get; }

        public string ScopePath { get; }

        public string? ThemeVariant { get; }

        public string ReferenceKinds { get; }

        public ResourceReferenceCandidate? StaticCandidate { get; }

        public ResourceReferenceCandidate? DynamicCandidate { get; }
    }

    internal sealed class ResourceReferenceScopeViewModel : ViewModelBase
    {
        private int _resourceCount;
        private bool _isExpanded;

        public ResourceReferenceScopeViewModel(string name, string scopePath)
        {
            Name = name;
            ScopePath = scopePath;
        }

        public string Name { get; }

        public string ScopePath { get; }

        public AvaloniaList<ResourceReferenceScopeViewModel> Children { get; } = new();

        public int ResourceCount
        {
            get => _resourceCount;
            private set => RaiseAndSetIfChanged(ref _resourceCount, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => RaiseAndSetIfChanged(ref _isExpanded, value);
        }

        public bool Contains(string scopePath)
        {
            return ScopePath.Length == 0 ||
                   scopePath == ScopePath ||
                   scopePath.StartsWith(ScopePath + " / ", StringComparison.Ordinal);
        }

        public ResourceReferenceScopeViewModel GetOrAddChild(string name, string scopePath)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                if (Children[i].ScopePath == scopePath)
                {
                    return Children[i];
                }
            }

            var child = new ResourceReferenceScopeViewModel(name, scopePath);
            Children.Add(child);
            return child;
        }

        public void IncrementResourceCount()
        {
            ResourceCount++;
        }
    }
}
