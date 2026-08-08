// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProDataGrid.SourceGenerators;

internal static partial class Discovery
{
    internal static bool HasCompilationWideRequests(ImmutableArray<AttributeData> assemblyAttributes) =>
        HasGlobalSchemaPolicies(assemblyAttributes) ||
        HasGlobalViewModelPolicies(assemblyAttributes) ||
        HasGlobalViewPolicies(assemblyAttributes);

    public static GenerationModel Build(Compilation compilation, CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        INamedTypeSymbol[] sourceTypes = GeneratorUtilities
            .EnumerateTypes(compilation.Assembly.GlobalNamespace)
            .Where(static type => type.Locations.Any(static location => location.IsInSource))
            .OrderBy(GeneratorUtilities.GetMetadataName, StringComparer.Ordinal)
            .ToArray();

        var schemas = new Dictionary<INamedTypeSymbol, SchemaModel>(SymbolEqualityComparer.Default);
        var viewModels = new Dictionary<string, PendingViewModel>(StringComparer.Ordinal);
        var controllers = new List<ControllerModel>();
        ImmutableArray<AttributeData> assemblyAttributes = compilation.Assembly.GetAttributes();
        bool hasGlobalSchemaPolicies = HasGlobalSchemaPolicies(assemblyAttributes);

        DiscoverNamespaceSchemas(sourceTypes, assemblyAttributes, schemas, diagnostics, cancellationToken);
        DiscoverAssemblySchemas(assemblyAttributes, schemas, diagnostics);
        DiscoverTypeAndPropertySchemas(sourceTypes, schemas, diagnostics, cancellationToken, !hasGlobalSchemaPolicies);

        DiscoverNamespaceViewModels(sourceTypes, assemblyAttributes, schemas, viewModels, diagnostics, cancellationToken);
        DiscoverAssemblyViewModels(assemblyAttributes, schemas, viewModels, diagnostics);
        bool enableDirectViewModels = !hasGlobalSchemaPolicies && !HasGlobalViewModelPolicies(assemblyAttributes);
        DiscoverTypeViewModels(sourceTypes, schemas, viewModels, diagnostics, cancellationToken, enableDirectViewModels);
        if (hasGlobalSchemaPolicies)
        {
            DiscoverTypeControllers(
                sourceTypes,
                schemas,
                controllers,
                diagnostics,
                cancellationToken,
                enableDirectIncremental: false);
        }

        ResolveProviderCollisions(schemas.Values);

        foreach (SchemaModel schema in schemas.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImmutableArray<Diagnostic>.Builder schemaDiagnostics = schema.IsDirectIncremental
                ? ImmutableArray.CreateBuilder<Diagnostic>()
                : diagnostics;
            if (!ValidateSchemaTarget(schema, schemaDiagnostics))
            {
                schema.Columns = ImmutableArray<ColumnModel>.Empty;
                continue;
            }

            if (schema.ImplementationType != null &&
                !ValidateImplementation(schema.ItemType, schema.ImplementationType))
            {
                schemaDiagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidImplementation,
                    schema.Location,
                    schema.ImplementationType.ToDisplayString(),
                    schema.ItemType.ToDisplayString()));
                schema.ImplementationType = null;
            }

