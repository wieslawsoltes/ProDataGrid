using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Markup.Xaml.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace Avalonia.Diagnostics.Services
{
    internal sealed record ResourceReferenceCandidate(
        object Key,
        string KeyText,
        object? Value,
        Type ValueType,
        string ScopePath,
        string? ThemeVariant,
        DevToolsResourceReferenceKind Kind)
    {
        public string ReferenceText => ResourceReferenceTextFormatter.Format(Kind, KeyText);

        public override string ToString()
        {
            var suffix = ThemeVariant is { Length: > 0 }
                ? $"{ValueType.Name}, {ThemeVariant}"
                : ValueType.Name;

            return $"{ReferenceText} ({suffix})";
        }
    }

    internal static class ResourceReferenceTextFormatter
    {
        public static string Format(DevToolsResourceReferenceKind kind, string keyText)
        {
            var extensionName = kind == DevToolsResourceReferenceKind.Dynamic
                ? "DynamicResource"
                : "StaticResource";

            return $"{{{extensionName} {keyText}}}";
        }
    }

    internal sealed class ResourceReferenceSuggestionService
    {
        private readonly IResourceNodeFormatter _formatter = new ResourceNodeFormatter();

        public IReadOnlyList<ResourceReferenceCandidate> GetCandidates(PropertyViewModel viewModel)
        {
            if (viewModel.IsReadonly ||
                viewModel.InspectedObject is not IResourceHost resourceHost)
            {
                return Array.Empty<ResourceReferenceCandidate>();
            }

            var targetType = Nullable.GetUnderlyingType(viewModel.PropertyType) ?? viewModel.PropertyType;
            var themeVariant = (resourceHost as IThemeVariantHost)?.ActualThemeVariant;
            var resources = new List<ResourceReferenceCandidate>();
            var seenKeys = new HashSet<object>();

            foreach (var host in EnumerateResourceHosts(resourceHost))
            {
                CollectHostResources(host, resourceHost, targetType, themeVariant, viewModel, resources, seenKeys);
            }

            resources.Sort(static (left, right) =>
            {
                var keyComparison = string.Compare(left.KeyText, right.KeyText, StringComparison.OrdinalIgnoreCase);
                if (keyComparison != 0)
                {
                    return keyComparison;
                }

                return left.Kind.CompareTo(right.Kind);
            });

            return resources;
        }

        private static IEnumerable<IResourceHost> EnumerateResourceHosts(IResourceHost start)
        {
            var seen = new HashSet<IResourceHost>();
            IResourceHost? current = start;

            while (current != null && seen.Add(current))
            {
                yield return current;
                current = (current as IStyleHost)?.StylingParent as IResourceHost;
            }

            if (Application.Current is IResourceHost applicationHost && seen.Add(applicationHost))
            {
                yield return applicationHost;
            }
        }

        private void CollectHostResources(
            IResourceHost host,
            IResourceHost lookupHost,
            Type targetType,
            ThemeVariant? themeVariant,
            PropertyViewModel viewModel,
            List<ResourceReferenceCandidate> resources,
            HashSet<object> seenKeys)
        {
            if (host is StyledElement styledElement)
            {
                CollectDictionary(
                    styledElement.Resources,
                    lookupHost,
                    targetType,
                    themeVariant,
                    viewModel,
                    _formatter.FormatHostName(host),
                    resources,
                    seenKeys);
            }
            else if (host is Application application)
            {
                CollectDictionary(
                    application.Resources,
                    lookupHost,
                    targetType,
                    themeVariant,
                    viewModel,
                    _formatter.FormatHostName(host),
                    resources,
                    seenKeys);
            }

            if (host is IStyleHost { IsStylesInitialized: true } styleHost)
            {
                CollectStyles(
                    styleHost.Styles,
                    lookupHost,
                    targetType,
                    themeVariant,
                    viewModel,
                    $"{_formatter.FormatHostName(host)} / Styles",
                    resources,
                    seenKeys);
            }
        }

        private void CollectProvider(
            IResourceProvider provider,
            IResourceHost lookupHost,
            Type targetType,
            ThemeVariant? themeVariant,
            PropertyViewModel viewModel,
            string scopePath,
            List<ResourceReferenceCandidate> resources,
            HashSet<object> seenKeys)
        {
            switch (provider)
            {
                case IResourceDictionary dictionary:
                    CollectDictionary(dictionary, lookupHost, targetType, themeVariant, viewModel, scopePath, resources, seenKeys);
                    break;

                case Styles styles:
                    CollectStyles(styles, lookupHost, targetType, themeVariant, viewModel, scopePath, resources, seenKeys);
                    break;

                case StyleBase style:
                    CollectStyle(style, lookupHost, targetType, themeVariant, viewModel, scopePath, resources, seenKeys);
                    break;
            }
        }

        private void CollectDictionary(
            IResourceDictionary dictionary,
            IResourceHost lookupHost,
            Type targetType,
            ThemeVariant? themeVariant,
            PropertyViewModel viewModel,
            string scopePath,
            List<ResourceReferenceCandidate> resources,
            HashSet<object> seenKeys)
        {
            foreach (var entry in dictionary)
            {
                AddEntry(entry.Key, lookupHost, targetType, themeVariant, viewModel, scopePath, null, resources, seenKeys);
            }

            foreach (var themeDictionary in EnumerateThemeDictionaries(dictionary, themeVariant))
            {
                var themeScope = themeDictionary.Key.ToString();
                CollectProvider(
                    themeDictionary.Value,
                    lookupHost,
                    targetType,
                    themeVariant,
                    viewModel,
                    $"{scopePath} / {themeScope}",
                    resources,
                    seenKeys);
            }

            for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var provider = dictionary.MergedDictionaries[i];
                CollectProvider(
                    provider,
                    lookupHost,
                    targetType,
                    themeVariant,
                    viewModel,
                    $"{scopePath} / {_formatter.FormatProviderName(provider)}",
                    resources,
                    seenKeys);
            }
        }

        private static IEnumerable<KeyValuePair<ThemeVariant, IThemeVariantProvider>> EnumerateThemeDictionaries(
            IResourceDictionary dictionary,
            ThemeVariant? themeVariant)
        {
            var seen = new HashSet<ThemeVariant>();
            var current = themeVariant;

            while (current != null && current != ThemeVariant.Default)
            {
                if (seen.Add(current) && dictionary.ThemeDictionaries.TryGetValue(current, out var provider))
                {
                    yield return new KeyValuePair<ThemeVariant, IThemeVariantProvider>(current, provider);
                }

                current = current.InheritVariant;
            }

            if (seen.Add(ThemeVariant.Default) &&
                dictionary.ThemeDictionaries.TryGetValue(ThemeVariant.Default, out var defaultProvider))
            {
                yield return new KeyValuePair<ThemeVariant, IThemeVariantProvider>(ThemeVariant.Default, defaultProvider);
            }
        }

        private void CollectStyles(
            Styles styles,
            IResourceHost lookupHost,
            Type targetType,
            ThemeVariant? themeVariant,
            PropertyViewModel viewModel,
            string scopePath,
            List<ResourceReferenceCandidate> resources,
            HashSet<object> seenKeys)
        {
            CollectDictionary(
                styles.Resources,
                lookupHost,
                targetType,
                themeVariant,
                viewModel,
                scopePath,
                resources,
                seenKeys);

            for (var i = styles.Count - 1; i >= 0; i--)
            {
                if (styles[i] is IResourceProvider provider)
                {
                    CollectProvider(
                        provider,
                        lookupHost,
                        targetType,
                        themeVariant,
                        viewModel,
                        $"{scopePath} / {_formatter.FormatProviderName(provider)}",
                        resources,
                        seenKeys);
                }
            }
        }

        private void CollectStyle(
            StyleBase style,
            IResourceHost lookupHost,
            Type targetType,
            ThemeVariant? themeVariant,
            PropertyViewModel viewModel,
            string scopePath,
            List<ResourceReferenceCandidate> resources,
            HashSet<object> seenKeys)
        {
            CollectDictionary(
                style.Resources,
                lookupHost,
                targetType,
                themeVariant,
                viewModel,
                scopePath,
                resources,
                seenKeys);

            foreach (var child in style.Children)
            {
                if (child is IResourceProvider provider)
                {
                    CollectProvider(
                        provider,
                        lookupHost,
                        targetType,
                        themeVariant,
                        viewModel,
                        $"{scopePath} / {_formatter.FormatProviderName(provider)}",
                        resources,
                        seenKeys);
                }
            }
        }

        private static void AddEntry(
            object key,
            IResourceHost lookupHost,
            Type targetType,
            ThemeVariant? themeVariant,
            PropertyViewModel viewModel,
            string scopePath,
            string? entryThemeVariant,
            List<ResourceReferenceCandidate> resources,
            HashSet<object> seenKeys)
        {
            if (!seenKeys.Add(key) ||
                !lookupHost.TryFindResource(key, themeVariant, out var value) ||
                ReferenceEquals(value, AvaloniaProperty.UnsetValue))
            {
                return;
            }

            var convertedValue = ColorToBrushConverter.Convert(value, targetType);
            if (!IsCompatibleValue(viewModel, targetType, convertedValue))
            {
                return;
            }

            var keyText = FormatKeyForXaml(key);
            resources.Add(new ResourceReferenceCandidate(
                key,
                keyText,
                convertedValue,
                convertedValue?.GetType() ?? typeof(object),
                scopePath,
                entryThemeVariant,
                DevToolsResourceReferenceKind.Static));

            if (viewModel.SupportsDynamicResourceReferences)
            {
                resources.Add(new ResourceReferenceCandidate(
                    key,
                    keyText,
                    convertedValue,
                    convertedValue?.GetType() ?? typeof(object),
                    scopePath,
                    entryThemeVariant,
                    DevToolsResourceReferenceKind.Dynamic));
            }
        }

        private static bool IsCompatibleValue(PropertyViewModel viewModel, Type targetType, object? value)
        {
            if (value is null)
            {
                return !targetType.IsValueType || Nullable.GetUnderlyingType(viewModel.PropertyType) != null;
            }

            if (targetType == typeof(object) || targetType.IsAssignableFrom(value.GetType()))
            {
                return IsCompatibleControlTheme(viewModel, value);
            }

            return false;
        }

        private static bool IsCompatibleControlTheme(PropertyViewModel viewModel, object value)
        {
            if (value is not ControlTheme { TargetType: { } targetType } ||
                viewModel.InspectedObject is not StyledElement styledElement)
            {
                return true;
            }

            return targetType.IsAssignableFrom(styledElement.StyleKey);
        }

        private static string FormatKeyForXaml(object key)
        {
            if (key is Type type)
            {
                return $"{{x:Type {type.Name}}}";
            }

            var text = key.ToString() ?? string.Empty;
            return CanUsePositionalKey(text)
                ? text
                : $"ResourceKey='{text.Replace("'", "&apos;", StringComparison.Ordinal)}'";
        }

        private static bool CanUsePositionalKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text[0] == '{')
            {
                return false;
            }

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
