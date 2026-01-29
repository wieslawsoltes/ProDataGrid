using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Styling;

namespace Avalonia.Diagnostics.ViewModels
{
    internal sealed class ResourceDetailItem
    {
        public ResourceDetailItem(string name, string? value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public string? Value { get; }
    }

    internal sealed class ResourceDetailsViewModel : ViewModelBase, IDisposable
    {
        private object? _inspectedObject;
        private DataGridCollectionView? _propertiesView;
        private PropertyViewModel? _selectedProperty;
        private Dictionary<object, PropertyViewModel[]>? _propertyIndex;
        private readonly bool _showProperties;

        public ResourceDetailsViewModel(ResourceTreeNode node, bool showImplementedInterfaces, bool showProperties = true)
        {
            _showProperties = showProperties;
            Title = node.Name;
            Subtitle = node.SecondaryText;
            Items = BuildItems(node);
            PropertiesFilter = new FilterViewModel();
            PropertiesFilter.RefreshFilter += (s, e) => PropertiesView?.Refresh();
            _inspectedObject = node.Source;

            if (_showProperties)
            {
                Inspect(node.Source, showImplementedInterfaces);
            }
        }

        public string Title { get; }
        public string? Subtitle { get; }
        public IReadOnlyList<ResourceDetailItem> Items { get; }
        public FilterViewModel PropertiesFilter { get; }

        public DataGridCollectionView? PropertiesView
        {
            get => _propertiesView;
            private set
            {
                if (RaiseAndSetIfChanged(ref _propertiesView, value))
                {
                    RaisePropertyChanged(nameof(HasProperties));
                }
            }
        }

        public PropertyViewModel? SelectedProperty
        {
            get => _selectedProperty;
            set => RaiseAndSetIfChanged(ref _selectedProperty, value);
        }

        public bool HasProperties => PropertiesView != null;

        public void UpdatePropertiesView(bool showImplementedInterfaces)
        {
            if (!_showProperties)
            {
                return;
            }

            Inspect(_inspectedObject, showImplementedInterfaces);
        }

        public void Dispose()
        {
            DetachPropertyNotifications();
        }

        private void Inspect(object? target, bool showImplementedInterfaces)
        {
            DetachPropertyNotifications();
            SelectedProperty = null;
            _propertyIndex = null;
            PropertiesView = null;
            _inspectedObject = target;

            if (target is null)
            {
                return;
            }

            var properties = BuildProperties(target, showImplementedInterfaces);
            if (properties.Count == 0)
            {
                return;
            }

            _propertyIndex = BuildPropertyIndex(properties);
            var view = new DataGridCollectionView(properties);
            view.Filter = FilterProperty;
            PropertiesView = view;

            AttachPropertyNotifications(target);
        }

        private void AttachPropertyNotifications(object target)
        {
            if (target is AvaloniaObject ao)
            {
                ao.PropertyChanged += AvaloniaPropertyChanged;
            }

            if (target is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += ClrPropertyChanged;
            }
        }

        private void DetachPropertyNotifications()
        {
            if (_inspectedObject is AvaloniaObject ao)
            {
                ao.PropertyChanged -= AvaloniaPropertyChanged;
            }

            if (_inspectedObject is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= ClrPropertyChanged;
            }
        }

        private void AvaloniaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_propertyIndex != null && _propertyIndex.TryGetValue(e.Property, out var properties))
            {
                foreach (var property in properties)
                {
                    property.Update();
                }
            }
        }