            schema.Columns = DiscoverColumns(schema, schemaDiagnostics, cancellationToken);
            ValidateFormulaMetadata(schema, schemaDiagnostics);
            schema.KeyMember = DiscoverKeyMember(schema, schemaDiagnostics, cancellationToken);
            schema.Hierarchy = DiscoverHierarchy(schema, schemaDiagnostics, cancellationToken);
            if (schema.Columns.Length == 0 && schema.ImplementationType == null)
            {
                schemaDiagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.NoColumns,
                    schema.Location,
                    schema.ItemType.ToDisplayString()));
            }

            if (!string.IsNullOrEmpty(schema.ConfigureMethod) &&
                !HasGlobalConfigureMethod(schema.ItemType, schema.ConfigureMethod!))
            {
                schemaDiagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidCustomizationMethod,
                    schema.Location,
                    schema.ConfigureMethod,
                    schema.ItemType.ToDisplayString()));
                schema.ConfigureMethod = null;
            }
        }

        ValidateControllerKeys(controllers, diagnostics);

        ImmutableArray<ViewModelModel> resolvedViewModels = ResolveViewModels(
            schemas,
            viewModels,
            diagnostics,
            cancellationToken,
            suppressDirectDiagnostics: true);

        ImmutableArray<SchemaModel> orderedSchemas = schemas.Values
            .OrderBy(static schema => schema.ProviderNamespace, StringComparer.Ordinal)
            .ThenBy(static schema => schema.ProviderName, StringComparer.Ordinal)
            .ToImmutableArray();

        ImmutableArray<ViewModelViewModel> views = DiscoverViews(
            compilation,
            sourceTypes,
            assemblyAttributes,
            resolvedViewModels,
            diagnostics,
            cancellationToken);

        RegistryModel? registry = DiscoverRegistry(compilation, assemblyAttributes, diagnostics);
        return new GenerationModel(
            orderedSchemas,
            resolvedViewModels,
            controllers.OrderBy(static controller => GeneratorUtilities.GetMetadataName(controller.ViewModelType), StringComparer.Ordinal)
                .ThenBy(static controller => controller.Name, StringComparer.Ordinal)
                .ToImmutableArray(),
            views,
            registry,
            diagnostics.ToImmutable());
    }

    public static DirectSchemaCandidate? CreateDirectSchemaCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetType ||
            (targetType.TypeKind != TypeKind.Class && targetType.TypeKind != TypeKind.Struct) ||
            HasGlobalSchemaPolicies(targetType.ContainingAssembly.GetAttributes()))
        {
            return null;
        }

        return new DirectSchemaCandidate
        {
            TargetType = targetType,
            Attributes = context.Attributes,
            SourceKind = DirectSchemaSourceKind.Schema,
            CacheKey = "schema|" + CreateDirectSchemaCacheKey(targetType, context.Attributes)
        };
    }

    public static DirectSchemaCandidate? CreateDirectPropertySchemaCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not IPropertySymbol property ||
            property.ContainingType is not { } targetType ||
            (targetType.TypeKind != TypeKind.Class && targetType.TypeKind != TypeKind.Struct) ||
            HasGlobalSchemaPolicies(targetType.ContainingAssembly.GetAttributes()))
        {
            return null;
        }

        return new DirectSchemaCandidate
        {
            TargetType = targetType,
            Attributes = context.Attributes,
            SourceKind = DirectSchemaSourceKind.Property,
            CacheKey = "property|" + CreateDirectSchemaCacheKey(targetType, context.Attributes)
        };
    }

    public static DirectSchemaCandidate? CreateDirectOwnerSchemaCandidate(
        GeneratorAttributeSyntaxContext context,
        DirectSchemaSourceKind sourceKind)
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetType ||
            targetType.TypeKind != TypeKind.Class ||
            HasGlobalSchemaPolicies(targetType.ContainingAssembly.GetAttributes()))
        {
            return null;
        }

        return new DirectSchemaCandidate
        {
            TargetType = targetType,
            Attributes = context.Attributes,
            SourceKind = sourceKind,
            CacheKey = ((int)sourceKind).ToString(CultureInfo.InvariantCulture) + "|" +
                CreateDirectSchemaCacheKey(targetType, context.Attributes)
        };
    }

    private static string CreateDirectSchemaCacheKey(
        INamedTypeSymbol targetType,
        ImmutableArray<AttributeData> attributes)
    {
        var builder = new StringBuilder(2048);
        var visitedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        AppendTypeFingerprint(builder, targetType, visitedTypes);
        foreach (AttributeData attribute in attributes)
        {
            AppendAttributeFingerprint(builder, attribute, visitedTypes);
        }

        return builder.ToString();
    }

    private static void AppendTypeFingerprint(
        StringBuilder builder,
        INamedTypeSymbol type,
        HashSet<INamedTypeSymbol> visitedTypes)
    {
        if (!visitedTypes.Add(type))
        {
            return;
        }

        builder.Append('|').Append(GeneratorUtilities.GetMetadataName(type));
        foreach (SyntaxReference syntaxReference in type.DeclaringSyntaxReferences)
        {
            builder.Append('|').Append(syntaxReference.GetSyntax().ToFullString());
        }

        if (type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
        {
            AppendTypeFingerprint(builder, type.BaseType, visitedTypes);
        }
    }

    private static void AppendAttributeFingerprint(
        StringBuilder builder,
        AttributeData attribute,
        HashSet<INamedTypeSymbol> visitedTypes)
    {
        builder.Append("|attribute:")
            .Append(attribute.AttributeClass == null ? string.Empty : GeneratorUtilities.GetMetadataName(attribute.AttributeClass));
        foreach (TypedConstant argument in attribute.ConstructorArguments)
        {
            AppendTypedConstantFingerprint(builder, argument, visitedTypes);
        }

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append('|').Append(argument.Key);
            AppendTypedConstantFingerprint(builder, argument.Value, visitedTypes);
        }
    }

    private static void AppendTypedConstantFingerprint(
        StringBuilder builder,
        TypedConstant constant,
        HashSet<INamedTypeSymbol> visitedTypes)
    {
        if (constant.Kind == TypedConstantKind.Array)
        {
            foreach (TypedConstant item in constant.Values)
            {
                AppendTypedConstantFingerprint(builder, item, visitedTypes);
            }

            return;
        }

        if (constant.Value is INamedTypeSymbol type)
        {
            AppendTypeFingerprint(builder, type, visitedTypes);
            return;
        }

        builder.Append('|').Append(constant.Value?.ToString() ?? "null");
    }

    public static DirectSchemaGenerationResult BuildDirectSchemas(
        ImmutableArray<DirectSchemaCandidate> schemaCandidates,
        ImmutableArray<DirectSchemaCandidate> propertyCandidates,
        ImmutableArray<DirectSchemaCandidate> viewModelCandidates,
        ImmutableArray<DirectSchemaCandidate> controllerCandidates,
        CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var schemas = new Dictionary<INamedTypeSymbol, SchemaModel>(SymbolEqualityComparer.Default);
        foreach (DirectSchemaCandidate candidate in schemaCandidates
                     .OrderBy(static candidate => GeneratorUtilities.GetMetadataName(candidate.TargetType), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (AttributeData attribute in candidate.Attributes)
            {
                INamedTypeSymbol itemType = GetConstructorType(attribute, 0) ?? candidate.TargetType;
                AddOrUpdateSchema(schemas, itemType, attribute, explicitProviderName: null, explicitConfiguration: true);
            }
        }

        foreach (DirectSchemaCandidate candidate in propertyCandidates
                     .OrderBy(static candidate => GeneratorUtilities.GetMetadataName(candidate.TargetType), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!schemas.ContainsKey(candidate.TargetType))
            {
                schemas.Add(
                    candidate.TargetType,
                    CreateDefaultSchema(
                        candidate.TargetType,
                        GeneratorUtilities.GetLocation(candidate.TargetType),
                        attributedOnly: true));
            }
        }

        ApplyDirectOwnerSchemaRequests(viewModelCandidates, schemas, isController: false, diagnostics, cancellationToken);
        ApplyDirectOwnerSchemaRequests(controllerCandidates, schemas, isController: true, diagnostics, cancellationToken);

        ResolveProviderCollisions(schemas.Values);
        var sources = ImmutableArray.CreateBuilder<GeneratedSource>();
        foreach (SchemaModel schema in schemas.Values
                     .OrderBy(static schema => schema.ProviderNamespace, StringComparer.Ordinal)
                     .ThenBy(static schema => schema.ProviderName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidateSchemaTarget(schema, diagnostics))
            {
                continue;
            }

            if (schema.ImplementationType != null &&
                !ValidateImplementation(schema.ItemType, schema.ImplementationType))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidImplementation,
                    schema.Location,
                    schema.ImplementationType.ToDisplayString(),
                    schema.ItemType.ToDisplayString()));
                schema.ImplementationType = null;
            }

            schema.Columns = DiscoverColumns(schema, diagnostics, cancellationToken);
            ValidateFormulaMetadata(schema, diagnostics);
            schema.KeyMember = DiscoverKeyMember(schema, diagnostics, cancellationToken);
            schema.Hierarchy = DiscoverHierarchy(schema, diagnostics, cancellationToken);
            if (schema.Columns.Length == 0 && schema.ImplementationType == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.NoColumns,
                    schema.Location,
                    schema.ItemType.ToDisplayString()));
                continue;
            }

            if (!string.IsNullOrEmpty(schema.ConfigureMethod) &&
                !HasGlobalConfigureMethod(schema.ItemType, schema.ConfigureMethod!))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidCustomizationMethod,
                    schema.Location,
                    schema.ConfigureMethod,
                    schema.ItemType.ToDisplayString()));
                schema.ConfigureMethod = null;
            }

            sources.Add(Emitter.EmitSchemaSource(schema));
        }

        return new DirectSchemaGenerationResult(sources.ToImmutable(), diagnostics.ToImmutable());
    }

    private static void ApplyDirectOwnerSchemaRequests(
        ImmutableArray<DirectSchemaCandidate> candidates,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        bool isController,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (DirectSchemaCandidate candidate in candidates
                     .OrderBy(static candidate => GeneratorUtilities.GetMetadataName(candidate.TargetType), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (AttributeData attribute in candidate.Attributes)
            {
                INamedTypeSymbol? itemType = GetConstructorType(attribute, 0);
                if (itemType == null)
                {
                    continue;
                }

                Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
                string? providerName = GeneratorUtilities.GetString(arguments, "ProviderName");
                if (!schemas.ContainsKey(itemType) && itemType.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Any(static property => GeneratorUtilities.HasAttribute(
                        property,
                        ProDataGridGenerator.ColumnAttributeName)))
                {
                    schemas.Add(itemType, CreateDefaultSchema(itemType, GetLocation(attribute), attributedOnly: true));
                }

                SchemaModel schema = EnsureSchema(schemas, itemType, attribute, providerName);
                ApplyFastOptions(schema, arguments);
                if (!isController)
                {
                    continue;
                }

                string? keyMember = GeneratorUtilities.GetString(arguments, "KeyMember");
                if (!string.IsNullOrWhiteSpace(keyMember))
                {
                    if (!string.IsNullOrEmpty(schema.ExplicitKeyMemberName) &&
                        !string.Equals(schema.ExplicitKeyMemberName, keyMember, StringComparison.Ordinal))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            GeneratorDiagnostics.InvalidItemKey,
                            GetLocation(attribute),
                            keyMember,
                            "controllers sharing a schema must use the same key member"));
                    }
                    else
                    {
                        schema.ExplicitKeyMemberName = keyMember;
                    }
                }

                int sourceKind = GetEnumValue(arguments, "SourceKind", 0);
                if (sourceKind == 4 || sourceKind == 5)
                {
                    schema.Streaming = true;
                }
            }
        }
    }

    private static bool HasGlobalSchemaPolicies(ImmutableArray<AttributeData> assemblyAttributes)
    {
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (IsAttribute(attribute, ProDataGridGenerator.GenerateColumnsAttributeName) ||
                IsAttribute(attribute, ProDataGridGenerator.GenerateColumnsForNamespaceAttributeName) ||
                IsAttribute(attribute, ProDataGridGenerator.GenerateRegistryAttributeName))
            {
                return true;
            }
        }

        return false;
    }

    private static RegistryModel? DiscoverRegistry(
        Compilation compilation,
        ImmutableArray<AttributeData> assemblyAttributes,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        RegistryModel? registry = null;
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (!IsAttribute(attribute, ProDataGridGenerator.GenerateRegistryAttributeName))
            {
                continue;
            }

            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            registry = new RegistryModel
            {
                RegistryName = GeneratorUtilities.SanitizeIdentifier(
                    GeneratorUtilities.GetString(arguments, "RegistryName") ?? "GeneratedProDataGridRegistration"),
                RegistryNamespace = GeneratorUtilities.GetString(arguments, "RegistryNamespace") ?? "ProDataGrid.Generated",
                IsPublic = compilation.GetTypeByMetadataName("Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider")?.DeclaredAccessibility == Accessibility.Public,
                HasMicrosoftDependencyInjection =
                    compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection") != null &&
                    compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceDescriptor") != null
            };
            break;
        }

        if (registry == null)
        {
            return null;
        }

        var registrations = ImmutableArray.CreateBuilder<ViewRegistrationModel>();
        var registeredViewModels = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (!IsAttribute(attribute, ProDataGridGenerator.ViewRegistrationAttributeName))
            {
                continue;
            }

            INamedTypeSymbol? viewModelType = GetConstructorType(attribute, 0);
            INamedTypeSymbol? viewType = GetConstructorType(attribute, 1);
            if (viewModelType == null || viewType == null ||
                !GeneratorUtilities.IsAccessibleFromGeneratedCode(viewType) ||
                !IsOrDerivesFrom(viewType, "Avalonia.Controls.Control") ||
                !viewType.InstanceConstructors.Any(static constructor =>
                    constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor)))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidTarget,
                    GetLocation(attribute),
                    viewType?.ToDisplayString() ?? "(unknown)",
                    "registered views must derive from Avalonia.Controls.Control and expose an accessible parameterless constructor"));
                continue;
            }

            if (!registeredViewModels.Add(viewModelType))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidTarget,
                    GetLocation(attribute),
                    viewModelType.ToDisplayString(),
                    "only one registered view is allowed for each view-model type"));
                continue;
            }

            registrations.Add(new ViewRegistrationModel
            {
                ViewModelType = viewModelType,
                ViewType = viewType
            });
        }

        registry.ViewRegistrations = registrations
            .OrderBy(static registration => GeneratorUtilities.GetMetadataName(registration.ViewModelType), StringComparer.Ordinal)
            .ToImmutableArray();
        return registry;
    }

    private static void DiscoverNamespaceSchemas(
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        ImmutableArray<AttributeData> assemblyAttributes,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (!IsAttribute(attribute, ProDataGridGenerator.GenerateColumnsForNamespaceAttributeName))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string? namespaceName = GetConstructorString(attribute, 0);
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.InvalidNamespace, GetLocation(attribute), namespaceName ?? string.Empty));
                continue;
            }

            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            bool includeNested = GeneratorUtilities.GetBoolean(arguments, "IncludeNestedNamespaces", true);
            INamedTypeSymbol[] matches = sourceTypes
                .Where(type => NamespaceMatches(type, namespaceName!, includeNested) && IsEligibleItemType(type))
                .ToArray();
            if (matches.Length == 0)
            {
                diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.InvalidNamespace, GetLocation(attribute), namespaceName));
            }

            foreach (INamedTypeSymbol type in matches)
            {
                AddOrUpdateSchema(schemas, type, attribute, explicitProviderName: null, explicitConfiguration: false);
            }
        }
    }

    private static void DiscoverAssemblySchemas(
        ImmutableArray<AttributeData> assemblyAttributes,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (!IsAttribute(attribute, ProDataGridGenerator.GenerateColumnsAttributeName))
            {
                continue;
            }

            INamedTypeSymbol? itemType = GetConstructorType(attribute, 0);
            if (itemType == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidTarget,
                    GetLocation(attribute),
                    "(unknown)",
                    "assembly-level generation requires an item type"));
                continue;
            }

            AddOrUpdateSchema(schemas, itemType, attribute, explicitProviderName: null, explicitConfiguration: true);
        }
    }

    private static void DiscoverTypeAndPropertySchemas(
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken,
        bool enableDirectIncremental)
    {
        foreach (INamedTypeSymbol type in sourceTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (AttributeData attribute in type.GetAttributes())
            {
                if (!IsAttribute(attribute, ProDataGridGenerator.GenerateColumnsAttributeName))
                {
                    continue;
                }

                INamedTypeSymbol itemType = GetConstructorType(attribute, 0) ?? type;
                AddOrUpdateSchema(schemas, itemType, attribute, explicitProviderName: null, explicitConfiguration: true);
                if (enableDirectIncremental)
                {
                    schemas[itemType].IsDirectIncremental = true;
                }
            }

            bool hasColumnAttribute = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(static property => GeneratorUtilities.HasAttribute(property, ProDataGridGenerator.ColumnAttributeName));
            if (hasColumnAttribute && !schemas.ContainsKey(type))
            {
                schemas.Add(type, CreateDefaultSchema(type, GeneratorUtilities.GetLocation(type), attributedOnly: true));
            }
        }
    }

    private static void DiscoverNamespaceViewModels(
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        ImmutableArray<AttributeData> assemblyAttributes,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        Dictionary<string, PendingViewModel> viewModels,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (!IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelsForNamespaceAttributeName))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string? namespaceName = GetConstructorString(attribute, 0);
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.InvalidNamespace, GetLocation(attribute), namespaceName ?? string.Empty));
                continue;
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

                SchemaModel schema = EnsureSchema(schemas, itemType, attribute, explicitProviderName: null);
                ApplyFastOptions(schema, arguments);
                AddPendingViewModel(viewModels, CreatePendingViewModel(viewModelType, itemType, attribute, arguments));
            }
        }
    }

    private static void DiscoverAssemblyViewModels(
        ImmutableArray<AttributeData> assemblyAttributes,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        Dictionary<string, PendingViewModel> viewModels,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (!IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelAttributeName))
            {
                continue;
            }

            INamedTypeSymbol? viewModelType = GetConstructorType(attribute, 0);
            INamedTypeSymbol? itemType = GetConstructorType(attribute, 1);
            if (viewModelType == null || itemType == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidTarget,
                    GetLocation(attribute),
                    viewModelType?.ToDisplayString() ?? "(unknown)",
                    "assembly-level view-model generation requires view-model and item types"));
                continue;
            }

            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            string? providerName = GeneratorUtilities.GetString(arguments, "ProviderName");
            SchemaModel schema = EnsureSchema(schemas, itemType, attribute, providerName);
            ApplyFastOptions(schema, arguments);
            AddPendingViewModel(viewModels, CreatePendingViewModel(viewModelType, itemType, attribute, arguments));
        }
    }

    private static void DiscoverTypeViewModels(
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        Dictionary<string, PendingViewModel> viewModels,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken,
        bool enableDirectIncremental)
    {
        foreach (INamedTypeSymbol viewModelType in sourceTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (AttributeData attribute in viewModelType.GetAttributes())
            {
                if (!IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelAttributeName))
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
                        "view-model generation requires an item type"));
                    continue;
                }

                Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
                string? providerName = GeneratorUtilities.GetString(arguments, "ProviderName");
                SchemaModel schema = EnsureSchema(schemas, itemType, attribute, providerName);
                if (enableDirectIncremental)
                {
                    schema.IsDirectIncremental = true;
                }
                ApplyFastOptions(schema, arguments);
                PendingViewModel pending = CreatePendingViewModel(viewModelType, itemType, attribute, arguments);
                pending.IsDirectIncremental = enableDirectIncremental;
                AddPendingViewModel(viewModels, pending);
            }
        }
    }

    public static DirectViewModelCandidate? CreateDirectViewModelCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol viewModelType ||
            viewModelType.TypeKind != TypeKind.Class)
        {
            return null;
        }

        ImmutableArray<AttributeData> assemblyAttributes = viewModelType.ContainingAssembly.GetAttributes();
        if (HasGlobalSchemaPolicies(assemblyAttributes) || HasGlobalViewModelPolicies(assemblyAttributes))
        {
            return null;
        }

        return new DirectViewModelCandidate
        {
            ViewModelType = viewModelType,
            Attributes = context.Attributes,
            CacheKey = CreateDirectSchemaCacheKey(viewModelType, context.Attributes)
        };
    }

    public static DirectViewModelGenerationResult BuildDirectViewModels(
        ImmutableArray<DirectViewModelCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var schemas = new Dictionary<INamedTypeSymbol, SchemaModel>(SymbolEqualityComparer.Default);
        var pendingViewModels = new Dictionary<string, PendingViewModel>(StringComparer.Ordinal);
        INamedTypeSymbol[] itemTypes = candidates
            .SelectMany(static candidate => candidate.Attributes)
            .Select(static attribute => GetConstructorType(attribute, 0))
            .Where(static itemType => itemType != null)
            .Select(static itemType => itemType!)
            .GroupBy(GeneratorUtilities.GetMetadataName, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
        DiscoverTypeAndPropertySchemas(
            itemTypes,
            schemas,
            diagnostics,
            cancellationToken,
            enableDirectIncremental: false);
        INamedTypeSymbol[] viewModelTypes = candidates
            .OrderBy(static candidate => GeneratorUtilities.GetMetadataName(candidate.ViewModelType), StringComparer.Ordinal)
            .Select(static candidate => candidate.ViewModelType)
            .ToArray();
        DiscoverTypeViewModels(
            viewModelTypes,
            schemas,
            pendingViewModels,
            diagnostics,
            cancellationToken,
            enableDirectIncremental: false);
        ResolveProviderCollisions(schemas.Values);

        ImmutableArray<GeneratedSource> sources = ResolveViewModels(
                schemas,
                pendingViewModels,
                diagnostics,
                cancellationToken,
                suppressDirectDiagnostics: false)
            .Where(static model => model.GenerateColumnDefinitionsProperty ||
                                   model.GenerateSchemaProperty ||
                                   model.GenerateFastPathOptionsProperty)
            .Select(Emitter.EmitViewModelSource)
            .ToImmutableArray();

        return new DirectViewModelGenerationResult(sources, diagnostics.ToImmutable());
    }

    private static ImmutableArray<ViewModelModel> ResolveViewModels(
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        Dictionary<string, PendingViewModel> pendingViewModels,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken,
        bool suppressDirectDiagnostics)
    {
        var resolved = ImmutableArray.CreateBuilder<ViewModelModel>();
        var generatedMembers = new Dictionary<INamedTypeSymbol, HashSet<string>>(SymbolEqualityComparer.Default);
        foreach (PendingViewModel pending in pendingViewModels.Values
                     .OrderBy(static model => GeneratorUtilities.GetMetadataName(model.ViewModelType), StringComparer.Ordinal)
                     .ThenBy(static model => model.ColumnDefinitionsPropertyName, StringComparer.Ordinal)
                     .ThenBy(static model => GeneratorUtilities.GetMetadataName(model.ItemType), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImmutableArray<Diagnostic>.Builder viewModelDiagnostics = suppressDirectDiagnostics && pending.IsDirectIncremental
                ? ImmutableArray.CreateBuilder<Diagnostic>()
                : diagnostics;
            if (!AllContainingTypesArePartial(pending.ViewModelType))
            {
                viewModelDiagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.ViewModelMustBePartial,
                    pending.Location,
                    pending.ViewModelType.ToDisplayString()));
                continue;
            }

            if (pending.ViewModelType.TypeParameters.Length != 0)
            {
                viewModelDiagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidTarget,
                    pending.Location,
                    pending.ViewModelType.ToDisplayString(),
                    "open generic view models are not supported"));
                continue;
            }

            var model = new ViewModelModel
            {
                ViewModelType = pending.ViewModelType,
                Schema = schemas[pending.ItemType],
                ColumnDefinitionsPropertyName = pending.ColumnDefinitionsPropertyName,
                SchemaPropertyName = pending.SchemaPropertyName,
                FastPathOptionsPropertyName = pending.FastPathOptionsPropertyName,
                Location = pending.Location,
                IsDirectIncremental = pending.IsDirectIncremental
            };
            model.GenerateColumnDefinitionsProperty = ValidateGeneratedViewModelMember(
                pending.ViewModelType,
                model.ColumnDefinitionsPropertyName,
                generatedMembers,
                viewModelDiagnostics,
                pending.Location);
            model.GenerateSchemaProperty = ValidateGeneratedViewModelMember(
                pending.ViewModelType,
                model.SchemaPropertyName,
                generatedMembers,
                viewModelDiagnostics,
                pending.Location);
            model.GenerateFastPathOptionsProperty = ValidateGeneratedViewModelMember(
                pending.ViewModelType,
                model.FastPathOptionsPropertyName,
                generatedMembers,
                viewModelDiagnostics,
                pending.Location);
            resolved.Add(model);
        }

        return resolved.ToImmutable();
    }

    private static bool HasGlobalViewModelPolicies(ImmutableArray<AttributeData> assemblyAttributes)
    {
        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelAttributeName) ||
                IsAttribute(attribute, ProDataGridGenerator.GenerateViewModelsForNamespaceAttributeName))
            {
                return true;
            }
        }

        return false;
    }

    public static DirectControllerCandidate? CreateDirectControllerCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol viewModelType ||
            viewModelType.TypeKind != TypeKind.Class ||
            HasGlobalSchemaPolicies(viewModelType.ContainingAssembly.GetAttributes()))
        {
            return null;
        }

        return new DirectControllerCandidate
        {
            ViewModelType = viewModelType,
            Attributes = context.Attributes,
            CacheKey = CreateDirectSchemaCacheKey(viewModelType, context.Attributes)
        };
    }

    public static DirectControllerGenerationResult BuildDirectControllers(
        ImmutableArray<DirectControllerCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var schemas = new Dictionary<INamedTypeSymbol, SchemaModel>(SymbolEqualityComparer.Default);
        var controllers = new List<ControllerModel>();
        INamedTypeSymbol[] itemTypes = candidates
            .SelectMany(static candidate => candidate.Attributes)
            .Select(static attribute => GetConstructorType(attribute, 0))
            .Where(static itemType => itemType != null)
            .Select(static itemType => itemType!)
            .GroupBy(GeneratorUtilities.GetMetadataName, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
        DiscoverTypeAndPropertySchemas(
            itemTypes,
            schemas,
            diagnostics,
            cancellationToken,
            enableDirectIncremental: false);
        INamedTypeSymbol[] viewModelTypes = candidates
            .OrderBy(static candidate => GeneratorUtilities.GetMetadataName(candidate.ViewModelType), StringComparer.Ordinal)
            .Select(static candidate => candidate.ViewModelType)
            .ToArray();
        DiscoverTypeControllers(
            viewModelTypes,
            schemas,
            controllers,
            diagnostics,
            cancellationToken,
            enableDirectIncremental: false);
        ResolveProviderCollisions(schemas.Values);

        foreach (SchemaModel schema in schemas.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidateSchemaTarget(schema, diagnostics))
            {
                continue;
            }

            schema.Columns = DiscoverColumns(schema, ImmutableArray.CreateBuilder<Diagnostic>(), cancellationToken);
            schema.KeyMember = DiscoverKeyMember(schema, ImmutableArray.CreateBuilder<Diagnostic>(), cancellationToken);
        }

        ValidateControllerKeys(controllers, diagnostics);
        ImmutableArray<GeneratedSource> sources = controllers
            .OrderBy(static controller => GeneratorUtilities.GetMetadataName(controller.ViewModelType), StringComparer.Ordinal)
            .ThenBy(static controller => controller.Name, StringComparer.Ordinal)
            .Select(Emitter.EmitControllerSource)
            .ToImmutableArray();
        return new DirectControllerGenerationResult(sources, diagnostics.ToImmutable());
    }

    private static void ValidateControllerKeys(
        List<ControllerModel> controllers,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        for (int index = controllers.Count - 1; index >= 0; index--)
        {
            ControllerModel controller = controllers[index];
            bool requiresKey = controller.SourceKind == 3 || controller.SourceKind == 4 ||
                controller.SourceKind == 5 || controller.SourceKind == 6 ||
                (controller.Features & ((1 << 4) | (1 << 5) | (1 << 13))) != 0;
            if (requiresKey && controller.Schema.KeyMember == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    controller.Location,
                    controller.Name,
                    "the selected source or controller features require a stable [DataGridKey] or KeyMember"));
                controllers.RemoveAt(index);
                continue;
            }

            if (controller.SourceKeyType != null && controller.Schema.KeyMember != null &&
                !SymbolEqualityComparer.Default.Equals(controller.SourceKeyType, controller.Schema.KeyMember.Type))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    controller.Location,
                    controller.Name,
                    "the SourceCache key type does not match the generated schema key type"));
                controllers.RemoveAt(index);
            }
        }
    }

    private static void DiscoverTypeControllers(
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        List<ControllerModel> controllers,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken,
        bool enableDirectIncremental)
    {
        foreach (INamedTypeSymbol viewModelType in sourceTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (AttributeData attribute in viewModelType.GetAttributes())
            {
                if (!IsAttribute(attribute, ProDataGridGenerator.GenerateControllerAttributeName))
                {
                    continue;
                }

                INamedTypeSymbol? itemType = GetConstructorType(attribute, 0);
                string? rawName = GetConstructorString(attribute, 1);
                if (itemType == null || string.IsNullOrWhiteSpace(rawName))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidTarget,
                        GetLocation(attribute),
                        viewModelType.ToDisplayString(),
                        "controller generation requires an item type and a non-empty name"));
                    continue;
                }

                string name = GeneratorUtilities.SanitizeIdentifier(rawName!);
                if (name.Length > 0 && name[0] == '@')
                {
                    name = name.Substring(1);
                }
                if (!names.Add(name))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.DuplicateController,
                        GetLocation(attribute),
                        viewModelType.ToDisplayString(),
                        name));
                    continue;
                }

                if (!AllContainingTypesArePartial(viewModelType))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.ViewModelMustBePartial,
                        GetLocation(attribute),
                        viewModelType.ToDisplayString()));
                    continue;
                }

                if (viewModelType.TypeParameters.Length != 0)
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidTarget,
                        GetLocation(attribute),
                        viewModelType.ToDisplayString(),
                        "open generic controller view models are not supported"));
                    continue;
                }

                Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
                string? providerName = GeneratorUtilities.GetString(arguments, "ProviderName");
                SchemaModel schema = EnsureSchema(schemas, itemType, attribute, providerName);
                if (enableDirectIncremental)
                {
                    schema.IsDirectIncremental = true;
                }
                ApplyFastOptions(schema, arguments);
                string? keyMember = GeneratorUtilities.GetString(arguments, "KeyMember");
                if (!string.IsNullOrWhiteSpace(keyMember))
                {
                    if (!string.IsNullOrEmpty(schema.ExplicitKeyMemberName) &&
                        !string.Equals(schema.ExplicitKeyMemberName, keyMember, StringComparison.Ordinal))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            GeneratorDiagnostics.InvalidItemKey,
                            GetLocation(attribute),
                            keyMember,
                            "controllers sharing a schema must use the same key member"));
                        continue;
                    }

                    schema.ExplicitKeyMemberName = keyMember;
                }

                string? sourceMember = GeneratorUtilities.GetString(arguments, "SourceMember");
                int sourceKind = GetEnumValue(arguments, "SourceKind", 0);
                if (sourceKind >= 2 && string.IsNullOrEmpty(sourceMember))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidControllerSource,
                        GetLocation(attribute),
                        "(none)",
                        viewModelType.ToDisplayString(),
                        GetControllerSourceKindName(sourceKind)));
                    continue;
                }
                ISymbol? sourceSymbol = string.IsNullOrEmpty(sourceMember)
                    ? null
                    : viewModelType.GetMembers(sourceMember!).FirstOrDefault(static member => !member.IsStatic);
                if (!string.IsNullOrEmpty(sourceMember) &&
                    (sourceSymbol == null || !IsCompatibleControllerSource(sourceSymbol, itemType, sourceKind)))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidControllerSource,
                        GetLocation(attribute),
                        sourceMember,
                        viewModelType.ToDisplayString(),
                        GetControllerSourceKindName(sourceKind)));
                    continue;
                }

                int operationExecution = GetEnumValue(arguments, "OperationExecution", 0);
                if (!IsCompatibleOperationExecution(sourceKind, operationExecution))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidOperationOwnership,
                        GetLocation(attribute),
                        name,
                        GetControllerSourceKindName(sourceKind),
                        GetOperationExecutionName(operationExecution)));
                    continue;
                }
                if (sourceKind == 4 || sourceKind == 5)
                {
                    schema.Streaming = true;
                }

                INamedTypeSymbol? implementationType = null;
                if (arguments.TryGetValue("ImplementationType", out TypedConstant implementation) &&
                    implementation.Value is INamedTypeSymbol candidateImplementation)
                {
                    if (!ValidateControllerImplementation(itemType, candidateImplementation))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            GeneratorDiagnostics.InvalidImplementation,
                            GetLocation(attribute),
                            candidateImplementation.ToDisplayString(),
                            itemType.ToDisplayString()));
                        continue;
                    }

                    implementationType = candidateImplementation;
                }

                string? configureMethod = GeneratorUtilities.GetString(arguments, "ConfigureMethod");
                if (!string.IsNullOrEmpty(configureMethod) &&
                    !HasControllerConfigureMethod(viewModelType, itemType, configureMethod!))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidCustomizationMethod,
                        GetLocation(attribute),
                        configureMethod,
                        viewModelType.ToDisplayString()));
                    continue;
                }

                bool canGenerate = ValidateGeneratedMember(viewModelType, name, diagnostics, GetLocation(attribute));
                canGenerate &= ValidateGeneratedMember(viewModelType, "Initialize" + name, diagnostics, GetLocation(attribute));
                canGenerate &= ValidateGeneratedMember(viewModelType, "Create" + name + "Controller", diagnostics, GetLocation(attribute));
                canGenerate &= ValidateGeneratedMember(viewModelType, "Dispose" + name, diagnostics, GetLocation(attribute));
                if (sourceKind == 2 || sourceKind == 3)
                {
                    canGenerate &= ValidateGeneratedMember(viewModelType, name + "Errors", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, name + "Completion", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Connect" + name + "Pipeline", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Disconnect" + name + "Pipeline", diagnostics, GetLocation(attribute));
                }
                if (sourceKind == 4 || sourceKind == 5)
                {
                    canGenerate &= ValidateGeneratedMember(viewModelType, name + "StreamPump", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, name + "StreamMetrics", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Run" + name + "StreamAsync", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Stop" + name + "Stream", diagnostics, GetLocation(attribute));
                }
                if (sourceKind == 6)
                {
                    canGenerate &= ValidateGeneratedMember(viewModelType, name + "RemoteQuery", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Create" + name + "RemoteQueryController", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Initialize" + name + "RemoteQuery", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Query" + name + "Async", diagnostics, GetLocation(attribute));
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Dispose" + name + "RemoteQuery", diagnostics, GetLocation(attribute));
                }
                if (!canGenerate)
                {
                    continue;
                }

                controllers.Add(new ControllerModel
                {
                    ViewModelType = viewModelType,
                    Schema = schema,
                    Name = name,
                    SourceMember = sourceMember,
                    SourceKeyType = GetSourceCacheKeyType(sourceSymbol, sourceKind),
                    SourceKind = sourceKind,
                    Features = GetEnumValue(arguments, "Features", 15),
                    OperationExecution = operationExecution,
                    ImplementationType = implementationType,
                    ConfigureMethod = configureMethod,
                    IsDirectIncremental = enableDirectIncremental,
                    Location = GetLocation(attribute)
                });
            }
        }
    }

    private static bool IsCompatibleControllerSource(ISymbol source, INamedTypeSymbol itemType, int sourceKind)
    {
        ITypeSymbol? sourceType = source switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property when property.GetMethod != null => property.Type,
            _ => null
        };
        if (sourceType == null)
        {
            return false;
        }

        return sourceKind switch
        {
            0 => IsGenericSourceOf(sourceType, itemType, "System.Collections.Generic.IEnumerable`1"),
            1 => IsGenericSourceOf(sourceType, itemType, "System.Collections.Generic.IEnumerable`1") &&
                 ImplementsMetadataName(sourceType, "System.Collections.Specialized.INotifyCollectionChanged"),
            2 => IsGenericSourceOf(sourceType, itemType, "DynamicData.SourceList`1"),
            3 => IsGenericSourceOf(sourceType, itemType, "DynamicData.SourceCache`2"),
            4 => IsGenericSourceOf(sourceType, itemType, "System.Collections.Generic.IAsyncEnumerable`1"),
            5 => IsGenericSourceOf(sourceType, itemType, "System.Threading.Channels.ChannelReader`1"),
            6 => IsGenericSourceOf(sourceType, itemType, "Avalonia.Controls.IDataGridQueryProvider`2"),
            _ => false
        };
    }

    private static ITypeSymbol? GetSourceCacheKeyType(ISymbol? source, int sourceKind)
    {
        if (sourceKind != 3 && sourceKind != 6)
        {
            return null;
        }

        ITypeSymbol? sourceType = source switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };
        string metadataName = sourceKind == 3
            ? "DynamicData.SourceCache`2"
            : "Avalonia.Controls.IDataGridQueryProvider`2";
        INamedTypeSymbol? constructed = FindConstructedType(sourceType, metadataName);
        if (constructed != null && constructed.TypeArguments.Length == 2)
        {
            return constructed.TypeArguments[1];
        }

        return null;
    }

    private static INamedTypeSymbol? FindConstructedType(ITypeSymbol? sourceType, string metadataName)
    {
        if (sourceType is not INamedTypeSymbol named)
        {
            return null;
        }
        if (string.Equals(GeneratorUtilities.GetMetadataName(named.OriginalDefinition), metadataName, StringComparison.Ordinal))
        {
            return named;
        }

        return named.AllInterfaces.FirstOrDefault(implemented => string.Equals(
            GeneratorUtilities.GetMetadataName(implemented.OriginalDefinition), metadataName, StringComparison.Ordinal));
    }

    private static bool IsGenericSourceOf(ITypeSymbol sourceType, ITypeSymbol itemType, string metadataName)
    {
        if (sourceType is INamedTypeSymbol named &&
            IsConstructedMetadataType(named, metadataName, itemType))
        {
            return true;
        }

        if (sourceType is INamedTypeSymbol sourceNamed)
        {
            foreach (INamedTypeSymbol implemented in sourceNamed.AllInterfaces)
            {
                if (IsConstructedMetadataType(implemented, metadataName, itemType))
                {
                    return true;
                }
            }

            INamedTypeSymbol? current = sourceNamed.BaseType;
            while (current != null)
            {
                if (IsConstructedMetadataType(current, metadataName, itemType))
                {
                    return true;
                }

                current = current.BaseType;
            }
        }

        return false;
    }

    private static bool IsConstructedMetadataType(INamedTypeSymbol type, string metadataName, ITypeSymbol itemType) =>
        type.TypeArguments.Length > 0 &&
        string.Equals(GeneratorUtilities.GetMetadataName(type.OriginalDefinition), metadataName, StringComparison.Ordinal) &&
        SymbolEqualityComparer.Default.Equals(type.TypeArguments[0], itemType);

    private static bool ImplementsMetadataName(ITypeSymbol sourceType, string metadataName)
    {
        if (sourceType is not INamedTypeSymbol named)
        {
            return false;
        }

        if (string.Equals(GeneratorUtilities.GetMetadataName(named.OriginalDefinition), metadataName, StringComparison.Ordinal))
        {
            return true;
        }

        return named.AllInterfaces.Any(implemented =>
            string.Equals(GeneratorUtilities.GetMetadataName(implemented.OriginalDefinition), metadataName, StringComparison.Ordinal));
    }

    private static bool IsCompatibleOperationExecution(int sourceKind, int operationExecution) =>
        sourceKind switch
        {
            2 or 3 or 4 or 5 => operationExecution == 1,
            6 => operationExecution == 2,
            _ => operationExecution != 2
        };

    private static string GetControllerSourceKindName(int value) => value switch
    {
        0 => "Enumerable",
        1 => "ObservableCollection",
        2 => "DynamicDataSourceList",
        3 => "DynamicDataSourceCache",
        4 => "AsyncEnumerable",
        5 => "ChannelReader",
        6 => "Remote",
        _ => value.ToString()
    };

    private static string GetOperationExecutionName(int value) => value switch
    {
        0 => "View",
        1 => "ExternalPipeline",
        2 => "Remote",
        _ => value.ToString()
    };

    private static SchemaModel EnsureSchema(
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        INamedTypeSymbol itemType,
        AttributeData attribute,
        string? explicitProviderName)
    {
        if (!schemas.TryGetValue(itemType, out SchemaModel? schema))
        {
            schema = CreateDefaultSchema(itemType, GetLocation(attribute), attributedOnly: false);
            schemas.Add(itemType, schema);
        }

        if (!string.IsNullOrWhiteSpace(explicitProviderName))
        {
            schema.ProviderName = GeneratorUtilities.SanitizeIdentifier(explicitProviderName!);
        }

        return schema;
    }

    private static void AddOrUpdateSchema(
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        INamedTypeSymbol itemType,
        AttributeData attribute,
        string? explicitProviderName,
        bool explicitConfiguration)
    {
        SchemaModel schema = EnsureSchema(schemas, itemType, attribute, explicitProviderName);
        Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
        string? providerName = explicitProviderName ?? GeneratorUtilities.GetString(arguments, "ProviderName");
        string? providerNamespace = GeneratorUtilities.GetString(arguments, "ProviderNamespace");
        string? schemaId = GeneratorUtilities.GetString(arguments, "SchemaId");
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            schema.ProviderName = GeneratorUtilities.SanitizeIdentifier(providerName!);
        }

        if (providerNamespace != null)
        {
            schema.ProviderNamespace = providerNamespace;
        }

        if (!string.IsNullOrWhiteSpace(schemaId))
        {
            schema.SchemaId = schemaId!;
        }

        int stateVersion = GeneratorUtilities.GetInt32(arguments, "StateVersion", schema.StateVersion);
        schema.StateVersion = stateVersion;

        if (explicitConfiguration)
        {
            schema.AttributedOnly = GetEnumValue(arguments, "Discovery", 0) == 1;
            schema.IncludeInherited = GeneratorUtilities.GetBoolean(arguments, "IncludeInherited", true);
            schema.ConfigureMethod = GeneratorUtilities.GetString(arguments, "ConfigureMethod");
            if (arguments.TryGetValue("ImplementationType", out TypedConstant implementation) &&
                implementation.Value is INamedTypeSymbol implementationType)
            {
                schema.ImplementationType = implementationType;
            }
        }

        ApplyFastOptions(schema, arguments);
    }

    private static void ApplyFastOptions(SchemaModel schema, Dictionary<string, TypedConstant> arguments)
    {
        schema.Strict = GeneratorUtilities.GetBoolean(arguments, "Strict", schema.Strict);
        schema.Streaming = GeneratorUtilities.GetBoolean(arguments, "Streaming", schema.Streaming);
        schema.HierarchicalRows = GeneratorUtilities.GetBoolean(arguments, "HierarchicalRows", schema.HierarchicalRows);
        schema.PerformanceProfile = GetEnumValue(arguments, "PerformanceProfile", schema.PerformanceProfile);
    }

    private static SchemaModel CreateDefaultSchema(INamedTypeSymbol itemType, Location location, bool attributedOnly)
    {
        return new SchemaModel
        {
            ItemType = itemType,
            ProviderName = GeneratorUtilities.GetDefaultProviderName(itemType),
            ProviderNamespace = itemType.ContainingNamespace?.IsGlobalNamespace == false
                ? itemType.ContainingNamespace.ToDisplayString()
                : string.Empty,
            SchemaId = GeneratorUtilities.GetMetadataName(itemType) + "/v1",
            AttributedOnly = attributedOnly,
            Location = location
        };
    }

    private static ImmutableArray<ColumnModel> DiscoverColumns(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var properties = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
        INamedTypeSymbol? current = schema.ItemType;
        while (current != null)
        {
            foreach (IPropertySymbol property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!properties.ContainsKey(property.Name))
                {
                    properties.Add(property.Name, property);
                }
            }

            current = schema.IncludeInherited ? current.BaseType : null;
        }

        var columns = ImmutableArray.CreateBuilder<ColumnModel>();
        var columnKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (IPropertySymbol property in properties.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttributeData? columnAttribute = GeneratorUtilities.FindAttribute(property, ProDataGridGenerator.ColumnAttributeName);
            if (GeneratorUtilities.HasAttribute(property, ProDataGridGenerator.IgnoreColumnAttributeName) ||
                (schema.AttributedOnly && columnAttribute == null))
            {
                continue;
            }

            string? unsupportedReason = GetUnsupportedPropertyReason(property);
            if (unsupportedReason != null)
            {
                if (columnAttribute != null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.UnsupportedProperty,
                        GeneratorUtilities.GetLocation(property),
                        property.ToDisplayString(),
                        unsupportedReason));
                }

                continue;
            }

            Dictionary<string, TypedConstant> options = GeneratorUtilities.GetNamedArguments(columnAttribute);
            string kind = GetColumnKind(property.Type, columnAttribute, options);
            string? header = GeneratorUtilities.GetString(options, "Header");
            string columnKey = GeneratorUtilities.GetString(options, "ColumnKey") ?? property.Name;
            if (string.IsNullOrWhiteSpace(columnKey) || !columnKeys.Add(columnKey))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.DuplicateStableKey,
                    GeneratorUtilities.GetLocation(property),
                    schema.ItemType.ToDisplayString(),
                    columnKey));
                continue;
            }
            ImmutableArray<string> configuredAliases = GeneratorUtilities.GetStringArray(options, "PreviousColumnKeys");
            var validAliases = ImmutableArray.CreateBuilder<string>(configuredAliases.Length);
            for (int aliasIndex = 0; aliasIndex < configuredAliases.Length; aliasIndex++)
            {
                string alias = configuredAliases[aliasIndex];
                if (string.IsNullOrWhiteSpace(alias) || !columnKeys.Add(alias))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidStateMetadata,
                        GeneratorUtilities.GetLocation(property),
                        schema.ItemType.ToDisplayString(),
                        string.IsNullOrWhiteSpace(alias) ? "column aliases cannot be empty" : $"column alias '{alias}' is duplicated"));
                }
                else
                {
                    validAliases.Add(alias);
                }
            }
            ImmutableArray<string> previousColumnKeys = validAliases.ToImmutable();
            string? configureMethod = GeneratorUtilities.GetString(options, "ConfigureMethod");
            string? factoryMethod = GeneratorUtilities.GetString(options, "FactoryMethod");
            string? parserMethod = ValidateEditMethod(schema.ItemType, property, options, "ParserMethod", EditMethodKind.Parser, diagnostics);
            string? formatterMethod = ValidateEditMethod(schema.ItemType, property, options, "FormatterMethod", EditMethodKind.Formatter, diagnostics);
            string? validatorMethod = ValidateEditMethod(schema.ItemType, property, options, "ValidatorMethod", EditMethodKind.Validator, diagnostics);
            string? asyncValidatorMethod = ValidateEditMethod(schema.ItemType, property, options, "AsyncValidatorMethod", EditMethodKind.AsyncValidator, diagnostics);
            string? coerceMethod = ValidateEditMethod(schema.ItemType, property, options, "CoerceMethod", EditMethodKind.Coerce, diagnostics);
            string? canEditMethod = ValidateEditMethod(schema.ItemType, property, options, "CanEditMethod", EditMethodKind.CanEdit, diagnostics);
            string? templateFactoryMethod = ValidateTemplateFactoryMethod(schema.ItemType, property, options, "TemplateFactoryMethod", diagnostics);
            string? editingTemplateFactoryMethod = ValidateTemplateFactoryMethod(schema.ItemType, property, options, "EditingTemplateFactoryMethod", diagnostics);
            string? newRowTemplateFactoryMethod = ValidateTemplateFactoryMethod(schema.ItemType, property, options, "NewRowTemplateFactoryMethod", diagnostics);
            ValidateDrawOperationFactory(
                schema.ItemType,
                property,
                kind,
                options,
                diagnostics,
                out INamedTypeSymbol? drawOperationFactoryType,
                out string? drawOperationFactoryMethod);
            IPropertySymbol? contentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, property, kind, options, "ContentMember", diagnostics);
            IPropertySymbol? checkedContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, property, kind, options, "CheckedContentMember", diagnostics);
            IPropertySymbol? uncheckedContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, property, kind, options, "UncheckedContentMember", diagnostics);
            IPropertySymbol? onContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, property, kind, options, "OnContentMember", diagnostics);
            IPropertySymbol? offContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, property, kind, options, "OffContentMember", diagnostics);
            IPropertySymbol? commandMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, property, kind, options, "CommandMember", diagnostics);
            IPropertySymbol? commandParameterMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, property, kind, options, "CommandParameterMember", diagnostics);
            string? headerProviderMethod = ValidateLocalizationMethod(
                schema.ItemType, property, options, "HeaderProviderMethod", diagnostics, out bool headerProviderAcceptsFormatProvider);
            string? descriptionProviderMethod = ValidateLocalizationMethod(
                schema.ItemType, property, options, "DescriptionProviderMethod", diagnostics, out bool descriptionProviderAcceptsFormatProvider);
            GroupModel? group = DiscoverGroup(schema.ItemType, property, diagnostics);
            ImmutableArray<SummaryModel> summaries = DiscoverSummaries(property);
            ImmutableArray<ConditionalRuleModel> conditionalRules = DiscoverConditionalRules(schema.ItemType, property, columnKey, diagnostics);
            ImmutableArray<BandModel> bands = DiscoverBands(property, diagnostics);
            ImmutableArray<AnalyticsRoleModel> analyticsRoles = DiscoverAnalyticsRoles(property, diagnostics);
            bool searchable = GeneratorUtilities.GetBoolean(options, "IsSearchable", true);

            if (!string.IsNullOrEmpty(configureMethod) &&
                !HasColumnConfigureMethod(schema.ItemType, configureMethod!, kind))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidCustomizationMethod,
                    GeneratorUtilities.GetLocation(property),
                    configureMethod,
                    schema.ItemType.ToDisplayString()));
                configureMethod = null;
            }

            if (!string.IsNullOrEmpty(factoryMethod) &&
                !HasColumnFactoryMethod(schema.ItemType, factoryMethod!))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidCustomizationMethod,
                    GeneratorUtilities.GetLocation(property),
                    factoryMethod,
                    schema.ItemType.ToDisplayString()));
                factoryMethod = null;
            }

            ValidateRequiredKindOptions(property, kind, options, diagnostics);

            int sourceOrder = property.Locations.FirstOrDefault(static location => location.IsInSource)?.SourceSpan.Start ?? int.MaxValue;
            columns.Add(new ColumnModel
            {
                Property = property,
                Kind = kind,
                Header = header ?? GeneratorUtilities.ToHeader(property.Name),
                HeaderProviderMethod = headerProviderMethod,
                HeaderProviderAcceptsFormatProvider = headerProviderAcceptsFormatProvider,
                DescriptionProviderMethod = descriptionProviderMethod,
                DescriptionProviderAcceptsFormatProvider = descriptionProviderAcceptsFormatProvider,
                Order = GeneratorUtilities.GetInt32(options, "Order", 0),
                SourceOrder = sourceOrder,
                Options = options.ToImmutableDictionary(StringComparer.Ordinal),
                ColumnKey = columnKey,
                PreviousColumnKeys = previousColumnKeys,
                ConfigureMethod = configureMethod,
                FactoryMethod = factoryMethod,
                ParserMethod = parserMethod,
                FormatterMethod = formatterMethod,
                ValidatorMethod = validatorMethod,
                AsyncValidatorMethod = asyncValidatorMethod,
                CoerceMethod = coerceMethod,
                CanEditMethod = canEditMethod,
                TemplateFactoryMethod = templateFactoryMethod,
                EditingTemplateFactoryMethod = editingTemplateFactoryMethod,
                NewRowTemplateFactoryMethod = newRowTemplateFactoryMethod,
                DrawOperationFactoryType = drawOperationFactoryType,
                DrawOperationFactoryMethod = drawOperationFactoryMethod,
                ContentMember = contentMember,
                CheckedContentMember = checkedContentMember,
                UncheckedContentMember = uncheckedContentMember,
                OnContentMember = onContentMember,
                OffContentMember = offContentMember,
                CommandMember = commandMember,
                CommandParameterMember = commandParameterMember,
                Group = group,
                Summaries = summaries,
                ConditionalRules = conditionalRules,
                Bands = bands,
                AnalyticsRoles = analyticsRoles,
                IsSearchable = searchable
            });
        }

        return columns
            .OrderBy(static column => column.Order)
            .ThenBy(static column => column.SourceOrder)
            .ThenBy(static column => column.Property.Name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static KeyMemberModel? DiscoverKeyMember(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(schema.ExplicitKeyMemberName))
        {
            ISymbol[] explicitMembers = EnumerateMembers(schema.ItemType, schema.IncludeInherited)
                .Where(member => string.Equals(member.Name, schema.ExplicitKeyMemberName, StringComparison.Ordinal))
                .ToArray();
            string? reason = null;
            KeyMemberModel? explicitKey = null;
            if (explicitMembers.Length != 1 ||
                !TryCreateKeyMember(explicitMembers[0], out explicitKey, out reason))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    explicitMembers.Length > 0 ? GeneratorUtilities.GetLocation(explicitMembers[0]) : schema.Location,
                    schema.ExplicitKeyMemberName,
                    reason ?? "the configured member was not found unambiguously"));
                return null;
            }

            return explicitKey;
        }

        var candidates = new List<KeyMemberModel>();
        INamedTypeSymbol? current = schema.ItemType;
        while (current != null)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!GeneratorUtilities.HasAttribute(member, ProDataGridGenerator.KeyAttributeName))
                {
                    continue;
                }

                if (!TryCreateKeyMember(member, out KeyMemberModel? keyMember, out string? reason))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidItemKey,
                        GeneratorUtilities.GetLocation(member),
                        member.ToDisplayString(),
                        reason ?? "the member type is unavailable"));
                    continue;
                }

                candidates.Add(keyMember!);
            }

            current = schema.IncludeInherited ? current.BaseType : null;
        }

        if (candidates.Count <= 1)
        {
            return candidates.Count == 1 ? candidates[0] : null;
        }

        for (int index = 1; index < candidates.Count; index++)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidItemKey,
                GeneratorUtilities.GetLocation(candidates[index].Member),
                candidates[index].Member.ToDisplayString(),
                "only one [DataGridKey] member is allowed per schema"));
        }

        return null;
    }

    private static bool TryCreateKeyMember(ISymbol? member, out KeyMemberModel? keyMember, out string? reason)
    {
        ITypeSymbol? memberType = null;
        reason = null;
        if (member is IPropertySymbol property)
        {
            memberType = property.Type;
            reason = GetUnsupportedPropertyReason(property);
        }
        else if (member is IFieldSymbol field)
        {
            memberType = field.Type;
            if (field.IsStatic)
            {
                reason = "static fields are not supported";
            }
            else if (!GeneratorUtilities.IsAccessibleFromGeneratedCode(field))
            {
                reason = "the field is not accessible to generated code";
            }
        }
        else
        {
            reason = "only fields and properties are supported";
        }

        if (reason == null && memberType != null && IsNullableKeyType(memberType))
        {
            reason = "nullable keys are not stable; use a non-nullable key type";
        }

        if (reason != null || memberType == null || member == null)
        {
            keyMember = null;
            return false;
        }

        keyMember = new KeyMemberModel { Member = member, Type = memberType };
        return true;
    }

    private static IEnumerable<ISymbol> EnumerateMembers(INamedTypeSymbol type, bool includeInherited)
    {
        INamedTypeSymbol? current = type;
        while (current != null)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                yield return member;
            }

            current = includeInherited ? current.BaseType : null;
        }
    }

    private static bool IsNullableKeyType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return type is INamedTypeSymbol named &&
               named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static HierarchyModel? DiscoverHierarchy(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        IPropertySymbol? children = null;
        IMethodSymbol? childLoader = null;
        IPropertySymbol? expanded = null;
        ISymbol? parentKey = null;
        INamedTypeSymbol? current = schema.ItemType;
        while (current != null)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (GeneratorUtilities.HasAttribute(member, ProDataGridGenerator.ChildrenAttributeName))
                {
                    if (children != null || member is not IPropertySymbol property ||
                        property.IsStatic || property.GetMethod == null ||
                        !GeneratorUtilities.IsAccessibleFromGeneratedCode(property) ||
                        !GeneratorUtilities.IsAccessibleFromGeneratedCode(property.GetMethod) ||
                        !TryGetEnumerableItemType(property.Type, out ITypeSymbol? childType) ||
                        !SymbolEqualityComparer.Default.Equals(childType, schema.ItemType))
                    {
                        ReportInvalidHierarchy(diagnostics, member, schema.ItemType,
                            children != null
                                ? "only one [DataGridChildren] property is allowed"
                                : "the member must be a readable instance IEnumerable<TItem> property");
                    }
                    else
                    {
                        children = property;
                        AttributeData? childrenAttribute = GeneratorUtilities.FindAttribute(
                            property,
                            ProDataGridGenerator.ChildrenAttributeName);
                        string? loaderMethodName = GeneratorUtilities.GetString(
                            GeneratorUtilities.GetNamedArguments(childrenAttribute),
                            "LoaderMethod");
                        if (!string.IsNullOrWhiteSpace(loaderMethodName))
                        {
                            childLoader = FindChildLoader(schema.ItemType, loaderMethodName!);
                            if (childLoader == null)
                            {
                                ReportInvalidHierarchy(
                                    diagnostics,
                                    member,
                                    schema.ItemType,
                                    $"loader method '{loaderMethodName}' must be an accessible instance method with signature ValueTask<IReadOnlyList<{schema.ItemType.Name}>> {loaderMethodName}(CancellationToken)");
                            }
                        }
                    }
                }

                if (GeneratorUtilities.HasAttribute(member, ProDataGridGenerator.ExpandedAttributeName))
                {
                    if (expanded != null || member is not IPropertySymbol property ||
                        property.IsStatic || property.GetMethod == null || property.SetMethod == null ||
                        property.SetMethod.IsInitOnly ||
                        property.Type.SpecialType != SpecialType.System_Boolean ||
                        !GeneratorUtilities.IsAccessibleFromGeneratedCode(property) ||
                        !GeneratorUtilities.IsAccessibleFromGeneratedCode(property.GetMethod) ||
                        !GeneratorUtilities.IsAccessibleFromGeneratedCode(property.SetMethod))
                    {
                        ReportInvalidHierarchy(diagnostics, member, schema.ItemType,
                            expanded != null
                                ? "only one [DataGridExpanded] property is allowed"
                                : "the member must be a readable and writable instance bool property");
                    }
                    else
                    {
                        expanded = property;
                    }
                }

                if (GeneratorUtilities.HasAttribute(member, ProDataGridGenerator.ParentKeyAttributeName))
                {
                    if (parentKey != null || member.IsStatic || !GeneratorUtilities.IsAccessibleFromGeneratedCode(member))
                    {
                        ReportInvalidHierarchy(diagnostics, member, schema.ItemType,
                            parentKey != null
                                ? "only one [DataGridParentKey] member is allowed"
                                : "the member must be an accessible instance field or property");
                    }
                    else
                    {
                        parentKey = member;
                    }
                }
            }

            current = schema.IncludeInherited ? current.BaseType : null;
        }

        return children == null && expanded == null && parentKey == null
            ? null
            : new HierarchyModel
            {
                ChildrenProperty = children,
                ChildLoaderMethod = childLoader,
                ExpandedProperty = expanded,
                ParentKeyMember = parentKey
            };
    }

    private static IMethodSymbol? FindChildLoader(INamedTypeSymbol itemType, string methodName)
    {
        INamedTypeSymbol? current = itemType;
        while (current != null)
        {
            foreach (ISymbol member in current.GetMembers(methodName))
            {
                if (member is IMethodSymbol method &&
                    !method.IsStatic &&
                    !method.IsGenericMethod &&
                    method.Parameters.Length == 1 &&
                    GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
                    IsNamedType(method.Parameters[0].Type, "System.Threading.CancellationToken") &&
                    IsChildLoaderReturnType(method.ReturnType, itemType))
                {
                    return method;
                }
            }

            current = current.BaseType;
        }

        return null;
    }

    private static bool IsChildLoaderReturnType(ITypeSymbol type, INamedTypeSymbol itemType)
    {
        if (type is not INamedTypeSymbol valueTask ||
            !valueTask.IsGenericType ||
            valueTask.TypeArguments.Length != 1 ||
            !IsNamedType(valueTask.OriginalDefinition, "System.Threading.Tasks.ValueTask`1"))
        {
            return false;
        }

        return valueTask.TypeArguments[0] is INamedTypeSymbol list &&
               list.IsGenericType &&
               list.TypeArguments.Length == 1 &&
               IsNamedType(list.OriginalDefinition, "System.Collections.Generic.IReadOnlyList`1") &&
               SymbolEqualityComparer.Default.Equals(list.TypeArguments[0], itemType);
    }

    private static bool IsNamedType(ITypeSymbol type, string metadataName) =>
        type is INamedTypeSymbol named &&
        string.Equals(GeneratorUtilities.GetMetadataName(named), metadataName, StringComparison.Ordinal);

    private static bool TryGetEnumerableItemType(ITypeSymbol type, out ITypeSymbol? itemType)
    {
        if (type is IArrayTypeSymbol array)
        {
            itemType = array.ElementType;
            return true;
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.IsGenericType && named.TypeArguments.Length == 1 && IsEnumerableDefinition(named.OriginalDefinition))
            {
                itemType = named.TypeArguments[0];
                return true;
            }

            foreach (INamedTypeSymbol implemented in named.AllInterfaces)
            {
                if (implemented.IsGenericType && implemented.TypeArguments.Length == 1 && IsEnumerableDefinition(implemented.OriginalDefinition))
                {
                    itemType = implemented.TypeArguments[0];
                    return true;
                }
            }
        }

        itemType = null;
        return false;
    }

    private static void ReportInvalidHierarchy(
        ImmutableArray<Diagnostic>.Builder diagnostics,
        ISymbol member,
        INamedTypeSymbol itemType,
        string reason)
    {
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidHierarchy,
            GeneratorUtilities.GetLocation(member),
            member.ToDisplayString(),
            itemType.ToDisplayString(),
            reason));
    }

    private static bool ValidateSchemaTarget(SchemaModel schema, ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        INamedTypeSymbol type = schema.ItemType;
        if (schema.StateVersion <= 0)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidStateMetadata,
                schema.Location,
                type.ToDisplayString(),
                "StateVersion must be greater than zero"));
            return false;
        }

        string? reason = null;
        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
        {
            reason = "only classes and structs are supported";
        }
        else if (type.IsUnboundGenericType || type.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.TypeParameter))
        {
            reason = "open generic item types are not supported";
        }
        else if (!GeneratorUtilities.IsAccessibleFromGeneratedCode(type))
        {
            reason = "the item type is inaccessible to generated code";
        }

        if (reason == null)
        {
            return true;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidTarget,
            schema.Location,
            type.ToDisplayString(),
            reason));
        return false;
    }

    private static string? GetUnsupportedPropertyReason(IPropertySymbol property)
    {
        if (property.IsStatic)
        {
            return "static properties are not supported";
        }

        if (property.IsIndexer)
        {
            return "indexers are not supported";
        }

        if (property.GetMethod == null || !GeneratorUtilities.IsAccessibleFromGeneratedCode(property.GetMethod))
        {
            return "the getter is not accessible";
        }

        if (property.ReturnsByRef || property.ReturnsByRefReadonly)
        {
            return "by-reference properties are not supported";
        }

        if (property.Type.TypeKind == TypeKind.Pointer || property.Type.TypeKind == TypeKind.FunctionPointer)
        {
            return "pointer properties are not supported";
        }

        return null;
    }

    private static string GetColumnKind(
        ITypeSymbol propertyType,
        AttributeData? attribute,
        Dictionary<string, TypedConstant> options)
    {
        int kindValue = GetEnumValue(options, "Kind", -1);
        if (kindValue < 0 && attribute != null && attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int constructorKind)
        {
            kindValue = constructorKind;
        }

        string[] kinds =
        {
            "Auto", "Text", "CheckBox", "Hyperlink", "Image", "Numeric", "ProgressBar", "Slider",
            "DatePicker", "TimePicker", "MaskedText", "AutoComplete", "ToggleButton", "ToggleSwitch",
            "Hierarchical", "CustomDrawing", "ComboBoxSelectedItem", "ComboBoxSelectedValue", "ComboBoxText",
            "Template", "Button", "Formula"
        };
        if (kindValue > 0 && kindValue < kinds.Length)
        {
            return kinds[kindValue];
        }

        ITypeSymbol effectiveType = UnwrapNullable(propertyType);
        if (effectiveType.TypeKind == TypeKind.Enum)
        {
            return "ComboBoxSelectedItem";
        }

        switch (effectiveType.SpecialType)
        {
            case SpecialType.System_Boolean:
                return "CheckBox";
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return "Numeric";
        }

        string metadataName = effectiveType is INamedTypeSymbol named
            ? GeneratorUtilities.GetMetadataName(named)
            : effectiveType.ToDisplayString();
        if (metadataName == "System.DateTime" || metadataName == "System.DateTimeOffset")
        {
            return "DatePicker";
        }

        if (metadataName == "System.TimeSpan")
        {
            return "TimePicker";
        }

        if (metadataName == "System.Uri")
        {
            return "Hyperlink";
        }

        return "Text";
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }

        return type;
    }

    private static void ValidateRequiredKindOptions(
        IPropertySymbol property,
        string kind,
        Dictionary<string, TypedConstant> options,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? required = null;
        if (kind == "Template" &&
            string.IsNullOrEmpty(GeneratorUtilities.GetString(options, "TemplateKey")) &&
            string.IsNullOrEmpty(GeneratorUtilities.GetString(options, "TemplateFactoryMethod")))
        {
            required = "TemplateKey";
        }
        else if (kind == "Formula" && string.IsNullOrEmpty(GeneratorUtilities.GetString(options, "Formula")))
        {
            required = "Formula";
        }

        if (required != null)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidColumnConfiguration,
                GeneratorUtilities.GetLocation(property),
                property.ToDisplayString(),
                kind,
                required));
        }
    }

    private static IPropertySymbol? ValidateAuxiliaryColumnBinding(
        INamedTypeSymbol itemType,
        IPropertySymbol columnProperty,
        string kind,
        Dictionary<string, TypedConstant> options,
        string optionName,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? memberName = GeneratorUtilities.GetString(options, optionName);
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return null;
        }

        if (!IsAuxiliaryColumnBindingSupported(kind, optionName))
        {
            ReportInvalidAuxiliaryBinding(
                diagnostics,
                columnProperty,
                optionName,
                $"column kind '{kind}' does not support this binding");
            return null;
        }

        string? staticOption = optionName switch
        {
            "ContentMember" => "Content",
            "CheckedContentMember" => "CheckedContent",
            "UncheckedContentMember" => "UncheckedContent",
            "OnContentMember" => "OnContent",
            "OffContentMember" => "OffContent",
            _ => null
        };
        bool conflictsWithNamedStaticOption = staticOption != null &&
            !string.IsNullOrEmpty(GeneratorUtilities.GetString(options, staticOption));
        bool conflictsWithLegacySwitchContent = optionName == "OnContentMember" &&
            !string.IsNullOrEmpty(GeneratorUtilities.GetString(options, "Content"));
        if (conflictsWithNamedStaticOption || conflictsWithLegacySwitchContent)
        {
            string conflictingOption = conflictsWithLegacySwitchContent ? "Content" : staticOption!;
            ReportInvalidAuxiliaryBinding(
                diagnostics,
                columnProperty,
                optionName,
                $"cannot be combined with static {conflictingOption}");
            return null;
        }

        IPropertySymbol? member = EnumerateMembers(itemType, includeInherited: true)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, memberName, StringComparison.Ordinal));
        if (member == null || member.IsStatic || member.Parameters.Length != 0 || member.GetMethod == null ||
            !GeneratorUtilities.IsAccessibleFromGeneratedCode(member) ||
            !GeneratorUtilities.IsAccessibleFromGeneratedCode(member.GetMethod))
        {
            ReportInvalidAuxiliaryBinding(
                diagnostics,
                columnProperty,
                optionName,
                $"member '{memberName}' must be an accessible readable instance property");
            return null;
        }

        if (optionName == "CommandMember" && !ImplementsMetadataName(member.Type, "System.Windows.Input.ICommand"))
        {
            ReportInvalidAuxiliaryBinding(
                diagnostics,
                columnProperty,
                optionName,
                $"member '{memberName}' must implement System.Windows.Input.ICommand");
            return null;
        }

        return member;
    }

    private static bool IsAuxiliaryColumnBindingSupported(string kind, string optionName) => optionName switch
    {
        "ContentMember" => kind is "Button" or "ToggleButton",
        "CheckedContentMember" or "UncheckedContentMember" => kind == "ToggleButton",
        "OnContentMember" or "OffContentMember" => kind == "ToggleSwitch",
        "CommandMember" or "CommandParameterMember" => kind is "Button" or "ToggleButton" or "ToggleSwitch",
        _ => false
    };

    private static void ReportInvalidAuxiliaryBinding(
        ImmutableArray<Diagnostic>.Builder diagnostics,
        IPropertySymbol property,
        string optionName,
        string reason)
    {
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidAuxiliaryBinding,
            GeneratorUtilities.GetLocation(property),
            optionName,
            property.ToDisplayString(),
            reason));
    }

    private static bool HasGlobalConfigureMethod(INamedTypeSymbol type, string name)
    {
        return type.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
            method.ReturnsVoid &&
            method.Parameters.Length == 1 &&
            string.Equals(
                method.Parameters[0].Type is INamedTypeSymbol named
                    ? GeneratorUtilities.GetMetadataName(named)
                    : method.Parameters[0].Type.ToDisplayString(),
                "Avalonia.Controls.DataGridColumnDefinitionList",
                StringComparison.Ordinal));
    }

    private static bool HasColumnConfigureMethod(INamedTypeSymbol type, string name, string kind)
    {
        foreach (IMethodSymbol method in type.GetMembers(name).OfType<IMethodSymbol>())
        {
            if (!method.IsStatic || !GeneratorUtilities.IsAccessibleFromGeneratedCode(method) || !method.ReturnsVoid || method.Parameters.Length != 1)
            {
                continue;
            }

            string parameterName = method.Parameters[0].Type.Name;
            if (parameterName == "DataGridColumnDefinition" || parameterName == "DataGrid" + kind + "ColumnDefinition")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasColumnFactoryMethod(INamedTypeSymbol type, string name)
    {
        return type.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
            method.Parameters.Length == 0 &&
            IsOrDerivesFrom(method.ReturnType, "Avalonia.Controls.DataGridColumnDefinition"));
    }

    private enum EditMethodKind
    {
        Parser,
        Formatter,
        Validator,
        AsyncValidator,
        Coerce,
        CanEdit
    }

    private static GroupModel? DiscoverGroup(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        AttributeData? attribute = GeneratorUtilities.FindAttribute(property, ProDataGridGenerator.GroupAttributeName);
        if (attribute == null) return null;
        Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
        string? formatter = GeneratorUtilities.GetString(arguments, "FormatterMethod");
        if (!string.IsNullOrWhiteSpace(formatter) &&
            !itemType.GetMembers(formatter!).OfType<IMethodSymbol>().Any(method =>
                IsCompatibleEditMethod(method, itemType, property.Type, EditMethodKind.Formatter)))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidCustomizationMethod,
                GeneratorUtilities.GetLocation(property), formatter, itemType.ToDisplayString()));
            formatter = null;
        }
        return new GroupModel
        {
            Order = GeneratorUtilities.GetInt32(arguments, "Order", 0),
            Direction = GetEnumValue(arguments, "Direction", 0),
            FormatterMethod = formatter
        };
    }

    private static ImmutableArray<SummaryModel> DiscoverSummaries(IPropertySymbol property)
    {
        var summaries = ImmutableArray.CreateBuilder<SummaryModel>();
        foreach (AttributeData attribute in GeneratorUtilities.FindAttributes(property, ProDataGridGenerator.SummaryAttributeName))
        {
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            summaries.Add(new SummaryModel
            {
                Aggregate = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int aggregate ? aggregate : 0,
                Scope = GetEnumValue(arguments, "Scope", 2),
                Format = GeneratorUtilities.GetString(arguments, "Format"),
                Title = GeneratorUtilities.GetString(arguments, "Title")
            });
        }
        return summaries.ToImmutable();
    }

    private static ImmutableArray<ConditionalRuleModel> DiscoverConditionalRules(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        string columnKey,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var rules = ImmutableArray.CreateBuilder<ConditionalRuleModel>();
        int index = 0;
        foreach (AttributeData attribute in GeneratorUtilities.FindAttributes(property, ProDataGridGenerator.ConditionalFormatAttributeName))
        {
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            int condition = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int value ? value : 0;
            string? predicate = GeneratorUtilities.GetString(arguments, "PredicateMethod");
            if (!string.IsNullOrWhiteSpace(predicate) &&
                !itemType.GetMembers(predicate!).OfType<IMethodSymbol>().Any(method =>
                    method.IsStatic && GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
                    method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, itemType) &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, property.Type)))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidCustomizationMethod,
                    GeneratorUtilities.GetLocation(property), predicate, itemType.ToDisplayString()));
                predicate = null;
            }
            if (condition == 8 && string.IsNullOrEmpty(predicate))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property), property.ToDisplayString(), "ConditionalFormat", "PredicateMethod"));
            }
            rules.Add(new ConditionalRuleModel
            {
                Condition = condition,
                RuleId = GeneratorUtilities.GetString(arguments, "RuleId") ?? columnKey + ":rule:" + index.ToString(CultureInfo.InvariantCulture),
                Operand = GeneratorUtilities.GetString(arguments, "Operand"),
                ThemeKey = GeneratorUtilities.GetString(arguments, "CellThemeKey"),
                Priority = GeneratorUtilities.GetInt32(arguments, "Priority", 0),
                StopIfTrue = GeneratorUtilities.GetBoolean(arguments, "StopIfTrue", true),
                PredicateMethod = predicate,
                Target = GetEnumValue(arguments, "Target", 0)
            });
            index++;
        }
        return rules.ToImmutable();
    }

    private static ImmutableArray<BandModel> DiscoverBands(
        IPropertySymbol property,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var bands = ImmutableArray.CreateBuilder<BandModel>();
        foreach (AttributeData attribute in GeneratorUtilities.FindAttributes(property, ProDataGridGenerator.BandAttributeName))
        {
            string? rawPath = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
            string[] segments = (rawPath ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property), property.ToDisplayString(), "Band", "non-empty path"));
                continue;
            }
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            bands.Add(new BandModel
            {
                Path = segments.Select(static segment => segment.Trim()).Where(static segment => segment.Length != 0).ToImmutableArray(),
                Order = GeneratorUtilities.GetInt32(arguments, "Order", 0)
            });
        }
        return bands.ToImmutable();
    }

    private static ImmutableArray<AnalyticsRoleModel> DiscoverAnalyticsRoles(
        IPropertySymbol property,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var roles = ImmutableArray.CreateBuilder<AnalyticsRoleModel>();
        AddRoleAttributes(property, ProDataGridGenerator.PivotAxisAttributeName, allowedRoles: 1 | 2 | 4, roles, diagnostics);
        AddRoleAttributes(property, ProDataGridGenerator.ChartFieldAttributeName, allowedRoles: 16 | 32 | 64 | 128 | 256, roles, diagnostics);
        AddRoleAttributes(property, ProDataGridGenerator.OutlineFieldAttributeName, allowedRoles: 512 | 1024, roles, diagnostics);

        foreach (AttributeData attribute in GeneratorUtilities.FindAttributes(property, ProDataGridGenerator.PivotValueAttributeName))
        {
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            roles.Add(new AnalyticsRoleModel
            {
                Role = 8,
                Order = GeneratorUtilities.GetInt32(arguments, "Order", 0),
                Name = GeneratorUtilities.GetString(arguments, "Name"),
                Format = GeneratorUtilities.GetString(arguments, "Format"),
                Aggregate = GetConstructorEnumValue(attribute, 0),
                PivotDisplayMode = GetEnumValue(arguments, "DisplayMode", 0)
            });
        }

        foreach (AttributeData attribute in GeneratorUtilities.FindAttributes(property, ProDataGridGenerator.FormulaFieldAttributeName))
        {
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            string? name = attribute.ConstructorArguments.Length == 0 ? null : attribute.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property), property.ToDisplayString(), "FormulaField", "non-empty formula name"));
                continue;
            }
            roles.Add(new AnalyticsRoleModel
            {
                Role = 2048,
                Order = GeneratorUtilities.GetInt32(arguments, "Order", 0),
                Name = name,
                Format = GeneratorUtilities.GetString(arguments, "Format"),
                Dependencies = GeneratorUtilities.GetStringArray(arguments, "Dependencies")
            });
        }
        return roles.ToImmutable();
    }

    private static void ValidateFormulaMetadata(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int columnIndex = 0; columnIndex < schema.Columns.Length; columnIndex++)
        {
            ColumnModel column = schema.Columns[columnIndex];
            keys.Add(column.ColumnKey);
            for (int aliasIndex = 0; aliasIndex < column.PreviousColumnKeys.Length; aliasIndex++)
            {
                keys.Add(column.PreviousColumnKeys[aliasIndex]);
            }
        }

        var formulaNames = new HashSet<string>(StringComparer.Ordinal);
        for (int columnIndex = 0; columnIndex < schema.Columns.Length; columnIndex++)
        {
            ColumnModel column = schema.Columns[columnIndex];
            for (int roleIndex = 0; roleIndex < column.AnalyticsRoles.Length; roleIndex++)
            {
                AnalyticsRoleModel role = column.AnalyticsRoles[roleIndex];
                if ((role.Role & 2048) == 0)
                {
                    continue;
                }

                string name = role.Name ?? string.Empty;
                if (!formulaNames.Add(name))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidFormulaMetadata,
                        GeneratorUtilities.GetLocation(column.Property),
                        name,
                        "formula names must be unique within a schema"));
                }

                for (int dependencyIndex = 0; dependencyIndex < role.Dependencies.Length; dependencyIndex++)
                {
                    string dependency = role.Dependencies[dependencyIndex];
                    if (!keys.Contains(dependency))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            GeneratorDiagnostics.InvalidFormulaMetadata,
                            GeneratorUtilities.GetLocation(column.Property),
                            name,
                            $"dependency '{dependency}' does not match a stable column key"));
                    }
                }
            }
        }
    }

    private static void AddRoleAttributes(
        IPropertySymbol property,
        string attributeName,
        int allowedRoles,
        ImmutableArray<AnalyticsRoleModel>.Builder roles,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (AttributeData attribute in GeneratorUtilities.FindAttributes(property, attributeName))
        {
            int role = GetConstructorEnumValue(attribute, 0);
            if (role == 0 || (role & ~allowedRoles) != 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property), property.ToDisplayString(), "Analytics", "compatible analytics role"));
                continue;
            }
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            roles.Add(new AnalyticsRoleModel
            {
                Role = role,
                Order = GeneratorUtilities.GetInt32(arguments, "Order", 0),
                Name = GeneratorUtilities.GetString(arguments, attributeName == ProDataGridGenerator.ChartFieldAttributeName ? "Series" : "Name"),
                Format = GeneratorUtilities.GetString(arguments, "Format"),
                Aggregate = GetEnumValue(arguments, "Aggregate", 0)
            });
        }
    }

    private static int GetConstructorEnumValue(AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index && attribute.ConstructorArguments[index].Value is int value ? value : 0;

    private static string? ValidateEditMethod(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        Dictionary<string, TypedConstant> options,
        string optionName,
        EditMethodKind kind,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? name = GeneratorUtilities.GetString(options, optionName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        bool found = itemType.GetMembers(name!).OfType<IMethodSymbol>().Any(method =>
            IsCompatibleEditMethod(method, itemType, property.Type, kind));
        if (found)
        {
            return name;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidCustomizationMethod,
            GeneratorUtilities.GetLocation(property),
            name,
            itemType.ToDisplayString()));
        return null;
    }

    private static string? ValidateLocalizationMethod(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        Dictionary<string, TypedConstant> options,
        string optionName,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        out bool acceptsFormatProvider)
    {
        acceptsFormatProvider = false;
        string? name = GeneratorUtilities.GetString(options, optionName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        IMethodSymbol? method = itemType.GetMembers(name!).OfType<IMethodSymbol>()
            .FirstOrDefault(static candidate =>
                candidate.IsStatic &&
                GeneratorUtilities.IsAccessibleFromGeneratedCode(candidate) &&
                candidate.ReturnType.SpecialType == SpecialType.System_String &&
                (candidate.Parameters.Length == 0 ||
                 candidate.Parameters.Length == 1 &&
                 candidate.Parameters[0].Type.ToDisplayString() == "System.IFormatProvider"));
        if (method != null)
        {
            acceptsFormatProvider = method.Parameters.Length == 1;
            return name;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidCustomizationMethod,
            GeneratorUtilities.GetLocation(property),
            name,
            itemType.ToDisplayString()));
        return null;
    }

    private static bool IsCompatibleEditMethod(
        IMethodSymbol method,
        INamedTypeSymbol itemType,
        ITypeSymbol valueType,
        EditMethodKind kind)
    {
        if (!method.IsStatic || !GeneratorUtilities.IsAccessibleFromGeneratedCode(method))
        {
            return false;
        }

        ImmutableArray<IParameterSymbol> parameters = method.Parameters;
        switch (kind)
        {
            case EditMethodKind.Parser:
                return method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                       parameters.Length == 3 &&
                       IsConstructedType(parameters[0].Type, "System.ReadOnlySpan`1", SpecialType.System_Char) &&
                       IsMetadataType(parameters[1].Type, "System.IFormatProvider") &&
                       parameters[2].RefKind == RefKind.Out &&
                       SymbolEqualityComparer.Default.Equals(parameters[2].Type, valueType);
            case EditMethodKind.Formatter:
                return method.ReturnType.SpecialType == SpecialType.System_String &&
                       parameters.Length == 2 &&
                       SymbolEqualityComparer.Default.Equals(parameters[0].Type, valueType) &&
                       IsMetadataType(parameters[1].Type, "System.IFormatProvider");
            case EditMethodKind.Validator:
                return method.ReturnType.SpecialType == SpecialType.System_String &&
                       parameters.Length == 2 &&
                       SymbolEqualityComparer.Default.Equals(parameters[0].Type, itemType) &&
                       SymbolEqualityComparer.Default.Equals(parameters[1].Type, valueType);
            case EditMethodKind.AsyncValidator:
                return IsConstructedType(method.ReturnType, "System.Threading.Tasks.ValueTask`1", SpecialType.System_String) &&
                       parameters.Length == 3 &&
                       SymbolEqualityComparer.Default.Equals(parameters[0].Type, itemType) &&
                       SymbolEqualityComparer.Default.Equals(parameters[1].Type, valueType) &&
                       IsMetadataType(parameters[2].Type, "System.Threading.CancellationToken");
            case EditMethodKind.Coerce:
                return SymbolEqualityComparer.Default.Equals(method.ReturnType, valueType) &&
                       parameters.Length == 2 &&
                       SymbolEqualityComparer.Default.Equals(parameters[0].Type, itemType) &&
                       SymbolEqualityComparer.Default.Equals(parameters[1].Type, valueType);
            case EditMethodKind.CanEdit:
                return method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                       parameters.Length == 1 &&
                       SymbolEqualityComparer.Default.Equals(parameters[0].Type, itemType);
            default:
                return false;
        }
    }

    private static string? ValidateTemplateFactoryMethod(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        Dictionary<string, TypedConstant> options,
        string optionName,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? name = GeneratorUtilities.GetString(options, optionName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        bool found = itemType.GetMembers(name!).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
            method.TypeParameters.Length == 0 && method.Parameters.Length == 2 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, itemType) &&
            IsMetadataType(method.Parameters[1].Type, "Avalonia.Controls.Control") &&
            IsOrDerivesFrom(method.ReturnType, "Avalonia.Controls.Control"));
        if (found)
        {
            return name;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidCustomizationMethod,
            GeneratorUtilities.GetLocation(property),
            name,
            itemType.ToDisplayString()));
        return null;
    }

    private static void ValidateDrawOperationFactory(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        string kind,
        Dictionary<string, TypedConstant> options,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        out INamedTypeSymbol? factoryType,
        out string? factoryMethod)
    {
        factoryType = GeneratorUtilities.GetType(options, "DrawOperationFactoryType");
        factoryMethod = GeneratorUtilities.GetString(options, "DrawOperationFactoryMethod");
        if (factoryType == null && string.IsNullOrWhiteSpace(factoryMethod))
        {
            factoryMethod = null;
            return;
        }

        Location location = GeneratorUtilities.GetLocation(property);
        if (!string.Equals(kind, "CustomDrawing", StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidDrawOperationFactory,
                location,
                factoryType?.ToDisplayString() ?? factoryMethod ?? string.Empty,
                property.Name,
                "factory options are only valid for CustomDrawing columns"));
            factoryType = null;
            factoryMethod = null;
            return;
        }

        if (factoryType != null && !string.IsNullOrWhiteSpace(factoryMethod))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidDrawOperationFactory,
                location,
                factoryType.ToDisplayString() + " / " + factoryMethod,
                property.Name,
                "specify either DrawOperationFactoryType or DrawOperationFactoryMethod, not both"));
            factoryType = null;
            factoryMethod = null;
            return;
        }

        if (factoryType != null)
        {
            bool validType = !factoryType.IsAbstract &&
                GeneratorUtilities.IsAccessibleFromGeneratedCode(factoryType) &&
                ImplementsMetadataName(factoryType, "Avalonia.Controls.IDataGridCellDrawOperationFactory") &&
                factoryType.InstanceConstructors.Any(static constructor =>
                    constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
            if (!validType)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidDrawOperationFactory,
                    location,
                    factoryType.ToDisplayString(),
                    property.Name,
                    "the type must be accessible, non-abstract, implement IDataGridCellDrawOperationFactory, and expose an accessible parameterless constructor"));
                factoryType = null;
            }
            return;
        }

        IMethodSymbol? method = itemType.GetMembers(factoryMethod!).OfType<IMethodSymbol>().FirstOrDefault(static methodCandidate =>
            methodCandidate.IsStatic &&
            methodCandidate.TypeParameters.Length == 0 &&
            methodCandidate.Parameters.Length == 0 &&
            GeneratorUtilities.IsAccessibleFromGeneratedCode(methodCandidate) &&
            ImplementsMetadataName(methodCandidate.ReturnType, "Avalonia.Controls.IDataGridCellDrawOperationFactory"));
        if (method == null)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidDrawOperationFactory,
                location,
                factoryMethod ?? string.Empty,
                property.Name,
                "the method must be accessible, static, parameterless, and return IDataGridCellDrawOperationFactory"));
            factoryMethod = null;
        }
    }

    private static bool IsMetadataType(ITypeSymbol type, string metadataName) =>
        type is INamedTypeSymbol named &&
        string.Equals(GeneratorUtilities.GetMetadataName(named), metadataName, StringComparison.Ordinal);

    private static bool IsConstructedType(ITypeSymbol type, string metadataName, SpecialType argumentType) =>
        type is INamedTypeSymbol named &&
        named.TypeArguments.Length == 1 &&
        named.TypeArguments[0].SpecialType == argumentType &&
        string.Equals(GeneratorUtilities.GetMetadataName(named.OriginalDefinition), metadataName, StringComparison.Ordinal);

    private static bool IsOrDerivesFrom(ITypeSymbol? type, string metadataName)
    {
        ITypeSymbol? current = type;
        while (current is INamedTypeSymbol named)
        {
            if (string.Equals(GeneratorUtilities.GetMetadataName(named), metadataName, StringComparison.Ordinal))
            {
                return true;
            }

            current = named.BaseType;
        }

        return false;
    }

    private static bool ValidateImplementation(INamedTypeSymbol itemType, INamedTypeSymbol implementationType)
    {
        if (!GeneratorUtilities.IsAccessibleFromGeneratedCode(implementationType) || implementationType.IsAbstract)
        {
            return false;
        }

        bool hasConstructor = implementationType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
        if (!hasConstructor)
        {
            return false;
        }

        foreach (INamedTypeSymbol implemented in implementationType.AllInterfaces)
        {
            if (string.Equals(
                    GeneratorUtilities.GetMetadataName(implemented.OriginalDefinition),
                    "Avalonia.Controls.IDataGridGeneratedSchema`1",
                    StringComparison.Ordinal) &&
                implemented.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], itemType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValidateControllerImplementation(
        INamedTypeSymbol itemType,
        INamedTypeSymbol implementationType)
    {
        if (!GeneratorUtilities.IsAccessibleFromGeneratedCode(implementationType) || implementationType.IsAbstract)
        {
            return false;
        }

        bool hasConstructor = implementationType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
        if (!hasConstructor)
        {
            return false;
        }

        return implementationType.AllInterfaces.Any(implemented =>
            string.Equals(
                GeneratorUtilities.GetMetadataName(implemented.OriginalDefinition),
                "Avalonia.Controls.IDataGridGeneratedControllerFactory`1",
                StringComparison.Ordinal) &&
            implemented.TypeArguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], itemType));
    }

    private static bool HasControllerConfigureMethod(
        INamedTypeSymbol viewModelType,
        INamedTypeSymbol itemType,
        string name)
    {
        foreach (IMethodSymbol method in viewModelType.GetMembers(name).OfType<IMethodSymbol>())
        {
            if (!method.IsStatic || !method.ReturnsVoid || method.Parameters.Length != 1 ||
                method.Parameters[0].RefKind != RefKind.Ref ||
                method.Parameters[0].Type is not INamedTypeSymbol parameterType ||
                !string.Equals(
                    GeneratorUtilities.GetMetadataName(parameterType.OriginalDefinition),
                    "Avalonia.Controls.DataGridGeneratedControllerOptions`1",
                    StringComparison.Ordinal) ||
                parameterType.TypeArguments.Length != 1 ||
                !SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], itemType))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ValidateGeneratedMember(
        INamedTypeSymbol viewModelType,
        string memberName,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        Location location)
    {
        if (viewModelType.GetMembers(memberName).Length == 0)
        {
            return true;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.MemberCollision,
            location,
            viewModelType.ToDisplayString(),
            memberName));
        return false;
    }

    private static bool ValidateGeneratedViewModelMember(
        INamedTypeSymbol viewModelType,
        string memberName,
        Dictionary<INamedTypeSymbol, HashSet<string>> generatedMembers,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        Location location)
    {
        if (!ValidateGeneratedMember(viewModelType, memberName, diagnostics, location))
        {
            return false;
        }

        if (!generatedMembers.TryGetValue(viewModelType, out HashSet<string>? names))
        {
            names = new HashSet<string>(StringComparer.Ordinal);
            generatedMembers.Add(viewModelType, names);
        }

        if (names.Add(memberName))
        {
            return true;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.MemberCollision,
            location,
            viewModelType.ToDisplayString(),
            memberName));
        return false;
    }

    private static bool AllContainingTypesArePartial(INamedTypeSymbol type)
    {
        INamedTypeSymbol? current = type;
        while (current != null)
        {
            if (!GeneratorUtilities.IsPartial(current))
            {
                return false;
            }

            current = current.ContainingType;
        }

        return true;
    }

    private static INamedTypeSymbol? InferItemType(INamedTypeSymbol viewModelType, string itemsPropertyName)
    {
        IPropertySymbol[] candidates = viewModelType.GetMembers(itemsPropertyName)
            .OfType<IPropertySymbol>()
            .Where(static property => !property.IsStatic && property.GetMethod != null)
            .ToArray();
        if (candidates.Length != 1)
        {
            return null;
        }

        ITypeSymbol type = candidates[0].Type;
        if (type is IArrayTypeSymbol array)
        {
            return array.ElementType as INamedTypeSymbol;
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.IsGenericType && named.TypeArguments.Length == 1 && IsEnumerableDefinition(named.OriginalDefinition))
            {
                return named.TypeArguments[0] as INamedTypeSymbol;
            }

            foreach (INamedTypeSymbol implemented in named.AllInterfaces)
            {
                if (implemented.IsGenericType && implemented.TypeArguments.Length == 1 && IsEnumerableDefinition(implemented.OriginalDefinition))
                {
                    return implemented.TypeArguments[0] as INamedTypeSymbol;
                }
            }
        }

        return null;
    }

    private static bool IsEnumerableDefinition(INamedTypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
    }

    private static PendingViewModel CreatePendingViewModel(
        INamedTypeSymbol viewModelType,
        INamedTypeSymbol itemType,
        AttributeData attribute,
        Dictionary<string, TypedConstant> arguments)
    {
        return new PendingViewModel
        {
            ViewModelType = viewModelType,
            ItemType = itemType,
            ColumnDefinitionsPropertyName = GeneratorUtilities.GetString(arguments, "ColumnDefinitionsPropertyName") ?? "ColumnDefinitions",
            SchemaPropertyName = GeneratorUtilities.GetString(arguments, "SchemaPropertyName") ?? "DataGridSchema",
            FastPathOptionsPropertyName = GeneratorUtilities.GetString(arguments, "FastPathOptionsPropertyName") ?? "FastPathOptions",
            Location = GetLocation(attribute)
        };
    }

    private static void AddPendingViewModel(
        Dictionary<string, PendingViewModel> viewModels,
        PendingViewModel pending)
    {
        string key = GeneratorUtilities.GetMetadataName(pending.ViewModelType) + "|" +
            GeneratorUtilities.GetMetadataName(pending.ItemType) + "|" +
            pending.ColumnDefinitionsPropertyName + "|" +
            pending.SchemaPropertyName + "|" +
            pending.FastPathOptionsPropertyName;
        viewModels[key] = pending;
    }

    private static void ResolveProviderCollisions(IEnumerable<SchemaModel> schemas)
    {
        foreach (IGrouping<string, SchemaModel> group in schemas.GroupBy(
                     static schema => schema.ProviderNamespace + "." + schema.ProviderName,
                     StringComparer.Ordinal))
        {
            SchemaModel[] collisions = group
                .OrderBy(static schema => GeneratorUtilities.GetMetadataName(schema.ItemType), StringComparer.Ordinal)
                .ToArray();
            if (collisions.Length < 2)
            {
                continue;
            }

            for (int i = 0; i < collisions.Length; i++)
            {
                collisions[i].ProviderName += "_" + StableHash(GeneratorUtilities.GetMetadataName(collisions[i].ItemType));
            }
        }
    }

    private static string StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }

            return hash.ToString("x8");
        }
    }

    private static bool NamespaceMatches(INamedTypeSymbol type, string target, bool includeNested)
    {
        string actual = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return string.Equals(actual, target, StringComparison.Ordinal) ||
               (includeNested && actual.StartsWith(target + ".", StringComparison.Ordinal));
    }

    private static bool IsEligibleItemType(INamedTypeSymbol type)
    {
        return (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) &&
               !type.IsStatic &&
               type.TypeParameters.Length == 0 &&
               GeneratorUtilities.IsAccessibleFromGeneratedCode(type);
    }

    private static bool IsAttribute(AttributeData attribute, string metadataName)
    {
        return attribute.AttributeClass != null &&
               string.Equals(GeneratorUtilities.GetMetadataName(attribute.AttributeClass), metadataName, StringComparison.Ordinal);
    }

    private static INamedTypeSymbol? GetConstructorType(AttributeData attribute, int index)
    {
        return attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as INamedTypeSymbol
            : null;
    }

    private static string? GetConstructorString(AttributeData attribute, int index)
    {
        return attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as string
            : null;
    }

    private static int GetEnumValue(Dictionary<string, TypedConstant> arguments, string name, int fallback)
    {
        return arguments.TryGetValue(name, out TypedConstant value) && value.Value is int number ? number : fallback;
    }

    private static Location GetLocation(AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
    }

    private sealed class PendingViewModel
    {
        public INamedTypeSymbol ViewModelType { get; set; } = null!;

        public INamedTypeSymbol ItemType { get; set; } = null!;

        public string ColumnDefinitionsPropertyName { get; set; } = "ColumnDefinitions";

        public string SchemaPropertyName { get; set; } = "DataGridSchema";

        public string FastPathOptionsPropertyName { get; set; } = "FastPathOptions";

        public bool IsDirectIncremental { get; set; }

        public Location Location { get; set; } = Location.None;
    }
}
