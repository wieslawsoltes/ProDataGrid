// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Microsoft.CodeAnalysis;

namespace ProDataGrid.SourceGenerators;

internal static class GeneratorDiagnostics
{
    public static readonly DiagnosticDescriptor InvalidTarget = Create(
        "PDGSG001",
        "Invalid source generation target",
        "Type '{0}' cannot be used for ProDataGrid source generation: {1}");

    public static readonly DiagnosticDescriptor NoColumns = Create(
        "PDGSG002",
        "No eligible columns",
        "Type '{0}' does not contain any eligible properties for generated columns",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor UnsupportedProperty = Create(
        "PDGSG003",
        "Unsupported column property",
        "Property '{0}' cannot be generated as a ProDataGrid column: {1}");

    public static readonly DiagnosticDescriptor InvalidCustomizationMethod = Create(
        "PDGSG004",
        "Invalid customization method",
        "Customization method '{0}' on type '{1}' was not found or has an incompatible signature");

    public static readonly DiagnosticDescriptor ViewModelMustBePartial = Create(
        "PDGSG005",
        "View model must be partial",
        "View model '{0}' and each containing type must be partial to receive generated ProDataGrid members");

    public static readonly DiagnosticDescriptor MemberCollision = Create(
        "PDGSG006",
        "Generated member collision",
        "Type '{0}' already defines member '{1}', so the ProDataGrid member was not generated");

    public static readonly DiagnosticDescriptor InvalidImplementation = Create(
        "PDGSG007",
        "Invalid user implementation",
        "Implementation type '{0}' must be accessible, have an accessible parameterless constructor, and implement IDataGridGeneratedSchema<{1}>");

    public static readonly DiagnosticDescriptor InvalidNamespace = Create(
        "PDGSG008",
        "Invalid namespace target",
        "Namespace '{0}' did not match any eligible source types",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor InvalidColumnConfiguration = Create(
        "PDGSG009",
        "Invalid column configuration",
        "Property '{0}' uses column kind '{1}' but required option '{2}' was not supplied");

    public static readonly DiagnosticDescriptor InaccessibleProperty = Create(
        "PDGSG010",
        "Inaccessible column property",
        "Property '{0}' is not accessible to generated code");

    public static readonly DiagnosticDescriptor AmbiguousItemsProperty = Create(
        "PDGSG011",
        "Cannot infer view-model item type",
        "View model '{0}' does not expose an unambiguous enumerable property named '{1}'",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor MissingViewMember = Create(
        "PDGSG012",
        "Missing generated-view binding member",
        "View model '{0}' does not expose a readable property named '{1}' required by generated view '{2}'");

    public static readonly DiagnosticDescriptor InvalidViewBase = Create(
        "PDGSG013",
        "Invalid generated-view base type",
        "Base type '{0}' for generated view '{1}' must be accessible, non-sealed, and have an accessible parameterless constructor");

    public static readonly DiagnosticDescriptor MissingViewFramework = Create(
        "PDGSG014",
        "Generated-view framework is unavailable",
        "Generated view '{0}' requests framework '{1}', but its required UI framework type is not referenced");

    public static readonly DiagnosticDescriptor DuplicateStableKey = Create(
        "PDGSG100",
        "Duplicate or empty stable key",
        "Schema '{0}' contains duplicate or empty stable key '{1}'");

    public static readonly DiagnosticDescriptor InvalidItemKey = Create(
        "PDGSG101",
        "Invalid item key",
        "Member '{0}' cannot be used as the stable item key: {1}");

    public static readonly DiagnosticDescriptor InvalidControllerSource = Create(
        "PDGSG103",
        "Invalid generated controller source",
        "Source member '{0}' on view model '{1}' is missing or incompatible with source kind '{2}'");

    public static readonly DiagnosticDescriptor InvalidHierarchy = Create(
        "PDGSG109",
        "Invalid generated hierarchy",
        "Hierarchy member '{0}' on item type '{1}' is invalid: {2}");

    public static readonly DiagnosticDescriptor InvalidOperationOwnership = Create(
        "PDGSG104",
        "Conflicting generated operation ownership",
        "Controller '{0}' uses source kind '{1}' with operation execution '{2}', which would apply operations in the wrong layer");

    public static readonly DiagnosticDescriptor DuplicateController = Create(
        "PDGSG117",
        "Duplicate generated controller",
        "View model '{0}' contains more than one generated controller named '{1}'");

    public static readonly DiagnosticDescriptor InvalidStateMetadata = Create(
        "PDGSG118",
        "Invalid generated state metadata",
        "Schema '{0}' contains invalid state metadata: {1}");

    public static readonly DiagnosticDescriptor InvalidFormulaMetadata = Create(
        "PDGSG121",
        "Invalid generated formula metadata",
        "Formula field '{0}' contains invalid generated metadata: {1}");

    public static readonly DiagnosticDescriptor InvalidDrawOperationFactory = Create(
        "PDGSG122",
        "Invalid custom drawing factory",
        "Custom drawing factory '{0}' for property '{1}' is invalid: {2}");

    public static readonly DiagnosticDescriptor InvalidRowDetails = Create(
        "PDGSG123",
        "Invalid generated row details",
        "Generated row details for view '{0}' are invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidAuxiliaryBinding = Create(
        "PDGSG124",
        "Invalid generated column binding",
        "Generated binding option '{0}' for property '{1}' is invalid: {2}");

    public static readonly DiagnosticDescriptor InvalidViewState = Create(
        "PDGSG125",
        "Invalid generated view state",
        "Generated state projection for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidViewEventBridge = Create(
        "PDGSG126",
        "Invalid generated view event bridge",
        "Generated routed-event bridge for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidViewInteraction = Create(
        "PDGSG127",
        "Invalid generated view interaction",
        "Generated ReactiveUI interaction for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidViewPerformanceIntegration = Create(
        "PDGSG128",
        "Invalid generated view performance integration",
        "Generated performance, input, or diagnostics integration for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidViewTransferIntegration = Create(
        "PDGSG129",
        "Invalid generated view transfer integration",
        "Generated clipboard or fill integration for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidViewFormulaIntegration = Create(
        "PDGSG130",
        "Invalid generated view formula integration",
        "Generated formula integration for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidViewConditionalFormattingIntegration = Create(
        "PDGSG131",
        "Invalid generated view conditional-formatting integration",
        "Generated conditional-formatting integration for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor AmbiguousInterfaceProperty = Create(
        "PDGSG132",
        "Ambiguous inherited interface property",
        "Interface schema '{0}' inherits unrelated properties named '{1}' from '{2}' and '{3}'; redeclare the property on the schema interface to select one contract");

    public static readonly DiagnosticDescriptor AmbiguousExplicitInterfaceProperty = Create(
        "PDGSG133",
        "Ambiguous explicit interface property",
        "Type schema '{0}' explicitly implements same-name property '{1}' from both '{2}' and '{3}'; expose one public forwarding property to select a contract");

    public static readonly DiagnosticDescriptor RuntimeShapeRequiresProvider = Create(
        "PDGSG134",
        "Runtime-defined shape requires an explicit provider",
        "Type '{0}' has a runtime-defined field shape; configure ImplementationType with an explicit IDataGridGeneratedSchema<{0}> implementation, typically based on DataGridRuntimeSchemaAdapter<{0}>");

    public static readonly DiagnosticDescriptor InvalidMutationHandler = Create(
        "PDGSG135",
        "Invalid collection mutation handler",
        "Mutation handler type '{0}' must be accessible, non-abstract, have an accessible parameterless constructor, and implement IDataGridGeneratedCollectionMutationHandler<{1}>");

    public static readonly DiagnosticDescriptor InvalidNewRowFactory = Create(
        "PDGSG136",
        "Invalid new-row factory",
        "New-row factory type '{0}' must be accessible, non-abstract, have an accessible parameterless constructor, and implement IDataGridGeneratedNewRowFactory<{1}>");

    public static readonly DiagnosticDescriptor InvalidFormulaFillTranslator = Create(
        "PDGSG137",
        "Invalid formula fill translator",
        "Formula fill translator type '{0}' must be accessible, non-abstract, have an accessible parameterless constructor, and implement IFormulaFillTranslator");

    public static readonly DiagnosticDescriptor InvalidFormulaSyntax = Create(
        "PDGSG138",
        "Invalid generated formula syntax",
        "Formula column '{0}' contains invalid formula syntax at position {1}: {2}");

    public static readonly DiagnosticDescriptor InvalidViewPresentation = Create(
        "PDGSG139",
        "Invalid generated view presentation",
        "Generated presentation for view '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidCollectionViewDefaults = Create(
        "PDGSG140",
        "Invalid generated collection-view defaults",
        "Generated collection-view defaults for item type '{0}' are invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidViewNavigationIntegration = Create(
        "PDGSG141",
        "Invalid generated view navigation integration",
        "Generated navigation integration for view '{0}' is invalid: {1}");

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            message,
            "ProDataGrid.SourceGeneration",
            severity,
            isEnabledByDefault: true);
    }
}
