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

            ValidateSchemaServiceImplementations(schema, schemaDiagnostics);
            schema.OperationPresetMethods = DiscoverOperationPresetMethods(schema, schemaDiagnostics);

            if (IsRuntimeDefinedShape(schema.ItemType))
            {
                schema.Columns = ImmutableArray<ColumnModel>.Empty;
                schema.KeyMember = null;
                schema.Hierarchy = null;
                if (schema.ImplementationType == null)
                {
                    schemaDiagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.RuntimeShapeRequiresProvider,
                        schema.Location,
                        schema.ItemType.ToDisplayString()));
                }

                continue;
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
            ValidateAnalyticsConfigureMethods(schema, schemaDiagnostics);
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
            (targetType.TypeKind != TypeKind.Class &&
             targetType.TypeKind != TypeKind.Struct &&
             targetType.TypeKind != TypeKind.Interface) ||
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
            (targetType.TypeKind != TypeKind.Class &&
             targetType.TypeKind != TypeKind.Struct &&
             targetType.TypeKind != TypeKind.Interface) ||
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

        foreach (INamedTypeSymbol implementedInterface in type.Interfaces
                     .OrderBy(GeneratorUtilities.GetMetadataName, StringComparer.Ordinal))
        {
            AppendTypeFingerprint(builder, implementedInterface, visitedTypes);
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

    public static DirectSchemaCompositionResult ComposeDirectSchemas(
        ImmutableArray<DirectSchemaCandidate> schemaCandidates,
        ImmutableArray<DirectSchemaCandidate> propertyCandidates,
        ImmutableArray<DirectSchemaCandidate> viewModelCandidates,
        ImmutableArray<DirectSchemaCandidate> controllerCandidates,
        CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var schemas = new Dictionary<INamedTypeSymbol, SchemaModel>(SymbolEqualityComparer.Default);
        var contributors = new Dictionary<INamedTypeSymbol, List<string>>(SymbolEqualityComparer.Default);
        foreach (DirectSchemaCandidate candidate in schemaCandidates
                     .OrderBy(static candidate => GeneratorUtilities.GetMetadataName(candidate.TargetType), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (AttributeData attribute in candidate.Attributes)
            {
                INamedTypeSymbol itemType = GetConstructorType(attribute, 0) ?? candidate.TargetType;
                AddOrUpdateSchema(schemas, itemType, attribute, explicitProviderName: null, explicitConfiguration: true);
                AddDirectSchemaContributor(contributors, itemType, candidate.CacheKey);
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

            AddDirectSchemaContributor(contributors, candidate.TargetType, candidate.CacheKey);
        }

        ApplyDirectOwnerSchemaRequests(
            viewModelCandidates,
            schemas,
            contributors,
            isController: false,
            diagnostics,
            cancellationToken);
        ApplyDirectOwnerSchemaRequests(
            controllerCandidates,
            schemas,
            contributors,
            isController: true,
            diagnostics,
            cancellationToken);

        ResolveProviderCollisions(schemas.Values);
        ImmutableArray<DirectSchemaBuildCandidate> buildCandidates = schemas.Values
            .OrderBy(static schema => schema.ProviderNamespace, StringComparer.Ordinal)
            .ThenBy(static schema => schema.ProviderName, StringComparer.Ordinal)
            .Select(schema => new DirectSchemaBuildCandidate(
                schema,
                CreateDirectSchemaBuildCacheKey(schema, contributors)))
            .ToImmutableArray();
        return new DirectSchemaCompositionResult(buildCandidates, diagnostics.ToImmutable());
    }

    public static DirectSchemaGenerationResult BuildDirectSchema(
        DirectSchemaBuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        SchemaModel schema = candidate.Schema;
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidateSchemaTarget(schema, diagnostics))
        {
            return new DirectSchemaGenerationResult(
                candidate.CacheKey,
                ImmutableArray<GeneratedSource>.Empty,
                diagnostics.ToImmutable());
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

        ValidateSchemaServiceImplementations(schema, diagnostics);
        schema.OperationPresetMethods = DiscoverOperationPresetMethods(schema, diagnostics);

        if (IsRuntimeDefinedShape(schema.ItemType))
        {
            schema.Columns = ImmutableArray<ColumnModel>.Empty;
            schema.KeyMember = null;
            schema.Hierarchy = null;
            if (schema.ImplementationType == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.RuntimeShapeRequiresProvider,
                    schema.Location,
                    schema.ItemType.ToDisplayString()));
                return new DirectSchemaGenerationResult(
                    candidate.CacheKey,
                    ImmutableArray<GeneratedSource>.Empty,
                    diagnostics.ToImmutable());
            }

            GeneratedSource runtimeSource = Emitter.EmitSchemaSource(schema);
            return new DirectSchemaGenerationResult(
                candidate.CacheKey,
                ImmutableArray.Create(runtimeSource),
                diagnostics.ToImmutable());
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
            return new DirectSchemaGenerationResult(
                candidate.CacheKey,
                ImmutableArray<GeneratedSource>.Empty,
                diagnostics.ToImmutable());
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
        ValidateAnalyticsConfigureMethods(schema, diagnostics);

        return new DirectSchemaGenerationResult(
            candidate.CacheKey,
            ImmutableArray.Create(Emitter.EmitSchemaSource(schema)),
            diagnostics.ToImmutable());
    }

    private static string CreateDirectSchemaBuildCacheKey(
        SchemaModel schema,
        Dictionary<INamedTypeSymbol, List<string>> contributors)
    {
        var builder = new StringBuilder();
        builder.Append(GeneratorUtilities.GetMetadataName(schema.ItemType))
            .Append('|').Append(schema.ProviderNamespace)
            .Append('|').Append(schema.ProviderName);
        if (contributors.TryGetValue(schema.ItemType, out List<string>? keys))
        {
            foreach (string key in keys.Distinct(StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal))
            {
                builder.Append('|').Append(key);
            }
        }

        return builder.ToString();
    }

    private static void AddDirectSchemaContributor(
        Dictionary<INamedTypeSymbol, List<string>> contributors,
        INamedTypeSymbol itemType,
        string cacheKey)
    {
        if (!contributors.TryGetValue(itemType, out List<string>? keys))
        {
            keys = new List<string>();
            contributors.Add(itemType, keys);
        }

        keys.Add(cacheKey);
    }

    private static void ApplyDirectOwnerSchemaRequests(
        ImmutableArray<DirectSchemaCandidate> candidates,
        Dictionary<INamedTypeSymbol, SchemaModel> schemas,
        Dictionary<INamedTypeSymbol, List<string>> contributors,
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

                AddDirectSchemaContributor(contributors, itemType, candidate.CacheKey);

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

                ApplyControllerKeyOptions(schema, arguments, attribute, diagnostics);

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
                                   model.GenerateFastPathOptionsProperty ||
                                   model.GenerateNavigationModelProperty ||
                                   model.GenerateNavigationInputModelProperty)
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
                NavigationModelPropertyName = pending.NavigationModelPropertyName,
                NavigationInputModelPropertyName = pending.NavigationInputModelPropertyName,
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
            model.GenerateNavigationModelProperty = pending.GenerateNavigationModel && ValidateGeneratedViewModelMember(
                pending.ViewModelType,
                model.NavigationModelPropertyName,
                generatedMembers,
                viewModelDiagnostics,
                pending.Location);
            model.GenerateNavigationInputModelProperty = pending.GenerateNavigationInputModel && ValidateGeneratedViewModelMember(
                pending.ViewModelType,
                model.NavigationInputModelPropertyName,
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
                if (!ApplyControllerKeyOptions(schema, arguments, attribute, diagnostics))
                {
                    continue;
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

                ITypeSymbol? sourceKeyType = GetSourceCacheKeyType(sourceSymbol, sourceKind);
                string? pipelineTransformMethod = GeneratorUtilities.GetString(arguments, "PipelineTransformMethod");
                if (!string.IsNullOrEmpty(pipelineTransformMethod) &&
                    !HasDynamicDataPipelineTransformMethod(
                        viewModelType,
                        itemType,
                        sourceKeyType,
                        sourceKind,
                        pipelineTransformMethod!))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidCustomizationMethod,
                        GetLocation(attribute),
                        pipelineTransformMethod,
                        viewModelType.ToDisplayString()));
                    continue;
                }

                bool canGenerate = ValidateGeneratedMember(viewModelType, name, diagnostics, GetLocation(attribute));
                canGenerate &= ValidateGeneratedMember(viewModelType, name + "Descriptors", diagnostics, GetLocation(attribute));
                canGenerate &= ValidateGeneratedMember(viewModelType, name + "Commands", diagnostics, GetLocation(attribute));
                canGenerate &= ValidateGeneratedMember(viewModelType, name + "Presets", diagnostics, GetLocation(attribute));
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
                    canGenerate &= ValidateGeneratedMember(viewModelType, "Prefetch" + name + "Async", diagnostics, GetLocation(attribute));
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
                    SourceKeyType = sourceKeyType,
                    SourceKind = sourceKind,
                    Features = GetEnumValue(arguments, "Features", 15),
                    OperationExecution = operationExecution,
                    ImplementationType = implementationType,
                    ConfigureMethod = configureMethod,
                    PipelineTransformMethod = pipelineTransformMethod,
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

            if (arguments.TryGetValue("MutationHandlerType", out TypedConstant mutationHandler) &&
                mutationHandler.Value is INamedTypeSymbol mutationHandlerType)
            {
                schema.MutationHandlerType = mutationHandlerType;
            }

            if (arguments.TryGetValue("NewRowFactoryType", out TypedConstant newRowFactory) &&
                newRowFactory.Value is INamedTypeSymbol newRowFactoryType)
            {
                schema.NewRowFactoryType = newRowFactoryType;
            }

            if (arguments.TryGetValue("FormulaFillTranslatorType", out TypedConstant formulaFillTranslator) &&
                formulaFillTranslator.Value is INamedTypeSymbol formulaFillTranslatorType)
            {
                schema.FormulaFillTranslatorType = formulaFillTranslatorType;
            }

            schema.OperationPresetMethodNames = GeneratorUtilities.GetStringArray(arguments, "OperationPresetMethods");

            schema.KeySelectorMethodName = GeneratorUtilities.GetString(arguments, "KeySelectorMethod");
            schema.UseReferenceIdentityKey = GeneratorUtilities.GetBoolean(arguments, "UseReferenceIdentityKey", false);
        }

        if (arguments.ContainsKey("PivotConfigureMethod"))
        {
            schema.PivotConfigureMethod = GeneratorUtilities.GetString(arguments, "PivotConfigureMethod");
        }
        if (arguments.ContainsKey("OutlineConfigureMethod"))
        {
            schema.OutlineConfigureMethod = GeneratorUtilities.GetString(arguments, "OutlineConfigureMethod");
        }

        ApplyFastOptions(schema, arguments);
    }

    private static void ApplyFastOptions(SchemaModel schema, Dictionary<string, TypedConstant> arguments)
    {
        schema.Strict = GeneratorUtilities.GetBoolean(arguments, "Strict", schema.Strict);
        schema.Streaming = GeneratorUtilities.GetBoolean(arguments, "Streaming", schema.Streaming);
        schema.HierarchicalRows = GeneratorUtilities.GetBoolean(arguments, "HierarchicalRows", schema.HierarchicalRows);
        schema.PerformanceProfile = GetEnumValue(arguments, "PerformanceProfile", schema.PerformanceProfile);
        schema.DefaultPageSize = GeneratorUtilities.GetInt32(arguments, "DefaultPageSize", schema.DefaultPageSize);
        schema.InitialPageIndex = GeneratorUtilities.GetInt32(arguments, "InitialPageIndex", schema.InitialPageIndex);
        schema.InitialCurrency = GetEnumValue(arguments, "InitialCurrency", schema.InitialCurrency);
        schema.PreserveCurrentItemByKey = GeneratorUtilities.GetBoolean(arguments, "PreserveCurrentItemByKey", schema.PreserveCurrentItemByKey);
        schema.PreserveSelectionByKey = GeneratorUtilities.GetBoolean(arguments, "PreserveSelectionByKey", schema.PreserveSelectionByKey);
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
        var properties = new Dictionary<string, (IPropertySymbol Source, IPropertySymbol Access, INamedTypeSymbol? Receiver)>(StringComparer.Ordinal);
        var ambiguousProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (INamedTypeSymbol current in EnumerateSchemaTypes(schema.ItemType, schema.IncludeInherited))
        {
            foreach (IPropertySymbol sourceProperty in current.GetMembers().OfType<IPropertySymbol>())
            {
                IPropertySymbol property = GetAccessProperty(sourceProperty, out INamedTypeSymbol? receiverType);
                if (ambiguousProperties.Contains(property.Name))
                {
                    continue;
                }

                if (!properties.TryGetValue(property.Name, out var existing))
                {
                    properties.Add(property.Name, (sourceProperty, property, receiverType));
                    continue;
                }

                if (schema.ItemType.TypeKind != TypeKind.Interface)
                {
                    bool existingExplicit = existing.Receiver != null;
                    bool currentExplicit = receiverType != null;
                    if (existingExplicit != currentExplicit)
                    {
                        if (!existingExplicit)
                        {
                            continue;
                        }

                        properties[property.Name] = (sourceProperty, property, receiverType);
                        continue;
                    }

                    if (!existingExplicit || SymbolEqualityComparer.Default.Equals(existing.Receiver, receiverType))
                    {
                        continue;
                    }

                    properties.Remove(property.Name);
                    ambiguousProperties.Add(property.Name);
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.AmbiguousExplicitInterfaceProperty,
                        GeneratorUtilities.GetLocation(sourceProperty),
                        schema.ItemType.ToDisplayString(),
                        property.Name,
                        existing.Receiver!.ToDisplayString(),
                        receiverType!.ToDisplayString()));
                    continue;
                }

                if (IsInterfaceDerivedFrom(existing.Access.ContainingType, property.ContainingType))
                {
                    continue;
                }

                if (IsInterfaceDerivedFrom(property.ContainingType, existing.Access.ContainingType))
                {
                    properties[property.Name] = (sourceProperty, property, receiverType);
                    continue;
                }

                properties.Remove(property.Name);
                ambiguousProperties.Add(property.Name);
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.AmbiguousInterfaceProperty,
                    schema.Location,
                    schema.ItemType.ToDisplayString(),
                    property.Name,
                    existing.Access.ContainingType.ToDisplayString(),
                    property.ContainingType.ToDisplayString()));
            }

        }

        var columns = ImmutableArray.CreateBuilder<ColumnModel>();
        var columnKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach ((IPropertySymbol sourceProperty, IPropertySymbol property, INamedTypeSymbol? receiverType) in properties.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttributeData? columnAttribute = GeneratorUtilities.FindAttribute(sourceProperty, ProDataGridGenerator.ColumnAttributeName) ??
                                             GeneratorUtilities.FindAttribute(property, ProDataGridGenerator.ColumnAttributeName);
            if (GeneratorUtilities.HasAttribute(sourceProperty, ProDataGridGenerator.IgnoreColumnAttributeName) ||
                GeneratorUtilities.HasAttribute(property, ProDataGridGenerator.IgnoreColumnAttributeName) ||
                (schema.AttributedOnly && columnAttribute == null))
            {
                continue;
            }

            string? unsupportedReason = GetUnsupportedPropertyReason(property);
            if (unsupportedReason != null)
            {
                if (columnAttribute != null)
                {
                    DiagnosticDescriptor descriptor = property.GetMethod == null ||
                        !GeneratorUtilities.IsAccessibleFromGeneratedCode(property.GetMethod)
                            ? GeneratorDiagnostics.InaccessibleProperty
                            : GeneratorDiagnostics.UnsupportedProperty;
                    diagnostics.Add(Diagnostic.Create(
                        descriptor,
                        GeneratorUtilities.GetLocation(sourceProperty),
                        descriptor == GeneratorDiagnostics.InaccessibleProperty
                            ? new object[] { sourceProperty.ToDisplayString() }
                            : new object[] { sourceProperty.ToDisplayString(), unsupportedReason }));
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
            string? parserMethod = ValidateEditMethod(schema.ItemType, sourceProperty, options, "ParserMethod", EditMethodKind.Parser, diagnostics);
            string? formatterMethod = ValidateEditMethod(schema.ItemType, sourceProperty, options, "FormatterMethod", EditMethodKind.Formatter, diagnostics);
            string? validatorMethod = ValidateEditMethod(schema.ItemType, sourceProperty, options, "ValidatorMethod", EditMethodKind.Validator, diagnostics);
            string? asyncValidatorMethod = ValidateEditMethod(schema.ItemType, sourceProperty, options, "AsyncValidatorMethod", EditMethodKind.AsyncValidator, diagnostics);
            string? coerceMethod = ValidateEditMethod(schema.ItemType, sourceProperty, options, "CoerceMethod", EditMethodKind.Coerce, diagnostics);
            string? canEditMethod = ValidateEditMethod(schema.ItemType, sourceProperty, options, "CanEditMethod", EditMethodKind.CanEdit, diagnostics);
            string? templateFactoryMethod = ValidateTemplateFactoryMethod(schema.ItemType, sourceProperty, options, "TemplateFactoryMethod", diagnostics);
            string? editingTemplateFactoryMethod = ValidateTemplateFactoryMethod(schema.ItemType, sourceProperty, options, "EditingTemplateFactoryMethod", diagnostics);
            string? newRowTemplateFactoryMethod = ValidateTemplateFactoryMethod(schema.ItemType, sourceProperty, options, "NewRowTemplateFactoryMethod", diagnostics);
            ValidateDrawOperationFactory(
                schema.ItemType,
                sourceProperty,
                kind,
                options,
                diagnostics,
                out INamedTypeSymbol? drawOperationFactoryType,
                out string? drawOperationFactoryMethod);
            IPropertySymbol? contentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, sourceProperty, kind, options, "ContentMember", diagnostics);
            IPropertySymbol? checkedContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, sourceProperty, kind, options, "CheckedContentMember", diagnostics);
            IPropertySymbol? uncheckedContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, sourceProperty, kind, options, "UncheckedContentMember", diagnostics);
            IPropertySymbol? onContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, sourceProperty, kind, options, "OnContentMember", diagnostics);
            IPropertySymbol? offContentMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, sourceProperty, kind, options, "OffContentMember", diagnostics);
            IPropertySymbol? commandMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, sourceProperty, kind, options, "CommandMember", diagnostics);
            IPropertySymbol? commandParameterMember = ValidateAuxiliaryColumnBinding(
                schema.ItemType, sourceProperty, kind, options, "CommandParameterMember", diagnostics);
            string? headerProviderMethod = ValidateLocalizationMethod(
                schema.ItemType, sourceProperty, options, "HeaderProviderMethod", diagnostics, out bool headerProviderAcceptsFormatProvider);
            string? descriptionProviderMethod = ValidateLocalizationMethod(
                schema.ItemType, sourceProperty, options, "DescriptionProviderMethod", diagnostics, out bool descriptionProviderAcceptsFormatProvider);
            GroupModel? group = DiscoverGroup(schema.ItemType, sourceProperty, diagnostics);
            ImmutableArray<SummaryModel> summaries = DiscoverSummaries(sourceProperty);
            ImmutableArray<ConditionalRuleModel> conditionalRules = DiscoverConditionalRules(schema.ItemType, sourceProperty, columnKey, diagnostics);
            ImmutableArray<BandModel> bands = DiscoverBands(sourceProperty, diagnostics);
            ImmutableArray<AnalyticsRoleModel> analyticsRoles = DiscoverAnalyticsRoles(schema.ItemType, sourceProperty, diagnostics);
            bool searchable = GeneratorUtilities.GetBoolean(options, "IsSearchable", true);
            int frozenPlacement = GetEnumValue(options, "FrozenPlacement", 0);
            if (frozenPlacement is < 0 or > 2)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property), property.ToDisplayString(), kind, "a valid FrozenPlacement"));
                frozenPlacement = 0;
            }
            if (options.ContainsKey("DisplayIndex") && GeneratorUtilities.GetInt32(options, "DisplayIndex", -1) < 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property), property.ToDisplayString(), kind, "a non-negative DisplayIndex"));
            }

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

            ValidateRequiredKindOptions(sourceProperty, kind, options, diagnostics);
            ValidateOptimizedColumnOptions(sourceProperty, kind, options, diagnostics);

            int sourceOrder = sourceProperty.Locations.FirstOrDefault(static location => location.IsInSource)?.SourceSpan.Start ?? int.MaxValue;
            columns.Add(new ColumnModel
            {
                Property = property,
                ConfigurationProperty = sourceProperty,
                AccessReceiverType = receiverType,
                Kind = kind,
                Header = header ?? GeneratorUtilities.ToHeader(property.Name),
                HeaderProviderMethod = headerProviderMethod,
                HeaderProviderAcceptsFormatProvider = headerProviderAcceptsFormatProvider,
                DescriptionProviderMethod = descriptionProviderMethod,
                DescriptionProviderAcceptsFormatProvider = descriptionProviderAcceptsFormatProvider,
                Order = GeneratorUtilities.GetInt32(options, "Order", 0),
                SourceOrder = sourceOrder,
                FrozenPlacement = frozenPlacement,
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
            .OrderBy(static column => column.FrozenPlacement == 1 ? 0 : column.FrozenPlacement == 2 ? 2 : 1)
            .ThenBy(static column => column.Order)
            .ThenBy(static column => column.SourceOrder)
            .ThenBy(static column => column.Property.Name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool ApplyControllerKeyOptions(
        SchemaModel schema,
        Dictionary<string, TypedConstant> arguments,
        AttributeData attribute,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? keyMember = GeneratorUtilities.GetString(arguments, "KeyMember");
        string? keySelectorMethod = GeneratorUtilities.GetString(arguments, "KeySelectorMethod");
        bool useReferenceIdentity = GeneratorUtilities.GetBoolean(arguments, "UseReferenceIdentityKey", false);
        int configuredKinds = (string.IsNullOrWhiteSpace(keyMember) ? 0 : 1) +
                              (string.IsNullOrWhiteSpace(keySelectorMethod) ? 0 : 1) +
                              (useReferenceIdentity ? 1 : 0);
        if (configuredKinds > 1)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidItemKey,
                GetLocation(attribute),
                keyMember ?? keySelectorMethod ?? schema.ItemType.ToDisplayString(),
                "KeyMember, KeySelectorMethod, and UseReferenceIdentityKey are mutually exclusive"));
            return false;
        }

        if (!string.IsNullOrWhiteSpace(keyMember))
        {
            if ((!string.IsNullOrEmpty(schema.ExplicitKeyMemberName) &&
                 !string.Equals(schema.ExplicitKeyMemberName, keyMember, StringComparison.Ordinal)) ||
                !string.IsNullOrEmpty(schema.KeySelectorMethodName) ||
                schema.UseReferenceIdentityKey)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    GetLocation(attribute),
                    keyMember,
                    "controllers sharing a schema must use the same key configuration"));
                return false;
            }

            schema.ExplicitKeyMemberName = keyMember;
        }
        else if (!string.IsNullOrWhiteSpace(keySelectorMethod))
        {
            if ((!string.IsNullOrEmpty(schema.KeySelectorMethodName) &&
                 !string.Equals(schema.KeySelectorMethodName, keySelectorMethod, StringComparison.Ordinal)) ||
                !string.IsNullOrEmpty(schema.ExplicitKeyMemberName) ||
                schema.UseReferenceIdentityKey)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    GetLocation(attribute),
                    keySelectorMethod,
                    "controllers sharing a schema must use the same key configuration"));
                return false;
            }

            schema.KeySelectorMethodName = keySelectorMethod;
        }
        else if (useReferenceIdentity)
        {
            if (!string.IsNullOrEmpty(schema.ExplicitKeyMemberName) ||
                !string.IsNullOrEmpty(schema.KeySelectorMethodName))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    GetLocation(attribute),
                    schema.ItemType.ToDisplayString(),
                    "controllers sharing a schema must use the same key configuration"));
                return false;
            }

            schema.UseReferenceIdentityKey = true;
        }

        return true;
    }

    private static KeyMemberModel? DiscoverKeyMember(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (schema.UseReferenceIdentityKey)
        {
            if (!string.IsNullOrEmpty(schema.ExplicitKeyMemberName) ||
                !string.IsNullOrEmpty(schema.KeySelectorMethodName))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    schema.Location,
                    schema.ItemType.ToDisplayString(),
                    "reference identity cannot be combined with KeyMember or KeySelectorMethod"));
                return null;
            }

            if (!schema.ItemType.IsReferenceType)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    schema.Location,
                    schema.ItemType.ToDisplayString(),
                    "reference identity requires a reference-type item schema"));
                return null;
            }

            return new KeyMemberModel
            {
                Member = schema.ItemType,
                Type = schema.ItemType,
                Kind = KeyAccessorKind.ReferenceIdentity
            };
        }

        if (!string.IsNullOrEmpty(schema.KeySelectorMethodName))
        {
            if (!string.IsNullOrEmpty(schema.ExplicitKeyMemberName))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    schema.Location,
                    schema.KeySelectorMethodName,
                    "KeyMember and KeySelectorMethod are mutually exclusive"));
                return null;
            }

            IMethodSymbol[] methods = schema.ItemType.GetMembers(schema.KeySelectorMethodName!)
                .OfType<IMethodSymbol>()
                .Where(method =>
                    method.IsStatic &&
                    !method.IsGenericMethod &&
                    method.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, schema.ItemType) &&
                    !method.ReturnsVoid &&
                    GeneratorUtilities.IsAccessibleFromGeneratedCode(method))
                .ToArray();
            if (methods.Length != 1 || IsNullableKeyType(methods[0].ReturnType))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidItemKey,
                    methods.Length > 0 ? GeneratorUtilities.GetLocation(methods[0]) : schema.Location,
                    schema.KeySelectorMethodName,
                    "expected one accessible static non-generic TKey Method(TItem item) returning a non-nullable key"));
                return null;
            }

            return new KeyMemberModel
            {
                Member = methods[0],
                Type = methods[0].ReturnType,
                Kind = KeyAccessorKind.StaticMethod
            };
        }

        if (!string.IsNullOrEmpty(schema.ExplicitKeyMemberName))
        {
            ISymbol[] explicitMembers = EnumerateMembers(schema.ItemType, schema.IncludeInherited)
                .Where(member => string.Equals(GetSchemaMemberName(member), schema.ExplicitKeyMemberName, StringComparison.Ordinal))
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
        foreach (INamedTypeSymbol current in EnumerateSchemaTypes(schema.ItemType, schema.IncludeInherited))
        {
            foreach (ISymbol member in current.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ISymbol accessMember = member is IPropertySymbol property
                    ? GetAccessProperty(property, out _)
                    : member;
                if (!GeneratorUtilities.HasAttribute(member, ProDataGridGenerator.KeyAttributeName) &&
                    !GeneratorUtilities.HasAttribute(accessMember, ProDataGridGenerator.KeyAttributeName))
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
        INamedTypeSymbol? receiverType = null;
        reason = null;
        if (member is IPropertySymbol property)
        {
            property = GetAccessProperty(property, out receiverType);
            member = property;
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

        keyMember = new KeyMemberModel
        {
            Member = member,
            Type = memberType,
            AccessReceiverType = receiverType,
            Kind = KeyAccessorKind.Member
        };
        return true;
    }

    private static string GetSchemaMemberName(ISymbol member)
    {
        return member is IPropertySymbol property
            ? GetAccessProperty(property, out _).Name
            : member.Name;
    }

    private static IEnumerable<ISymbol> EnumerateMembers(INamedTypeSymbol type, bool includeInherited)
    {
        foreach (INamedTypeSymbol current in EnumerateSchemaTypes(type, includeInherited))
        {
            foreach (ISymbol member in current.GetMembers())
            {
                yield return member;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateSchemaTypes(
        INamedTypeSymbol type,
        bool includeInherited)
    {
        yield return type;
        if (!includeInherited)
        {
            yield break;
        }

        if (type.TypeKind != TypeKind.Interface)
        {
            INamedTypeSymbol? current = type.BaseType;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }

            yield break;
        }

        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { type };
        foreach (INamedTypeSymbol inherited in EnumerateInheritedInterfaces(type, visited))
        {
            yield return inherited;
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateInheritedInterfaces(
        INamedTypeSymbol type,
        HashSet<INamedTypeSymbol> visited)
    {
        foreach (INamedTypeSymbol inherited in type.Interfaces
                     .OrderBy(GeneratorUtilities.GetMetadataName, StringComparer.Ordinal))
        {
            if (!visited.Add(inherited))
            {
                continue;
            }

            yield return inherited;
            foreach (INamedTypeSymbol nested in EnumerateInheritedInterfaces(inherited, visited))
            {
                yield return nested;
            }
        }
    }

    private static bool IsInterfaceDerivedFrom(INamedTypeSymbol candidate, INamedTypeSymbol possibleBase)
    {
        if (SymbolEqualityComparer.Default.Equals(candidate, possibleBase))
        {
            return true;
        }

        return candidate.AllInterfaces.Any(
            inherited => SymbolEqualityComparer.Default.Equals(inherited, possibleBase));
    }

    private static IPropertySymbol GetAccessProperty(
        IPropertySymbol property,
        out INamedTypeSymbol? receiverType)
    {
        if (property.ExplicitInterfaceImplementations.Length == 0)
        {
            receiverType = null;
            return property;
        }

        IPropertySymbol contract = property.ExplicitInterfaceImplementations
            .OrderBy(static candidate => GeneratorUtilities.GetMetadataName(candidate.ContainingType), StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Name, StringComparer.Ordinal)
            .First();
        receiverType = contract.ContainingType;
        return contract;
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

        if (schema.DefaultPageSize < 0 || schema.InitialPageIndex < 0 ||
            schema.InitialCurrency < 0 || schema.InitialCurrency > 3)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidCollectionViewDefaults,
                schema.Location,
                type.ToDisplayString(),
                "DefaultPageSize and InitialPageIndex must be non-negative and InitialCurrency must be a supported value"));
            return false;
        }

        if (schema.DefaultPageSize == 0 && schema.InitialPageIndex != 0)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidCollectionViewDefaults,
                schema.Location,
                type.ToDisplayString(),
                "InitialPageIndex requires a positive DefaultPageSize"));
            return false;
        }

        string? reason = null;
        if (type.TypeKind != TypeKind.Class &&
            type.TypeKind != TypeKind.Struct &&
            type.TypeKind != TypeKind.Interface)
        {
            reason = "only classes, structs, and interfaces are supported";
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

        if (kind == "Formula" && required == null)
        {
            string formula = GeneratorUtilities.GetString(options, "Formula")!;
            if (!global::ProDataGrid.FormulaEngine.Excel.ExcelFormulaSyntaxValidator.TryValidate(
                formula,
                out global::ProDataGrid.FormulaEngine.Excel.ExcelFormulaSyntaxError syntaxError))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidFormulaSyntax,
                    GeneratorUtilities.GetLocation(property),
                    property.ToDisplayString(),
                    syntaxError.Position,
                    syntaxError.Message));
            }
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

    private static void ValidateOptimizedColumnOptions(
        IPropertySymbol property,
        string kind,
        Dictionary<string, TypedConstant> options,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ValidateKindSpecificOption(property, kind, options, diagnostics, "UseDirectTextCell", "Text");
        ValidateKindSpecificOption(property, kind, options, diagnostics, "UseDirectCell", "Hierarchical");
        ValidateKindSpecificOption(property, kind, options, diagnostics, "UseDirectTextContent", "Text", "Hierarchical");
        ValidateKindSpecificOption(property, kind, options, diagnostics, "UseOptimizedPresenter", "Hierarchical");
        ValidateKindSpecificOption(property, kind, options, diagnostics, "TrackDirectTextValueChanges", "Text", "Hierarchical");
        ValidateKindSpecificOption(property, kind, options, diagnostics, "UseDirectValueAccessor", "CustomDrawing");
        ValidateKindSpecificOption(property, kind, options, diagnostics, "TrackDirectValueChanges", "CustomDrawing");
    }

    private static void ValidateKindSpecificOption(
        IPropertySymbol property,
        string kind,
        Dictionary<string, TypedConstant> options,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string option,
        params string[] supportedKinds)
    {
        if (!options.ContainsKey(option) || supportedKinds.Contains(kind, StringComparer.Ordinal))
        {
            return;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidColumnConfiguration,
            GeneratorUtilities.GetLocation(property),
            property.ToDisplayString(),
            kind,
            $"{option} is supported only by {string.Join(" or ", supportedKinds)} columns"));
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

    private static void ValidateAnalyticsConfigureMethods(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ValidateSchemaConfigureMethod(
            schema,
            diagnostics,
            schema.PivotConfigureMethod,
            "Avalonia.Controls.DataGridPivoting.PivotTableModel",
            static (target, value) => target.PivotConfigureMethod = value);
        ValidateSchemaConfigureMethod(
            schema,
            diagnostics,
            schema.OutlineConfigureMethod,
            "Avalonia.Controls.DataGridReporting.OutlineReportModel",
            static (target, value) => target.OutlineConfigureMethod = value);
    }

    private static void ValidateSchemaConfigureMethod(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        string? methodName,
        string parameterMetadataName,
        Action<SchemaModel, string?> assign)
    {
        if (string.IsNullOrEmpty(methodName) ||
            HasStaticVoidConfigureMethod(schema.ItemType, methodName!, parameterMetadataName))
        {
            return;
        }

        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidCustomizationMethod,
            schema.Location,
            methodName,
            schema.ItemType.ToDisplayString()));
        assign(schema, null);
    }

    private static bool HasStaticVoidConfigureMethod(
        INamedTypeSymbol type,
        string name,
        string parameterMetadataName) =>
        type.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.MethodKind == MethodKind.Ordinary &&
            method.IsStatic &&
            !method.IsGenericMethod &&
            GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
            method.ReturnsVoid &&
            method.Parameters.Length == 1 &&
            method.Parameters[0].RefKind == RefKind.None &&
            method.Parameters[0].Type is INamedTypeSymbol parameterType &&
            string.Equals(GeneratorUtilities.GetMetadataName(parameterType), parameterMetadataName, StringComparison.Ordinal));

    private static ImmutableArray<IMethodSymbol> DiscoverOperationPresetMethods(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (schema.OperationPresetMethodNames.IsDefaultOrEmpty)
        {
            return ImmutableArray<IMethodSymbol>.Empty;
        }

        var methods = ImmutableArray.CreateBuilder<IMethodSymbol>(schema.OperationPresetMethodNames.Length);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string configuredName in schema.OperationPresetMethodNames)
        {
            if (string.IsNullOrWhiteSpace(configuredName) || !names.Add(configuredName))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidCustomizationMethod,
                    schema.Location,
                    configuredName ?? string.Empty,
                    schema.ItemType.ToDisplayString()));
                continue;
            }

            IMethodSymbol[] matches = schema.ItemType.GetMembers(configuredName)
                .OfType<IMethodSymbol>()
                .Where(static method =>
                    method.MethodKind == MethodKind.Ordinary &&
                    method.IsStatic &&
                    !method.IsGenericMethod &&
                    method.Parameters.Length == 0 &&
                    GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
                    method.ReturnType is INamedTypeSymbol returnType &&
                    GeneratorUtilities.GetMetadataName(returnType) == "Avalonia.Controls.DataGridGeneratedOperationPreset")
                .ToArray();
            if (matches.Length != 1)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidCustomizationMethod,
                    schema.Location,
                    configuredName,
                    schema.ItemType.ToDisplayString()));
                continue;
            }

            methods.Add(matches[0]);
        }

        return methods.ToImmutable();
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
            string? operand = GeneratorUtilities.GetString(arguments, "Operand");
            string? operand2 = GeneratorUtilities.GetString(arguments, "Operand2");
            string? invalidOption = GetInvalidConditionalRuleOption(property.Type, condition, operand, operand2);
            if (invalidOption != null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property), property.ToDisplayString(), "ConditionalFormat", invalidOption));
            }
            rules.Add(new ConditionalRuleModel
            {
                Condition = condition,
                RuleId = GeneratorUtilities.GetString(arguments, "RuleId") ?? columnKey + ":rule:" + index.ToString(CultureInfo.InvariantCulture),
                Operand = operand,
                Operand2 = operand2,
                StringComparison = GetEnumValue(arguments, "StringComparison", 4),
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

    private static string? GetInvalidConditionalRuleOption(
        ITypeSymbol propertyType,
        int condition,
        string? operand,
        string? operand2)
    {
        if (condition is 6 or 7 or 8)
        {
            return null;
        }

        ITypeSymbol effectiveType = propertyType;
        if (propertyType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            effectiveType = nullable.TypeArguments[0];
        }

        if (condition is 10 or 11 or 12)
        {
            if (effectiveType.SpecialType != SpecialType.System_String)
            {
                return "a string property for text predicates";
            }

            return operand == null ? "Operand" : null;
        }

        if (!IsSupportedConditionalOperand(effectiveType, operand))
        {
            return "a valid typed Operand";
        }

        return condition == 9 && !IsSupportedConditionalOperand(effectiveType, operand2)
            ? "a valid typed Operand2"
            : null;
    }

    private static bool IsSupportedConditionalOperand(ITypeSymbol type, string? text)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return text != null;
        }

        if (type.SpecialType == SpecialType.System_Boolean)
        {
            return bool.TryParse(text, out _);
        }

        bool isNumeric = type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
                SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
                SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
                SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
        return isNumeric && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
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
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var roles = ImmutableArray.CreateBuilder<AnalyticsRoleModel>();
        AddRoleAttributes(itemType, property, ProDataGridGenerator.PivotAxisAttributeName, allowedRoles: 1 | 2 | 4, roles, diagnostics);
        AddRoleAttributes(itemType, property, ProDataGridGenerator.ChartFieldAttributeName, allowedRoles: 16 | 32 | 64 | 128 | 256, roles, diagnostics);
        AddRoleAttributes(itemType, property, ProDataGridGenerator.OutlineFieldAttributeName, allowedRoles: 512 | 1024, roles, diagnostics);

        foreach (AttributeData attribute in GeneratorUtilities.FindAttributes(property, ProDataGridGenerator.PivotValueAttributeName))
        {
            Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(attribute);
            int aggregate = GetConstructorEnumValue(attribute, 0);
            string? configureMethod = ValidateAnalyticsFieldConfigureMethod(
                itemType,
                property,
                arguments,
                "ConfigureMethod",
                "Avalonia.Controls.DataGridPivoting.PivotValueField",
                diagnostics);
            string? aggregatorFactory = ValidateAggregatorFactoryMethod(
                itemType,
                property,
                arguments,
                diagnostics);
            string? formula = GeneratorUtilities.GetString(arguments, "Formula");
            if (string.IsNullOrWhiteSpace(formula))
            {
                formula = null;
            }
            if (aggregatorFactory != null && aggregate != 15)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property),
                    property.ToDisplayString(),
                    "PivotValue",
                    "Custom aggregate when CustomAggregatorFactoryMethod is supplied"));
                aggregatorFactory = null;
            }
            if (formula != null && aggregatorFactory != null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property),
                    property.ToDisplayString(),
                    "PivotValue",
                    "either Formula or CustomAggregatorFactoryMethod"));
                aggregatorFactory = null;
            }
            if (aggregate == 15 && aggregatorFactory == null && configureMethod == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property),
                    property.ToDisplayString(),
                    "PivotValue",
                    "CustomAggregatorFactoryMethod or ConfigureMethod for a Custom aggregate"));
            }
            roles.Add(new AnalyticsRoleModel
            {
                Role = 8,
                Order = GeneratorUtilities.GetInt32(arguments, "Order", 0),
                Name = GeneratorUtilities.GetString(arguments, "Name"),
                Format = GeneratorUtilities.GetString(arguments, "Format"),
                Aggregate = aggregate,
                PivotDisplayMode = GetEnumValue(arguments, "DisplayMode", 0),
                Formula = formula,
                Dependencies = GeneratorUtilities.GetStringArray(arguments, "Dependencies"),
                ConfigureMethod = configureMethod,
                CustomAggregatorFactoryMethod = aggregatorFactory
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
                bool isFormulaField = (role.Role & 2048) != 0;
                bool isCalculatedPivotValue = (role.Role & 8) != 0 && role.Formula != null;
                if (!isFormulaField && !isCalculatedPivotValue)
                {
                    continue;
                }

                string name = role.Name ?? column.ColumnKey;
                if (isFormulaField && !formulaNames.Add(name))
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidFormulaMetadata,
                        GeneratorUtilities.GetLocation(column.Property),
                        name,
                        "formula names must be unique within a schema"));
                }

                if (isCalculatedPivotValue && role.Dependencies.Length == 0)
                {
                    diagnostics.Add(Diagnostic.Create(
                        GeneratorDiagnostics.InvalidFormulaMetadata,
                        GeneratorUtilities.GetLocation(column.Property),
                        name,
                        "calculated pivot values must declare at least one stable dependency key"));
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
        INamedTypeSymbol itemType,
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
            string? configureParameter = attributeName == ProDataGridGenerator.PivotAxisAttributeName
                ? "Avalonia.Controls.DataGridPivoting.PivotAxisField"
                : attributeName == ProDataGridGenerator.OutlineFieldAttributeName
                    ? role == 512
                        ? "Avalonia.Controls.DataGridReporting.OutlineGroupField"
                        : "Avalonia.Controls.DataGridReporting.OutlineValueField"
                    : null;
            string? configureMethod = configureParameter == null
                ? null
                : ValidateAnalyticsFieldConfigureMethod(
                    itemType,
                    property,
                    arguments,
                    "ConfigureMethod",
                    configureParameter,
                    diagnostics);
            string? aggregatorFactory = attributeName == ProDataGridGenerator.OutlineFieldAttributeName && role == 1024
                ? ValidateAggregatorFactoryMethod(itemType, property, arguments, diagnostics)
                : null;
            if (attributeName == ProDataGridGenerator.OutlineFieldAttributeName &&
                role == 512 &&
                !string.IsNullOrWhiteSpace(GeneratorUtilities.GetString(arguments, "CustomAggregatorFactoryMethod")))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property),
                    property.ToDisplayString(),
                    "OutlineField",
                    "CustomAggregatorFactoryMethod only on OutlineDetail roles"));
            }
            int aggregate = GetEnumValue(arguments, "Aggregate", 0);
            if (aggregatorFactory != null && aggregate != 9)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property),
                    property.ToDisplayString(),
                    "OutlineField",
                    "Custom aggregate when CustomAggregatorFactoryMethod is supplied"));
                aggregatorFactory = null;
            }
            if (attributeName == ProDataGridGenerator.OutlineFieldAttributeName &&
                role == 1024 && aggregate == 9 && aggregatorFactory == null && configureMethod == null)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.InvalidColumnConfiguration,
                    GeneratorUtilities.GetLocation(property),
                    property.ToDisplayString(),
                    "OutlineField",
                    "CustomAggregatorFactoryMethod or ConfigureMethod for a Custom aggregate"));
            }
            roles.Add(new AnalyticsRoleModel
            {
                Role = role,
                Order = GeneratorUtilities.GetInt32(arguments, "Order", 0),
                Name = GeneratorUtilities.GetString(arguments, attributeName == ProDataGridGenerator.ChartFieldAttributeName ? "Series" : "Name"),
                Format = GeneratorUtilities.GetString(arguments, "Format"),
                Aggregate = aggregate,
                ConfigureMethod = configureMethod,
                CustomAggregatorFactoryMethod = aggregatorFactory
            });
        }
    }

    private static string? ValidateAnalyticsFieldConfigureMethod(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        Dictionary<string, TypedConstant> arguments,
        string optionName,
        string parameterMetadataName,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? methodName = GeneratorUtilities.GetString(arguments, optionName);
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }
        if (HasStaticVoidConfigureMethod(itemType, methodName!, parameterMetadataName))
        {
            return methodName;
        }
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidCustomizationMethod,
            GeneratorUtilities.GetLocation(property),
            methodName,
            itemType.ToDisplayString()));
        return null;
    }

    private static string? ValidateAggregatorFactoryMethod(
        INamedTypeSymbol itemType,
        IPropertySymbol property,
        Dictionary<string, TypedConstant> arguments,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        string? methodName = GeneratorUtilities.GetString(arguments, "CustomAggregatorFactoryMethod");
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }
        bool valid = itemType.GetMembers(methodName!).OfType<IMethodSymbol>().Any(method =>
            method.MethodKind == MethodKind.Ordinary &&
            method.IsStatic &&
            !method.IsGenericMethod &&
            GeneratorUtilities.IsAccessibleFromGeneratedCode(method) &&
            method.Parameters.Length == 0 &&
            method.ReturnType is INamedTypeSymbol returnType &&
            (string.Equals(
                 GeneratorUtilities.GetMetadataName(returnType),
                 "Avalonia.Controls.DataGridPivoting.IPivotAggregator",
                 StringComparison.Ordinal) ||
             ImplementsMetadataName(returnType, "Avalonia.Controls.DataGridPivoting.IPivotAggregator")));
        if (valid)
        {
            return methodName;
        }
        diagnostics.Add(Diagnostic.Create(
            GeneratorDiagnostics.InvalidCustomizationMethod,
            GeneratorUtilities.GetLocation(property),
            methodName,
            itemType.ToDisplayString()));
        return null;
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

    private static void ValidateSchemaServiceImplementations(
        SchemaModel schema,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (schema.MutationHandlerType != null &&
            !ValidateServiceImplementation(
                schema.ItemType,
                schema.MutationHandlerType,
                "Avalonia.Controls.IDataGridGeneratedCollectionMutationHandler`1"))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidMutationHandler,
                schema.Location,
                schema.MutationHandlerType.ToDisplayString(),
                schema.ItemType.ToDisplayString()));
            schema.MutationHandlerType = null;
        }

        if (schema.NewRowFactoryType != null &&
            !ValidateServiceImplementation(
                schema.ItemType,
                schema.NewRowFactoryType,
                "Avalonia.Controls.IDataGridGeneratedNewRowFactory`1"))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidNewRowFactory,
                schema.Location,
                schema.NewRowFactoryType.ToDisplayString(),
                schema.ItemType.ToDisplayString()));
            schema.NewRowFactoryType = null;
        }

        if (schema.FormulaFillTranslatorType != null &&
            !ValidateFormulaFillTranslator(schema.FormulaFillTranslatorType))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidFormulaFillTranslator,
                schema.Location,
                schema.FormulaFillTranslatorType.ToDisplayString()));
            schema.FormulaFillTranslatorType = null;
        }
    }

    private static bool ValidateFormulaFillTranslator(INamedTypeSymbol implementationType)
    {
        if (!GeneratorUtilities.IsAccessibleFromGeneratedCode(implementationType) ||
            implementationType.IsAbstract ||
            implementationType.IsUnboundGenericType ||
            implementationType.TypeParameters.Length != 0)
        {
            return false;
        }

        bool hasConstructor = implementationType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 && GeneratorUtilities.IsAccessibleFromGeneratedCode(constructor));
        return hasConstructor && implementationType.AllInterfaces.Any(implemented =>
            string.Equals(
                GeneratorUtilities.GetMetadataName(implemented),
                "ProDataGrid.FormulaEngine.IFormulaFillTranslator",
                StringComparison.Ordinal));
    }

    private static bool ValidateServiceImplementation(
        INamedTypeSymbol itemType,
        INamedTypeSymbol implementationType,
        string interfaceMetadataName)
    {
        if (!GeneratorUtilities.IsAccessibleFromGeneratedCode(implementationType) ||
            implementationType.IsAbstract ||
            implementationType.IsUnboundGenericType ||
            implementationType.TypeParameters.Length != 0)
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
                interfaceMetadataName,
                StringComparison.Ordinal) &&
            implemented.TypeArguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], itemType));
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

    private static bool HasDynamicDataPipelineTransformMethod(
        INamedTypeSymbol viewModelType,
        INamedTypeSymbol itemType,
        ITypeSymbol? keyType,
        int sourceKind,
        string name)
    {
        if (sourceKind is not 2 and not 3)
        {
            return false;
        }

        foreach (IMethodSymbol method in viewModelType.GetMembers(name).OfType<IMethodSymbol>())
        {
            if (method.Parameters.Length != 1 || method.Parameters[0].RefKind != RefKind.None ||
                !SymbolEqualityComparer.Default.Equals(method.ReturnType, method.Parameters[0].Type) ||
                method.Parameters[0].Type is not INamedTypeSymbol observable ||
                !IsMetadataType(observable.OriginalDefinition, "System.IObservable`1") ||
                observable.TypeArguments.Length != 1 ||
                observable.TypeArguments[0] is not INamedTypeSymbol changeSet)
            {
                continue;
            }

            string expectedChangeSet = sourceKind == 2 ? "DynamicData.IChangeSet`1" : "DynamicData.IChangeSet`2";
            if (!IsMetadataType(changeSet.OriginalDefinition, expectedChangeSet) ||
                changeSet.TypeArguments.Length != (sourceKind == 2 ? 1 : 2) ||
                !SymbolEqualityComparer.Default.Equals(changeSet.TypeArguments[0], itemType))
            {
                continue;
            }

            if (sourceKind == 2 ||
                (keyType != null && SymbolEqualityComparer.Default.Equals(changeSet.TypeArguments[1], keyType)))
            {
                return true;
            }
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
            GenerateNavigationModel = GeneratorUtilities.GetBoolean(arguments, "GenerateNavigationModel", false),
            NavigationModelPropertyName = GeneratorUtilities.GetString(arguments, "NavigationModelPropertyName") ?? "NavigationModel",
            GenerateNavigationInputModel = GeneratorUtilities.GetBoolean(arguments, "GenerateNavigationInputModel", false),
            NavigationInputModelPropertyName = GeneratorUtilities.GetString(arguments, "NavigationInputModelPropertyName") ?? "NavigationInputModel",
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
            pending.FastPathOptionsPropertyName + "|" +
            pending.GenerateNavigationModel + "|" +
            pending.NavigationModelPropertyName + "|" +
            pending.GenerateNavigationInputModel + "|" +
            pending.NavigationInputModelPropertyName;
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
        return (type.TypeKind == TypeKind.Class ||
                type.TypeKind == TypeKind.Struct ||
                type.TypeKind == TypeKind.Interface) &&
               !type.IsStatic &&
               type.TypeParameters.Length == 0 &&
               GeneratorUtilities.IsAccessibleFromGeneratedCode(type);
    }

    private static bool IsRuntimeDefinedShape(INamedTypeSymbol type)
    {
        if (IsRuntimeDefinedShapeContract(type))
        {
            return true;
        }

        foreach (INamedTypeSymbol contract in type.AllInterfaces)
        {
            if (IsRuntimeDefinedShapeContract(contract))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRuntimeDefinedShapeContract(INamedTypeSymbol type)
    {
        string metadataName = GeneratorUtilities.GetMetadataName(type.OriginalDefinition);
        return metadataName is
            "System.Data.DataTable" or
            "System.Data.DataRow" or
            "System.Data.DataRowView" or
            "System.Data.IDataRecord" or
            "System.Collections.IDictionary" or
            "System.Collections.Generic.IDictionary`2" or
            "System.Collections.Generic.IReadOnlyDictionary`2" or
            "System.ComponentModel.ICustomTypeDescriptor" or
            "System.Dynamic.IDynamicMetaObjectProvider";
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

        public bool GenerateNavigationModel { get; set; }

        public string NavigationModelPropertyName { get; set; } = "NavigationModel";

        public bool GenerateNavigationInputModel { get; set; }

        public string NavigationInputModelPropertyName { get; set; } = "NavigationInputModel";

        public bool IsDirectIncremental { get; set; }

        public Location Location { get; set; } = Location.None;
    }
}
