// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ProDataGrid.SourceGenerators;

internal static partial class Discovery
{
    private static ImmutableArray<ViewModelViewModel> DiscoverViews(
        Compilation compilation,
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        ImmutableArray<AttributeData> assemblyAttributes,
        ImmutableArray<ViewModelModel> generatedViewModels,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var requests = new List<ViewRequest>();

        foreach (AttributeData attribute in assemblyAttributes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsAttribute(attribute, ProDataGridGenerator.GenerateViewsForNamespaceAttributeName))
            {
                DiscoverNamespaceViewRequests(sourceTypes, attribute, requests, diagnostics);
            }
            else if (IsAttribute(attribute, ProDataGridGenerator.GenerateViewAttributeName))
            {
                INamedTypeSymbol? viewModelType = GetConstructorType(attribute, 0);
                INamedTypeSymbol? itemType = GetConstructorType(attribute, 1);
                if (viewModelType == null || itemType == null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidTarget,
                        GetLocation(attribute),
                        viewModelType?.ToDisplayString() ?? "(unknown)",
                        "assembly-level view generation requires view-model and item types"));
                    continue;
                }

                requests.Add(CreateViewRequest(viewModelType, itemType, attribute));
            }
        }

        bool hasGlobalViewPolicies = HasGlobalViewPolicies(assemblyAttributes);
        if (hasGlobalViewPolicies)
        {
            foreach (INamedTypeSymbol viewModelType in sourceTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (AttributeData attribute in viewModelType.GetAttributes())
                {
                    if (!IsAttribute(attribute, ProDataGridGenerator.GenerateViewAttributeName))
                    {
                        continue;
                    }

                    INamedTypeSymbol? itemType = GetConstructorType(attribute, 0);
                    if (itemType == null)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            GeneratorDiagnostics.InvalidTarget,
                            GetLocation(attribute),
                            viewModelType.ToDisplayString(),
                            "view generation requires an item type"));
                        continue;
                    }

                    requests.Add(CreateViewRequest(viewModelType, itemType, attribute));
                }
            }
        }

        var generatedTypes = new HashSet<INamedTypeSymbol>(
            generatedViewModels.Select(static model => model.ViewModelType),
            SymbolEqualityComparer.Default);
        var views = ImmutableArray.CreateBuilder<ViewModelViewModel>();
        foreach (ViewRequest request in requests
                     .GroupBy(static request => request.ViewNamespace + "." + request.ViewName, StringComparer.Ordinal)
                     .Select(static group => group.Last())
                     .OrderBy(static request => GeneratorUtilities.GetMetadataName(request.ViewModelType), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ViewModelViewModel? view = ResolveView(compilation, request, generatedTypes, diagnostics);
            if (view != null)
            {
                views.Add(view);
            }
        }

        return views.ToImmutable();
    }

    public static DirectViewCandidate? CreateDirectViewCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol viewModelType ||
            viewModelType.TypeKind != TypeKind.Class ||
            HasGlobalViewPolicies(viewModelType.ContainingAssembly.GetAttributes()))
        {
            return null;
        }

        Compilation compilation = context.SemanticModel.Compilation;
        ImmutableArray<AttributeData> assemblyAttributes = viewModelType.ContainingAssembly.GetAttributes();
        var existingViewTypes = ImmutableArray.CreateBuilder<string>();
        foreach (AttributeData attribute in context.Attributes)
        {
            INamedTypeSymbol? itemType = GetConstructorType(attribute, 0);
            if (itemType == null)
            {
                continue;
            }

            ViewRequest request = CreateViewRequest(viewModelType, itemType, attribute);
            string metadataName = GetViewMetadataName(request);
            if (compilation.GetTypeByMetadataName(metadataName) != null)
            {
                existingViewTypes.Add(metadataName);
            }
        }

        bool isGeneratedViewModel = IsGeneratedViewModel(viewModelType, assemblyAttributes);
        bool hasAvaloniaUserControl = compilation.GetTypeByMetadataName("Avalonia.Controls.UserControl") != null;
        bool hasReactiveUserControl = compilation.GetTypeByMetadataName("ReactiveUI.Avalonia.ReactiveUserControl`1") != null;
        ImmutableArray<string> orderedExistingViewTypes = existingViewTypes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static metadataName => metadataName, StringComparer.Ordinal)
            .ToImmutableArray();
        string cacheKey = CreateDirectSchemaCacheKey(viewModelType, context.Attributes) +
            "|generated-view-model:" + isGeneratedViewModel +
            "|avalonia-user-control:" + hasAvaloniaUserControl +
            "|reactive-user-control:" + hasReactiveUserControl +
            "|existing-view-types:" + string.Join(";", orderedExistingViewTypes);

        return new DirectViewCandidate
        {
            ViewModelType = viewModelType,
            Attributes = context.Attributes,
            ExistingViewTypes = orderedExistingViewTypes,
            IsGeneratedViewModel = isGeneratedViewModel,
            HasAvaloniaUserControl = hasAvaloniaUserControl,
            HasReactiveUserControl = hasReactiveUserControl,
            CacheKey = cacheKey
        };
    }

    public static DirectViewGenerationResult BuildDirectViews(
        ImmutableArray<DirectViewCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var requests = new List<KeyValuePair<ViewRequest, DirectViewCandidate>>();
        var generatedViewModels = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (DirectViewCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate.IsGeneratedViewModel)
            {
                generatedViewModels.Add(candidate.ViewModelType);
            }

            foreach (AttributeData attribute in candidate.Attributes)
            {
                INamedTypeSymbol? itemType = GetConstructorType(attribute, 0);
                if (itemType == null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidTarget,
                        GetLocation(attribute),
                        candidate.ViewModelType.ToDisplayString(),
                        "view generation requires an item type"));
                    continue;
                }

                requests.Add(new KeyValuePair<ViewRequest, DirectViewCandidate>(
                    CreateViewRequest(candidate.ViewModelType, itemType, attribute),
                    candidate));
            }
        }

        var sources = ImmutableArray.CreateBuilder<GeneratedSource>();
        foreach (KeyValuePair<ViewRequest, DirectViewCandidate> entry in requests
                     .GroupBy(static entry => GetViewMetadataName(entry.Key), StringComparer.Ordinal)
                     .Select(static group => group.Last())
                     .OrderBy(static entry => GeneratorUtilities.GetMetadataName(entry.Key.ViewModelType), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ViewRequest request = entry.Key;
            DirectViewCandidate candidate = entry.Value;
            bool frameworkAvailable = request.BaseType != null ||
                (request.Framework == ViewFrameworkModel.ReactiveUI
                    ? candidate.HasReactiveUserControl
                    : candidate.HasAvaloniaUserControl);
            ViewModelViewModel? view = ResolveView(
                request,
                generatedViewModels.Contains(request.ViewModelType),
                candidate.ExistingViewTypes.Contains(GetViewMetadataName(request), StringComparer.Ordinal),
                frameworkAvailable,
                diagnostics);
            if (view != null)
            {
                sources.Add(Emitter.EmitViewSource(view));
            }
        }

        return new DirectViewGenerationResult(sources.ToImmutable(), diagnostics.ToImmutable());
    }

    private static bool HasGlobalViewPolicies(ImmutableArray<AttributeData> assemblyAttributes)
    {
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (IsAttribute(attribute, ProDataGridGenerator.GenerateViewAttributeName) ||
                IsAttribute(attribute, ProDataGridGenerator.GenerateViewsForNamespaceAttributeName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedViewModel(
        INamedTypeSymbol viewModelType,
        ImmutableArray<AttributeData> assemblyAttributes)
    {
        if (viewModelType.GetAttributes().Any(static attribute =>
                IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelAttributeName)))
        {
            return true;
        }

        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelAttributeName) &&
                SymbolEqualityComparer.Default.Equals(GetConstructorType(attribute, 0), viewModelType))
            {
                return true;
            }

            if (!IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelsForNamespaceAttributeName))
            {
                continue;
            }

            string? namespaceName = GetConstructorString(attribute, 0);
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            bool includeNested = GeneratorUtilities.GetBoolean(arguments, "IncludeNestedNamespaces", true);
            if (!string.IsNullOrWhiteSpace(namespaceName) &&
                NamespaceMatches(viewModelType, namespaceName!, includeNested))
            {
                return true;
            }
        }

        return false;
    }

    private static void DiscoverNamespaceViewRequests(
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        AttributeData attribute,
        List<ViewRequest> requests,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? namespaceName = GetConstructorString(attribute, 0);
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.InvalidNamespace, GetLocation(attribute), namespaceName ?? string.Empty));
            return;
        }

        Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
        bool includeNested = GeneratorUtilities.GetBoolean(arguments, "IncludeNestedNamespaces", true);
        string itemsPropertyName = GeneratorUtilities.GetString(arguments, "ItemsPropertyName") ?? "Items";
        INamedTypeSymbol[] matches = sourceTypes
            .Where(type => type.TypeKind == TypeKind.Class && NamespaceMatches(type, namespaceName!, includeNested))
            .ToArray();
        if (matches.Length == 0)
        {
            diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.InvalidNamespace, GetLocation(attribute), namespaceName));
        }

        foreach (INamedTypeSymbol viewModelType in matches)
        {
            INamedTypeSymbol? itemType = InferItemType(viewModelType, itemsPropertyName);
            if (itemType == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.AmbiguousItemsProperty,
                    GeneratorUtilities.GetLocation(viewModelType),
                    viewModelType.ToDisplayString(),
                    itemsPropertyName));
                continue;
            }

            requests.Add(CreateViewRequest(viewModelType, itemType, attribute));
        }
    }

    private static ViewRequest CreateViewRequest(
        INamedTypeSymbol viewModelType,
        INamedTypeSymbol itemType,
        AttributeData attribute)
    {
        Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
        string defaultName = viewModelType.Name.EndsWith("ViewModel", StringComparison.Ordinal)
            ? viewModelType.Name.Substring(0, viewModelType.Name.Length - "ViewModel".Length) + "View"
            : viewModelType.Name + "View";
        string viewModelNamespace = viewModelType.ContainingNamespace?.IsGlobalNamespace == false
            ? viewModelType.ContainingNamespace.ToDisplayString()
            : string.Empty;
        string defaultNamespace = viewModelNamespace.EndsWith(".ViewModels", StringComparison.Ordinal)
            ? viewModelNamespace.Substring(0, viewModelNamespace.Length - ".ViewModels".Length) + ".Views"
            : viewModelNamespace;

        return new ViewRequest
        {
            ViewModelType = viewModelType,
            ItemType = itemType,
            ViewName = GeneratorUtilities.SanitizeIdentifier(GeneratorUtilities.GetString(arguments, "ViewName") ?? defaultName),
            ViewNamespace = GeneratorUtilities.GetString(arguments, "ViewNamespace") ?? defaultNamespace,
            Framework = GetViewFramework(arguments),
            BaseType = GeneratorUtilities.GetType(arguments, "BaseType"),
            Title = GeneratorUtilities.GetString(arguments, "Title") ?? SplitWords(defaultName.Replace("View", string.Empty)),
            ItemsPropertyName = GeneratorUtilities.GetString(arguments, "ItemsPropertyName") ?? "Items",
            ColumnDefinitionsPropertyName = GeneratorUtilities.GetString(arguments, "ColumnDefinitionsPropertyName") ?? "ColumnDefinitions",
            FastPathOptionsPropertyName = GeneratorUtilities.GetString(arguments, "FastPathOptionsPropertyName") ?? "FastPathOptions",
            LayoutModelPropertyName = GeneratorUtilities.GetString(arguments, "LayoutModelPropertyName"),
            Layout = GetEnumValue(arguments, "Layout", 0),
            LayoutOrientation = GetEnumValue(arguments, "LayoutOrientation", 0),
            LayoutSpacing = GeneratorUtilities.GetDouble(arguments, "LayoutSpacing", 0),
            LayoutHorizontalSpacing = GeneratorUtilities.GetDouble(arguments, "LayoutHorizontalSpacing", 0),
            LayoutVerticalSpacing = GeneratorUtilities.GetDouble(arguments, "LayoutVerticalSpacing", 0),
            LayoutMinItemWidth = GeneratorUtilities.GetDouble(arguments, "LayoutMinItemWidth", double.NaN),
            LayoutMinItemHeight = GeneratorUtilities.GetDouble(arguments, "LayoutMinItemHeight", double.NaN),
            LayoutMaximumRowsOrColumns = GeneratorUtilities.GetInt32(arguments, "LayoutMaximumRowsOrColumns", int.MaxValue),
            LayoutItemsJustification = GetEnumValue(arguments, "LayoutItemsJustification", 0),
            LayoutItemsStretch = GetEnumValue(arguments, "LayoutItemsStretch", 0),
            LayoutDisableVirtualization = GeneratorUtilities.GetBoolean(arguments, "LayoutDisableVirtualization", false),
            LayoutMaximumCachedLines = GeneratorUtilities.GetInt32(arguments, "LayoutMaximumCachedLines", 256),
            SortingModelPropertyName = GeneratorUtilities.GetString(arguments, "SortingModelPropertyName"),
            FilteringModelPropertyName = GeneratorUtilities.GetString(arguments, "FilteringModelPropertyName"),
            HierarchyFilterPolicy = GetEnumValue(arguments, "HierarchyFilterPolicy", 1),
            SearchModelPropertyName = GeneratorUtilities.GetString(arguments, "SearchModelPropertyName"),
            SearchTextPropertyName = GeneratorUtilities.GetString(arguments, "SearchTextPropertyName"),
            SelectionModelPropertyName = GeneratorUtilities.GetString(arguments, "SelectionModelPropertyName"),
            NavigationModelPropertyName = GeneratorUtilities.GetString(arguments, "NavigationModelPropertyName"),
            RouteNavigationModelPropertyName = GeneratorUtilities.GetString(arguments, "RouteNavigationModelPropertyName"),
            NavigationInputModelPropertyName = GeneratorUtilities.GetString(arguments, "NavigationInputModelPropertyName"),
            RouteContextFactoryPropertyName = GeneratorUtilities.GetString(arguments, "RouteContextFactoryPropertyName"),
            SelectionMode = GetEnumValue(arguments, "SelectionMode", 1),
            SelectionUnit = GetEnumValue(arguments, "SelectionUnit", 0),
            HasSelectionConfiguration = arguments.Keys.Any(static key =>
                key is "SelectionModelPropertyName" or "SelectionMode" or "SelectionUnit"),
            ClipboardImportModelPropertyName = GeneratorUtilities.GetString(arguments, "ClipboardImportModelPropertyName"),
            FillModelPropertyName = GeneratorUtilities.GetString(arguments, "FillModelPropertyName"),
            FormulaModelPropertyName = GeneratorUtilities.GetString(arguments, "FormulaModelPropertyName"),
            ConditionalFormattingModelPropertyName = GeneratorUtilities.GetString(arguments, "ConditionalFormattingModelPropertyName"),
            EditTriggers = GetEnumValue(arguments, "EditTriggers", 9),
            RestrictTextInputEditToCells = GeneratorUtilities.GetBoolean(arguments, "RestrictTextInputEditToCells", false),
            RequiredPointerEditModifiers = GetEnumValue(arguments, "RequiredPointerEditModifiers", 0),
            RequireExactPointerEditModifiers = GeneratorUtilities.GetBoolean(arguments, "RequireExactPointerEditModifiers", false),
            ClipboardCopyMode = GetEnumValue(arguments, "ClipboardCopyMode", 1),
            IsReadOnly = GeneratorUtilities.GetBoolean(arguments, "IsReadOnly", false),
            CanUserAddRows = GeneratorUtilities.GetBoolean(arguments, "CanUserAddRows", false),
            CanUserDeleteRows = GeneratorUtilities.GetBoolean(arguments, "CanUserDeleteRows", false),
            ShowTotalSummary = GeneratorUtilities.GetBoolean(arguments, "ShowTotalSummary", false),
            ShowGroupSummary = GeneratorUtilities.GetBoolean(arguments, "ShowGroupSummary", false),
            TotalSummaryPosition = GetEnumValue(arguments, "TotalSummaryPosition", 1),
            GroupSummaryPosition = GetEnumValue(arguments, "GroupSummaryPosition", 1),
            HierarchicalModelPropertyName = GeneratorUtilities.GetString(arguments, "HierarchicalModelPropertyName"),
            StateControllerPropertyName = GeneratorUtilities.GetString(arguments, "StateControllerPropertyName"),
            ViewStatePropertyName = GeneratorUtilities.GetString(arguments, "ViewStatePropertyName"),
            ErrorMessagePropertyName = GeneratorUtilities.GetString(arguments, "ErrorMessagePropertyName"),
            RetryCommandPropertyName = GeneratorUtilities.GetString(arguments, "RetryCommandPropertyName"),
            RoutedEvents = GetEnumValue(arguments, "RoutedEvents", 0),
            RoutedEventCommandPropertyName = GeneratorUtilities.GetString(arguments, "RoutedEventCommandPropertyName"),
            HasRoutedEventConfiguration = arguments.Keys.Any(static key =>
                key is "RoutedEvents" or "RoutedEventCommandPropertyName"),
            InteractionPropertyNames = GeneratorUtilities.GetStringArray(arguments, "InteractionPropertyNames"),
            InteractionHandlerTypes = GeneratorUtilities.GetTypeArray(arguments, "InteractionHandlerTypes"),
            HasInteractionConfiguration = arguments.Keys.Any(static key =>
                key is "InteractionPropertyNames" or "InteractionHandlerTypes"),
            NavigationInteractionPropertyName = GeneratorUtilities.GetString(arguments, "NavigationInteractionPropertyName"),
            HasNavigationInteractionConfiguration = arguments.ContainsKey("NavigationInteractionPropertyName"),
            PerformanceProfile = GetEnumValue(arguments, "PerformanceProfile", 0),
            InputMapType = GeneratorUtilities.GetType(arguments, "InputMapType"),
            InputCommandPropertyName = GeneratorUtilities.GetString(arguments, "InputCommandPropertyName"),
            DiagnosticsSinkType = GeneratorUtilities.GetType(arguments, "DiagnosticsSinkType"),
            DiagnosticsStatusPropertyName = GeneratorUtilities.GetString(arguments, "DiagnosticsStatusPropertyName"),
            ViewThemeKey = GeneratorUtilities.GetString(arguments, "ViewThemeKey"),
            DataGridThemeKey = GeneratorUtilities.GetString(arguments, "DataGridThemeKey"),
            ToolbarThemeKey = GeneratorUtilities.GetString(arguments, "ToolbarThemeKey"),
            RecipeContentThemeKey = GeneratorUtilities.GetString(arguments, "RecipeContentThemeKey"),
            ViewClasses = GeneratorUtilities.GetStringArray(arguments, "ViewClasses"),
            DataGridClasses = GeneratorUtilities.GetStringArray(arguments, "DataGridClasses"),
            ToolbarClasses = GeneratorUtilities.GetStringArray(arguments, "ToolbarClasses"),
            RecipeContentClasses = GeneratorUtilities.GetStringArray(arguments, "RecipeContentClasses"),
            LoadingText = GeneratorUtilities.GetString(arguments, "LoadingText") ?? "Loading data…",
            EmptyText = GeneratorUtilities.GetString(arguments, "EmptyText") ?? "No items to display.",
            ErrorText = GeneratorUtilities.GetString(arguments, "ErrorText") ?? "Unable to load data.",
            RetryText = GeneratorUtilities.GetString(arguments, "RetryText") ?? "Retry",
            HasViewStateConfiguration = arguments.Keys.Any(static key =>
                key is "ViewStatePropertyName" or "ErrorMessagePropertyName" or "RetryCommandPropertyName" or
                    "LoadingText" or "EmptyText" or "ErrorText" or "RetryText"),
            RowDetailsArguments = arguments,
            Recipe = GetEnumValue(arguments, "Recipe", 1),
            ControllerName = GeneratorUtilities.GetString(arguments, "ControllerName"),
            AutomationId = GeneratorUtilities.GetString(arguments, "AutomationId") ?? defaultName,
            Location = GetLocation(attribute)
        };
    }

    private static ViewModelViewModel? ResolveView(
        Compilation compilation,
        ViewRequest request,
        HashSet<INamedTypeSymbol> generatedViewModels,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string requiredFrameworkType = request.Framework == ViewFrameworkModel.ReactiveUI
            ? "ReactiveUI.Avalonia.ReactiveUserControl`1"
            : "Avalonia.Controls.UserControl";
        return ResolveView(
            request,
            generatedViewModels.Contains(request.ViewModelType),
            compilation.GetTypeByMetadataName(GetViewMetadataName(request)) != null,
            request.BaseType != null || compilation.GetTypeByMetadataName(requiredFrameworkType) != null,
            diagnostics);
    }

    private static ViewModelViewModel? ResolveView(
        ViewRequest request,
        bool generatedViewModel,
        bool targetViewTypeExists,
        bool frameworkAvailable,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (request.ViewModelType.TypeParameters.Length != 0)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidTarget,
                request.Location,
                request.ViewModelType.ToDisplayString(),
                "open generic generated views are not supported"));
            return null;
        }

        string metadataName = GetViewMetadataName(request);
        if (targetViewTypeExists)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidTarget,
                request.Location,
                metadataName,
                "a type with the generated view name already exists"));
            return null;
        }

        if (!frameworkAvailable)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.MissingViewFramework,
                request.Location,
                metadataName,
                request.Framework.ToString()));
            return null;
        }

        const int supportedKeyModifiers = 1 | 2 | 4 | 8;
        if ((request.RequiredPointerEditModifiers & ~supportedKeyModifiers) != 0)
        {
            ReportInvalidViewPerformanceIntegration(
                request,
                diagnostics,
                "RequiredPointerEditModifiers contains unsupported modifier flags");
            return null;
        }

        if (request.BaseType != null && !ValidateViewBase(request.BaseType))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidViewBase,
                request.Location,
                request.BaseType.ToDisplayString(),
                metadataName));
            return null;
        }

        bool needsReactiveActivation = request.Framework == ViewFrameworkModel.ReactiveUI &&
            (request.HasRoutedEventConfiguration || request.HasInteractionConfiguration ||
                request.HasNavigationInteractionConfiguration ||
                !string.IsNullOrWhiteSpace(request.InputCommandPropertyName) || request.DiagnosticsSinkType != null);
        if (needsReactiveActivation && request.BaseType != null &&
            !ImplementsMetadataName(request.BaseType, "ReactiveUI.IActivatableView"))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidViewBase,
                request.Location,
                request.BaseType.ToDisplayString(),
                metadataName));
            return null;
        }

        ViewBindingModel? items = ResolveViewBinding(request, request.ItemsPropertyName, generatedFallbackType: null, canUseGeneratedFallback: false, diagnostics);
        ViewBindingModel? columns = ResolveViewBinding(
            request,
            request.ColumnDefinitionsPropertyName,
            "global::Avalonia.Controls.DataGridColumnDefinitionList",
            generatedViewModel,
            diagnostics);
        ViewBindingModel? fastOptions = ResolveViewBinding(
            request,
            request.FastPathOptionsPropertyName,
            "global::Avalonia.Controls.DataGridFastPathOptions",
            generatedViewModel,
            diagnostics);
        if (items == null || columns == null || fastOptions == null)
        {
            return null;
        }

        ViewBindingModel? layoutModel = null;
        if (!string.IsNullOrWhiteSpace(request.LayoutModelPropertyName))
        {
            if (request.Layout != 0)
            {
                ReportInvalidViewPresentation(request, diagnostics, "Layout and LayoutModelPropertyName cannot be used together");
                return null;
            }
            layoutModel = ResolveValidatedViewBinding(
                request,
                request.LayoutModelPropertyName!,
                static type => ImplementsMetadataName(type, "Avalonia.Controls.DataGridLayouts.IDataGridLayoutModel"),
                "must be an accessible readable IDataGridLayoutModel property",
                diagnostics);
            if (layoutModel == null)
            {
                return null;
            }
        }
        else if (request.LayoutModelPropertyName != null)
        {
            ReportInvalidViewPresentation(request, diagnostics, "LayoutModelPropertyName cannot be empty or whitespace");
            return null;
        }

        if (request.Layout < 0 || request.Layout > 4 || request.LayoutOrientation < 0 || request.LayoutOrientation > 2 ||
            request.LayoutMaximumRowsOrColumns <= 0 || request.LayoutMaximumCachedLines <= 0)
        {
            ReportInvalidViewPresentation(request, diagnostics, "layout configuration contains an unsupported value");
            return null;
        }

        ViewBindingModel? viewState = null;
        ViewBindingModel? errorMessage = null;
        ViewBindingModel? retryCommand = null;
        ViewBindingModel? routedEventCommand = null;
        if (request.HasViewStateConfiguration)
        {
            if (string.IsNullOrWhiteSpace(request.ViewStatePropertyName))
            {
                ReportInvalidViewState(request, diagnostics, "ViewStatePropertyName is required when a state projection option is configured");
                return null;
            }

            viewState = ResolveValidatedViewBinding(
                request,
                request.ViewStatePropertyName!,
                static type => IsMetadataType(type, "Avalonia.Controls.DataGridGeneratedViewState"),
                "must be an accessible readable DataGridGeneratedViewState property",
                diagnostics);
            if (!string.IsNullOrWhiteSpace(request.ErrorMessagePropertyName))
            {
                errorMessage = ResolveValidatedViewBinding(
                    request,
                    request.ErrorMessagePropertyName!,
                    static type => type.SpecialType == SpecialType.System_String,
                    "must be an accessible readable string property",
                    diagnostics);
            }
            if (!string.IsNullOrWhiteSpace(request.RetryCommandPropertyName))
            {
                retryCommand = ResolveValidatedViewBinding(
                    request,
                    request.RetryCommandPropertyName!,
                    static type => ImplementsMetadataName(type, "System.Windows.Input.ICommand"),
                    "must be an accessible readable property implementing System.Windows.Input.ICommand",
                    diagnostics);
            }

            if (viewState == null ||
                (!string.IsNullOrWhiteSpace(request.ErrorMessagePropertyName) && errorMessage == null) ||
                (!string.IsNullOrWhiteSpace(request.RetryCommandPropertyName) && retryCommand == null))
            {
                return null;
            }
        }

        if (request.HasRoutedEventConfiguration)
        {
            const int allRoutedEvents = (1 << 12) - 1;
            if (request.RoutedEvents == 0)
            {
                ReportInvalidViewEventBridge(request, diagnostics, "RoutedEvents must select at least one supported event");
                return null;
            }
            if ((request.RoutedEvents & ~allRoutedEvents) != 0)
            {
                ReportInvalidViewEventBridge(request, diagnostics, "RoutedEvents contains unsupported flags");
                return null;
            }
            if (string.IsNullOrWhiteSpace(request.RoutedEventCommandPropertyName))
            {
                ReportInvalidViewEventBridge(request, diagnostics, "RoutedEventCommandPropertyName is required when RoutedEvents is configured");
                return null;
            }

            routedEventCommand = ResolveViewBinding(
                request,
                request.RoutedEventCommandPropertyName!,
                null,
                false,
                diagnostics);
            if (routedEventCommand == null)
            {
                return null;
            }

            ITypeSymbol? commandType = FindViewBindingMemberType(
                request.ViewModelType,
                request.RoutedEventCommandPropertyName!);
            if (commandType == null || !ImplementsMetadataName(commandType, "System.Windows.Input.ICommand"))
            {
                ReportInvalidViewEventBridge(
                    request,
                    diagnostics,
                    $"member '{request.RoutedEventCommandPropertyName}' must be an accessible readable property implementing System.Windows.Input.ICommand");
                return null;
            }
        }

        if ((request.HierarchyFilterPolicy & ~3) != 0)
        {
            ReportInvalidViewPerformanceIntegration(request, diagnostics, "HierarchyFilterPolicy contains unsupported flags");
            return null;
        }

        ImmutableArray<ViewInteractionModel> interactions = ResolveViewInteractions(request, diagnostics);
        if (request.HasInteractionConfiguration && interactions.IsDefault)
        {
            return null;
        }

        ViewBindingModel? navigationInteraction = ResolveNavigationInteraction(request, diagnostics);
        if (request.HasNavigationInteractionConfiguration && navigationInteraction == null)
        {
            return null;
        }

        if (request.PerformanceProfile < 0 || request.PerformanceProfile > 6)
        {
            ReportInvalidViewPerformanceIntegration(request, diagnostics, "PerformanceProfile contains an unsupported value");
            return null;
        }

        if (request.InputMapType != null &&
            !ValidateParameterlessImplementation(request.InputMapType, "Avalonia.Controls.IDataGridGeneratedInputMap"))
        {
            ReportInvalidViewPerformanceIntegration(
                request,
                diagnostics,
                $"input map '{request.InputMapType.ToDisplayString()}' must be an accessible non-abstract type with a parameterless constructor implementing IDataGridGeneratedInputMap");
            return null;
        }

        ViewBindingModel? inputCommand = null;
        if (!string.IsNullOrWhiteSpace(request.InputCommandPropertyName))
        {
            inputCommand = ResolveViewBinding(request, request.InputCommandPropertyName!, null, false, diagnostics);
            ITypeSymbol? commandType = FindViewBindingMemberType(request.ViewModelType, request.InputCommandPropertyName!);
            if (inputCommand == null || commandType == null || !ImplementsMetadataName(commandType, "System.Windows.Input.ICommand"))
            {
                ReportInvalidViewPerformanceIntegration(
                    request,
                    diagnostics,
                    $"member '{request.InputCommandPropertyName}' must be an accessible readable property implementing System.Windows.Input.ICommand");
                return null;
            }
        }

        if (request.DiagnosticsSinkType != null &&
            !ValidateParameterlessImplementation(request.DiagnosticsSinkType, "Avalonia.Controls.IDataGridGeneratedMetricsSink"))
        {
            ReportInvalidViewPerformanceIntegration(
                request,
                diagnostics,
                $"diagnostics sink '{request.DiagnosticsSinkType.ToDisplayString()}' must be an accessible non-abstract type with a parameterless constructor implementing IDataGridGeneratedMetricsSink");
            return null;
        }

        bool invalidPresentation = false;
        invalidPresentation |= !ValidateViewThemeKey(request, diagnostics, nameof(request.ViewThemeKey), request.ViewThemeKey);
        invalidPresentation |= !ValidateViewThemeKey(request, diagnostics, nameof(request.DataGridThemeKey), request.DataGridThemeKey);
        invalidPresentation |= !ValidateViewThemeKey(request, diagnostics, nameof(request.ToolbarThemeKey), request.ToolbarThemeKey);
        invalidPresentation |= !ValidateViewThemeKey(request, diagnostics, nameof(request.RecipeContentThemeKey), request.RecipeContentThemeKey);
        invalidPresentation |= !ValidateViewClasses(request, diagnostics, nameof(request.ViewClasses), request.ViewClasses);
        invalidPresentation |= !ValidateViewClasses(request, diagnostics, nameof(request.DataGridClasses), request.DataGridClasses);
        invalidPresentation |= !ValidateViewClasses(request, diagnostics, nameof(request.ToolbarClasses), request.ToolbarClasses);
        invalidPresentation |= !ValidateViewClasses(request, diagnostics, nameof(request.RecipeContentClasses), request.RecipeContentClasses);

        ViewBindingModel? diagnosticsStatus = null;
        if (!string.IsNullOrWhiteSpace(request.DiagnosticsStatusPropertyName))
        {
            ITypeSymbol? statusType = FindViewBindingMemberType(
                request.ViewModelType,
                request.DiagnosticsStatusPropertyName!);
            if (statusType?.SpecialType != SpecialType.System_String)
            {
                ReportInvalidViewPresentation(
                    request,
                    diagnostics,
                    $"DiagnosticsStatusPropertyName member '{request.DiagnosticsStatusPropertyName}' must be an accessible readable string property");
                invalidPresentation = true;
            }
            else
            {
                diagnosticsStatus = ResolveViewBinding(
                    request,
                    request.DiagnosticsStatusPropertyName!,
                    null,
                    false,
                    diagnostics);
                invalidPresentation |= diagnosticsStatus == null;
            }
        }
        else if (request.DiagnosticsStatusPropertyName != null)
        {
            ReportInvalidViewPresentation(
                request,
                diagnostics,
                "DiagnosticsStatusPropertyName cannot be empty or whitespace");
            invalidPresentation = true;
        }
        if (invalidPresentation)
        {
            return null;
        }

        RowDetailsViewModel? rowDetails = ResolveRowDetails(request, diagnostics);
        if (request.HasRowDetailsConfiguration && rowDetails == null)
        {
            return null;
        }
        if (request.PerformanceProfile == 6 && rowDetails?.VisibilityMode == 1)
        {
            ReportInvalidViewPerformanceIntegration(
                request,
                diagnostics,
                "HighFrequencyStreaming is incompatible with RowDetailsVisibilityMode.Visible because it realizes details for every row");
            return null;
        }

        return new ViewModelViewModel
        {
            ViewModelType = request.ViewModelType,
            ItemType = request.ItemType,
            ViewName = request.ViewName,
            ViewNamespace = request.ViewNamespace,
            Framework = request.Framework,
            BaseType = request.BaseType,
            Title = request.Title,
            Recipe = request.Recipe,
            ControllerName = request.ControllerName,
            AutomationId = request.AutomationId,
            Items = items,
            ColumnDefinitions = columns,
            FastPathOptions = fastOptions,
            LayoutModel = layoutModel,
            Layout = request.Layout,
            LayoutOrientation = request.LayoutOrientation,
            LayoutSpacing = request.LayoutSpacing,
            LayoutHorizontalSpacing = request.LayoutHorizontalSpacing,
            LayoutVerticalSpacing = request.LayoutVerticalSpacing,
            LayoutMinItemWidth = request.LayoutMinItemWidth,
            LayoutMinItemHeight = request.LayoutMinItemHeight,
            LayoutMaximumRowsOrColumns = request.LayoutMaximumRowsOrColumns,
            LayoutItemsJustification = request.LayoutItemsJustification,
            LayoutItemsStretch = request.LayoutItemsStretch,
            LayoutDisableVirtualization = request.LayoutDisableVirtualization,
            LayoutMaximumCachedLines = request.LayoutMaximumCachedLines,
            SortingModel = ResolveOptionalViewBinding(request, request.SortingModelPropertyName, diagnostics),
            FilteringModel = ResolveOptionalViewBinding(request, request.FilteringModelPropertyName, diagnostics),
            HierarchyFilterPolicy = request.HierarchyFilterPolicy,
            SearchModel = ResolveOptionalViewBinding(request, request.SearchModelPropertyName, diagnostics),
            SearchText = ResolveOptionalViewBinding(request, request.SearchTextPropertyName, diagnostics, requireSetter: true),
            SelectionModel = ResolveOptionalViewBinding(request, request.SelectionModelPropertyName, diagnostics),
            NavigationModel = ResolveNavigationModelViewBinding(
                request,
                request.NavigationModelPropertyName,
                "Avalonia.Controls.DataGridNavigation.IDataGridNavigationModel",
                "IDataGridNavigationModel",
                diagnostics),
            RouteNavigationModel = ResolveNavigationModelViewBinding(
                request,
                request.RouteNavigationModelPropertyName,
                "Avalonia.Controls.DataGridNavigation.IDataGridRouteNavigationModel",
                "IDataGridRouteNavigationModel",
                diagnostics),
            NavigationInputModel = ResolveNavigationModelViewBinding(
                request,
                request.NavigationInputModelPropertyName,
                "Avalonia.Controls.DataGridNavigation.IDataGridNavigationInputModel",
                "IDataGridNavigationInputModel",
                "GenerateNavigationInputModel",
                "NavigationInputModelPropertyName",
                true,
                diagnostics),
            RouteContextFactory = ResolveNavigationModelViewBinding(
                request,
                request.RouteContextFactoryPropertyName,
                "Avalonia.Controls.DataGridNavigation.IDataGridRouteContextFactory",
                "IDataGridRouteContextFactory",
                "GenerateRouteContextFactory",
                "RouteContextFactoryPropertyName",
                true,
                diagnostics),
            SelectionMode = request.SelectionMode,
            SelectionUnit = request.SelectionUnit,
            ConfigureSelection = request.HasSelectionConfiguration,
            ClipboardImportModel = ResolveTransferViewBinding(
                request,
                request.ClipboardImportModelPropertyName,
                "Avalonia.Controls.DataGridClipboard.IDataGridClipboardImportModel",
                "IDataGridClipboardImportModel",
                diagnostics),
            FillModel = ResolveTransferViewBinding(
                request,
                request.FillModelPropertyName,
                "Avalonia.Controls.DataGridFilling.IDataGridFillModel",
                "IDataGridFillModel",
                diagnostics),
            FormulaModel = ResolveFormulaViewBinding(request, diagnostics),
            ConditionalFormattingModel = ResolveConditionalFormattingViewBinding(request, diagnostics),
            EditTriggers = request.EditTriggers,
            RestrictTextInputEditToCells = request.RestrictTextInputEditToCells,
            RequiredPointerEditModifiers = request.RequiredPointerEditModifiers,
            RequireExactPointerEditModifiers = request.RequireExactPointerEditModifiers,
            ClipboardCopyMode = request.ClipboardCopyMode,
            IsReadOnly = request.IsReadOnly,
            CanUserAddRows = request.CanUserAddRows,
            CanUserDeleteRows = request.CanUserDeleteRows,
            ShowTotalSummary = request.ShowTotalSummary,
            ShowGroupSummary = request.ShowGroupSummary,
            TotalSummaryPosition = request.TotalSummaryPosition,
            GroupSummaryPosition = request.GroupSummaryPosition,
            HierarchicalModel = ResolveOptionalViewBinding(request, request.HierarchicalModelPropertyName, diagnostics),
            StateController = ResolveOptionalViewBinding(request, request.StateControllerPropertyName, diagnostics),
            ViewState = viewState,
            ErrorMessage = errorMessage,
            RetryCommand = retryCommand,
            RoutedEventCommand = routedEventCommand,
            RoutedEvents = request.RoutedEvents,
            Interactions = interactions.IsDefault ? ImmutableArray<ViewInteractionModel>.Empty : interactions,
            NavigationInteraction = navigationInteraction,
            PerformanceProfile = request.PerformanceProfile,
            InputMapType = request.InputMapType,
            InputCommand = inputCommand,
            DiagnosticsSinkType = request.DiagnosticsSinkType,
            DiagnosticsStatus = diagnosticsStatus,
            ViewThemeKey = request.ViewThemeKey,
            DataGridThemeKey = request.DataGridThemeKey,
            ToolbarThemeKey = request.ToolbarThemeKey,
            RecipeContentThemeKey = request.RecipeContentThemeKey,
            ViewClasses = request.ViewClasses,
            DataGridClasses = request.DataGridClasses,
            ToolbarClasses = request.ToolbarClasses,
            RecipeContentClasses = request.RecipeContentClasses,
            LoadingText = request.LoadingText,
            EmptyText = request.EmptyText,
            ErrorText = request.ErrorText,
            RetryText = request.RetryText,
            RowDetails = rowDetails,
            Location = request.Location
        };
    }

    private static ImmutableArray<ViewInteractionModel> ResolveViewInteractions(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (!request.HasInteractionConfiguration)
        {
            return ImmutableArray<ViewInteractionModel>.Empty;
        }

        if (request.Framework != ViewFrameworkModel.ReactiveUI)
        {
            ReportInvalidViewInteraction(request, diagnostics, "Interaction adapters require Framework = DataGridViewFramework.ReactiveUI");
            return default;
        }

        if (request.InteractionPropertyNames.IsDefaultOrEmpty || request.InteractionHandlerTypes.IsDefaultOrEmpty ||
            request.InteractionPropertyNames.Length != request.InteractionHandlerTypes.Length)
        {
            ReportInvalidViewInteraction(
                request,
                diagnostics,
                "InteractionPropertyNames and InteractionHandlerTypes must contain the same non-zero number of entries");
            return default;
        }

        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<ViewInteractionModel>(request.InteractionPropertyNames.Length);
        for (int index = 0; index < request.InteractionPropertyNames.Length; index++)
        {
            string propertyName = request.InteractionPropertyNames[index];
            INamedTypeSymbol handlerType = request.InteractionHandlerTypes[index];
            if (string.IsNullOrWhiteSpace(propertyName) || !seenProperties.Add(propertyName))
            {
                ReportInvalidViewInteraction(request, diagnostics, $"interaction property name '{propertyName}' is empty or duplicated");
                return default;
            }

            ITypeSymbol? propertyType = FindViewBindingMemberType(request.ViewModelType, propertyName);
            INamedTypeSymbol? interactionType = FindConstructedInterface(propertyType, "ReactiveUI.IInteraction`2");
            if (interactionType == null)
            {
                ReportInvalidViewInteraction(
                    request,
                    diagnostics,
                    $"member '{propertyName}' must be an accessible readable ReactiveUI.IInteraction<TInput, TOutput> property");
                return default;
            }

            if (!ValidateViewInteractionHandler(handlerType, interactionType.TypeArguments[0], interactionType.TypeArguments[1]))
            {
                ReportInvalidViewInteraction(
                    request,
                    diagnostics,
                    $"handler '{handlerType.ToDisplayString()}' must be an accessible non-abstract type with a parameterless constructor implementing IDataGridGeneratedViewInteractionHandler<{interactionType.TypeArguments[0].ToDisplayString()}, {interactionType.TypeArguments[1].ToDisplayString()}> exactly");
                return default;
            }

            result.Add(new ViewInteractionModel
            {
                PropertyName = propertyName,
                InputType = interactionType.TypeArguments[0],
                OutputType = interactionType.TypeArguments[1],
                HandlerType = handlerType
            });
        }

        return result.ToImmutable();
    }

    private static ViewBindingModel? ResolveNavigationInteraction(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (!request.HasNavigationInteractionConfiguration)
        {
            return null;
        }

        if (request.Framework != ViewFrameworkModel.ReactiveUI)
        {
            ReportInvalidViewInteraction(
                request,
                diagnostics,
                "NavigationInteractionPropertyName requires Framework = DataGridViewFramework.ReactiveUI");
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.NavigationInteractionPropertyName))
        {
            ReportInvalidViewInteraction(request, diagnostics, "NavigationInteractionPropertyName cannot be empty");
            return null;
        }

        string propertyName = request.NavigationInteractionPropertyName!;
        ViewBindingModel? binding = ResolveViewBinding(request, propertyName, null, false, diagnostics);
        ITypeSymbol? propertyType = FindViewBindingMemberType(request.ViewModelType, propertyName);
        INamedTypeSymbol? interactionType = FindConstructedInterface(propertyType, "ReactiveUI.IInteraction`2");
        if (binding == null || interactionType == null ||
            !IsConstructedForItem(
                interactionType.TypeArguments[0],
                "Avalonia.Controls.DataGridGeneratedNavigationRequest`1",
                request.ItemType) ||
            !IsConstructedForItem(
                interactionType.TypeArguments[1],
                "Avalonia.Controls.DataGridGeneratedNavigationResult`1",
                request.ItemType))
        {
            ReportInvalidViewInteraction(
                request,
                diagnostics,
                $"member '{propertyName}' must be an accessible readable IInteraction<DataGridGeneratedNavigationRequest<{request.ItemType.ToDisplayString()}>, DataGridGeneratedNavigationResult<{request.ItemType.ToDisplayString()}>> property");
            return null;
        }

        return binding;
    }

    private static bool IsConstructedForItem(
        ITypeSymbol type,
        string metadataName,
        INamedTypeSymbol itemType) =>
        type is INamedTypeSymbol namedType &&
        namedType.TypeArguments.Length == 1 &&
        string.Equals(
            GeneratorUtilities.GetMetadataName(namedType.OriginalDefinition),
            metadataName,
            StringComparison.Ordinal) &&
        SymbolEqualityComparer.Default.Equals(namedType.TypeArguments[0], itemType);

    private static INamedTypeSymbol? FindConstructedInterface(ITypeSymbol? type, string metadataName)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }

        if (namedType.TypeArguments.Length == 2 &&
            string.Equals(GeneratorUtilities.GetMetadataName(namedType.OriginalDefinition), metadataName, StringComparison.Ordinal))
        {
            return namedType;
        }

        return namedType.AllInterfaces.FirstOrDefault(implemented =>
            implemented.TypeArguments.Length == 2 &&
            string.Equals(GeneratorUtilities.GetMetadataName(implemented.OriginalDefinition), metadataName, StringComparison.Ordinal));
    }

    private static bool ValidateViewInteractionHandler(
        INamedTypeSymbol handlerType,
        ITypeSymbol inputType,
        ITypeSymbol outputType)
    {
        if (handlerType.TypeKind != TypeKind.Class || handlerType.IsAbstract ||
            handlerType.TypeParameters.Length != 0 || !GeneratorUtilities.IsAccessibleFromGeneratedCode(handlerType))
        {
            return false;
        }

        bool hasConstructor = handlerType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
        if (!hasConstructor)
        {
            return false;
        }

        return handlerType.AllInterfaces.Any(implemented =>
            implemented.TypeArguments.Length == 2 &&
            string.Equals(
                GeneratorUtilities.GetMetadataName(implemented.OriginalDefinition),
                "Avalonia.Controls.IDataGridGeneratedViewInteractionHandler`2",
                StringComparison.Ordinal) &&
            SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], inputType) &&
            SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[1], outputType));
    }

    private static bool ValidateParameterlessImplementation(INamedTypeSymbol implementationType, string interfaceMetadataName)
    {
        if (implementationType.TypeKind != TypeKind.Class || implementationType.IsAbstract ||
            implementationType.TypeParameters.Length != 0 ||
            !GeneratorUtilities.IsAccessibleFromGeneratedCode(implementationType))
        {
            return false;
        }

        bool hasConstructor = implementationType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
        return hasConstructor && ImplementsMetadataName(implementationType, interfaceMetadataName);
    }

    private static RowDetailsViewModel? ResolveRowDetails(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        Dictionary<string, TypedConstant> arguments = request.RowDetailsArguments;
        string? resourceKey = GeneratorUtilities.GetString(arguments, "RowDetailsTemplateKey");
        INamedTypeSymbol? implementationType = GeneratorUtilities.GetType(arguments, "RowDetailsTemplateImplementationType");
        string? factoryMethod = GeneratorUtilities.GetString(arguments, "RowDetailsTemplateFactoryMethod");
        INamedTypeSymbol? nestedItemType = GeneratorUtilities.GetType(arguments, "RowDetailsNestedItemType");
        string? nestedItemsMember = GeneratorUtilities.GetString(arguments, "RowDetailsNestedItemsMember");
        bool hasNestedConfiguration = nestedItemType != null || !string.IsNullOrWhiteSpace(nestedItemsMember);
        int sourceCount = (!string.IsNullOrWhiteSpace(resourceKey) ? 1 : 0) +
                          (implementationType != null ? 1 : 0) +
                          (!string.IsNullOrWhiteSpace(factoryMethod) ? 1 : 0) +
                          (hasNestedConfiguration ? 1 : 0);

        if (sourceCount == 0)
        {
            if (request.HasRowDetailsConfiguration)
            {
                ReportInvalidRowDetails(request, diagnostics, "a template key, implementation type, factory method, or complete nested-grid recipe is required");
            }
            return null;
        }

        if (sourceCount != 1)
        {
            ReportInvalidRowDetails(request, diagnostics, "template key, implementation type, factory method, and nested-grid recipe are mutually exclusive");
            return null;
        }

        var model = new RowDetailsViewModel
        {
            VisibilityMode = GetEnumValue(arguments, "RowDetailsVisibilityMode", 2),
            AreFrozen = GeneratorUtilities.GetBoolean(arguments, "AreRowDetailsFrozen", false),
            AutomationId = GeneratorUtilities.GetString(arguments, "RowDetailsAutomationId") ?? request.AutomationId + "-details"
        };

        if (!string.IsNullOrWhiteSpace(resourceKey))
        {
            model.Source = RowDetailsTemplateSourceModel.Resource;
            model.ResourceKey = resourceKey;
            return model;
        }

        if (implementationType != null)
        {
            if (!ValidateRowDetailsImplementation(implementationType))
            {
                ReportInvalidRowDetails(
                    request,
                    diagnostics,
                    $"implementation type '{implementationType.ToDisplayString()}' must be accessible, non-abstract, parameterless, and implement IDataTemplate");
                return null;
            }

            model.Source = RowDetailsTemplateSourceModel.Implementation;
            model.ImplementationType = implementationType;
            return model;
        }

        if (!string.IsNullOrWhiteSpace(factoryMethod))
        {
            if (!HasRowDetailsFactoryMethod(request.ItemType, factoryMethod!))
            {
                ReportInvalidRowDetails(
                    request,
                    diagnostics,
                    $"factory method '{factoryMethod}' must be accessible, static, non-generic, accept ({request.ItemType.Name}, Control), and return Control");
                return null;
            }

            model.Source = RowDetailsTemplateSourceModel.FactoryMethod;
            model.FactoryMethod = factoryMethod;
            return model;
        }

        if (nestedItemType == null || string.IsNullOrWhiteSpace(nestedItemsMember))
        {
            ReportInvalidRowDetails(request, diagnostics, "nested-grid recipes require both RowDetailsNestedItemType and RowDetailsNestedItemsMember");
            return null;
        }

        IPropertySymbol? nestedItemsProperty = FindReadableInstanceProperty(request.ItemType, nestedItemsMember!);
        if (nestedItemsProperty == null || !IsEnumerableOf(nestedItemsProperty.Type, nestedItemType))
        {
            ReportInvalidRowDetails(
                request,
                diagnostics,
                $"nested items member '{nestedItemsMember}' must be an accessible readable IEnumerable<{nestedItemType.ToDisplayString()}> property");
            return null;
        }

        string? summaryMember = GeneratorUtilities.GetString(arguments, "RowDetailsSummaryMember");
        IPropertySymbol? summaryProperty = null;
        if (!string.IsNullOrWhiteSpace(summaryMember))
        {
            summaryProperty = FindReadableInstanceProperty(request.ItemType, summaryMember!);
            if (summaryProperty == null || summaryProperty.Type.SpecialType != SpecialType.System_String)
            {
                ReportInvalidRowDetails(
                    request,
                    diagnostics,
                    $"summary member '{summaryMember}' must be an accessible readable string property");
                return null;
            }
        }

        string nestedProviderNamespace = GeneratorUtilities.GetString(arguments, "RowDetailsNestedProviderNamespace") ??
            (nestedItemType.ContainingNamespace?.IsGlobalNamespace == false
                ? nestedItemType.ContainingNamespace.ToDisplayString()
                : string.Empty);
        model.Source = RowDetailsTemplateSourceModel.NestedGrid;
        model.NestedItemType = nestedItemType;
        model.NestedItemsProperty = nestedItemsProperty;
        model.SummaryProperty = summaryProperty;
        model.NestedProviderName = GeneratorUtilities.SanitizeIdentifier(
            GeneratorUtilities.GetString(arguments, "RowDetailsNestedProviderName") ??
            GeneratorUtilities.GetDefaultProviderName(nestedItemType));
        model.NestedProviderNamespace = nestedProviderNamespace;
        return model;
    }

    private static void ReportInvalidRowDetails(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string reason) =>
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidRowDetails,
            request.Location,
            request.ViewName,
            reason));

    private static ViewBindingModel? ResolveValidatedViewBinding(
        ViewRequest request,
        string propertyName,
        Func<ITypeSymbol, bool> isValidType,
        string invalidReason,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ViewBindingModel? binding = ResolveViewBinding(request, propertyName, null, false, diagnostics);
        if (binding == null)
        {
            return null;
        }

        ITypeSymbol? memberType = FindViewBindingMemberType(request.ViewModelType, propertyName);
        if (memberType != null && isValidType(memberType))
        {
            return binding;
        }

        ReportInvalidViewState(request, diagnostics, $"member '{propertyName}' {invalidReason}");
        return null;
    }

    private static ITypeSymbol? FindViewBindingMemberType(INamedTypeSymbol viewModelType, string propertyName)
    {
        IPropertySymbol? property = viewModelType.GetMembers(propertyName)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(static property =>
                !property.IsStatic &&
                property.GetMethod != null &&
                GeneratorUtilities.IsAccessibleFromGeneratedCode(property.GetMethod));
        if (property != null)
        {
            return property.Type;
        }

        IFieldSymbol? reactiveField = viewModelType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field =>
                !field.IsStatic &&
                string.Equals(GetReactivePropertyName(field.Name), propertyName, StringComparison.Ordinal) &&
                field.GetAttributes().Any(static attribute =>
                    string.Equals(
                        attribute.AttributeClass?.ToDisplayString(),
                        "ReactiveUI.SourceGenerators.ReactiveAttribute",
                        StringComparison.Ordinal)));
        return reactiveField?.Type;
    }

    private static void ReportInvalidViewState(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string reason) =>
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewState,
            request.Location,
            request.ViewName,
            reason));

    private static void ReportInvalidViewEventBridge(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string reason) =>
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewEventBridge,
            request.Location,
            request.ViewName,
            reason));

    private static void ReportInvalidViewInteraction(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string reason) =>
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewInteraction,
            request.Location,
            request.ViewName,
            reason));

    private static void ReportInvalidViewPerformanceIntegration(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string reason) =>
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewPerformanceIntegration,
            request.Location,
            request.ViewName,
            reason));

    private static bool ValidateViewThemeKey(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string optionName,
        string? key)
    {
        if (key == null || !string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        ReportInvalidViewPresentation(request, diagnostics, $"{optionName} cannot be empty or whitespace");
        return false;
    }

    private static bool ValidateViewClasses(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string optionName,
        ImmutableArray<string> classes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < classes.Length; index++)
        {
            string className = classes[index];
            if (string.IsNullOrWhiteSpace(className) || className.Any(char.IsWhiteSpace))
            {
                ReportInvalidViewPresentation(
                    request,
                    diagnostics,
                    $"{optionName} entry at index {index} must be one non-empty class token");
                return false;
            }
            if (!seen.Add(className))
            {
                ReportInvalidViewPresentation(
                    request,
                    diagnostics,
                    $"{optionName} contains duplicate class '{className}'");
                return false;
            }
        }
        return true;
    }

    private static void ReportInvalidViewPresentation(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string reason) =>
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewPresentation,
            request.Location,
            request.ViewName,
            reason));

    private static bool ValidateRowDetailsImplementation(INamedTypeSymbol implementationType)
    {
        if (!GeneratorUtilities.IsAccessibleFromGeneratedCode(implementationType) || implementationType.IsAbstract)
        {
            return false;
        }

        bool hasConstructor = implementationType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
        return hasConstructor && implementationType.AllInterfaces.Any(static implemented =>
            string.Equals(
                GeneratorUtilities.GetMetadataName(implemented),
                "Avalonia.Controls.Templates.IDataTemplate",
                StringComparison.Ordinal));
    }

    private static bool HasRowDetailsFactoryMethod(INamedTypeSymbol itemType, string methodName) =>
        itemType.GetMembers(methodName).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
            method.TypeParameters.Length == 0 &&
            method.Parameters.Length == 2 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, itemType) &&
            IsMetadataType(method.Parameters[1].Type, "Avalonia.Controls.Control") &&
            IsOrDerivesFrom(method.ReturnType, "Avalonia.Controls.Control"));

    private static IPropertySymbol? FindReadableInstanceProperty(INamedTypeSymbol ownerType, string propertyName)
    {
        INamedTypeSymbol? current = ownerType;
        while (current != null)
        {
            IPropertySymbol? property = current.GetMembers(propertyName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(static property =>
                    !property.IsStatic &&
                    property.Parameters.Length == 0 &&
                    property.GetMethod != null &&
                    GeneratorUtilities.IsAccessibleFromGeneratedCode(property.GetMethod));
            if (property != null)
            {
                return property;
            }

            current = current.BaseType;
        }

        return null;
    }

    private static bool IsEnumerableOf(ITypeSymbol sequenceType, INamedTypeSymbol elementType)
    {
        if (sequenceType is IArrayTypeSymbol arrayType)
        {
            return SymbolEqualityComparer.Default.Equals(arrayType.ElementType, elementType);
        }

        if (sequenceType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (IsEnumerableInterface(namedType, elementType))
        {
            return true;
        }

        return namedType.AllInterfaces.Any(implemented => IsEnumerableInterface(implemented, elementType));
    }

    private static bool IsEnumerableInterface(INamedTypeSymbol type, INamedTypeSymbol elementType) =>
        type.TypeArguments.Length == 1 &&
        string.Equals(
            GeneratorUtilities.GetMetadataName(type.OriginalDefinition),
            "System.Collections.Generic.IEnumerable`1",
            StringComparison.Ordinal) &&
        SymbolEqualityComparer.Default.Equals(type.TypeArguments[0], elementType);

    private static string GetViewMetadataName(ViewRequest request) =>
        string.IsNullOrEmpty(request.ViewNamespace)
            ? request.ViewName
            : request.ViewNamespace + "." + request.ViewName;

    private static ViewBindingModel? ResolveOptionalViewBinding(
        ViewRequest request,
        string? propertyName,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        bool requireSetter = false)
    {
        return string.IsNullOrWhiteSpace(propertyName)
            ? null
            : ResolveViewBinding(request, propertyName!, null, false, diagnostics, requireSetter);
    }

    private static ViewBindingModel? ResolveTransferViewBinding(
        ViewRequest request,
        string? propertyName,
        string interfaceMetadataName,
        string interfaceDisplayName,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ViewBindingModel? binding = ResolveOptionalViewBinding(request, propertyName, diagnostics);
        if (binding == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return binding;
        }

        ITypeSymbol? propertyType = FindViewBindingMemberType(request.ViewModelType, propertyName!);
        if (propertyType is INamedTypeSymbol namedType &&
            (string.Equals(GeneratorUtilities.GetMetadataName(namedType), interfaceMetadataName, StringComparison.Ordinal) ||
             namedType.AllInterfaces.Any(implemented => string.Equals(
                 GeneratorUtilities.GetMetadataName(implemented),
                 interfaceMetadataName,
                 StringComparison.Ordinal))))
        {
            return binding;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewTransferIntegration,
            request.Location,
            request.ViewName,
            $"member '{propertyName}' must implement {interfaceDisplayName}"));
        return null;
    }

    private static ViewBindingModel? ResolveNavigationModelViewBinding(
        ViewRequest request,
        string? propertyName,
        string interfaceMetadataName,
        string interfaceDisplayName,
        ImmutableArray<Diagnostic>.Builder diagnostics) =>
        ResolveNavigationModelViewBinding(
            request,
            propertyName,
            interfaceMetadataName,
            interfaceDisplayName,
            "GenerateNavigationModel",
            "NavigationModelPropertyName",
            string.Equals(
                interfaceMetadataName,
                "Avalonia.Controls.DataGridNavigation.IDataGridNavigationModel",
                StringComparison.Ordinal),
            diagnostics);

    private static ViewBindingModel? ResolveNavigationModelViewBinding(
        ViewRequest request,
        string? propertyName,
        string interfaceMetadataName,
        string interfaceDisplayName,
        string generatePropertyName,
        string generatedPropertyName,
        bool allowGeneratedModel,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        bool usesGeneratedModel = allowGeneratedModel && IsGeneratedNavigationModelProperty(
            request.ViewModelType,
            propertyName!,
            generatePropertyName,
            generatedPropertyName);
        ViewBindingModel? binding = ResolveViewBinding(
            request,
            propertyName!,
            usesGeneratedModel ? GetGeneratedNavigationPropertyType(generatePropertyName) : null,
            usesGeneratedModel,
            diagnostics);
        if (binding == null || usesGeneratedModel)
        {
            return binding;
        }

        ITypeSymbol? propertyType = FindViewBindingMemberType(request.ViewModelType, propertyName!);
        ITypeSymbol? unwrappedType = propertyType == null ? null : UnwrapNullable(propertyType);
        if (unwrappedType is INamedTypeSymbol namedType &&
            (string.Equals(GeneratorUtilities.GetMetadataName(namedType), interfaceMetadataName, StringComparison.Ordinal) ||
             namedType.AllInterfaces.Any(implemented => string.Equals(
                 GeneratorUtilities.GetMetadataName(implemented),
                 interfaceMetadataName,
                 StringComparison.Ordinal))))
        {
            return binding;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewNavigationIntegration,
            request.Location,
            request.ViewName,
            $"member '{propertyName}' must implement {interfaceDisplayName}"));
        return null;
    }

    private static string GetGeneratedNavigationPropertyType(string generatePropertyName) => generatePropertyName switch
    {
        "GenerateNavigationInputModel" =>
            "global::Avalonia.Controls.DataGridNavigation.DataGridNavigationInputModel",
        "GenerateRouteContextFactory" =>
            "global::Avalonia.Controls.DataGridNavigation.DataGridRouteContextFactory",
        _ => "global::Avalonia.Controls.DataGridNavigation.DataGridNavigationModel"
    };

    private static bool IsGeneratedNavigationModelProperty(
        INamedTypeSymbol viewModelType,
        string propertyName,
        string generatePropertyName,
        string generatedPropertyName)
    {
        foreach (AttributeData attribute in viewModelType.GetAttributes())
        {
            if (IsGeneratedNavigationModelProperty(attribute, propertyName, generatePropertyName, generatedPropertyName))
            {
                return true;
            }
        }

        foreach (AttributeData attribute in viewModelType.ContainingAssembly.GetAttributes())
        {
            if (IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelAttributeName) &&
                SymbolEqualityComparer.Default.Equals(GetConstructorType(attribute, 0), viewModelType) &&
                IsGeneratedNavigationModelProperty(attribute, propertyName, generatePropertyName, generatedPropertyName))
            {
                return true;
            }

            if (!IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelsForNamespaceAttributeName))
            {
                continue;
            }

            string? namespaceName = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            if (!string.IsNullOrWhiteSpace(namespaceName) &&
                NamespaceMatches(
                    viewModelType,
                    namespaceName!,
                    GeneratorUtilities.GetBoolean(arguments, "IncludeNestedNamespaces", true)) &&
                IsGeneratedNavigationModelProperty(attribute, propertyName, generatePropertyName, generatedPropertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedNavigationModelProperty(
        AttributeData attribute,
        string propertyName,
        string generatePropertyName,
        string generatedPropertyName)
    {
        Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
        return GeneratorUtilities.GetBoolean(arguments, generatePropertyName, false) &&
            string.Equals(
                GeneratorUtilities.GetString(arguments, generatedPropertyName) ??
                    GetDefaultGeneratedNavigationPropertyName(generatePropertyName),
                propertyName,
                StringComparison.Ordinal);
    }

    private static string GetDefaultGeneratedNavigationPropertyName(string generatePropertyName) =>
        generatePropertyName switch
        {
            "GenerateNavigationInputModel" => "NavigationInputModel",
            "GenerateRouteContextFactory" => "RouteContextFactory",
            _ => "NavigationModel"
        };

    private static ViewBindingModel? ResolveFormulaViewBinding(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ViewBindingModel? binding = ResolveOptionalViewBinding(request, request.FormulaModelPropertyName, diagnostics);
        if (binding == null || string.IsNullOrWhiteSpace(request.FormulaModelPropertyName))
        {
            return binding;
        }

        const string interfaceMetadataName = "Avalonia.Controls.DataGridFormulas.IDataGridFormulaModel";
        ITypeSymbol? propertyType = FindViewBindingMemberType(request.ViewModelType, request.FormulaModelPropertyName!);
        if (propertyType is INamedTypeSymbol namedType &&
            (string.Equals(GeneratorUtilities.GetMetadataName(namedType), interfaceMetadataName, StringComparison.Ordinal) ||
             namedType.AllInterfaces.Any(implemented => string.Equals(
                 GeneratorUtilities.GetMetadataName(implemented),
                 interfaceMetadataName,
                 StringComparison.Ordinal))))
        {
            return binding;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewFormulaIntegration,
            request.Location,
            request.ViewName,
            $"member '{request.FormulaModelPropertyName}' must implement IDataGridFormulaModel"));
        return null;
    }

    private static ViewBindingModel? ResolveConditionalFormattingViewBinding(
        ViewRequest request,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ViewBindingModel? binding = ResolveOptionalViewBinding(
            request,
            request.ConditionalFormattingModelPropertyName,
            diagnostics);
        if (binding == null || string.IsNullOrWhiteSpace(request.ConditionalFormattingModelPropertyName))
        {
            return binding;
        }

        const string interfaceMetadataName =
            "Avalonia.Controls.DataGridConditionalFormatting.IConditionalFormattingModel";
        ITypeSymbol? propertyType = FindViewBindingMemberType(
            request.ViewModelType,
            request.ConditionalFormattingModelPropertyName!);
        if (propertyType is INamedTypeSymbol namedType &&
            (string.Equals(GeneratorUtilities.GetMetadataName(namedType), interfaceMetadataName, StringComparison.Ordinal) ||
             namedType.AllInterfaces.Any(implemented => string.Equals(
                 GeneratorUtilities.GetMetadataName(implemented),
                 interfaceMetadataName,
                 StringComparison.Ordinal))))
        {
            return binding;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidViewConditionalFormattingIntegration,
            request.Location,
            request.ViewName,
            $"member '{request.ConditionalFormattingModelPropertyName}' must implement IConditionalFormattingModel"));
        return null;
    }

    private static ViewBindingModel? ResolveViewBinding(
        ViewRequest request,
        string propertyName,
        string? generatedFallbackType,
        bool canUseGeneratedFallback,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        bool requireSetter = false)
    {
        IPropertySymbol? property = request.ViewModelType.GetMembers(propertyName)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(static property => !property.IsStatic && property.GetMethod != null);
        if (property != null && GeneratorUtilities.IsAccessibleFromGeneratedCode(property.GetMethod!))
        {
            bool canWrite = property.SetMethod != null && GeneratorUtilities.IsAccessibleFromGeneratedCode(property.SetMethod);
            if (!requireSetter || canWrite)
            {
                ITypeSymbol runtimeType = UnwrapNullable(property.Type);
                return new ViewBindingModel
                {
                    PropertyName = propertyName,
                    PropertyType = property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat),
                    RuntimePropertyType = runtimeType.ToDisplayString(GeneratorUtilities.FullyQualifiedFormat),
                    CanWrite = canWrite
                };
            }
        }

        IFieldSymbol? reactiveField = request.ViewModelType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field =>
                !field.IsStatic &&
                string.Equals(GetReactivePropertyName(field.Name), propertyName, StringComparison.Ordinal) &&
                field.GetAttributes().Any(static attribute =>
                    string.Equals(
                        attribute.AttributeClass?.ToDisplayString(),
                        "ReactiveUI.SourceGenerators.ReactiveAttribute",
                        StringComparison.Ordinal)));
        if (reactiveField != null)
        {
            ITypeSymbol runtimeType = UnwrapNullable(reactiveField.Type);
            return new ViewBindingModel
            {
                PropertyName = propertyName,
                PropertyType = reactiveField.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat),
                RuntimePropertyType = runtimeType.ToDisplayString(GeneratorUtilities.FullyQualifiedFormat),
                CanWrite = true
            };
        }

        if (canUseGeneratedFallback && generatedFallbackType != null)
        {
            return new ViewBindingModel
            {
                PropertyName = propertyName,
                PropertyType = generatedFallbackType,
                RuntimePropertyType = generatedFallbackType,
                CanWrite = false
            };
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.MissingViewMember,
            request.Location,
            request.ViewModelType.ToDisplayString(),
            propertyName,
            request.ViewName));
        return null;
    }

    private static string GetReactivePropertyName(string fieldName)
    {
        string trimmed = fieldName.TrimStart('_');
        if (trimmed.Length == 0)
        {
            return fieldName;
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
    }

    private static bool ValidateViewBase(INamedTypeSymbol baseType)
    {
        if (baseType.IsSealed || !GeneratorUtilities.IsAccessibleFromGeneratedCode(baseType))
        {
            return false;
        }

        bool hasConstructor = baseType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
        if (!hasConstructor)
        {
            return false;
        }

        INamedTypeSymbol? current = baseType;
        while (current != null)
        {
            if (string.Equals(
                    GeneratorUtilities.GetMetadataName(current.OriginalDefinition),
                    "Avalonia.Controls.UserControl",
                    StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static ViewFrameworkModel GetViewFramework(Dictionary<string, TypedConstant> arguments)
    {
        if (arguments.TryGetValue("Framework", out TypedConstant value) && value.Value is int frameworkValue && frameworkValue == 1)
        {
            return ViewFrameworkModel.ReactiveUI;
        }

        return ViewFrameworkModel.Avalonia;
    }

    private static string SplitWords(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 4);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1]))
            {
                result.Append(' ');
            }

            result.Append(character);
        }

        return result.ToString();
    }

    private sealed class ViewRequest
    {
        public INamedTypeSymbol ViewModelType { get; set; } = null!;
        public INamedTypeSymbol ItemType { get; set; } = null!;
        public string ViewName { get; set; } = string.Empty;
        public string ViewNamespace { get; set; } = string.Empty;
        public ViewFrameworkModel Framework { get; set; }
        public INamedTypeSymbol? BaseType { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Recipe { get; set; }
        public string? ControllerName { get; set; }
        public string AutomationId { get; set; } = string.Empty;
        public string ItemsPropertyName { get; set; } = "Items";
        public string ColumnDefinitionsPropertyName { get; set; } = "ColumnDefinitions";
        public string FastPathOptionsPropertyName { get; set; } = "FastPathOptions";
        public string? LayoutModelPropertyName { get; set; }
        public int Layout { get; set; }
        public int LayoutOrientation { get; set; }
        public double LayoutSpacing { get; set; }
        public double LayoutHorizontalSpacing { get; set; }
        public double LayoutVerticalSpacing { get; set; }
        public double LayoutMinItemWidth { get; set; } = double.NaN;
        public double LayoutMinItemHeight { get; set; } = double.NaN;
        public int LayoutMaximumRowsOrColumns { get; set; } = int.MaxValue;
        public int LayoutItemsJustification { get; set; }
        public int LayoutItemsStretch { get; set; }
        public bool LayoutDisableVirtualization { get; set; }
        public int LayoutMaximumCachedLines { get; set; } = 256;
        public string? SortingModelPropertyName { get; set; }
        public string? FilteringModelPropertyName { get; set; }
        public int HierarchyFilterPolicy { get; set; } = 1;
        public string? SearchModelPropertyName { get; set; }
        public string? SearchTextPropertyName { get; set; }
        public string? SelectionModelPropertyName { get; set; }
        public string? NavigationModelPropertyName { get; set; }
        public string? RouteNavigationModelPropertyName { get; set; }
        public string? NavigationInputModelPropertyName { get; set; }
        public string? RouteContextFactoryPropertyName { get; set; }
        public int SelectionMode { get; set; } = 1;
        public int SelectionUnit { get; set; }
        public bool HasSelectionConfiguration { get; set; }
        public string? ClipboardImportModelPropertyName { get; set; }
        public string? FillModelPropertyName { get; set; }
        public string? FormulaModelPropertyName { get; set; }
        public string? ConditionalFormattingModelPropertyName { get; set; }
        public int EditTriggers { get; set; } = 9;
        public bool RestrictTextInputEditToCells { get; set; }
        public int RequiredPointerEditModifiers { get; set; }
        public bool RequireExactPointerEditModifiers { get; set; }
        public int ClipboardCopyMode { get; set; } = 1;
        public bool IsReadOnly { get; set; }
        public bool CanUserAddRows { get; set; }
        public bool CanUserDeleteRows { get; set; }
        public bool ShowTotalSummary { get; set; }
        public bool ShowGroupSummary { get; set; }
        public int TotalSummaryPosition { get; set; } = 1;
        public int GroupSummaryPosition { get; set; } = 1;
        public string? HierarchicalModelPropertyName { get; set; }
        public string? StateControllerPropertyName { get; set; }
        public string? ViewStatePropertyName { get; set; }
        public string? ErrorMessagePropertyName { get; set; }
        public string? RetryCommandPropertyName { get; set; }
        public int RoutedEvents { get; set; }
        public string? RoutedEventCommandPropertyName { get; set; }
        public bool HasRoutedEventConfiguration { get; set; }
        public ImmutableArray<string> InteractionPropertyNames { get; set; } = ImmutableArray<string>.Empty;
        public ImmutableArray<INamedTypeSymbol> InteractionHandlerTypes { get; set; } = ImmutableArray<INamedTypeSymbol>.Empty;
        public bool HasInteractionConfiguration { get; set; }
        public string? NavigationInteractionPropertyName { get; set; }
        public bool HasNavigationInteractionConfiguration { get; set; }
        public int PerformanceProfile { get; set; }
        public INamedTypeSymbol? InputMapType { get; set; }
        public string? InputCommandPropertyName { get; set; }
        public INamedTypeSymbol? DiagnosticsSinkType { get; set; }
        public string? DiagnosticsStatusPropertyName { get; set; }
        public string? ViewThemeKey { get; set; }
        public string? DataGridThemeKey { get; set; }
        public string? ToolbarThemeKey { get; set; }
        public string? RecipeContentThemeKey { get; set; }
        public ImmutableArray<string> ViewClasses { get; set; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> DataGridClasses { get; set; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> ToolbarClasses { get; set; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> RecipeContentClasses { get; set; } = ImmutableArray<string>.Empty;
        public string LoadingText { get; set; } = "Loading data…";
        public string EmptyText { get; set; } = "No items to display.";
        public string ErrorText { get; set; } = "Unable to load data.";
        public string RetryText { get; set; } = "Retry";
        public bool HasViewStateConfiguration { get; set; }
        public Dictionary<string, TypedConstant> RowDetailsArguments { get; set; } = new(StringComparer.Ordinal);
        public bool HasRowDetailsConfiguration =>
            RowDetailsArguments.Keys.Any(static key => key.StartsWith("RowDetails", StringComparison.Ordinal) ||
                                                       string.Equals(key, "AreRowDetailsFrozen", StringComparison.Ordinal));
        public Location Location { get; set; } = Location.None;
    }
}