        private void ClrPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null
                && _propertyIndex != null
                && _propertyIndex.TryGetValue(e.PropertyName, out var properties))
            {
                foreach (var property in properties)
                {
                    property.Update();
                }
            }
        }

        private bool FilterProperty(object arg)
        {
            if (arg is not PropertyViewModel property)
            {
                return true;
            }

            if (PropertiesFilter.Filter(property.Name))
            {
                return true;
            }

            if (PropertiesFilter.Filter(property.Type))
            {
                return true;
            }

            var valueText = property.Value?.ToString();
            return !string.IsNullOrWhiteSpace(valueText) && PropertiesFilter.Filter(valueText);
        }

        private static List<PropertyViewModel> BuildProperties(object target, bool showImplementedInterfaces)
        {
            var properties = new List<PropertyViewModel>();

            properties.AddRange(GetAvaloniaProperties(target));
            properties.AddRange(GetClrProperties(target, showImplementedInterfaces));
            properties.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));

            return properties;
        }

        private static IEnumerable<PropertyViewModel> GetAvaloniaProperties(object o)
        {
            if (o is AvaloniaObject ao)
            {
                return AvaloniaPropertyRegistry.Instance.GetRegistered(ao)
                    .Union(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(ao.GetType()))
                    .Select(x => new AvaloniaPropertyViewModel(ao, x));
            }

            return Enumerable.Empty<AvaloniaPropertyViewModel>();
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
            return t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.GetIndexParameters().Length == 0)
                .Select(x => new ClrPropertyViewModel(o, x));
        }

        private static Dictionary<object, PropertyViewModel[]> BuildPropertyIndex(IEnumerable<PropertyViewModel> properties)
        {
            var index = new Dictionary<object, List<PropertyViewModel>>();

            foreach (var property in properties)
            {
                if (!index.TryGetValue(property.Key, out var list))
                {
                    list = new List<PropertyViewModel>();
                    index[property.Key] = list;
                }

                list.Add(property);
            }

            return index.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        }

        private static IReadOnlyList<ResourceDetailItem> BuildItems(ResourceTreeNode node)
        {
            var items = new List<ResourceDetailItem>();

            switch (node)
            {
                case ResourceHostNode host:
                    items.Add(new ResourceDetailItem("Host Type", host.HostTypeName));
                    if (!string.IsNullOrWhiteSpace(host.HostName))
                    {
                        items.Add(new ResourceDetailItem("Name", host.HostName));
                    }
                    items.Add(new ResourceDetailItem("Has Resources", host.HasResources ? "Yes" : "No"));
                    items.Add(new ResourceDetailItem("Has Styles", host.HasStyles ? "Yes" : "No"));
                    items.Add(new ResourceDetailItem("DataTemplates", host.DataTemplateCount.ToString()));
                    AppendThemeVariants(items, host.Host);
                    break;
                case ResourceDictionaryNode dictionary:
                    items.Add(new ResourceDetailItem("Entries", dictionary.EntryCount.ToString()));
                    items.Add(new ResourceDetailItem("Deferred Entries", CountDeferredEntries(dictionary.Dictionary).ToString()));
                    items.Add(new ResourceDetailItem("Merged Dictionaries", dictionary.MergedCount.ToString()));
                    items.Add(new ResourceDetailItem("Theme Dictionaries", dictionary.ThemeCount.ToString()));
                    if (!string.IsNullOrWhiteSpace(dictionary.OwnerTypeName))
                    {
                        items.Add(new ResourceDetailItem("Owner", dictionary.OwnerTypeName));
                    }
                    AppendSource(items, dictionary.Dictionary);
                    break;
                case ResourceStylesNode styles:
                    items.Add(new ResourceDetailItem("Style Count", styles.StyleCount.ToString()));
                    items.Add(new ResourceDetailItem("Has Resources", styles.HasResources ? "Yes" : "No"));
                    AppendSource(items, styles.Styles);
                    break;
                case ResourceStyleNode style:
                    items.Add(new ResourceDetailItem("Style Type", style.StyleTypeName));
                    items.Add(new ResourceDetailItem("Description", style.StyleDescription));
                    if (style.Style is ControlTheme controlTheme)
                    {
                        items.Add(new ResourceDetailItem("Target Type", controlTheme.TargetType?.Name));
                        items.Add(new ResourceDetailItem("Based On", controlTheme.BasedOn?.TargetType?.Name));
                    }
                    else if (style.Style is Style selectorStyle)
                    {
                        items.Add(new ResourceDetailItem("Selector", selectorStyle.Selector?.ToString()));
                    }
                    items.Add(new ResourceDetailItem("Has Resources", style.HasResources ? "Yes" : "No"));
                    items.Add(new ResourceDetailItem("Child Styles", style.ChildStyleCount.ToString()));
                    AppendSource(items, style.Style);
                    break;
                case ResourceStyleLeafNode styleLeaf:
                    items.Add(new ResourceDetailItem("Style Type", styleLeaf.StyleTypeName));
                    break;
                case ResourceDataTemplatesNode templates:
                    items.Add(new ResourceDetailItem("Template Count", templates.TemplateCount.ToString()));
                    break;
                case ResourceDataTemplateNode template:
                    items.Add(new ResourceDetailItem("Template Type", template.TemplateTypeName));
                    items.Add(new ResourceDetailItem("Data Type", template.DataTypeName ?? "(none)"));
                    items.Add(new ResourceDetailItem("Recycling", template.IsRecycling ? "Yes" : "No"));
                    items.Add(new ResourceDetailItem("Tree Template", template.IsTreeTemplate ? "Yes" : "No"));
                    if (!string.IsNullOrWhiteSpace(template.Description))
                    {
                        items.Add(new ResourceDetailItem("Description", template.Description));
                    }
                    break;
                case ResourceEntryProviderNode entryProvider:
                    items.Add(new ResourceDetailItem("Key", entryProvider.KeyDisplay));
                    items.Add(new ResourceDetailItem("Key Type", entryProvider.KeyTypeName));
                    items.Add(new ResourceDetailItem("Provider Type", entryProvider.ProviderTypeName));
                    items.Add(new ResourceDetailItem("Deferred", entryProvider.IsDeferred ? "Yes" : "No"));
                    if (!string.IsNullOrWhiteSpace(entryProvider.ValuePreviewText))
                    {
                        items.Add(new ResourceDetailItem("Value", entryProvider.ValuePreviewText));
                    }
                    AppendSource(items, entryProvider.Provider);
                    break;
                case ResourceEntryNode entry:
                    items.Add(new ResourceDetailItem("Key", entry.KeyDisplay));
                    items.Add(new ResourceDetailItem("Key Type", entry.KeyTypeName));
                    items.Add(new ResourceDetailItem("Value Type", entry.ValueTypeName));
                    items.Add(new ResourceDetailItem("Deferred", entry.IsDeferred ? "Yes" : "No"));
                    items.Add(new ResourceDetailItem("Value", entry.ValuePreviewText));
                    break;
                case ResourceThemeVariantNode variant:
                    items.Add(new ResourceDetailItem("Variant", variant.VariantDisplay));
                    items.Add(new ResourceDetailItem("Provider", variant.Provider.GetType().Name));
                    break;
                case ResourceMergedDictionariesNode merged:
                    items.Add(new ResourceDetailItem("Count", merged.ProviderCount.ToString()));
                    break;
                case ResourceThemeDictionariesNode theme:
                    items.Add(new ResourceDetailItem("Count", theme.ProviderCount.ToString()));
                    break;
                case ResourceProviderNode provider:
                    items.Add(new ResourceDetailItem("Provider Type", provider.ProviderTypeName));
                    AppendSource(items, provider.Provider);
                    break;
            }

            return items;
        }

        private static void AppendSource(ICollection<ResourceDetailItem> items, object? source)
        {
            var sourceText = TryGetSourceText(source);
            if (!string.IsNullOrWhiteSpace(sourceText))
            {
                items.Add(new ResourceDetailItem("Source", sourceText));
            }
        }

        private static string? TryGetSourceText(object? target)
        {
            if (target is null)
            {
                return null;
            }

            var property = target.GetType().GetProperty("Source", BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return null;
            }

            var value = property.GetValue(target);
            if (value is Uri uri)
            {
                return uri.ToString();
            }

            if (value is string text)
            {
                return text;
            }

            return null;
        }

        private static int CountDeferredEntries(IResourceDictionary dictionary)
        {
            var count = 0;

            foreach (var entry in dictionary)
            {
                if (entry.Value is IDeferredContent)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AppendThemeVariants(ICollection<ResourceDetailItem> items, IResourceHost host)
        {
            if (host is IThemeVariantHost themeHost)
            {
                items.Add(new ResourceDetailItem("Actual Theme", themeHost.ActualThemeVariant.ToString()));
            }

            switch (host)
            {
                case Application app:
                    items.Add(new ResourceDetailItem("Requested Theme", FormatThemeVariant(app.RequestedThemeVariant)));
                    break;
                case TopLevel topLevel:
                    items.Add(new ResourceDetailItem("Requested Theme", FormatThemeVariant(topLevel.RequestedThemeVariant)));
                    break;
                case ThemeVariantScope scope:
                    items.Add(new ResourceDetailItem("Requested Theme", FormatThemeVariant(scope.RequestedThemeVariant)));
                    break;
            }
        }

        private static string FormatThemeVariant(ThemeVariant? variant)
        {
            return variant?.ToString() ?? "Default";
        }
    }
}
