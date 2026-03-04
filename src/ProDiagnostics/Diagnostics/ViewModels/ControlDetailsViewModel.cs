using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class ControlDetailsViewModel : ViewModelBase, IDisposable, IClassesChangedListener
    {
        private static readonly ISourceLocationService DefaultSourceLocationService = new PortablePdbSourceLocationService();
        private readonly AvaloniaObject _avaloniaObject;
        private readonly ISet<string> _pinnedProperties;
        private readonly ISourceLocationService _sourceLocationService;
        private IDictionary<object, PropertyViewModel[]>? _propertyIndex;
        private PropertyViewModel? _selectedProperty;
        private DataGridCollectionView? _propertiesView;
        private bool _snapshotFrames;
        private bool _showInactiveFrames;
        private string? _framesStatus;
        private object? _selectedEntity;
        private readonly Stack<(string Name, object Entry)> _selectedEntitiesStack = new();
        private string? _selectedEntityName;
        private string? _selectedEntityType;
        private string? _xamlSourceText;
        private string? _codeSourceText;
        private string? _sourceLocationStatus;
        private bool _showImplementedInterfaces;
        private readonly IRemoteReadOnlyDiagnosticsDomainService? _remoteReadOnly;
        private readonly IRemoteMutationDiagnosticsDomainService? _remoteMutation;
        private readonly Func<(string Scope, string? NodePath, string? ControlName)>? _remoteContextAccessor;
        private long _remoteRefreshVersion;
        // new DataGridPathGroupDescription(nameof(AvaloniaPropertyViewModel.Group))
        private readonly static IReadOnlyList<DataGridPathGroupDescription> GroupDescriptors = new DataGridPathGroupDescription[]
        {
            new DataGridPathGroupDescription(nameof(AvaloniaPropertyViewModel.Group))
        };

        private readonly static IReadOnlyList<DataGridSortDescription> SortDescriptions = new DataGridSortDescription[]
        {
            new DataGridComparerSortDescription(PropertyComparer.Instance!, ListSortDirection.Ascending),
        };

        public ControlDetailsViewModel(
            TreePageViewModel treePage,
            AvaloniaObject avaloniaObject,
            ISet<string> pinnedProperties,
            ISourceLocationService? sourceLocationService = null,
            IRemoteReadOnlyDiagnosticsDomainService? remoteReadOnly = null,
            IRemoteMutationDiagnosticsDomainService? remoteMutation = null,
            Func<(string Scope, string? NodePath, string? ControlName)>? remoteContextAccessor = null)
        {
            _avaloniaObject = avaloniaObject;
            _pinnedProperties = pinnedProperties;
            _sourceLocationService = sourceLocationService ?? DefaultSourceLocationService;
            _remoteReadOnly = remoteReadOnly;
            _remoteMutation = remoteMutation;
            _remoteContextAccessor = remoteContextAccessor;
            TreePage = treePage;
            Layout = avaloniaObject is Visual visual
                ? new ControlLayoutViewModel(visual)
                : default;

            AppliedFrames = new ObservableCollection<ValueFrameViewModel>();
            PseudoClasses = new ObservableCollection<PseudoClassViewModel>();

            if (avaloniaObject is StyledElement styledElement)
            {
                if (_remoteReadOnly is null)
                {
                    styledElement.Classes.AddListener(this);
                }

                var pseudoClassAttributes = styledElement.GetType().GetCustomAttributes<PseudoClassesAttribute>(true);

                foreach (var classAttribute in pseudoClassAttributes)
                {
                    foreach (var className in classAttribute.PseudoClasses)
                    {
                        PseudoClasses.Add(new PseudoClassViewModel(
                            className,
                            styledElement,
                            _remoteMutation is null ? null : QueueRemotePseudoClassMutation));
                    }
                }

                if (_remoteReadOnly is null)
                {
                    var styleDiagnostics = styledElement.GetValueStoreDiagnostic();

                    var clipboard = TopLevel.GetTopLevel(_avaloniaObject as Visual)?.Clipboard;

                    foreach (var appliedStyle in styleDiagnostics.AppliedFrames.OrderBy(s => s.Priority))
                    {
                        AppliedFrames.Add(new ValueFrameViewModel(styledElement, appliedStyle, clipboard));
                    }

                    UpdateStyles();
                }
            }

            if (_remoteReadOnly is null)
            {
                NavigateToProperty(_avaloniaObject, (_avaloniaObject as Control)?.Name ?? _avaloniaObject.ToString());
            }
            else
            {
                SelectedEntity = _avaloniaObject;
                SelectedEntityName = (_avaloniaObject as Control)?.Name ?? _avaloniaObject.ToString();
                SelectedEntityType = _avaloniaObject.GetType().FullName ?? _avaloniaObject.GetType().Name;
                _ = RefreshFromRemoteAsync();
            }
        }

        public bool CanNavigateToParentProperty => _selectedEntitiesStack.Count >= 1;

        public TreePageViewModel TreePage { get; }

        public DataGridCollectionView? PropertiesView
        {
            get => _propertiesView;
            private set => RaiseAndSetIfChanged(ref _propertiesView, value);
        }

        public ObservableCollection<ValueFrameViewModel> AppliedFrames { get; }

        public ObservableCollection<PseudoClassViewModel> PseudoClasses { get; }

        public object? SelectedEntity
        {
            get => _selectedEntity;
            set => RaiseAndSetIfChanged(ref _selectedEntity, value);
        }

        public string? SelectedEntityName
        {
            get => _selectedEntityName;
            set => RaiseAndSetIfChanged(ref _selectedEntityName, value);
        }

        public string? SelectedEntityType
        {
            get => _selectedEntityType;
            set => RaiseAndSetIfChanged(ref _selectedEntityType, value);
        }

        public string? XamlSourceText
        {
            get => _xamlSourceText;
            private set
            {
                if (RaiseAndSetIfChanged(ref _xamlSourceText, value))
                {
                    RaisePropertyChanged(nameof(HasXamlSource));
                    RaisePropertyChanged(nameof(HasAnySourceLocation));
                }
            }
        }

        public string? CodeSourceText
        {
            get => _codeSourceText;
            private set
            {
                if (RaiseAndSetIfChanged(ref _codeSourceText, value))
                {
                    RaisePropertyChanged(nameof(HasCodeSource));
                    RaisePropertyChanged(nameof(HasAnySourceLocation));
                }
            }
        }

        public string? SourceLocationStatus
        {
            get => _sourceLocationStatus;
            private set => RaiseAndSetIfChanged(ref _sourceLocationStatus, value);
        }

        public bool HasXamlSource => !string.IsNullOrWhiteSpace(XamlSourceText);

        public bool HasCodeSource => !string.IsNullOrWhiteSpace(CodeSourceText);

        public bool HasAnySourceLocation => HasXamlSource || HasCodeSource;

        public PropertyViewModel? SelectedProperty
        {
            get => _selectedProperty;
            set => RaiseAndSetIfChanged(ref _selectedProperty, value);
        }

        public bool SnapshotFrames
        {
            get => _snapshotFrames;
            set => RaiseAndSetIfChanged(ref _snapshotFrames, value);
        }

        public bool ShowInactiveFrames
        {
            get => _showInactiveFrames;
            set => RaiseAndSetIfChanged(ref _showInactiveFrames, value);
        }

        public string? FramesStatus
        {
            get => _framesStatus;
            set => RaiseAndSetIfChanged(ref _framesStatus, value);
        }

        public ControlLayoutViewModel? Layout { get; }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(SnapshotFrames))
            {
                if (!SnapshotFrames)
                {
                    UpdateStyles();
                }
            }
        }

        public void UpdateStyleFilters()
        {
            foreach (var style in AppliedFrames)
            {
                var hasVisibleSetter = false;

                foreach (var setter in style.Setters)
                {
                    setter.IsVisible = TreePage.SettersFilter.Filter(setter.Name);

                    hasVisibleSetter |= setter.IsVisible;
                }

                style.IsVisible = hasVisibleSetter;
            }
        }

        public void Dispose()
        {
            if (_avaloniaObject is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= ControlPropertyChanged;
            }

            if (_avaloniaObject is AvaloniaObject ao)
            {
                ao.PropertyChanged -= ControlPropertyChanged;
            }

            if (_avaloniaObject is StyledElement se)
            {
                se.Classes.RemoveListener(this);
            }
        }

        private static IEnumerable<PropertyViewModel> GetAvaloniaProperties(object o)
        {
            if (o is AvaloniaObject ao)
            {
                return AvaloniaPropertyRegistry.Instance.GetRegistered(ao)
                    .Union(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(ao.GetType()))
                    .Select(x => new AvaloniaPropertyViewModel(ao, x));
            }
            else
            {
                return Enumerable.Empty<AvaloniaPropertyViewModel>();
            }
        }

        private static IEnumerable<PropertyViewModel> GetClrProperties(object o, bool showImplementedInterfaces)
        {
            foreach (var p in GetClrProperties(o, o.GetType()))
            {
                yield return p;
            }

            if (showImplementedInterfaces)
            {
                foreach (var i in o.GetType().GetInterfaces())
                {
                    foreach (var p in GetClrProperties(o, i))
                    {
                        yield return p;
                    }
                }
            }
        }

        private static IEnumerable<PropertyViewModel> GetClrProperties(object o, Type t)
        {
            return t.GetProperties()
                .Where(x => x.GetIndexParameters().Length == 0)
                .Select(x => new ClrPropertyViewModel(o, x));
        }

        private void ControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_propertyIndex is { } && _propertyIndex.TryGetValue(e.Property, out var properties))
            {
                foreach (var property in properties)
                {
                    property.Update();
                }
            }

            Layout?.ControlPropertyChanged(sender, e);
        }

        private void ControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null
                && _propertyIndex is { }
                && _propertyIndex.TryGetValue(e.PropertyName, out var properties))
            {
                foreach (var property in properties)
                {
                    property.Update();
                }
            }

            if (!SnapshotFrames)
            {
                Dispatcher.UIThread.Post(UpdateStyles);
            }
        }

        void IClassesChangedListener.Changed()
        {
            if (!SnapshotFrames)
            {
                Dispatcher.UIThread.Post(UpdateStyles);
            }
        }

        private void UpdateStyles()
        {
            int activeCount = 0;

            foreach (var style in AppliedFrames)
            {
                style.Update();

                if (style.IsActive)
                {
                    activeCount++;
                }
            }

            var propertyBuckets = new Dictionary<string, List<SetterViewModel>>(StringComparer.Ordinal);

            foreach (var style in AppliedFrames.Reverse())
            {
                if (!style.IsActive)
                {
                    continue;
                }

                foreach (var setter in style.Setters)
                {
                    var propertyKey = setter.Name;
                    if (propertyBuckets.TryGetValue(propertyKey, out var setters))
                    {
                        foreach (var otherSetter in setters)
                        {
                            otherSetter.IsActive = false;
                        }

                        setter.IsActive = true;

                        setters.Add(setter);
                    }
                    else
                    {
                        setter.IsActive = true;

                        setters = new List<SetterViewModel> { setter };

                        propertyBuckets.Add(propertyKey, setters);
                    }
                }
            }

            foreach (var pseudoClass in PseudoClasses)
            {
                pseudoClass.Update();
            }

            FramesStatus = $"Value Frames ({activeCount}/{AppliedFrames.Count} active)";
        }

        private bool FilterProperty(object arg)
        {
            return !(arg is PropertyViewModel property) || TreePage.PropertiesFilter.Filter(property.Name);
        }

        private class PropertyComparer : IComparer<PropertyViewModel>, IComparer
        {
            public static PropertyComparer Instance { get; } = new PropertyComparer();

            public int Compare(PropertyViewModel? x, PropertyViewModel? y)
            {
                if (x is null && y is null)
                    return 0;

                if (x is null && y is not null)
                    return -1;

                if (x is not null && y is null)
                    return 1;

                var groupX = GroupIndex(x!.Group);
                var groupY = GroupIndex(y!.Group);

                if (groupX != groupY)
                {
                    return groupX - groupY;
                }
                else
                {
                    return string.CompareOrdinal(x.Name, y.Name);
                }
            }

            private static int GroupIndex(string? group)
            {
                switch (group)
                {
                    case "Pinned":
                        return -1;
                    case "Properties":
                        return 0;
                    case "Attached Properties":
                        return 1;
                    case "CLR Properties":
                        return 2;
                    default:
                        return 3;
                }
            }

            public int Compare(object? x, object? y) =>
                Compare(x as PropertyViewModel, y as PropertyViewModel);
        }

        private static IEnumerable<PropertyInfo> GetAllPublicProperties(Type type)
        {
            return type
                .GetProperties()
                .Concat(type.GetInterfaces().SelectMany(i => i.GetProperties()));
        }

        public void NavigateToSelectedProperty()
        {
            if (_remoteReadOnly is not null)
            {
                return;
            }

            var selectedProperty = SelectedProperty;
            var selectedEntity = SelectedEntity;
            var selectedEntityName = SelectedEntityName;
            if (selectedEntity == null
                || selectedProperty == null
                || selectedProperty.PropertyType == typeof(string)
                || selectedProperty.PropertyType.IsValueType)
                return;

            object? property = null;

            switch (selectedProperty)
            {
                case AvaloniaPropertyViewModel avaloniaProperty:

                    property = (_selectedEntity as Control)?.GetValue(avaloniaProperty.Property);

                    break;

                case ClrPropertyViewModel clrProperty:
                    {
                        property = GetAllPublicProperties(selectedEntity.GetType())
                            .FirstOrDefault(pi => clrProperty.Property == pi)?
                            .GetValue(selectedEntity);

                        break;
                    }
            }

            if (property == null)
                return;

            _selectedEntitiesStack.Push((Name: selectedEntityName!, Entry: selectedEntity));

            var propertyName = selectedProperty.Name;

            //Strip out interface names
            if (propertyName.LastIndexOf('.') is var p && p != -1)
            {
                propertyName = propertyName.Substring(p + 1);
            }

            NavigateToProperty(property, selectedEntityName + "." + propertyName);

            RaisePropertyChanged(nameof(CanNavigateToParentProperty));
        }

        public void NavigateToParentProperty()
        {
            if (_remoteReadOnly is not null)
            {
                return;
            }

            if (_selectedEntitiesStack.Count > 0)
            {
                var property = _selectedEntitiesStack.Pop();
                NavigateToProperty(property.Entry, property.Name);

                RaisePropertyChanged(nameof(CanNavigateToParentProperty));
            }
        }

        protected void NavigateToProperty(object o, string? entityName)
        {
            if (_remoteReadOnly is not null)
            {
                return;
            }

            var oldSelectedEntity = SelectedEntity;

            switch (oldSelectedEntity)
            {
                case AvaloniaObject ao1:
                    ao1.PropertyChanged -= ControlPropertyChanged;
                    break;

                case INotifyPropertyChanged inpc1:
                    inpc1.PropertyChanged -= ControlPropertyChanged;
                    break;
            }

            SelectedEntity = o;
            SelectedEntityName = entityName;
            SelectedEntityType = o.ToString();
            UpdateSourceLocation(o);

            var properties = GetAvaloniaProperties(o)
                .Concat(GetClrProperties(o, _showImplementedInterfaces))
                .Do(p =>
                    {
                        p.IsPinned = _pinnedProperties.Contains(p.FullName);
                    })
                .ToArray();

            _propertyIndex = properties
                .GroupBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.ToArray());

            var view = new DataGridCollectionView(properties);
            view.GroupDescriptions.AddRange(GroupDescriptors);
            view.SortDescriptions.AddRange(SortDescriptions);
            view.Filter = FilterProperty;
            PropertiesView = view;

            switch (o)
            {
                case AvaloniaObject ao2:
                    ao2.PropertyChanged += ControlPropertyChanged;
                    break;

                case INotifyPropertyChanged inpc2:
                    inpc2.PropertyChanged += ControlPropertyChanged;
                    break;
            }
        }

        private void UpdateSourceLocation(object selectedEntity)
        {
            var sourceLocation = _sourceLocationService.ResolveObject(selectedEntity);
            XamlSourceText = sourceLocation.XamlLocation is null
                ? null
                : "XAML: " + sourceLocation.XamlLocation.DisplayText;
            CodeSourceText = sourceLocation.CodeLocation is null
                ? null
                : "C#: " + sourceLocation.CodeLocation.DisplayText;
            SourceLocationStatus = sourceLocation.Status;
        }

        internal void SelectProperty(AvaloniaProperty property)
        {
            SelectedProperty = null;

            if (SelectedEntity != _avaloniaObject)
            {
                NavigateToProperty(
                    _avaloniaObject,
                    (_avaloniaObject as Control)?.Name ?? _avaloniaObject.ToString());
            }

            if (PropertiesView is null)
            {
                return;
            }

            foreach (object o in PropertiesView)
            {
                if (o is AvaloniaPropertyViewModel propertyVm && propertyVm.Property == property)
                {
                    SelectedProperty = propertyVm;

                    break;
                }
            }
        }

        internal void UpdatePropertiesView(bool showImplementedInterfaces)
        {
            _showImplementedInterfaces = showImplementedInterfaces;
            SelectedProperty = null;
            if (_remoteReadOnly is not null)
            {
                _ = RefreshFromRemoteAsync();
                return;
            }

            NavigateToProperty(_avaloniaObject, (_avaloniaObject as Control)?.Name ?? _avaloniaObject.ToString());
        }

        public void TogglePinnedProperty(object parameter)
        {
            if (parameter is PropertyViewModel model)
            {
                var fullname = model.FullName;
                if (_pinnedProperties.Contains(fullname))
                {
                    _pinnedProperties.Remove(fullname);
                    model.IsPinned = false;
                }
                else
                {
                    _pinnedProperties.Add(fullname);
                    model.IsPinned = true;
                }
                PropertiesView?.Refresh();
            }
        }

        public void SetPropertyBreakpoint(object? parameter)
        {
            if (parameter is not AvaloniaPropertyViewModel propertyViewModel)
            {
                return;
            }

            if (SelectedEntity is not AvaloniaObject target)
            {
                target = _avaloniaObject;
            }

            if (_remoteMutation is not null)
            {
                _ = QueueRemotePropertyBreakpointAsync(target, propertyViewModel.Name);
                TreePage.MainView.ShowBreakpoints();
                return;
            }

            TreePage.MainView.BreakpointService.AddPropertyBreakpoint(
                propertyViewModel.Property,
                target,
                DescribeTarget(target));
            TreePage.MainView.ShowBreakpoints();
        }

        private static string DescribeTarget(AvaloniaObject target)
        {
            if (target is INamed named && !string.IsNullOrWhiteSpace(named.Name))
            {
                return named.Name + " (" + target.GetType().Name + ")";
            }

            return target.GetType().Name;
        }

        private async Task RefreshFromRemoteAsync()
        {
            var readOnly = _remoteReadOnly;
            if (readOnly is null)
            {
                return;
            }

            var context = _remoteContextAccessor?.Invoke() ?? (
                Scope: "combined",
                NodePath: TreePage.SelectedNodePath,
                ControlName: (_avaloniaObject as INamed)?.Name);
            var refreshVersion = Interlocked.Increment(ref _remoteRefreshVersion);
            try
            {
                var snapshot = await readOnly.GetPropertiesSnapshotAsync(
                    new RemotePropertiesSnapshotRequest
                    {
                        Scope = context.Scope,
                        NodePath = context.NodePath,
                        ControlName = context.ControlName,
                        IncludeClrProperties = _showImplementedInterfaces,
                    }).ConfigureAwait(false);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (refreshVersion != _remoteRefreshVersion)
                    {
                        return;
                    }

                    ApplyRemoteSnapshot(snapshot);
                });
            }
            catch
            {
                // Keep previous state on remote read failures.
            }
        }

        private void ApplyRemoteSnapshot(RemotePropertiesSnapshot snapshot)
        {
            var previousPropertyName = SelectedProperty?.Name;

            SelectedEntity = _avaloniaObject;
            SelectedEntityName = string.IsNullOrWhiteSpace(snapshot.Target)
                ? (_avaloniaObject as Control)?.Name ?? _avaloniaObject.ToString()
                : snapshot.Target;
            SelectedEntityType = string.IsNullOrWhiteSpace(snapshot.TargetType)
                ? _avaloniaObject.GetType().FullName ?? _avaloniaObject.GetType().Name
                : snapshot.TargetType;

            XamlSourceText = FormatSourceLabel("XAML", snapshot.Source.Xaml);
            CodeSourceText = FormatSourceLabel("C#", snapshot.Source.Code);
            SourceLocationStatus = snapshot.Source.Status;

            var properties = new PropertyViewModel[snapshot.Properties.Count];
            for (var i = 0; i < snapshot.Properties.Count; i++)
            {
                var property = snapshot.Properties[i];
                var typeText = !string.IsNullOrWhiteSpace(property.Type)
                    ? property.Type
                    : property.PropertyType;
                var propertyViewModel = new RemotePropertyViewModel(
                    name: property.Name,
                    group: property.Group,
                    displayType: typeText,
                    declaringTypeName: property.DeclaringType,
                    priority: property.Priority,
                    isAttached: property.IsAttached,
                    isReadOnly: property.IsReadOnly,
                    valueText: property.ValueText,
                    setValueCallback: OnRemotePropertyValueChanged);
                propertyViewModel.IsPinned = _pinnedProperties.Contains(propertyViewModel.FullName);
                properties[i] = propertyViewModel;
            }

            _propertyIndex = null;
            var view = new DataGridCollectionView(properties);
            view.GroupDescriptions.AddRange(GroupDescriptors);
            view.SortDescriptions.AddRange(SortDescriptions);
            view.Filter = FilterProperty;
            PropertiesView = view;

            SelectedProperty = null;
            if (!string.IsNullOrWhiteSpace(previousPropertyName))
            {
                for (var i = 0; i < properties.Length; i++)
                {
                    if (string.Equals(properties[i].Name, previousPropertyName, StringComparison.Ordinal))
                    {
                        SelectedProperty = properties[i];
                        break;
                    }
                }
            }

            AppliedFrames.Clear();
            PseudoClasses.Clear();
            for (var i = 0; i < snapshot.Frames.Count; i++)
            {
                var frame = snapshot.Frames[i];
                var setters = new List<SetterViewModel>(frame.Setters.Count);
                for (var j = 0; j < frame.Setters.Count; j++)
                {
                    var setter = frame.Setters[j];
                    var setterViewModel = new SetterViewModel(
                        setter.Name,
                        setter.ValueText,
                        setter.SourceLocation)
                    {
                        IsActive = setter.IsActive,
                        IsVisible = true,
                    };
                    setters.Add(setterViewModel);
                }

                AppliedFrames.Add(
                    new ValueFrameViewModel(
                        frame.Id,
                        frame.Description,
                        frame.IsActive,
                        frame.SourceLocation,
                        setters));
            }

            UpdateStyles();
            UpdateStyleFilters();
        }

        private static string? FormatSourceLabel(string label, string? sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return null;
            }

            return label + ": " + sourceText;
        }

        private void OnRemotePropertyValueChanged(RemotePropertyViewModel property, object? value)
        {
            if (_remoteMutation is null)
            {
                return;
            }

            _ = ApplyRemotePropertyMutationAsync(property.Name, value);
        }

        private async Task ApplyRemotePropertyMutationAsync(string propertyName, object? value)
        {
            var mutation = _remoteMutation;
            if (mutation is null)
            {
                return;
            }

            var context = _remoteContextAccessor?.Invoke() ?? (
                Scope: "combined",
                NodePath: TreePage.SelectedNodePath,
                ControlName: (_avaloniaObject as INamed)?.Name);
            try
            {
                var valueText = value switch
                {
                    null => null,
                    string text => text,
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                    _ => value.ToString(),
                };

                await mutation.SetPropertyAsync(
                    new RemoteSetPropertyRequest
                    {
                        Scope = context.Scope,
                        NodePath = context.NodePath,
                        ControlName = context.ControlName,
                        PropertyName = propertyName,
                        ValueText = valueText,
                        ValueIsNull = value is null,
                        ClearValue = false,
                    }).ConfigureAwait(false);

                await RefreshFromRemoteAsync().ConfigureAwait(false);
            }
            catch
            {
                // Keep current value when remote command fails.
            }
        }

        private void QueueRemotePseudoClassMutation(string pseudoClass, bool isActive)
        {
            if (_remoteMutation is null)
            {
                if (_avaloniaObject is StyledElement styledElement)
                {
                    styledElement.Classes.Set(pseudoClass, isActive);
                }

                return;
            }

            _ = ApplyRemotePseudoClassMutationAsync(pseudoClass, isActive);
        }

        private async Task ApplyRemotePseudoClassMutationAsync(string pseudoClass, bool isActive)
        {
            var mutation = _remoteMutation;
            if (mutation is null)
            {
                return;
            }

            var context = _remoteContextAccessor?.Invoke() ?? (
                Scope: "combined",
                NodePath: TreePage.SelectedNodePath,
                ControlName: (_avaloniaObject as INamed)?.Name);
            try
            {
                await mutation.SetPseudoClassAsync(
                    new RemoteSetPseudoClassRequest
                    {
                        Scope = context.Scope,
                        NodePath = context.NodePath,
                        ControlName = context.ControlName,
                        PseudoClass = pseudoClass,
                        IsActive = isActive,
                    }).ConfigureAwait(false);
            }
            catch
            {
                // Keep UI responsive when remote pseudo-class command fails.
            }
        }

        private async Task QueueRemotePropertyBreakpointAsync(AvaloniaObject target, string propertyName)
        {
            var mutation = _remoteMutation;
            if (mutation is null)
            {
                return;
            }

            var context = TreePage.MainView.GetRemoteTargetContext(target);
            try
            {
                await mutation.AddPropertyBreakpointAsync(
                    new RemoteAddPropertyBreakpointRequest
                    {
                        Scope = context.Scope,
                        NodePath = context.NodePath,
                        ControlName = context.ControlName,
                        PropertyName = propertyName,
                    }).ConfigureAwait(false);
            }
            catch
            {
                // Ignore remote errors; local fallback remains available when remote mutation is disabled.
            }
        }
    }
}
