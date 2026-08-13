// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ProDataGrid.SourceGenerators;

internal sealed class GenerationModel
{
    public GenerationModel(
        ImmutableArray<SchemaModel> schemas,
        ImmutableArray<ViewModelModel> viewModels,
        ImmutableArray<ControllerModel> controllers,
        ImmutableArray<ViewModelViewModel> views,
        RegistryModel? registry,
        ImmutableArray<Diagnostic> diagnostics)
    {
        Schemas = schemas;
        ViewModels = viewModels;
        Controllers = controllers;
        Views = views;
        Registry = registry;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<SchemaModel> Schemas { get; }

    public ImmutableArray<ViewModelModel> ViewModels { get; }

    public ImmutableArray<ControllerModel> Controllers { get; }

    public ImmutableArray<ViewModelViewModel> Views { get; }

    public RegistryModel? Registry { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal sealed class RegistryModel
{
    public string RegistryName { get; set; } = "GeneratedProDataGridRegistration";

    public string RegistryNamespace { get; set; } = "ProDataGrid.Generated";

    public bool HasMicrosoftDependencyInjection { get; set; }

    public bool IsPublic { get; set; } = true;

    public ImmutableArray<ViewRegistrationModel> ViewRegistrations { get; set; } = ImmutableArray<ViewRegistrationModel>.Empty;
}

internal sealed class ViewRegistrationModel
{
    public INamedTypeSymbol ViewModelType { get; set; } = null!;

    public INamedTypeSymbol ViewType { get; set; } = null!;
}

internal enum ViewFrameworkModel
{
    Avalonia,
    ReactiveUI
}

internal sealed class ViewInteractionModel
{
    public string PropertyName { get; set; } = string.Empty;

    public ITypeSymbol InputType { get; set; } = null!;

    public ITypeSymbol OutputType { get; set; } = null!;

    public INamedTypeSymbol HandlerType { get; set; } = null!;

}

internal sealed class ViewModelViewModel
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

    public ViewBindingModel Items { get; set; } = null!;

    public ViewBindingModel ColumnDefinitions { get; set; } = null!;

    public ViewBindingModel FastPathOptions { get; set; } = null!;

    public ViewBindingModel? LayoutModel { get; set; }

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

    public ViewBindingModel? SortingModel { get; set; }

    public ViewBindingModel? FilteringModel { get; set; }

    public int HierarchyFilterPolicy { get; set; }

    public ViewBindingModel? SearchModel { get; set; }

    public ViewBindingModel? SearchText { get; set; }

    public ViewBindingModel? SelectionModel { get; set; }

    public ViewBindingModel? NavigationModel { get; set; }

    public ViewBindingModel? RouteNavigationModel { get; set; }

    public int SelectionMode { get; set; }

    public int SelectionUnit { get; set; }

    public bool ConfigureSelection { get; set; }

    public ViewBindingModel? ClipboardImportModel { get; set; }

    public ViewBindingModel? FillModel { get; set; }

    public ViewBindingModel? FormulaModel { get; set; }

    public ViewBindingModel? ConditionalFormattingModel { get; set; }

    public int EditTriggers { get; set; }

    public bool RestrictTextInputEditToCells { get; set; }

    public int RequiredPointerEditModifiers { get; set; }

    public bool RequireExactPointerEditModifiers { get; set; }

    public int ClipboardCopyMode { get; set; }

    public bool IsReadOnly { get; set; }

    public bool CanUserAddRows { get; set; }

    public bool CanUserDeleteRows { get; set; }

    public bool ShowTotalSummary { get; set; }

    public bool ShowGroupSummary { get; set; }

    public int TotalSummaryPosition { get; set; }

    public int GroupSummaryPosition { get; set; }

    public ViewBindingModel? HierarchicalModel { get; set; }

    public ViewBindingModel? StateController { get; set; }

    public ViewBindingModel? ViewState { get; set; }

    public ViewBindingModel? ErrorMessage { get; set; }

    public ViewBindingModel? RetryCommand { get; set; }

    public ViewBindingModel? RoutedEventCommand { get; set; }

    public int RoutedEvents { get; set; }

    public ImmutableArray<ViewInteractionModel> Interactions { get; set; } = ImmutableArray<ViewInteractionModel>.Empty;

    public ViewBindingModel? NavigationInteraction { get; set; }

    public int PerformanceProfile { get; set; }

    public INamedTypeSymbol? InputMapType { get; set; }

    public ViewBindingModel? InputCommand { get; set; }

    public INamedTypeSymbol? DiagnosticsSinkType { get; set; }

    public ViewBindingModel? DiagnosticsStatus { get; set; }

    public string? ViewThemeKey { get; set; }

    public string? DataGridThemeKey { get; set; }

    public string? ToolbarThemeKey { get; set; }

    public string? RecipeContentThemeKey { get; set; }

    public ImmutableArray<string> ViewClasses { get; set; } = ImmutableArray<string>.Empty;

    public ImmutableArray<string> DataGridClasses { get; set; } = ImmutableArray<string>.Empty;

    public ImmutableArray<string> ToolbarClasses { get; set; } = ImmutableArray<string>.Empty;

    public ImmutableArray<string> RecipeContentClasses { get; set; } = ImmutableArray<string>.Empty;

    public string LoadingText { get; set; } = string.Empty;

    public string EmptyText { get; set; } = string.Empty;

    public string ErrorText { get; set; } = string.Empty;

    public string RetryText { get; set; } = string.Empty;

    public RowDetailsViewModel? RowDetails { get; set; }

    public Location Location { get; set; } = Location.None;
}

internal enum RowDetailsTemplateSourceModel
{
    Resource,
    Implementation,
    FactoryMethod,
    NestedGrid
}

internal sealed class RowDetailsViewModel
{
    public RowDetailsTemplateSourceModel Source { get; set; }

    public int VisibilityMode { get; set; }

    public bool AreFrozen { get; set; }

    public string AutomationId { get; set; } = string.Empty;

    public string? ResourceKey { get; set; }

    public INamedTypeSymbol? ImplementationType { get; set; }

    public string? FactoryMethod { get; set; }

    public INamedTypeSymbol? NestedItemType { get; set; }

    public IPropertySymbol? NestedItemsProperty { get; set; }

    public IPropertySymbol? SummaryProperty { get; set; }

    public string? NestedProviderName { get; set; }

    public string? NestedProviderNamespace { get; set; }
}

internal sealed class DirectViewCandidate
{
    public INamedTypeSymbol ViewModelType { get; set; } = null!;

    public ImmutableArray<AttributeData> Attributes { get; set; } = ImmutableArray<AttributeData>.Empty;

    public ImmutableArray<string> ExistingViewTypes { get; set; } = ImmutableArray<string>.Empty;

    public bool IsGeneratedViewModel { get; set; }

    public bool HasAvaloniaUserControl { get; set; }

    public bool HasReactiveUserControl { get; set; }

    public string CacheKey { get; set; } = string.Empty;
}

internal sealed class DirectViewCandidateComparer : IEqualityComparer<DirectViewCandidate>
{
    public static readonly DirectViewCandidateComparer Instance = new();

    public bool Equals(DirectViewCandidate? x, DirectViewCandidate? y) =>
        ReferenceEquals(x, y) ||
        (x != null && y != null && string.Equals(x.CacheKey, y.CacheKey, StringComparison.Ordinal));

    public int GetHashCode(DirectViewCandidate candidate) =>
        StringComparer.Ordinal.GetHashCode(candidate.CacheKey);
}

internal sealed class DirectViewGenerationResult
{
    public DirectViewGenerationResult(
        ImmutableArray<GeneratedSource> sources,
        ImmutableArray<Diagnostic> diagnostics)
    {
        Sources = sources;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<GeneratedSource> Sources { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal sealed class ViewBindingModel
{
    public string PropertyName { get; set; } = string.Empty;

    public string PropertyType { get; set; } = "object";

    public string RuntimePropertyType { get; set; } = "object";

    public bool CanWrite { get; set; }
}

internal sealed class SchemaModel
{
    public INamedTypeSymbol ItemType { get; set; } = null!;

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderNamespace { get; set; } = string.Empty;

    public string SchemaId { get; set; } = string.Empty;

    public int StateVersion { get; set; } = 1;

    public bool AttributedOnly { get; set; }

    public bool IncludeInherited { get; set; } = true;

    public bool Strict { get; set; } = true;

    public bool Streaming { get; set; }

    public bool HierarchicalRows { get; set; }

    public int PerformanceProfile { get; set; }

    public int DefaultPageSize { get; set; }

    public int InitialPageIndex { get; set; }

    public int InitialCurrency { get; set; }

    public bool PreserveCurrentItemByKey { get; set; } = true;

    public bool PreserveSelectionByKey { get; set; } = true;

    public INamedTypeSymbol? ImplementationType { get; set; }

    public INamedTypeSymbol? MutationHandlerType { get; set; }

    public INamedTypeSymbol? NewRowFactoryType { get; set; }

    public INamedTypeSymbol? FormulaFillTranslatorType { get; set; }

    public ImmutableArray<IMethodSymbol> OperationPresetMethods { get; set; } = ImmutableArray<IMethodSymbol>.Empty;

    public ImmutableArray<string> OperationPresetMethodNames { get; set; } = ImmutableArray<string>.Empty;

    public string? ConfigureMethod { get; set; }

    public string? PivotConfigureMethod { get; set; }

    public string? OutlineConfigureMethod { get; set; }

    public Location Location { get; set; } = Location.None;

    public ImmutableArray<ColumnModel> Columns { get; set; } = ImmutableArray<ColumnModel>.Empty;

    public KeyMemberModel? KeyMember { get; set; }

    public string? ExplicitKeyMemberName { get; set; }

    public string? KeySelectorMethodName { get; set; }

    public bool UseReferenceIdentityKey { get; set; }

    public HierarchyModel? Hierarchy { get; set; }

    public bool IsDirectIncremental { get; set; }
}

internal sealed class DirectSchemaCandidate
{
    public INamedTypeSymbol TargetType { get; set; } = null!;

    public ImmutableArray<AttributeData> Attributes { get; set; } = ImmutableArray<AttributeData>.Empty;

    public DirectSchemaSourceKind SourceKind { get; set; }

    public string CacheKey { get; set; } = string.Empty;
}

internal enum DirectSchemaSourceKind
{
    Schema,
    ViewModel,
    Controller,
    Property
}

internal sealed class DirectSchemaCandidateComparer : IEqualityComparer<DirectSchemaCandidate>
{
    public static readonly DirectSchemaCandidateComparer Instance = new();

    public bool Equals(DirectSchemaCandidate? x, DirectSchemaCandidate? y) =>
        ReferenceEquals(x, y) ||
        (x != null && y != null && string.Equals(x.CacheKey, y.CacheKey, StringComparison.Ordinal));

    public int GetHashCode(DirectSchemaCandidate candidate) =>
        StringComparer.Ordinal.GetHashCode(candidate.CacheKey);
}

internal sealed class DirectSchemaGenerationResult
{
    public DirectSchemaGenerationResult(
        string cacheKey,
        ImmutableArray<GeneratedSource> sources,
        ImmutableArray<Diagnostic> diagnostics)
    {
        CacheKey = cacheKey;
        Sources = sources;
        Diagnostics = diagnostics;
    }

    public string CacheKey { get; }

    public ImmutableArray<GeneratedSource> Sources { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal sealed class DirectSchemaCompositionResult
{
    public DirectSchemaCompositionResult(
        ImmutableArray<DirectSchemaBuildCandidate> schemas,
        ImmutableArray<Diagnostic> diagnostics)
    {
        Schemas = schemas;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<DirectSchemaBuildCandidate> Schemas { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal sealed class DirectSchemaBuildCandidate
{
    public DirectSchemaBuildCandidate(SchemaModel schema, string cacheKey)
    {
        Schema = schema;
        CacheKey = cacheKey;
    }

    public SchemaModel Schema { get; }

    public string CacheKey { get; }
}

internal sealed class DirectSchemaBuildCandidateComparer : IEqualityComparer<DirectSchemaBuildCandidate>
{
    public static readonly DirectSchemaBuildCandidateComparer Instance = new();

    public bool Equals(DirectSchemaBuildCandidate? x, DirectSchemaBuildCandidate? y) =>
        ReferenceEquals(x, y) ||
        (x != null && y != null && string.Equals(x.CacheKey, y.CacheKey, StringComparison.Ordinal));

    public int GetHashCode(DirectSchemaBuildCandidate candidate) =>
        StringComparer.Ordinal.GetHashCode(candidate.CacheKey);
}

internal sealed class IndexedColumnsCandidate
{
    public INamedTypeSymbol TargetType { get; set; } = null!;
    public AttributeData Attribute { get; set; } = null!;
    public string CacheKey { get; set; } = string.Empty;
}

internal sealed class IndexedColumnsCandidateComparer : IEqualityComparer<IndexedColumnsCandidate>
{
    public static readonly IndexedColumnsCandidateComparer Instance = new();
    public bool Equals(IndexedColumnsCandidate? left, IndexedColumnsCandidate? right) =>
        ReferenceEquals(left, right) ||
        (left != null && right != null && string.Equals(left.CacheKey, right.CacheKey, StringComparison.Ordinal));
    public int GetHashCode(IndexedColumnsCandidate candidate) => StringComparer.Ordinal.GetHashCode(candidate.CacheKey);
}

internal sealed class IndexedColumnsGenerationResult
{
    public IndexedColumnsGenerationResult(GeneratedSource? source, ImmutableArray<Diagnostic> diagnostics)
    {
        Source = source;
        Diagnostics = diagnostics;
    }
    public GeneratedSource? Source { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal sealed class CellDrawCacheCandidate
{
    public INamedTypeSymbol TargetType { get; set; } = null!;
    public AttributeData Attribute { get; set; } = null!;
    public string CacheKey { get; set; } = string.Empty;
}

internal sealed class CellDrawCacheCandidateComparer : IEqualityComparer<CellDrawCacheCandidate>
{
    public static readonly CellDrawCacheCandidateComparer Instance = new();

    public bool Equals(CellDrawCacheCandidate? x, CellDrawCacheCandidate? y) =>
        ReferenceEquals(x, y) ||
        (x != null && y != null && string.Equals(x.CacheKey, y.CacheKey, StringComparison.Ordinal));

    public int GetHashCode(CellDrawCacheCandidate candidate) =>
        StringComparer.Ordinal.GetHashCode(candidate.CacheKey);
}

internal sealed class CellDrawCacheGenerationResult
{
    public CellDrawCacheGenerationResult(GeneratedSource? source, ImmutableArray<Diagnostic> diagnostics)
    {
        Source = source;
        Diagnostics = diagnostics;
    }

    public GeneratedSource? Source { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal sealed class KeyMemberModel
{
    public ISymbol Member { get; set; } = null!;

    public ITypeSymbol Type { get; set; } = null!;

    public INamedTypeSymbol? AccessReceiverType { get; set; }

    public KeyAccessorKind Kind { get; set; }
}

internal enum KeyAccessorKind
{
    Member,
    StaticMethod,
    ReferenceIdentity
}

internal sealed class HierarchyModel
{
    public IPropertySymbol? ChildrenProperty { get; set; }

    public IMethodSymbol? ChildLoaderMethod { get; set; }

    public IPropertySymbol? ExpandedProperty { get; set; }

    public ISymbol? ParentKeyMember { get; set; }
}

internal sealed class ColumnModel
{
    public IPropertySymbol Property { get; set; } = null!;

    public IPropertySymbol ConfigurationProperty { get; set; } = null!;

    public INamedTypeSymbol? AccessReceiverType { get; set; }

    public string Kind { get; set; } = "Auto";

    public string Header { get; set; } = string.Empty;

    public string? HeaderProviderMethod { get; set; }

    public bool HeaderProviderAcceptsFormatProvider { get; set; }

    public string? DescriptionProviderMethod { get; set; }

    public bool DescriptionProviderAcceptsFormatProvider { get; set; }

    public int Order { get; set; }

    public int SourceOrder { get; set; }

    public int FrozenPlacement { get; set; }

    public ImmutableDictionary<string, TypedConstant> Options { get; set; } = ImmutableDictionary<string, TypedConstant>.Empty;

    public string ColumnKey { get; set; } = string.Empty;

    public ImmutableArray<string> PreviousColumnKeys { get; set; } = ImmutableArray<string>.Empty;

    public string? ConfigureMethod { get; set; }

    public string? FactoryMethod { get; set; }

    public string? ParserMethod { get; set; }

    public string? FormatterMethod { get; set; }

    public string? ValidatorMethod { get; set; }

    public string? AsyncValidatorMethod { get; set; }

    public string? CoerceMethod { get; set; }

    public string? CanEditMethod { get; set; }

    public string? TemplateFactoryMethod { get; set; }

    public string? EditingTemplateFactoryMethod { get; set; }

    public string? NewRowTemplateFactoryMethod { get; set; }

    public INamedTypeSymbol? DrawOperationFactoryType { get; set; }

    public string? DrawOperationFactoryMethod { get; set; }

    public IPropertySymbol? ContentMember { get; set; }

    public IPropertySymbol? CheckedContentMember { get; set; }

    public IPropertySymbol? UncheckedContentMember { get; set; }

    public IPropertySymbol? OnContentMember { get; set; }

    public IPropertySymbol? OffContentMember { get; set; }

    public IPropertySymbol? CommandMember { get; set; }

    public IPropertySymbol? CommandParameterMember { get; set; }

    public GroupModel? Group { get; set; }

    public ImmutableArray<SummaryModel> Summaries { get; set; } = ImmutableArray<SummaryModel>.Empty;

    public ImmutableArray<ConditionalRuleModel> ConditionalRules { get; set; } = ImmutableArray<ConditionalRuleModel>.Empty;

    public ImmutableArray<BandModel> Bands { get; set; } = ImmutableArray<BandModel>.Empty;

    public ImmutableArray<AnalyticsRoleModel> AnalyticsRoles { get; set; } = ImmutableArray<AnalyticsRoleModel>.Empty;

    public bool IsSearchable { get; set; } = true;
}

internal sealed class AnalyticsRoleModel
{
    public int Role { get; set; }
    public int Order { get; set; }
    public string? Name { get; set; }
    public string? Format { get; set; }
    public int Aggregate { get; set; }
    public int PivotDisplayMode { get; set; }
    public string? Formula { get; set; }
    public string? ConfigureMethod { get; set; }
    public string? CustomAggregatorFactoryMethod { get; set; }
    public ImmutableArray<string> Dependencies { get; set; } = ImmutableArray<string>.Empty;
}

internal sealed class GroupModel
{
    public int Order { get; set; }
    public int Direction { get; set; }
    public string? FormatterMethod { get; set; }
}

internal sealed class SummaryModel
{
    public int Aggregate { get; set; }
    public int Scope { get; set; }
    public string? Format { get; set; }
    public string? Title { get; set; }
}

internal sealed class ConditionalRuleModel
{
    public int Condition { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string? Operand { get; set; }
    public string? Operand2 { get; set; }
    public int StringComparison { get; set; }
    public string? ThemeKey { get; set; }
    public int Priority { get; set; }
    public bool StopIfTrue { get; set; }
    public string? PredicateMethod { get; set; }
    public int Target { get; set; }
}

internal sealed class BandModel
{
    public ImmutableArray<string> Path { get; set; } = ImmutableArray<string>.Empty;
    public int Order { get; set; }
}

internal sealed class ViewModelModel
{
    public INamedTypeSymbol ViewModelType { get; set; } = null!;

    public SchemaModel Schema { get; set; } = null!;

    public string ColumnDefinitionsPropertyName { get; set; } = "ColumnDefinitions";

    public string SchemaPropertyName { get; set; } = "DataGridSchema";

    public string FastPathOptionsPropertyName { get; set; } = "FastPathOptions";

    public string NavigationModelPropertyName { get; set; } = "NavigationModel";

    public bool GenerateColumnDefinitionsProperty { get; set; } = true;

    public bool GenerateSchemaProperty { get; set; } = true;

    public bool GenerateFastPathOptionsProperty { get; set; } = true;

    public bool GenerateNavigationModelProperty { get; set; }

    public bool IsDirectIncremental { get; set; }

    public Location Location { get; set; } = Location.None;
}

internal sealed class DirectViewModelCandidate
{
    public INamedTypeSymbol ViewModelType { get; set; } = null!;

    public ImmutableArray<AttributeData> Attributes { get; set; } = ImmutableArray<AttributeData>.Empty;

    public string CacheKey { get; set; } = string.Empty;
}

internal sealed class DirectViewModelCandidateComparer : IEqualityComparer<DirectViewModelCandidate>
{
    public static readonly DirectViewModelCandidateComparer Instance = new();

    public bool Equals(DirectViewModelCandidate? x, DirectViewModelCandidate? y) =>
        ReferenceEquals(x, y) ||
        (x != null && y != null && string.Equals(x.CacheKey, y.CacheKey, StringComparison.Ordinal));

    public int GetHashCode(DirectViewModelCandidate candidate) =>
        StringComparer.Ordinal.GetHashCode(candidate.CacheKey);
}

internal sealed class DirectViewModelGenerationResult
{
    public DirectViewModelGenerationResult(
        ImmutableArray<GeneratedSource> sources,
        ImmutableArray<Diagnostic> diagnostics)
    {
        Sources = sources;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<GeneratedSource> Sources { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal sealed class ControllerModel
{
    public INamedTypeSymbol ViewModelType { get; set; } = null!;

    public SchemaModel Schema { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? SourceMember { get; set; }

    public ITypeSymbol? SourceKeyType { get; set; }

    public int SourceKind { get; set; }

    public int Features { get; set; }

    public int OperationExecution { get; set; }

    public INamedTypeSymbol? ImplementationType { get; set; }

    public string? ConfigureMethod { get; set; }

    public string? PipelineTransformMethod { get; set; }

    public bool IsDirectIncremental { get; set; }

    public Location Location { get; set; } = Location.None;
}

internal sealed class DirectControllerCandidate
{
    public INamedTypeSymbol ViewModelType { get; set; } = null!;

    public ImmutableArray<AttributeData> Attributes { get; set; } = ImmutableArray<AttributeData>.Empty;

    public string CacheKey { get; set; } = string.Empty;
}

internal sealed class DirectControllerCandidateComparer : IEqualityComparer<DirectControllerCandidate>
{
    public static readonly DirectControllerCandidateComparer Instance = new();

    public bool Equals(DirectControllerCandidate? x, DirectControllerCandidate? y) =>
        ReferenceEquals(x, y) ||
        (x != null && y != null && string.Equals(x.CacheKey, y.CacheKey, StringComparison.Ordinal));

    public int GetHashCode(DirectControllerCandidate candidate) =>
        StringComparer.Ordinal.GetHashCode(candidate.CacheKey);
}

internal sealed class DirectControllerGenerationResult
{
    public DirectControllerGenerationResult(
        ImmutableArray<GeneratedSource> sources,
        ImmutableArray<Diagnostic> diagnostics)
    {
        Sources = sources;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<GeneratedSource> Sources { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }
}

internal readonly struct GeneratedSource
{
    public GeneratedSource(string hintName, string source)
    {
        HintName = hintName;
        Source = source;
    }

    public string HintName { get; }

    public string Source { get; }
}

internal sealed class GeneratedSourceComparer : IEqualityComparer<GeneratedSource>
{
    public static GeneratedSourceComparer Instance { get; } = new();

    private GeneratedSourceComparer()
    {
    }

    public bool Equals(GeneratedSource x, GeneratedSource y) =>
        string.Equals(x.HintName, y.HintName, System.StringComparison.Ordinal) &&
        string.Equals(x.Source, y.Source, System.StringComparison.Ordinal);

    public int GetHashCode(GeneratedSource obj)
    {
        unchecked
        {
            return ((obj.HintName?.GetHashCode() ?? 0) * 397) ^ (obj.Source?.GetHashCode() ?? 0);
        }
    }
}
