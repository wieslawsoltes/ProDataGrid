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

namespace ProDataGrid.SourceGenerators;

internal static class Emitter
{
    public static IEnumerable<GeneratedSource> Emit(GenerationModel model, CancellationToken cancellationToken)
    {
        foreach (SchemaModel schema in model.Schemas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (schema.IsDirectIncremental ||
                (schema.Columns.Length == 0 && schema.ImplementationType == null))
            {
                continue;
            }

            yield return EmitSchemaSource(schema);
        }

        foreach (ViewModelModel viewModel in model.ViewModels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (viewModel.IsDirectIncremental ||
                (!viewModel.GenerateColumnDefinitionsProperty &&
                 !viewModel.GenerateSchemaProperty &&
                 !viewModel.GenerateFastPathOptionsProperty &&
                 !viewModel.GenerateNavigationModelProperty &&
                 !viewModel.GenerateNavigationInputModelProperty &&
                 !viewModel.GenerateRouteContextFactoryProperty))
            {
                continue;
            }

            yield return EmitViewModelSource(viewModel);
        }

        foreach (ControllerModel controller in model.Controllers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!controller.IsDirectIncremental)
            {
                yield return EmitControllerSource(controller);
            }
        }

        foreach (ViewModelViewModel view in model.Views)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return EmitViewSource(view);
        }

        if (model.Registry != null)
        {
            yield return new GeneratedSource(
                CreateHintName(model.Registry.RegistryNamespace, model.Registry.RegistryName, "Registry"),
                EmitRegistry(model, model.Registry));
        }
    }

    internal static GeneratedSource EmitSchemaSource(SchemaModel schema) =>
        new(
            CreateHintName(schema.ProviderNamespace, schema.ProviderName, "Schema"),
            EmitSchema(schema));

    internal static GeneratedSource EmitViewSource(ViewModelViewModel view) =>
        new(
            CreateHintName(view.ViewNamespace, view.ViewName, "View"),
            EmitView(view));

    internal static GeneratedSource EmitViewModelSource(ViewModelModel viewModel) =>
        new(
            CreateHintName(
                viewModel.ViewModelType.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                GeneratorUtilities.GetMetadataName(viewModel.ViewModelType) + "." +
                viewModel.ColumnDefinitionsPropertyName,
                "ViewModel"),
            EmitViewModel(viewModel));

    internal static GeneratedSource EmitControllerSource(ControllerModel controller) =>
        new(
            CreateHintName(
                controller.ViewModelType.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                GeneratorUtilities.GetMetadataName(controller.ViewModelType) + "." + controller.Name,
                "Controller"),
            EmitController(controller));

    private static string EmitRegistry(GenerationModel model, RegistryModel registry)
    {
        SchemaModel[] schemas = model.Schemas
            .Where(static schema => schema.Columns.Length > 0 || schema.ImplementationType != null)
            .ToArray();
        var builder = new StringBuilder(4096);
        AppendHeader(builder);
        OpenNamespace(builder, registry.RegistryNamespace);
        builder.Append("    ").Append(registry.IsPublic ? "public" : "internal").Append(" static class ").Append(registry.RegistryName).AppendLine()
            .AppendLine("    {")
            .AppendLine("        private static readonly global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider[] s_schemas =")
            .AppendLine("            new global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider[]")
            .AppendLine("            {");
        foreach (SchemaModel schema in schemas)
        {
            builder.Append("                ").Append(GetProviderType(schema)).AppendLine(".Instance,");
        }

        builder.AppendLine("            };")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider> Schemas => s_schemas;")
            .AppendLine()
            .AppendLine("        public static bool TryGetSchema(")
            .AppendLine("            global::System.Type itemType,")
            .AppendLine("            out global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider schema)")
            .AppendLine("        {")
            .AppendLine("            if (itemType is null)")
            .AppendLine("            {")
            .AppendLine("                schema = null!;")
            .AppendLine("                return false;")
            .AppendLine("            }");
        foreach (SchemaModel schema in schemas)
        {
            string itemType = schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append("            if (itemType == typeof(").Append(itemType).AppendLine("))")
                .AppendLine("            {")
                .Append("                schema = ").Append(GetProviderType(schema)).AppendLine(".Instance;")
                .AppendLine("                return true;")
                .AppendLine("            }");
        }

        builder.AppendLine("            schema = null!;")
            .AppendLine("            return false;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        public static bool TryGetSchema(")
            .AppendLine("            string schemaId,")
            .AppendLine("            out global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider schema)")
            .AppendLine("        {");
        foreach (SchemaModel schema in schemas)
        {
            builder.Append("            if (global::System.String.Equals(schemaId, ")
                .Append(GetProviderType(schema)).AppendLine(".SchemaId, global::System.StringComparison.Ordinal))")
                .AppendLine("            {")
                .Append("                schema = ").Append(GetProviderType(schema)).AppendLine(".Instance;")
                .AppendLine("                return true;")
                .AppendLine("            }");
        }

        builder.AppendLine("            schema = null!;")
            .AppendLine("            return false;")
            .AppendLine("        }");

        builder.AppendLine()
            .AppendLine("        public static bool TryCreateView(")
            .AppendLine("            object? viewModel,")
            .AppendLine("            out global::Avalonia.Controls.Control? view)")
            .AppendLine("        {");
        for (int registrationIndex = 0; registrationIndex < registry.ViewRegistrations.Length; registrationIndex++)
        {
            ViewRegistrationModel registration = registry.ViewRegistrations[registrationIndex];
            string viewModelType = registration.ViewModelType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string viewType = registration.ViewType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string variableName = "typedViewModel" + registrationIndex.ToString(CultureInfo.InvariantCulture);
            builder.Append("            if (viewModel is ").Append(viewModelType).Append(' ').Append(variableName).AppendLine(")")
                .AppendLine("            {")
                .Append("                view = new ").Append(viewType).Append(" { DataContext = ").Append(variableName).AppendLine(" };")
                .AppendLine("                return true;")
                .AppendLine("            }");
        }

        builder.AppendLine("            view = null;")
            .AppendLine("            return false;")
            .AppendLine("        }");

        if (registry.HasMicrosoftDependencyInjection)
        {
            builder.AppendLine()
                .AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedProDataGrids(")
                .AppendLine("            this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)")
                .AppendLine("        {")
                .AppendLine("            global::System.ArgumentNullException.ThrowIfNull(services);");
            foreach (SchemaModel schema in schemas)
            {
                string itemType = schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
                string providerType = GetProviderType(schema);
                builder.AppendLine("            services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(")
                    .AppendLine("                typeof(global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider),")
                    .Append("                ").Append(providerType).AppendLine(".Instance));")
                    .AppendLine("            services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(")
                    .Append("                typeof(global::Avalonia.Controls.IDataGridGeneratedSchema<").Append(itemType).AppendLine(">),")
                    .Append("                ").Append(providerType).AppendLine(".Instance));");
            }
            builder.AppendLine("            return services;")
                .AppendLine("        }");
        }

        builder.AppendLine("    }");
        CloseNamespace(builder, registry.RegistryNamespace);
        return builder.ToString();
    }

    private static string GetProviderType(SchemaModel schema) =>
        string.IsNullOrEmpty(schema.ProviderNamespace)
            ? "global::" + schema.ProviderName
            : "global::" + schema.ProviderNamespace + "." + schema.ProviderName;

    private static string EmitSchema(SchemaModel schema)
    {
        string itemType = schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string accessibility = IsPubliclyAccessible(schema.ItemType) ? "public" : "internal";
        var builder = new StringBuilder(16384);
        AppendHeader(builder);
        OpenNamespace(builder, schema.ProviderNamespace);
        builder.Append("    ").Append(accessibility).Append(" sealed class ").Append(schema.ProviderName)
            .Append(" : global::Avalonia.Controls.IDataGridGeneratedSchema<").Append(itemType).Append(">,")
            .AppendLine()
            .AppendLine("        global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider");
        if (IsRuntimeDefinedImplementation(schema))
        {
            builder.AppendLine("        , global::Avalonia.Controls.IDataGridRuntimeDefinedSchema");
        }
        if (schema.KeyMember != null)
        {
            string keyType = schema.KeyMember.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append("        , global::Avalonia.Controls.IDataGridItemKey<").Append(itemType).Append(", ")
                .Append(keyType).AppendLine(">");
        }

        builder
            .AppendLine("    {")
            .Append("        public static ").Append(schema.ProviderName).AppendLine(" Instance { get; } = new();")
            .AppendLine();

        foreach (ColumnModel column in schema.Columns)
        {
            EmitAccessorFields(builder, schema, column, itemType);
            EmitAuxiliaryBindingFields(builder, column, itemType);
            EmitEditField(builder, schema, column, itemType);
        }

        EmitManifest(builder, schema, itemType);

        if (schema.ImplementationType != null)
        {
            EmitImplementationForwarder(builder, schema, itemType);
        }
        else
        {
            EmitGeneratedSchemaBody(builder, schema, itemType);
        }

        builder.AppendLine("    }");
        CloseNamespace(builder, schema.ProviderNamespace);
        return builder.ToString();
    }

    private static void EmitImplementationForwarder(StringBuilder builder, SchemaModel schema, string itemType)
    {
        string implementationType = schema.ImplementationType!.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        builder.Append("        private readonly ").Append(implementationType).Append(" _implementation = new ")
            .Append(implementationType).AppendLine("();")
            .AppendLine()
            .Append("        private ").Append(schema.ProviderName).AppendLine("()")
            .AppendLine("        {")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        public global::Avalonia.Controls.DataGridColumnDefinitionList CreateColumnDefinitions()")
            .AppendLine("            => _implementation.CreateColumnDefinitions();")
            .AppendLine()
            .Append("        public global::System.Collections.Generic.IComparer<").Append(itemType)
            .AppendLine("> CreateSortComparer(global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridSorting.SortingDescriptor> descriptors)")
            .AppendLine("            => _implementation.CreateSortComparer(descriptors);")
            .AppendLine()
            .Append("        public global::System.Func<").Append(itemType)
            .AppendLine(", bool> CreateFilterPredicate(global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridFiltering.FilteringDescriptor> descriptors)")
            .AppendLine("            => _implementation.CreateFilterPredicate(descriptors);")
            .AppendLine()
            .Append("        public global::System.Func<").Append(itemType)
            .AppendLine(", bool> CreateSearchPredicate(global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridSearching.SearchDescriptor> descriptors)")
            .AppendLine("            => _implementation.CreateSearchPredicate(descriptors);")
            .AppendLine()
            .AppendLine("        public global::Avalonia.Controls.DataGridFastPathOptions CreateFastPathOptions()")
            .AppendLine("            => _implementation.CreateFastPathOptions();")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridGeneratedPerformanceOptions CreatePerformanceOptions()")
            .Append("            => global::Avalonia.Controls.DataGridGeneratedPerformanceOptions.Create((global::Avalonia.Controls.DataGridGeneratedPerformanceProfile)")
            .Append(schema.PerformanceProfile.ToString(CultureInfo.InvariantCulture)).AppendLine(");");

        if (ImplementsInterface(schema.ImplementationType!, "Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider"))
        {
            builder.AppendLine()
                .AppendLine("        public global::Avalonia.Controls.DataGridGeneratedSchemaManifest Manifest => _implementation.Manifest;");
        }
        else
        {
            builder.AppendLine()
                .AppendLine("        public global::Avalonia.Controls.DataGridGeneratedSchemaManifest Manifest => s_manifest;");
        }

        if (IsRuntimeDefinedImplementation(schema))
        {
            builder.AppendLine()
                .AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.IDataGridRuntimeSchemaField> RuntimeFields")
                .AppendLine("            => _implementation.RuntimeFields;");
        }
    }

    private static void EmitGeneratedSchemaBody(StringBuilder builder, SchemaModel schema, string itemType)
    {
        builder.AppendLine("        private static readonly global::Avalonia.Controls.DataGridGeneratedDataOperations<" + itemType + "> s_operations =")
            .AppendLine("            new global::Avalonia.Controls.DataGridGeneratedDataOperations<" + itemType + ">(")
            .AppendLine("                new global::Avalonia.Controls.DataGridColumnAccessorRegistration[]")
            .AppendLine("                {");
        foreach (ColumnModel column in schema.Columns)
        {
            string fieldName = GetFieldName(column.Property);
            builder.Append("                    new global::Avalonia.Controls.DataGridColumnAccessorRegistration(")
                .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", ")
                .Append(GeneratorUtilities.EscapeString(column.Property.Name)).Append(", ")
                .Append(fieldName).Append("Accessor, ")
                .Append(column.IsSearchable ? "true" : "false").AppendLine("),");
        }

        builder.AppendLine("                });")
            .AppendLine()
            .Append("        private ").Append(schema.ProviderName).AppendLine("()")
            .AppendLine("        {")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        public global::Avalonia.Controls.DataGridColumnDefinitionList CreateColumnDefinitions()")
            .AppendLine("        {")
            .Append("            var builder = global::Avalonia.Controls.DataGridColumnDefinitionBuilder.For<")
            .Append(itemType).AppendLine(">();")
            .AppendLine("            var columns = new global::Avalonia.Controls.DataGridColumnDefinitionList")
            .AppendLine("            {")
            .Append("                FrozenColumnCount = ").Append(schema.Columns.Count(static column => column.FrozenPlacement == 1).ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                FrozenColumnCountRight = ").Append(schema.Columns.Count(static column => column.FrozenPlacement == 2).ToString(CultureInfo.InvariantCulture)).AppendLine()
            .AppendLine("            };");
        foreach (ColumnModel column in schema.Columns)
        {
            builder.Append("            columns.Add(Create").Append(GetMethodSuffix(column.Property)).AppendLine("Column(builder));");
        }

        if (!string.IsNullOrEmpty(schema.ConfigureMethod))
        {
            builder.Append("            ").Append(itemType).Append('.').Append(GeneratorUtilities.EscapeIdentifier(schema.ConfigureMethod!))
                .AppendLine("(columns);");
        }

        builder.AppendLine("            return columns;")
            .AppendLine("        }")
            .AppendLine();

        foreach (ColumnModel column in schema.Columns)
        {
            EmitColumnFactory(builder, schema, column, itemType);
        }

        builder.Append("        public global::System.Collections.Generic.IComparer<").Append(itemType)
            .AppendLine("> CreateSortComparer(global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridSorting.SortingDescriptor> descriptors)")
            .AppendLine("            => s_operations.CreateSortComparer(descriptors);")
            .AppendLine()
            .Append("        public global::System.Func<").Append(itemType)
            .AppendLine(", bool> CreateFilterPredicate(global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridFiltering.FilteringDescriptor> descriptors)")
            .AppendLine("            => s_operations.CreateFilterPredicate(descriptors);")
            .AppendLine()
            .Append("        public global::System.Func<").Append(itemType)
            .AppendLine(", bool> CreateSearchPredicate(global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridSearching.SearchDescriptor> descriptors)")
            .AppendLine("            => s_operations.CreateSearchPredicate(descriptors);")
            .AppendLine()
            .AppendLine("        public global::Avalonia.Controls.DataGridFastPathOptions CreateFastPathOptions()")
            .AppendLine("        {")
            .AppendLine("            return new global::Avalonia.Controls.DataGridFastPathOptions")
            .AppendLine("            {")
            .Append("                UseAccessorsOnly = ").Append(schema.Strict ? "true" : "false").AppendLine(",")
            .Append("                ThrowOnMissingAccessor = ").Append(schema.Strict ? "true" : "false").AppendLine(",")
            .AppendLine("                EnableHighPerformanceSearching = true,")
            .Append("                HighPerformanceSearchTrackItemChanges = ").Append(schema.Streaming || schema.PerformanceProfile == 6 ? "false" : "true").AppendLine()
            .AppendLine("            };")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridGeneratedPerformanceOptions CreatePerformanceOptions()")
            .Append("            => global::Avalonia.Controls.DataGridGeneratedPerformanceOptions.Create((global::Avalonia.Controls.DataGridGeneratedPerformanceProfile)")
            .Append(schema.PerformanceProfile.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
    }

    private static void EmitManifest(StringBuilder builder, SchemaModel schema, string itemType)
    {
        builder.AppendLine("        public const int ManifestVersion = 1;")
            .Append("        public const int StateVersion = ").Append(schema.StateVersion.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
            .Append("        public const int DefaultPageSize = ").Append(schema.DefaultPageSize.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
            .Append("        public const int InitialPageIndex = ").Append(schema.InitialPageIndex.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
            .Append("        public const global::Avalonia.Controls.DataGridGeneratedInitialCurrency InitialCurrency = (global::Avalonia.Controls.DataGridGeneratedInitialCurrency)")
            .Append(schema.InitialCurrency.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
            .Append("        public const bool PreserveCurrentItemByKey = ").Append(schema.PreserveCurrentItemByKey ? "true" : "false").AppendLine(";")
            .Append("        public const bool PreserveSelectionByKey = ").Append(schema.PreserveSelectionByKey ? "true" : "false").AppendLine(";")
            .Append("        public const string SchemaId = ").Append(GeneratorUtilities.EscapeString(schema.SchemaId)).AppendLine(";")
            .Append("        public const string SchemaHash = ").Append(GeneratorUtilities.EscapeString(GetSchemaHash(schema))).AppendLine(";")
            .AppendLine();

        for (int index = 0; index < schema.Columns.Length; index++)
        {
            ColumnModel column = schema.Columns[index];
            string fieldName = GetFieldName(column.Property);
            string typedFieldName = GetTypedFieldName(column.Property);
            string descriptorType = GetTypedFieldDescriptorType(column, itemType);
            builder.Append("        public static ").Append(descriptorType).Append(' ')
                .Append(typedFieldName).Append(" { get; } = new ")
                .Append(descriptorType).Append('(')
                .Append(index.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", ")
                .Append(GeneratorUtilities.EscapeString(column.Property.Name)).Append(", ")
                .Append(fieldName).Append("Accessor, ")
                .Append(column.IsSearchable ? "true" : "false").AppendLine(",")
                .Append("            ");
            EmitFieldMetadata(builder, column, itemType);
            builder.AppendLine(");");
        }

        builder.AppendLine()
            .AppendLine("        private static readonly global::Avalonia.Controls.DataGridGeneratedField[] s_fields =")
            .AppendLine("            new global::Avalonia.Controls.DataGridGeneratedField[]")
            .AppendLine("            {");

        for (int index = 0; index < schema.Columns.Length; index++)
        {
            ColumnModel column = schema.Columns[index];
            builder.Append("                ").Append(GetTypedFieldName(column.Property)).AppendLine(",");
        }

        builder.AppendLine("            };")
            .AppendLine()
            .AppendLine("        private static readonly global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedField> s_readOnlyFields =")
            .AppendLine("            global::System.Array.AsReadOnly(s_fields);")
            .AppendLine()
            .AppendLine("        private static readonly global::Avalonia.Controls.DataGridGeneratedSchemaManifest s_manifest =")
            .AppendLine("            new global::Avalonia.Controls.DataGridGeneratedSchemaManifest(")
            .AppendLine("                ManifestVersion,")
            .AppendLine("                SchemaId,")
            .AppendLine("                SchemaHash,")
            .Append("                typeof(").Append(itemType).AppendLine("),")
            .AppendLine("                s_fields,");

        if (schema.KeyMember != null)
        {
            string keyType = schema.KeyMember.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append("                ").Append(GeneratorUtilities.EscapeString(GetKeyName(schema))).AppendLine(",")
                .Append("                typeof(").Append(keyType).AppendLine("));");
        }
        else
        {
            builder.AppendLine("                keyMemberName: null,")
                .AppendLine("                keyType: null);");
        }

        if (schema.ImplementationType == null)
        {
            builder.AppendLine()
                .AppendLine("        public global::Avalonia.Controls.DataGridGeneratedSchemaManifest Manifest => s_manifest;");
        }

        builder.AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedField> Fields => Instance.Manifest.Fields;");

        EmitEditFieldCollection(builder, schema, itemType);
        EmitOperationPresets(builder, schema, itemType);
        EmitAnalyticsMetadata(builder, schema, itemType);
        EmitDiagnosticsManifest(builder, schema, itemType);

        builder.AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridGeneratedColumnLayoutController CreateColumnLayoutController()")
            .AppendLine("            => new global::Avalonia.Controls.DataGridGeneratedColumnLayoutController(Instance.CreateColumnDefinitions(), BandFields);")
            .AppendLine()
            .Append("        public static global::Avalonia.Controls.DataGridGeneratedHeaderCommandController<").Append(itemType).AppendLine("> CreateHeaderCommandController(")
            .Append("            global::Avalonia.Controls.DataGridGeneratedOperationController<").Append(itemType).AppendLine("> operations,")
            .AppendLine("            global::Avalonia.Controls.DataGridGeneratedColumnLayoutController layout,")
            .AppendLine("            global::Avalonia.Controls.IDataGridGeneratedHeaderInteraction? interaction = null)")
            .Append("            => new global::Avalonia.Controls.DataGridGeneratedHeaderCommandController<").Append(itemType).AppendLine(">(Instance.Manifest, operations, layout, interaction);");

        builder.AppendLine()
            .AppendLine("        public static bool TryGetField(string key, out global::Avalonia.Controls.DataGridGeneratedField field)")
            .AppendLine("            => Instance.Manifest.TryGetField(key, out field);")
            .AppendLine()
            .Append("        public static global::Avalonia.Controls.DataGridGeneratedOperationController<").Append(itemType)
            .AppendLine("> CreateController(global::Avalonia.Controls.DataGridOperationExecution execution = global::Avalonia.Controls.DataGridOperationExecution.View)")
            .Append("            => new global::Avalonia.Controls.DataGridGeneratedOperationController<").Append(itemType)
            .AppendLine(">(Instance, execution);")
            .AppendLine()
            .Append("        public static global::Avalonia.Controls.DataGridGeneratedOperationController<").Append(itemType)
            .AppendLine("> CreateController(global::Avalonia.Controls.DataGridOperationExecution execution, global::Avalonia.Controls.DataGridGeneratedFeatures features)")
            .Append("            => new global::Avalonia.Controls.DataGridGeneratedOperationController<").Append(itemType)
            .AppendLine(">(Instance, execution, features);");

        EmitHierarchyManifest(builder, schema, itemType);

        if (schema.KeyMember != null)
        {
            string keyType = schema.KeyMember.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.AppendLine()
                .Append("        public static global::System.Collections.Generic.IEqualityComparer<").Append(keyType)
                .Append("> KeyComparer { get; } = ")
                .Append(schema.KeyMember.Kind == KeyAccessorKind.ReferenceIdentity
                    ? "global::System.Collections.Generic.ReferenceEqualityComparer.Instance;"
                    : "global::System.Collections.Generic.EqualityComparer<" + keyType + ">.Default;")
                .AppendLine()
                .AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedItemIndex<").Append(itemType).Append(", ")
                .Append(keyType).AppendLine("> CreateItemIndex()")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedItemIndex<").Append(itemType).Append(", ")
                .Append(keyType).AppendLine(">(Instance, KeyComparer);")
                .AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedItemIndex<").Append(itemType).Append(", ")
                .Append(keyType).Append("> CreateItemIndex(global::System.Collections.Generic.IReadOnlyList<")
                .Append(itemType).AppendLine("> items)")
                .AppendLine("        {")
                .Append("            var index = new global::Avalonia.Controls.DataGridGeneratedItemIndex<").Append(itemType).Append(", ")
                .Append(keyType).AppendLine(">(Instance, KeyComparer, items?.Count ?? 0);")
                .AppendLine("            index.Reset(items ?? throw new global::System.ArgumentNullException(nameof(items)));")
                .AppendLine("            return index;")
                .AppendLine("        }")
                .AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedSnapshotReconciler<")
                .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateSnapshotReconciler(")
                .Append("            global::System.Collections.Generic.IEqualityComparer<").Append(itemType).AppendLine(">? itemComparer = null)")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedSnapshotReconciler<")
                .Append(itemType).Append(", ").Append(keyType).AppendLine(">(Instance, KeyComparer, itemComparer);");

            if (schema.Columns.Any(static column => CanEdit(column)))
            {
                builder.AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedEditController<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateEditController(")
                    .Append("            global::System.Func<").Append(keyType).Append(", ").Append(itemType)
                    .AppendLine(">? itemResolver = null)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedEditController<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine(">(")
                    .AppendLine("                Instance,")
                    .AppendLine("                s_editFields,")
                    .AppendLine("                itemResolver,")
                    .AppendLine("                KeyComparer);")
                    .AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedValidationProjection<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateValidationProjection(")
                    .Append("            global::Avalonia.Controls.DataGridGeneratedEditController<").Append(itemType).Append(", ")
                    .Append(keyType).AppendLine("> editController,")
                    .AppendLine("            bool ownsController = false)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedValidationProjection<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine(">(Instance, editController, KeyComparer, ownsController);")
                    .AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedClipboardController<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateClipboardController(")
                    .Append("            global::Avalonia.Controls.DataGridGeneratedEditController<").Append(itemType).Append(", ")
                    .Append(keyType).AppendLine("> editController)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedClipboardController<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine(">(Instance, editController);")
                    .AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedFillController<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateFillController(")
                    .Append("            global::Avalonia.Controls.DataGridGeneratedEditController<").Append(itemType).Append(", ")
                    .Append(keyType).AppendLine("> editController)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedFillController<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine(">(Instance, editController);")
                    .AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedClipboardImportModel<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateClipboardImportModel(")
                    .Append("            global::Avalonia.Controls.DataGridGeneratedEditController<").Append(itemType).Append(", ")
                    .Append(keyType).AppendLine("> editController,")
                    .Append("            global::System.Action<global::Avalonia.Controls.DataGridGeneratedTransferResult<")
                    .Append(keyType).AppendLine(">>? resultHandler = null,")
                    .AppendLine("            global::System.IFormatProvider? formatProvider = null,")
                    .AppendLine("            global::Avalonia.Controls.DataGridGeneratedTransferLimits? limits = null)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedClipboardImportModel<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine(">(Instance, editController, resultHandler, formatProvider, limits);")
                    .AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedFillModel<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateFillModel(")
                    .Append("            global::Avalonia.Controls.DataGridGeneratedEditController<").Append(itemType).Append(", ")
                    .Append(keyType).AppendLine("> editController,")
                    .Append("            global::System.Action<global::Avalonia.Controls.DataGridGeneratedTransferResult<")
                    .Append(keyType).AppendLine(">>? resultHandler = null,")
                    .AppendLine("            int maximumCells = 100000,")
                    .AppendLine("            bool useSeries = true,")
                    .AppendLine("            global::ProDataGrid.FormulaEngine.IFormulaFillTranslator? formulaTranslator = null)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedFillModel<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine(">(Instance, editController, resultHandler, maximumCells, useSeries, formulaTranslator);");

                if (schema.FormulaFillTranslatorType != null)
                {
                    string translatorType = schema.FormulaFillTranslatorType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
                    builder.AppendLine()
                        .Append("        public static global::Avalonia.Controls.DataGridGeneratedFillModel<")
                        .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateConfiguredFormulaFillModel(")
                        .Append("            global::Avalonia.Controls.DataGridGeneratedEditController<").Append(itemType).Append(", ")
                        .Append(keyType).AppendLine("> editController,")
                        .Append("            global::System.Action<global::Avalonia.Controls.DataGridGeneratedTransferResult<")
                        .Append(keyType).AppendLine(">>? resultHandler = null,")
                        .AppendLine("            int maximumCells = 100000,")
                        .AppendLine("            bool useSeries = true)")
                        .Append("            => new global::Avalonia.Controls.DataGridGeneratedFillModel<")
                        .Append(itemType).Append(", ").Append(keyType).Append(">(Instance, editController, resultHandler, maximumCells, useSeries, new ")
                        .Append(translatorType).AppendLine("());");
                }
            }

            builder.AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedDragDropController<")
                .Append(keyType).AppendLine("> CreateDragDropController(")
                .Append("            global::Avalonia.Controls.IDataGridGeneratedDropHandler<").Append(keyType).AppendLine("> handler,")
                .Append("            global::System.Func<global::Avalonia.Controls.DataGridGeneratedDropRequest<").Append(keyType)
                .AppendLine(">, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<string?>>? validator = null,")
                .Append("            global::System.Func<").Append(keyType).Append(", ").Append(keyType).AppendLine(", bool>? isDescendant = null)")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedDragDropController<")
                .Append(keyType).AppendLine(">(handler, validator, isDescendant, KeyComparer);");

            if (schema.Streaming)
            {
                builder.AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedStreamBuffer<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateStreamBuffer(")
                    .AppendLine("            int capacity = 1024,")
                    .AppendLine("            global::Avalonia.Controls.DataGridGeneratedStreamOverflowPolicy overflowPolicy = global::Avalonia.Controls.DataGridGeneratedStreamOverflowPolicy.CoalesceByKey)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedStreamBuffer<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine(">(capacity, overflowPolicy, KeyComparer);")
                    .AppendLine()
                    .Append("        public static global::Avalonia.Controls.DataGridGeneratedAsyncStreamPump<")
                    .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateAsyncStreamPump(")
                    .Append("            global::System.Func<global::System.ReadOnlyMemory<global::Avalonia.Controls.DataGridGeneratedStreamUpdate<")
                    .Append(itemType).Append(", ").Append(keyType)
                    .AppendLine(">>, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask> applyBatch,")
                    .AppendLine("            int capacity = 1024,")
                    .AppendLine("            int batchSize = 128,")
                    .AppendLine("            global::Avalonia.Controls.DataGridGeneratedStreamOverflowPolicy overflowPolicy = global::Avalonia.Controls.DataGridGeneratedStreamOverflowPolicy.CoalesceByKey)")
                    .Append("            => new global::Avalonia.Controls.DataGridGeneratedAsyncStreamPump<")
                    .Append(itemType).Append(", ").Append(keyType)
                    .AppendLine(">(Instance, applyBatch, capacity, batchSize, overflowPolicy, KeyComparer);");
            }

            builder.AppendLine()
                .AppendLine("        public static global::Avalonia.Controls.DataGridSelection.IdentitySelectionModel CreateIdentitySelectionModel()")
                .Append("            => new global::Avalonia.Controls.DataGridSelection.IdentitySelectionModel(static item => ")
                .Append(GetKeyAccessExpression(schema, "((" + itemType + ")item)")).AppendLine("!);")
                .AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedSelectionController<")
                .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateSelectionController(")
                .AppendLine("            global::Avalonia.Controls.DataGridGeneratedSelectionProfile? profile = null)")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedSelectionController<")
                .Append(itemType).Append(", ").Append(keyType).AppendLine(">(Instance, profile, KeyComparer);")
                .AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridStateOptions CreateStateOptions(")
                .Append("global::System.Func<").Append(keyType).Append(", ").Append(itemType).Append(">? itemKeyResolver = null, ")
                .AppendLine("global::System.Func<object, global::Avalonia.Controls.DataGridColumn>? columnKeyResolver = null)")
                .AppendLine("        {")
                .AppendLine("            return new global::Avalonia.Controls.DataGridStateOptions")
                .AppendLine("            {")
                .AppendLine("                ColumnKeySelector = static column => column.ColumnKey,")
                .AppendLine("                ColumnKeyResolver = columnKeyResolver,")
                .Append("                ItemKeySelector = static item => ")
                .Append(GetKeyAccessExpression(schema, "((" + itemType + ")item)")).AppendLine("!,")
                .Append("                ItemKeyResolver = itemKeyResolver is null ? null : key => itemKeyResolver((")
                .Append(keyType).AppendLine(")key)")
                .AppendLine("            };")
                .AppendLine("        }");

            builder.AppendLine()
                .AppendLine("        public static global::Avalonia.Controls.DataGridGeneratedStateDescriptor CreateStateDescriptor()")
                .AppendLine("        {")
                .AppendLine("            return new global::Avalonia.Controls.DataGridGeneratedStateDescriptor(")
                .AppendLine("                SchemaId,")
                .AppendLine("                SchemaHash,")
                .AppendLine("                StateVersion,")
                .AppendLine("                global::Avalonia.Controls.DataGridStateSections.All,");
            EmitColumnAliasMap(builder, schema);
            builder.AppendLine("            );")
                .AppendLine("        }")
                .AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedStateController CreateStateController(")
                .Append("global::System.Func<").Append(keyType).Append(", ").Append(itemType).Append(">? itemKeyResolver = null, ")
                .AppendLine("global::System.Func<object, global::Avalonia.Controls.DataGridColumn>? columnKeyResolver = null,")
                .AppendLine("            global::Avalonia.Controls.DataGridGeneratedStateMigration? migration = null,")
                .AppendLine("            global::Avalonia.Controls.IDataGridStateSerializer? serializer = null,")
                .AppendLine("            global::Avalonia.Controls.DataGridStatePersistenceOptions? persistenceOptions = null)")
                .AppendLine("            => new global::Avalonia.Controls.DataGridGeneratedStateController(")
                .AppendLine("                CreateStateDescriptor(),")
                .AppendLine("                CreateStateOptions(itemKeyResolver, columnKeyResolver),")
                .AppendLine("                migration,")
                .AppendLine("                serializer,")
                .AppendLine("                persistenceOptions);")
                .AppendLine()
                .Append("        public ").Append(keyType).Append(" GetKey(").Append(itemType).Append(" item)")
                .AppendLine()
                .Append("            => ").Append(GetKeyAccessExpression(schema, "item")).AppendLine(";");
        }

        builder.AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridNavigation.DataGridNavigationModel CreateNavigationModel()")
            .AppendLine("            => new global::Avalonia.Controls.DataGridNavigation.DataGridNavigationModel();")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridNavigation.DataGridNavigationInputModel CreateNavigationInputModel()")
            .AppendLine("            => new global::Avalonia.Controls.DataGridNavigation.DataGridNavigationInputModel();")
            .AppendLine();

        if (schema.KeyMember != null)
        {
            builder.AppendLine("        public static global::Avalonia.Controls.DataGridNavigation.DataGridRouteContextFactory CreateRouteContextFactory()")
                .Append("            => new global::Avalonia.Controls.DataGridNavigation.DataGridRouteContextFactory(static item => Instance.GetKey((")
                .Append(itemType).AppendLine(")item)!);");
        }
        else
        {
            builder.AppendLine("        public static global::Avalonia.Controls.DataGridNavigation.DataGridRouteContextFactory CreateRouteContextFactory()")
                .AppendLine("            => new global::Avalonia.Controls.DataGridNavigation.DataGridRouteContextFactory();");
        }

        builder
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridNavigation.DataGridRouteNavigationModel CreateRouteNavigationModel(")
            .AppendLine("            global::Avalonia.Controls.DataGridNavigation.IDataGridRouteResolver resolver,")
            .AppendLine("            global::Avalonia.Controls.DataGridNavigation.IDataGridRouteNavigator navigator)")
            .AppendLine("            => new global::Avalonia.Controls.DataGridNavigation.DataGridRouteNavigationModel(resolver, navigator);")
            .AppendLine();
    }

    private static void EmitColumnAliasMap(StringBuilder builder, SchemaModel schema)
    {
        int aliasCount = schema.Columns.Sum(static column => column.PreviousColumnKeys.Length);
        if (aliasCount == 0)
        {
            builder.AppendLine("                null");
            return;
        }

        builder.AppendLine("                new global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.Ordinal)")
            .AppendLine("                {");
        foreach (ColumnModel column in schema.Columns)
        {
            foreach (string alias in column.PreviousColumnKeys)
            {
                builder.Append("                    [").Append(GeneratorUtilities.EscapeString(alias)).Append("] = ")
                    .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).AppendLine(",");
            }
        }
        builder.AppendLine("                }");
    }

    private static void EmitHierarchyManifest(StringBuilder builder, SchemaModel schema, string itemType)
    {
        if (schema.Hierarchy == null)
        {
            return;
        }

        builder.AppendLine()
            .Append("        public static global::Avalonia.Controls.DataGridHierarchical.HierarchicalOptions<")
            .Append(itemType).AppendLine("> CreateHierarchicalOptions()")
            .AppendLine("        {")
            .Append("            return new global::Avalonia.Controls.DataGridHierarchical.HierarchicalOptions<")
            .Append(itemType).AppendLine(">")
            .AppendLine("            {");
        if (schema.Hierarchy.ChildrenProperty != null)
        {
            builder.Append("                ChildrenSelector = static item => item.")
                .Append(GeneratorUtilities.EscapeIdentifier(schema.Hierarchy.ChildrenProperty.Name)).AppendLine(",");
            if (schema.Hierarchy.ChildLoaderMethod != null)
            {
                builder.Append("                ChildrenSelectorAsync = static async (item, cancellationToken) => await item.")
                    .Append(GeneratorUtilities.EscapeIdentifier(schema.Hierarchy.ChildLoaderMethod.Name))
                    .AppendLine("(cancellationToken).ConfigureAwait(false),");
            }
        }

        if (schema.Hierarchy.ExpandedProperty != null)
        {
            string expanded = GeneratorUtilities.EscapeIdentifier(schema.Hierarchy.ExpandedProperty.Name);
            builder.Append("                IsExpandedSelector = static item => item.").Append(expanded).AppendLine(",")
                .Append("                IsExpandedSetter = static (item, value) => item.").Append(expanded).AppendLine(" = value,");
        }

        if (schema.KeyMember != null)
        {
            builder.AppendLine("                ExpandedStateKeyMode = global::Avalonia.Controls.DataGridHierarchical.ExpandedStateKeyMode.Custom,")
                .Append("                ExpandedStateKeySelector = static item => ")
                .Append(GetKeyAccessExpression(schema, "item")).AppendLine(",");
        }

        builder.AppendLine("            };")
            .AppendLine("        }")
            .AppendLine()
            .Append("        public static global::Avalonia.Controls.DataGridHierarchical.HierarchicalModel<")
            .Append(itemType).AppendLine("> CreateHierarchicalModel()")
            .Append("            => new global::Avalonia.Controls.DataGridHierarchical.HierarchicalModel<")
            .Append(itemType).AppendLine(">(CreateHierarchicalOptions());")
            .AppendLine()
            .Append("        public static global::Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter<")
            .Append(itemType).AppendLine("> CreateHierarchicalAdapter(")
            .Append("            global::Avalonia.Controls.DataGridHierarchical.IHierarchicalModel<")
            .Append(itemType).AppendLine("> model,")
            .Append("            global::System.Action<global::Avalonia.Controls.DataGridHierarchical.FlattenedChangedEventArgs<")
            .Append(itemType).AppendLine(">>? flattenedChanged = null)")
            .Append("            => new global::Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter<")
            .Append(itemType).AppendLine(">(model, flattenedChanged);")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridFiltering.DataGridHierarchicalFilteringAdapter CreateHierarchicalFilteringAdapter(")
            .AppendLine("            global::Avalonia.Controls.DataGridFiltering.IFilteringModel model,")
            .AppendLine("            global::System.Func<global::System.Collections.Generic.IEnumerable<global::Avalonia.Controls.DataGridColumn>> columnProvider,")
            .AppendLine("            global::Avalonia.Controls.DataGridHierarchical.IHierarchicalModel hierarchicalModel,")
            .AppendLine("            global::Avalonia.Controls.DataGridFiltering.DataGridHierarchyFilterPolicy policy = global::Avalonia.Controls.DataGridFiltering.DataGridHierarchyFilterPolicy.KeepAncestorsOfMatches,")
            .AppendLine("            global::Avalonia.Controls.DataGridFastPathOptions? options = null,")
            .AppendLine("            global::System.Action? beforeViewRefresh = null,")
            .AppendLine("            global::System.Action? afterViewRefresh = null)")
            .AppendLine("            => new global::Avalonia.Controls.DataGridFiltering.DataGridHierarchicalFilteringAdapter(")
            .AppendLine("                model,")
            .AppendLine("                columnProvider,")
            .AppendLine("                hierarchicalModel,")
            .AppendLine("                policy,")
            .AppendLine("                options ?? Instance.CreateFastPathOptions(),")
            .AppendLine("                beforeViewRefresh,")
            .AppendLine("                afterViewRefresh);")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridFiltering.DataGridHierarchicalFilteringAdapterFactory CreateHierarchicalFilteringAdapterFactory(")
            .AppendLine("            global::Avalonia.Controls.DataGridFiltering.DataGridHierarchyFilterPolicy policy = global::Avalonia.Controls.DataGridFiltering.DataGridHierarchyFilterPolicy.KeepAncestorsOfMatches)")
            .AppendLine("            => new global::Avalonia.Controls.DataGridFiltering.DataGridHierarchicalFilteringAdapterFactory { Policy = policy };");

        if (schema.KeyMember != null && schema.Hierarchy.ChildrenProperty != null)
        {
            string keyType = schema.KeyMember.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string children = GeneratorUtilities.EscapeIdentifier(schema.Hierarchy.ChildrenProperty.Name);
            builder.AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedHierarchyController<")
                .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateHierarchyController()")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedHierarchyController<")
                .Append(itemType).Append(", ").Append(keyType).AppendLine(">(")
                .AppendLine("                Instance,")
                .Append("                static item => item.").Append(children).AppendLine(",");
            if (schema.Hierarchy.ExpandedProperty != null)
            {
                string expanded = GeneratorUtilities.EscapeIdentifier(schema.Hierarchy.ExpandedProperty.Name);
                builder.Append("                static item => item.").Append(expanded).AppendLine(",")
                    .Append("                static (item, value) => item.").Append(expanded).AppendLine(" = value,");
            }
            else
            {
                builder.AppendLine("                null,")
                    .AppendLine("                null,");
            }
            if (schema.Hierarchy.ChildLoaderMethod != null)
            {
                builder.Append("                static (item, cancellationToken) => item.")
                    .Append(GeneratorUtilities.EscapeIdentifier(schema.Hierarchy.ChildLoaderMethod.Name))
                    .AppendLine("(cancellationToken),");
            }
            else
            {
                builder.AppendLine("                null,");
            }
            builder
                .AppendLine("                KeyComparer);");
        }

        if (schema.Hierarchy.ParentKeyMember != null)
        {
            ITypeSymbol? parentKeyType = schema.Hierarchy.ParentKeyMember switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };
            if (parentKeyType != null)
            {
                builder.AppendLine()
                    .Append("        public static ")
                    .Append(parentKeyType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat))
                    .Append(" GetParentKey(").Append(itemType).AppendLine(" item)")
                    .Append("            => item.")
                    .Append(GeneratorUtilities.EscapeIdentifier(schema.Hierarchy.ParentKeyMember.Name)).AppendLine(";");
            }
        }
    }

    private static string GetTypedFieldDescriptorType(ColumnModel column, string itemType)
    {
        string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        ITypeSymbol effectiveType = UnwrapNullable(column.Property.Type);
        if (effectiveType.SpecialType == SpecialType.System_String)
        {
            return "global::Avalonia.Controls.DataGridGeneratedStringField<" + itemType + ", " + valueType + ">";
        }

        if (effectiveType.TypeKind == TypeKind.Enum ||
            column.Kind == "Numeric" ||
            column.Kind == "ProgressBar" ||
            column.Kind == "Slider" ||
            column.Kind == "DatePicker" ||
            column.Kind == "TimePicker")
        {
            return "global::Avalonia.Controls.DataGridGeneratedComparableField<" + itemType + ", " + valueType + ">";
        }

        return "global::Avalonia.Controls.DataGridGeneratedField<" + itemType + ", " + valueType + ">";
    }

    private static void EmitOperationPresets(StringBuilder builder, SchemaModel schema, string itemType)
    {
        builder.AppendLine()
            .AppendLine("        private static readonly global::System.Lazy<global::Avalonia.Controls.DataGridGeneratedOperationPreset[]> s_operationPresets = new(CreateOperationPresets);")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedOperationPreset> OperationPresets => s_operationPresets.Value;")
            .AppendLine()
            .AppendLine("        public static bool TryGetOperationPreset(string name, out global::Avalonia.Controls.DataGridGeneratedOperationPreset preset)")
            .AppendLine("        {")
            .AppendLine("            if (name is not null)")
            .AppendLine("            {")
            .AppendLine("                global::Avalonia.Controls.DataGridGeneratedOperationPreset[] presets = s_operationPresets.Value;")
            .AppendLine("                for (int index = 0; index < presets.Length; index++)")
            .AppendLine("                {")
            .AppendLine("                    global::Avalonia.Controls.DataGridGeneratedOperationPreset candidate = presets[index];")
            .AppendLine("                    if (global::System.String.Equals(candidate.Name, name, global::System.StringComparison.Ordinal))")
            .AppendLine("                    {")
            .AppendLine("                        preset = candidate;")
            .AppendLine("                        return true;")
            .AppendLine("                    }")
            .AppendLine("                }")
            .AppendLine("            }")
            .AppendLine()
            .AppendLine("            preset = null!;")
            .AppendLine("            return false;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        private static global::Avalonia.Controls.DataGridGeneratedOperationPreset[] CreateOperationPresets()")
            .AppendLine("        {");

        if (schema.OperationPresetMethods.IsDefaultOrEmpty)
        {
            builder.AppendLine("            return global::System.Array.Empty<global::Avalonia.Controls.DataGridGeneratedOperationPreset>();");
        }
        else
        {
            builder.AppendLine("            global::Avalonia.Controls.DataGridGeneratedOperationPreset[] presets = new global::Avalonia.Controls.DataGridGeneratedOperationPreset[]")
                .AppendLine("            {");
            foreach (IMethodSymbol method in schema.OperationPresetMethods)
            {
                builder.Append("                ").Append(itemType).Append('.').Append(method.Name).AppendLine("(),");
            }

            builder.AppendLine("            };")
                .AppendLine("            var names = new global::System.Collections.Generic.HashSet<string>(global::System.StringComparer.Ordinal);")
                .AppendLine("            for (int index = 0; index < presets.Length; index++)")
                .AppendLine("            {")
                .AppendLine("                global::Avalonia.Controls.DataGridGeneratedOperationPreset preset = presets[index]")
                .AppendLine("                    ?? throw new global::System.InvalidOperationException(\"An operation preset factory returned null.\");")
                .AppendLine("                if (!names.Add(preset.Name))")
                .AppendLine("                {")
                .AppendLine("                    throw new global::System.InvalidOperationException(\"Generated operation preset names must be unique.\");")
                .AppendLine("                }")
                .AppendLine("            }")
                .AppendLine("            return presets;");
        }

        builder.AppendLine("        }");
    }

    private static string GetSchemaHash(SchemaModel schema)
    {
        var canonical = new StringBuilder(1024);
        canonical.Append("v1|")
            .Append(GeneratorUtilities.GetMetadataName(schema.ItemType)).Append('|')
            .Append(schema.SchemaId).Append('|')
            .Append(schema.StateVersion.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(schema.Strict ? '1' : '0').Append('|')
            .Append(schema.Streaming ? '1' : '0').Append('|')
            .Append(schema.HierarchicalRows ? '1' : '0').Append('|');
        canonical.Append(schema.PerformanceProfile.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(schema.DefaultPageSize.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(schema.InitialPageIndex.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(schema.InitialCurrency.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(schema.PreserveCurrentItemByKey ? '1' : '0').Append('|')
            .Append(schema.PreserveSelectionByKey ? '1' : '0').Append('|')
            .Append(schema.ConfigureMethod).Append('|')
            .Append(schema.PivotConfigureMethod).Append('|')
            .Append(schema.OutlineConfigureMethod).Append('|');

        if (schema.KeyMember != null)
        {
            canonical.Append(GetKeyName(schema)).Append(':')
                .Append((int)schema.KeyMember.Kind).Append(':')
                .Append(schema.KeyMember.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat));
        }

        canonical.Append('|');
        foreach (IMethodSymbol method in schema.OperationPresetMethods)
        {
            canonical.Append("preset:").Append(method.Name).Append(';');
        }
        canonical.Append('|');
        foreach (ColumnModel column in schema.Columns)
        {
            canonical.Append(column.ColumnKey).Append(':')
                .Append(column.Property.Name).Append(':')
                .Append(column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat)).Append(':')
                .Append(column.Kind).Append(':')
                .Append(column.IsSearchable ? '1' : '0').Append(':');
            foreach (string alias in column.PreviousColumnKeys)
            {
                canonical.Append(alias).Append(',');
            }
            foreach (KeyValuePair<string, TypedConstant> option in column.Options.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                canonical.Append(option.Key).Append('=');
                AppendCanonicalConstant(canonical, option.Value);
                canonical.Append(',');
            }
            foreach (ConditionalRuleModel rule in column.ConditionalRules)
            {
                canonical.Append("condition=")
                    .Append(rule.Condition.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(rule.RuleId).Append(':')
                    .Append(rule.Operand).Append(':')
                    .Append(rule.Operand2).Append(':')
                    .Append(rule.StringComparison.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(rule.ThemeKey).Append(':')
                    .Append(rule.Priority.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(rule.StopIfTrue ? '1' : '0').Append(':')
                    .Append(rule.PredicateMethod).Append(':')
                    .Append(rule.Target.ToString(CultureInfo.InvariantCulture)).Append(',');
            }
            if (column.Group != null)
            {
                canonical.Append("group=")
                    .Append(column.Group.Order.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(column.Group.Direction.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(column.Group.FormatterMethod).Append(',');
            }
            foreach (SummaryModel summary in column.Summaries)
            {
                canonical.Append("summary=")
                    .Append(summary.Aggregate.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(summary.Scope.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(summary.Format).Append(':')
                    .Append(summary.Title).Append(',');
            }
            foreach (BandModel band in column.Bands)
            {
                canonical.Append("band=")
                    .Append(band.Order.ToString(CultureInfo.InvariantCulture)).Append(':');
                for (int pathIndex = 0; pathIndex < band.Path.Length; pathIndex++)
                {
                    canonical.Append(band.Path[pathIndex]).Append('/');
                }
                canonical.Append(',');
            }
            foreach (AnalyticsRoleModel role in column.AnalyticsRoles)
            {
                canonical.Append("analytics=")
                    .Append(role.Role.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(role.Order.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(role.Name).Append(':')
                    .Append(role.Format).Append(':')
                    .Append(role.Aggregate.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(role.PivotDisplayMode.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(role.Formula).Append(':')
                    .Append(role.ConfigureMethod).Append(':')
                    .Append(role.CustomAggregatorFactoryMethod).Append(':');
                for (int dependencyIndex = 0; dependencyIndex < role.Dependencies.Length; dependencyIndex++)
                {
                    canonical.Append(role.Dependencies[dependencyIndex]).Append('+');
                }
                canonical.Append(',');
            }
            canonical.Append(';');
        }

        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string value = canonical.ToString();
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash ^= (byte)character;
                hash *= prime;
                hash ^= (byte)(character >> 8);
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }

    private static void EmitFieldMetadata(StringBuilder builder, ColumnModel column, string itemType)
    {
        int filterEditor = 0;
        if (column.Options.TryGetValue("FilterEditor", out TypedConstant filterEditorConstant) &&
            filterEditorConstant.Value is int configuredFilterEditor)
        {
            filterEditor = configuredFilterEditor;
        }
        bool isSensitive = column.Options.TryGetValue("IsSensitive", out TypedConstant sensitiveConstant) &&
            sensitiveConstant.Value is bool configuredSensitive && configuredSensitive;
        builder.AppendLine("new global::Avalonia.Controls.DataGridGeneratedFieldMetadata(")
            .Append("                exportFormat: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "ExportFormat"))).AppendLine(",")
            .Append("                exportNullText: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "ExportNullText"))).AppendLine(",")
            .Append("                backendFieldName: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "BackendFieldName") ?? column.ColumnKey)).AppendLine(",")
            .Append("                filterEditor: (global::Avalonia.Controls.DataGridGeneratedFilterEditorKind)").Append(filterEditor.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                filterEditorResourceKey: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "FilterEditorResourceKey"))).AppendLine(",")
            .Append("                headerResourceKey: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "HeaderResourceKey"))).AppendLine(",")
            .Append("                descriptionResourceKey: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "DescriptionResourceKey"))).AppendLine(",")
            .Append("                automationId: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "AutomationId") ?? column.ColumnKey)).AppendLine(",")
            .Append("                automationName: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "AutomationName") ?? column.Header)).AppendLine(",")
            .Append("                automationHelpText: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "AutomationHelpText"))).AppendLine(",")
            .Append("                isSensitive: ").Append(isSensitive ? "true" : "false").AppendLine(",")
            .Append("                header: ").Append(GeneratorUtilities.EscapeString(column.Header)).AppendLine(",")
            .Append("                description: ").Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "Description"))).AppendLine(",")
            .Append("                headerProvider: ").Append(GetLocalizationProviderExpression(
                itemType, column.HeaderProviderMethod, column.HeaderProviderAcceptsFormatProvider)).AppendLine(",")
            .Append("                descriptionProvider: ").Append(GetLocalizationProviderExpression(
                itemType, column.DescriptionProviderMethod, column.DescriptionProviderAcceptsFormatProvider)).Append(')');
    }

    private static string GetLocalizationProviderExpression(
        string itemType,
        string? method,
        bool acceptsFormatProvider)
    {
        if (string.IsNullOrEmpty(method))
        {
            return "null";
        }

        string escapedMethod = GeneratorUtilities.EscapeIdentifier(method!);
        return acceptsFormatProvider
            ? "static provider => " + itemType + "." + escapedMethod + "(provider)"
            : "static provider => " + itemType + "." + escapedMethod + "()";
    }

    private static string GetLocalizedTextExpression(
        string itemType,
        string? method,
        bool acceptsFormatProvider,
        string fallback)
    {
        if (string.IsNullOrEmpty(method))
        {
            return GeneratorUtilities.EscapeString(fallback);
        }

        string escapedMethod = GeneratorUtilities.EscapeIdentifier(method!);
        return acceptsFormatProvider
            ? itemType + "." + escapedMethod + "(global::System.Globalization.CultureInfo.CurrentUICulture)"
            : itemType + "." + escapedMethod + "()";
    }

    private static void AppendCanonicalConstant(StringBuilder builder, TypedConstant constant)
    {
        if (constant.Kind == TypedConstantKind.Array)
        {
            builder.Append('[');
            foreach (TypedConstant item in constant.Values)
            {
                AppendCanonicalConstant(builder, item);
                builder.Append('|');
            }
            builder.Append(']');
            return;
        }
        builder.Append(constant.Value?.ToString() ?? "null");
    }

    private static void EmitAccessorFields(StringBuilder builder, SchemaModel schema, ColumnModel column, string itemType)
    {
        IPropertySymbol property = column.Property;
        string valueType = property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string runtimeValueType = UnwrapNullable(property.Type).ToDisplayString(GeneratorUtilities.FullyQualifiedFormat);
        string fieldName = GetFieldName(property);
        bool canWrite = CanWrite(property);
        string itemAccess = GetColumnAccessExpression(column, "item");
        string nodeItemAccess = GetColumnAccessExpression(column, "((" + itemType + ")node.Item)");

        builder.Append("        private static readonly global::Avalonia.Data.Core.IPropertyInfo ")
            .Append(fieldName).AppendLine("Property =")
            .AppendLine("            new global::Avalonia.Data.Core.ClrPropertyInfo(")
            .Append("                ").Append(GeneratorUtilities.EscapeString(property.Name)).AppendLine(",")
            .Append("                static target => target is ").Append(itemType).Append(" item ? ")
            .Append(itemAccess).Append(" : default(").Append(valueType).AppendLine("),");
        if (canWrite)
        {
            builder.AppendLine("                static (target, value) =>")
                .AppendLine("                {")
                .Append("                    if (target is ").Append(itemType).AppendLine(" item)")
                .AppendLine("                    {")
                .Append("                        ").Append(itemAccess).Append(" = value is null ? default! : (")
                .Append(valueType).AppendLine(")value;")
                .AppendLine("                    }")
                .AppendLine("                },");
        }
        else
        {
            builder.AppendLine("                setter: null,");
        }

        builder.Append("                typeof(").Append(runtimeValueType).AppendLine("));")
            .AppendLine()
            .Append("        private static readonly global::Avalonia.Controls.DataGridColumnValueAccessor<")
            .Append(itemType).Append(", ").Append(valueType).Append("> ").Append(fieldName).AppendLine("Accessor =")
            .Append("            new global::Avalonia.Controls.DataGridColumnValueAccessor<")
            .Append(itemType).Append(", ").Append(valueType).AppendLine(">(")
            .Append("                static item => ").Append(itemAccess);
        if (canWrite)
        {
            builder.AppendLine(",")
                .Append("                static (item, value) => ").Append(itemAccess).AppendLine(" = value);");
        }
        else
        {
            builder.AppendLine(");");
        }

        if (schema.HierarchicalRows)
        {
            const string nodeType = "global::Avalonia.Controls.DataGridHierarchical.HierarchicalNode";
            builder.AppendLine()
                .Append("        private static readonly global::Avalonia.Data.Core.IPropertyInfo ")
                .Append(fieldName).AppendLine("HierarchicalProperty =")
                .AppendLine("            new global::Avalonia.Data.Core.ClrPropertyInfo(")
                .Append("                ").Append(GeneratorUtilities.EscapeString(property.Name)).AppendLine(",")
                .Append("                static target => target is ").Append(nodeType).Append(" node && node.Item is ")
                .Append(itemType).Append(" item ? ").Append(itemAccess).Append(" : default(")
                .Append(valueType).AppendLine("),");
            if (canWrite)
            {
                builder.AppendLine("                static (target, value) =>")
                    .AppendLine("                {")
                    .Append("                    if (target is ").Append(nodeType).Append(" node && node.Item is ")
                    .Append(itemType).AppendLine(" item)")
                    .AppendLine("                    {")
                    .Append("                        ").Append(itemAccess).Append(" = value is null ? default! : (")
                    .Append(valueType).AppendLine(")value;")
                    .AppendLine("                    }")
                    .AppendLine("                },");
            }
            else
            {
                builder.AppendLine("                setter: null,");
            }

            builder.Append("                typeof(").Append(runtimeValueType).AppendLine("));")
                .AppendLine()
                .Append("        private static readonly global::Avalonia.Controls.DataGridColumnValueAccessor<")
                .Append(nodeType).Append(", ").Append(valueType).Append("> ").Append(fieldName).AppendLine("HierarchicalAccessor =")
                .Append("            new global::Avalonia.Controls.DataGridColumnValueAccessor<")
                .Append(nodeType).Append(", ").Append(valueType).AppendLine(">(")
                .Append("                static node => node.Item is ").Append(itemType).Append(" item ? ")
                .Append(itemAccess).Append(" : default!");
            if (canWrite)
            {
                builder.AppendLine(",")
                    .Append("                static (node, value) => ").Append(nodeItemAccess).AppendLine(" = value);");
            }
            else
            {
                builder.AppendLine(");");
            }

            builder.AppendLine()
                .Append("        private static readonly global::Avalonia.Controls.DataGridBindingDefinition ")
                .Append(fieldName).AppendLine("HierarchicalBinding =")
                .Append("            global::Avalonia.Controls.DataGridBindingDefinition.CreateCached<")
                .Append(nodeType).Append(", ").Append(valueType).AppendLine(">(")
                .Append("                ").Append(fieldName).AppendLine("HierarchicalProperty,")
                .Append("                static node => node.Item is ").Append(itemType).Append(" item ? ")
                .Append(itemAccess).Append(" : default!");
            if (canWrite)
            {
                builder.AppendLine(",")
                    .Append("                static (node, value) => ").Append(nodeItemAccess).AppendLine(" = value);");
            }
            else
            {
                builder.AppendLine(");");
            }
        }

        builder.AppendLine();
    }

    private static void EmitAuxiliaryBindingFields(StringBuilder builder, ColumnModel column, string itemType)
    {
        EmitAuxiliaryBindingField(builder, column, column.ContentMember, "Content", itemType);
        EmitAuxiliaryBindingField(builder, column, column.CheckedContentMember, "CheckedContent", itemType);
        EmitAuxiliaryBindingField(builder, column, column.UncheckedContentMember, "UncheckedContent", itemType);
        EmitAuxiliaryBindingField(builder, column, column.OnContentMember, "OnContent", itemType);
        EmitAuxiliaryBindingField(builder, column, column.OffContentMember, "OffContent", itemType);
        EmitAuxiliaryBindingField(builder, column, column.CommandMember, "Command", itemType);
        EmitAuxiliaryBindingField(builder, column, column.CommandParameterMember, "CommandParameter", itemType);
    }

    private static void EmitAuxiliaryBindingField(
        StringBuilder builder,
        ColumnModel column,
        IPropertySymbol? member,
        string role,
        string itemType)
    {
        if (member == null)
        {
            return;
        }

        string valueType = member.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string runtimeValueType = UnwrapNullable(member.Type).ToDisplayString(GeneratorUtilities.FullyQualifiedFormat);
        string memberName = GeneratorUtilities.EscapeIdentifier(member.Name);
        string prefix = GetAuxiliaryBindingPrefix(column, role);
        builder.Append("        private static readonly global::Avalonia.Data.Core.IPropertyInfo ")
            .Append(prefix).AppendLine("Property =")
            .AppendLine("            new global::Avalonia.Data.Core.ClrPropertyInfo(")
            .Append("                ").Append(GeneratorUtilities.EscapeString(member.Name)).AppendLine(",")
            .Append("                static target => target is ").Append(itemType).Append(" item ? item.")
            .Append(memberName).Append(" : default(").Append(valueType).AppendLine("),")
            .AppendLine("                setter: null,")
            .Append("                typeof(").Append(runtimeValueType).AppendLine("));")
            .AppendLine()
            .Append("        private static readonly global::Avalonia.Controls.DataGridBindingDefinition ")
            .Append(prefix).AppendLine("Binding =")
            .Append("            global::Avalonia.Controls.DataGridBindingDefinition.CreateCached<")
            .Append(itemType).Append(", ").Append(valueType).AppendLine(">(")
            .Append("                ").Append(prefix).AppendLine("Property,")
            .Append("                static item => item.").Append(memberName).AppendLine(");")
            .AppendLine();
    }

    private static void EmitEditField(StringBuilder builder, SchemaModel schema, ColumnModel column, string itemType)
    {
        IPropertySymbol property = column.Property;
        if (!CanEdit(column))
        {
            return;
        }

        string valueType = property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string itemAccess = GetColumnAccessExpression(column, "item");
        builder.Append("        private static readonly global::Avalonia.Controls.DataGridGeneratedEditField<")
            .Append(itemType).Append(", ").Append(valueType).Append("> ").Append(GetEditFieldName(property)).AppendLine(" =")
            .Append("            new global::Avalonia.Controls.DataGridGeneratedEditField<")
            .Append(itemType).Append(", ").Append(valueType).AppendLine(">(")
            .Append("                ").Append(GeneratorUtilities.EscapeString(column.ColumnKey)).AppendLine(",")
            .Append("                static item => ").Append(itemAccess).AppendLine(",")
            .Append("                static (item, value) => ").Append(itemAccess).AppendLine(" = value,")
            .Append("                ");
        EmitParser(builder, schema, column);
        builder.AppendLine(",")
            .Append("                ");
        EmitFormatter(builder, schema, column);
        builder.AppendLine(",")
            .Append("                ");
        EmitValidator(builder, schema, column);
        builder.AppendLine(",")
            .Append("                ").Append(EmitOptionalMethod(schema, column.AsyncValidatorMethod)).AppendLine(",")
            .Append("                ").Append(EmitOptionalMethod(schema, column.CoerceMethod)).AppendLine(",")
            .Append("                ").Append(EmitOptionalMethod(schema, column.CanEditMethod)).AppendLine(",")
            .Append("                ").Append(GeneratorUtilities.EscapeString(property.Name)).AppendLine(");")
            .AppendLine();
    }

    private static void EmitEditFieldCollection(StringBuilder builder, SchemaModel schema, string itemType)
    {
        builder.AppendLine()
            .Append("        private static readonly global::Avalonia.Controls.IDataGridGeneratedEditField<")
            .Append(itemType).AppendLine(">[] s_editFields =")
            .Append("            new global::Avalonia.Controls.IDataGridGeneratedEditField<")
            .Append(itemType).AppendLine(">[]")
            .AppendLine("            {");
        foreach (ColumnModel column in schema.Columns)
        {
            if (CanEdit(column))
            {
                builder.Append("                ").Append(GetEditFieldName(column.Property)).AppendLine(",");
            }
        }
        builder.AppendLine("            };")
            .AppendLine()
            .Append("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedEditField<")
            .Append(itemType).AppendLine(">> EditFields { get; } = global::System.Array.AsReadOnly(s_editFields);");
    }

    private static void EmitAnalyticsMetadata(StringBuilder builder, SchemaModel schema, string itemType)
    {
        foreach (ColumnModel column in schema.Columns)
        {
            string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string suffix = GetMethodSuffix(column.Property);
            string itemAccess = GetColumnAccessExpression(column, "item");
            builder.AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedDistinctValueProvider<")
                .Append(itemType).Append(", ").Append(valueType).Append("> ").Append(suffix).AppendLine("DistinctValues { get; } =")
                .Append("            new global::Avalonia.Controls.DataGridGeneratedDistinctValueProvider<")
                .Append(itemType).Append(", ").Append(valueType).Append(">(")
                .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", static item => ")
                .Append(itemAccess).AppendLine(");")
                .AppendLine()
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedRemoteDistinctValueController<")
                .Append(valueType).Append("> Create").Append(suffix).AppendLine("RemoteDistinctValues(")
                .Append("            global::Avalonia.Controls.IDataGridGeneratedRemoteDistinctValueProvider<")
                .Append(valueType).AppendLine("> provider)")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedRemoteDistinctValueController<")
                .Append(valueType).Append(">(").Append(GeneratorUtilities.EscapeString(column.ColumnKey)).AppendLine(", provider);");
        }

        ColumnModel[] groups = schema.Columns.Where(static column => column.Group != null)
            .OrderBy(static column => column.Group!.Order).ToArray();
        builder.AppendLine()
            .Append("        private static readonly global::Avalonia.Controls.IDataGridGeneratedGroupField<")
            .Append(itemType).AppendLine(">[] s_groupFields =")
            .Append("            new global::Avalonia.Controls.IDataGridGeneratedGroupField<").Append(itemType).AppendLine(">[]")
            .AppendLine("            {");
        foreach (ColumnModel column in groups)
        {
            string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string itemAccess = GetColumnAccessExpression(column, "item");
            builder.Append("                new global::Avalonia.Controls.DataGridGeneratedGroupField<")
                .Append(itemType).Append(", ").Append(valueType).Append(">(")
                .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", ")
                .Append(column.Group!.Order.ToString(CultureInfo.InvariantCulture)).Append(", (global::System.ComponentModel.ListSortDirection)")
                .Append(column.Group.Direction.ToString(CultureInfo.InvariantCulture)).Append(", static item => ")
                .Append(itemAccess).Append(", ")
                .Append(EmitOptionalMethod(schema, column.Group.FormatterMethod)).AppendLine("),");
        }
        builder.AppendLine("            };")
            .AppendLine()
            .Append("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedGroupField<")
            .Append(itemType).AppendLine(">> GroupFields { get; } = global::System.Array.AsReadOnly(s_groupFields);")
            .AppendLine()
            .AppendLine("        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"Generated collection views install typed group and sort descriptions and do not invoke the TypeDescriptor compatibility endpoint.\")]")
            .Append("        public static global::Avalonia.Collections.DataGridCollectionView CreateCollectionView(global::System.Collections.Generic.IEnumerable<")
            .Append(itemType).AppendLine("> source,")
            .AppendLine("            int pageSize = DefaultPageSize,")
            .AppendLine("            bool sourceIsSorted = false,")
            .AppendLine("            bool sourceIsInGroupOrder = false,")
            .AppendLine("            int initialPageIndex = InitialPageIndex,")
            .AppendLine("            global::Avalonia.Controls.DataGridGeneratedInitialCurrency initialCurrency = InitialCurrency)")
            .AppendLine("        {")
            .AppendLine("            if (source is null) throw new global::System.ArgumentNullException(nameof(source));")
            .AppendLine("            if (pageSize < 0) throw new global::System.ArgumentOutOfRangeException(nameof(pageSize));")
            .AppendLine("            if (initialPageIndex < 0) throw new global::System.ArgumentOutOfRangeException(nameof(initialPageIndex));")
            .AppendLine("            if (pageSize == 0 && initialPageIndex != 0) throw new global::System.ArgumentException(\"An initial page requires a positive page size.\", nameof(initialPageIndex));")
            .AppendLine("            var view = new global::Avalonia.Collections.DataGridCollectionView(source, sourceIsSorted, sourceIsInGroupOrder);")
            .AppendLine("            using (view.DeferRefresh())")
            .AppendLine("            {")
            .AppendLine("            for (int index = 0; index < s_groupFields.Length; index++)")
            .AppendLine("            {")
            .AppendLine("                global::Avalonia.Controls.IDataGridGeneratedGroupField<" + itemType + "> field = s_groupFields[index];")
            .AppendLine("                view.GroupDescriptions.Add(field.CreateDescription());")
            .AppendLine("                if (!sourceIsSorted)")
            .AppendLine("                {")
            .AppendLine("                    view.SortDescriptions.Add(global::Avalonia.Collections.DataGridSortDescription.FromComparer(field.CreateSortComparer(), field.Direction, field.ColumnKey));")
            .AppendLine("                }")
            .AppendLine("            }")
            .AppendLine("                view.PageSize = pageSize;")
            .AppendLine("            }")
            .AppendLine("            if (pageSize > 0 && initialPageIndex != 0)")
            .AppendLine("            {")
            .AppendLine("                int pageCount = global::System.Math.Max(1, (view.ItemCount + pageSize - 1) / pageSize);")
            .AppendLine("                if (initialPageIndex >= pageCount) throw new global::System.ArgumentOutOfRangeException(nameof(initialPageIndex));")
            .AppendLine("                view.MoveToPage(initialPageIndex);")
            .AppendLine("            }")
            .AppendLine("            switch (initialCurrency)")
            .AppendLine("            {")
            .AppendLine("                case global::Avalonia.Controls.DataGridGeneratedInitialCurrency.None: view.MoveCurrentTo(null); break;")
            .AppendLine("                case global::Avalonia.Controls.DataGridGeneratedInitialCurrency.First: view.MoveCurrentToFirst(); break;")
            .AppendLine("                case global::Avalonia.Controls.DataGridGeneratedInitialCurrency.Last: view.MoveCurrentToLast(); break;")
            .AppendLine("                case global::Avalonia.Controls.DataGridGeneratedInitialCurrency.Unchanged: break;")
            .AppendLine("                default: throw new global::System.ArgumentOutOfRangeException(nameof(initialCurrency));")
            .AppendLine("            }")
            .AppendLine("            return view;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        public static void ApplyCollectionViewSorting(")
            .AppendLine("            global::Avalonia.Collections.DataGridCollectionView view,")
            .AppendLine("            global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridSorting.SortingDescriptor> descriptors)")
            .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedCollectionViewOperations.ApplySorting(view, Instance, descriptors);")
            .AppendLine();

        if (schema.KeyMember != null)
        {
            string keyType = schema.KeyMember.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.AppendLine("        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"Generated collection views install typed group and sort descriptions and do not invoke the TypeDescriptor compatibility endpoint.\")]")
                .Append("        public static global::Avalonia.Controls.DataGridGeneratedCollectionViewController<")
                .Append(itemType).Append(", ").Append(keyType).AppendLine("> CreateCollectionViewController(")
                .Append("            global::System.Collections.Generic.IEnumerable<").Append(itemType).AppendLine("> source,")
                .AppendLine("            int pageSize = DefaultPageSize,")
                .AppendLine("            bool sourceIsSorted = false,")
                .AppendLine("            bool sourceIsInGroupOrder = false,")
                .AppendLine("            int initialPageIndex = InitialPageIndex,")
                .AppendLine("            global::Avalonia.Controls.DataGridGeneratedInitialCurrency initialCurrency = InitialCurrency,")
                .AppendLine("            global::Avalonia.Controls.DataGridGeneratedSelectionProfile? selectionProfile = null)")
                .AppendLine("        {")
                .AppendLine("            selectionProfile ??= new global::Avalonia.Controls.DataGridGeneratedSelectionProfile { PreserveUnloadedKeys = PreserveSelectionByKey };")
                .AppendLine("            return new global::Avalonia.Controls.DataGridGeneratedCollectionViewController<" + itemType + ", " + keyType + ">(")
                .AppendLine("                CreateCollectionView(source, pageSize, sourceIsSorted, sourceIsInGroupOrder, initialPageIndex, initialCurrency),")
                .AppendLine("                Instance,")
                .AppendLine("                selectionProfile,")
                .AppendLine("                PreserveCurrentItemByKey,")
                .AppendLine("                KeyComparer);")
                .AppendLine("        }")
                .AppendLine();
        }

        EmitCollectionMutationFactories(builder, schema, itemType);

        builder
            .Append("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedSummary<")
            .Append(itemType).AppendLine(">> CreateSummaries()")
            .AppendLine("        {")
            .Append("            return new global::Avalonia.Controls.IDataGridGeneratedSummary<").Append(itemType).AppendLine(">[]")
            .AppendLine("            {");
        foreach (ColumnModel column in schema.Columns)
        {
            foreach (SummaryModel summary in column.Summaries)
            {
                EmitSummary(builder, column, summary, itemType);
            }
        }
        builder.AppendLine("            };")
            .AppendLine("        }")
            .AppendLine();

        builder.AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedConditionalRule> ConditionalRules { get; } =")
            .AppendLine("            global::System.Array.AsReadOnly(new global::Avalonia.Controls.IDataGridGeneratedConditionalRule[]")
            .AppendLine("            {");
        foreach (ColumnModel column in schema.Columns)
        {
            foreach (ConditionalRuleModel rule in column.ConditionalRules)
            {
                EmitConditionalRule(builder, schema, column, rule, itemType);
            }
        }
        builder.AppendLine("            });")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridConditionalFormatting.IConditionalFormattingModel CreateConditionalFormattingModel()")
            .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedConditionalFormatting.CreateModel(ConditionalRules);")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedBandField> BandFields { get; } =")
            .AppendLine("            global::System.Array.AsReadOnly(new global::Avalonia.Controls.DataGridGeneratedBandField[]")
            .AppendLine("            {");
        foreach (ColumnModel column in schema.Columns)
        {
            foreach (BandModel band in column.Bands.OrderBy(static band => band.Order))
            {
                builder.Append("                new global::Avalonia.Controls.DataGridGeneratedBandField(")
                    .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", new string[] { ");
                for (int index = 0; index < band.Path.Length; index++)
                {
                    if (index != 0) builder.Append(", ");
                    builder.Append(GeneratorUtilities.EscapeString(band.Path[index]));
                }
                builder.Append(" }, ").Append(band.Order.ToString(CultureInfo.InvariantCulture)).AppendLine("),");
            }
        }
        builder.AppendLine("            });")
            .AppendLine()
            .AppendLine("        private static readonly global::Avalonia.Controls.IDataGridGeneratedAnalyticsField[] s_analyticsFields =")
            .AppendLine("            new global::Avalonia.Controls.IDataGridGeneratedAnalyticsField[]")
            .AppendLine("            {");
        foreach (ColumnModel column in schema.Columns)
        {
            foreach (AnalyticsRoleModel role in column.AnalyticsRoles.OrderBy(static role => role.Order))
            {
                EmitAnalyticsRole(builder, column, role, itemType);
            }
        }
        builder.AppendLine("            };")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedAnalyticsField> AnalyticsFields { get; } =")
            .AppendLine("            global::System.Array.AsReadOnly(s_analyticsFields);")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridPivoting.PivotAxisField> CreatePivotAxisFields(")
            .AppendLine("            global::Avalonia.Controls.DataGridGeneratedAnalyticsRole roles = global::Avalonia.Controls.DataGridGeneratedAnalyticsRole.PivotRow | global::Avalonia.Controls.DataGridGeneratedAnalyticsRole.PivotColumn | global::Avalonia.Controls.DataGridGeneratedAnalyticsRole.PivotFilter)")
            .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedPivotAdapter.CreateAxisFields(AnalyticsFields, roles);")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridPivoting.PivotValueField> CreatePivotValueFields()")
            .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedPivotAdapter.CreateValueFields(AnalyticsFields);")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridPivoting.PivotTableModel CreatePivotTableModel(")
            .AppendLine("            global::System.Collections.IEnumerable items,")
            .AppendLine("            global::System.Action<global::Avalonia.Controls.DataGridPivoting.PivotTableModel>? configure = null)")
            .AppendLine("        {")
            .AppendLine("            return global::Avalonia.Controls.DataGridGeneratedPivotAdapter.CreateModel(items, AnalyticsFields, model =>")
            .AppendLine("            {");
        if (!string.IsNullOrEmpty(schema.PivotConfigureMethod))
        {
            builder.Append("                ").Append(itemType).Append('.').Append(GeneratorUtilities.EscapeIdentifier(schema.PivotConfigureMethod!)).AppendLine("(model);");
        }
        builder.AppendLine("                configure?.Invoke(model);")
            .AppendLine("            });")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridReporting.OutlineGroupField> CreateOutlineGroupFields()")
            .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedOutlineAdapter.CreateGroupFields(AnalyticsFields);")
            .AppendLine()
            .AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridReporting.OutlineValueField> CreateOutlineValueFields()")
            .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedOutlineAdapter.CreateValueFields(AnalyticsFields);")
            .AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridReporting.OutlineReportModel CreateOutlineReportModel(")
            .AppendLine("            global::System.Collections.IEnumerable items,")
            .AppendLine("            global::System.Action<global::Avalonia.Controls.DataGridReporting.OutlineReportModel>? configure = null)")
            .AppendLine("        {")
            .AppendLine("            return global::Avalonia.Controls.DataGridGeneratedOutlineAdapter.CreateModel(items, AnalyticsFields, model =>")
            .AppendLine("            {");
        if (!string.IsNullOrEmpty(schema.OutlineConfigureMethod))
        {
            builder.Append("                ").Append(itemType).Append('.').Append(GeneratorUtilities.EscapeIdentifier(schema.OutlineConfigureMethod!)).AppendLine("(model);");
        }
        builder.AppendLine("                configure?.Invoke(model);")
            .AppendLine("            });")
            .AppendLine("        }");
    }

    private static void EmitCollectionMutationFactories(StringBuilder builder, SchemaModel schema, string itemType)
    {
        builder.Append("        public static global::Avalonia.Controls.DataGridGeneratedCollectionMutationService<")
            .Append(itemType).AppendLine("> CreateCollectionMutationService(")
            .Append("            global::Avalonia.Controls.IDataGridGeneratedCollectionMutationHandler<")
            .Append(itemType).AppendLine("> handler,")
            .AppendLine("            int maximumItemsPerMutation = 65536)")
            .Append("            => new global::Avalonia.Controls.DataGridGeneratedCollectionMutationService<")
            .Append(itemType).AppendLine(">(handler, maximumItemsPerMutation);")
            .AppendLine()
            .Append("        public static global::Avalonia.Controls.DataGridGeneratedNewRowService<")
            .Append(itemType).AppendLine("> CreateNewRowService(")
            .Append("            global::Avalonia.Controls.IDataGridGeneratedNewRowFactory<")
            .Append(itemType).AppendLine("> factory)")
            .Append("            => new global::Avalonia.Controls.DataGridGeneratedNewRowService<")
            .Append(itemType).AppendLine(">(factory);")
            .AppendLine();

        if (schema.MutationHandlerType != null)
        {
            string handlerType = schema.MutationHandlerType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append("        public static global::Avalonia.Controls.DataGridGeneratedCollectionMutationService<")
                .Append(itemType).AppendLine("> CreateConfiguredCollectionMutationService(")
                .AppendLine("            int maximumItemsPerMutation = 65536)")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedCollectionMutationService<")
                .Append(itemType).Append(">(new ").Append(handlerType).AppendLine("(), maximumItemsPerMutation);")
                .AppendLine();
        }

        if (schema.NewRowFactoryType != null)
        {
            string factoryType = schema.NewRowFactoryType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append("        public static global::Avalonia.Controls.DataGridGeneratedNewRowService<")
                .Append(itemType).AppendLine("> CreateConfiguredNewRowService()")
                .Append("            => new global::Avalonia.Controls.DataGridGeneratedNewRowService<")
                .Append(itemType).Append(">(new ").Append(factoryType).AppendLine("());")
                .AppendLine();
        }
    }

    private static void EmitAnalyticsRole(StringBuilder builder, ColumnModel column, AnalyticsRoleModel role, string itemType)
    {
        string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        bool hasNumericSelector = IsNumericType(UnwrapNullable(column.Property.Type));
        string itemAccess = GetColumnAccessExpression(column, "item");
        string typedAccess = GetColumnAccessExpression(column, "typed");
        builder.Append("                new global::Avalonia.Controls.DataGridGeneratedAnalyticsField<")
            .Append(itemType).Append(", ").Append(valueType).Append(">(")
            .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", (global::Avalonia.Controls.DataGridGeneratedAnalyticsRole)")
            .Append(role.Role.ToString(CultureInfo.InvariantCulture)).Append(", ")
            .Append(role.Order.ToString(CultureInfo.InvariantCulture)).Append(", static item => ")
            .Append(itemAccess).Append(", ");
        if (hasNumericSelector)
        {
            builder.Append("static item => item is ").Append(itemType).Append(" typed ? (double?)")
                .Append(typedAccess).Append(" : null");
        }
        else
        {
            builder.Append("(global::System.Func<object, double?>?)null");
        }
        if (HasAdvancedAnalyticsOptions(role))
        {
            builder.Append(", new global::Avalonia.Controls.DataGridGeneratedAdvancedAnalyticsOptions { ");
            AppendAdvancedAnalyticsOptions(builder, role, itemType);
            builder.Append(" }, ");
        }
        else
        {
            builder.Append(", ");
        }
        builder
            .Append(GeneratorUtilities.EscapeString(role.Name)).Append(", ")
            .Append(GeneratorUtilities.EscapeString(role.Format)).Append(", ")
            .Append(role.Aggregate.ToString(CultureInfo.InvariantCulture)).Append(", (global::Avalonia.Controls.DataGridPivoting.PivotValueDisplayMode)")
            .Append(role.PivotDisplayMode.ToString(CultureInfo.InvariantCulture)).Append(", new string[] { ");
        for (int index = 0; index < role.Dependencies.Length; index++)
        {
            if (index != 0) builder.Append(", ");
            builder.Append(GeneratorUtilities.EscapeString(role.Dependencies[index]));
        }
        builder.AppendLine(" }),");
    }

    private static bool HasAdvancedAnalyticsOptions(AnalyticsRoleModel role) =>
        !string.IsNullOrEmpty(role.Formula) ||
        !string.IsNullOrEmpty(role.CustomAggregatorFactoryMethod) ||
        !string.IsNullOrEmpty(role.ConfigureMethod);

    private static void AppendAdvancedAnalyticsOptions(
        StringBuilder builder,
        AnalyticsRoleModel role,
        string itemType)
    {
        bool needsSeparator = false;
        if (!string.IsNullOrEmpty(role.Formula))
        {
            builder.Append("Formula = ").Append(GeneratorUtilities.EscapeString(role.Formula));
            needsSeparator = true;
        }
        if (!string.IsNullOrEmpty(role.CustomAggregatorFactoryMethod))
        {
            if (needsSeparator) builder.Append(", ");
            builder.Append("CustomAggregatorFactory = ");
            AppendAnalyticsFactoryDelegate(builder, role.CustomAggregatorFactoryMethod, itemType);
            needsSeparator = true;
        }
        if (string.IsNullOrEmpty(role.ConfigureMethod))
        {
            return;
        }
        if (needsSeparator) builder.Append(", ");
        if (role.Role is 1 or 2 or 4)
        {
            builder.Append("ConfigurePivotAxis = ");
        }
        else if (role.Role == 8)
        {
            builder.Append("ConfigurePivotValue = ");
        }
        else if (role.Role == 512)
        {
            builder.Append("ConfigureOutlineGroup = ");
        }
        else
        {
            builder.Append("ConfigureOutlineValue = ");
        }
        AppendAnalyticsConfigureDelegate(builder, true, role.ConfigureMethod, itemType);
    }

    private static void AppendAnalyticsFactoryDelegate(StringBuilder builder, string? methodName, string itemType)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            builder.Append("null");
            return;
        }
        builder.Append("static () => ").Append(itemType).Append('.')
            .Append(GeneratorUtilities.EscapeIdentifier(methodName!)).Append("()");
    }

    private static void AppendAnalyticsConfigureDelegate(
        StringBuilder builder,
        bool applies,
        string? methodName,
        string itemType)
    {
        if (!applies || string.IsNullOrEmpty(methodName))
        {
            builder.Append("null");
            return;
        }
        builder.Append("static field => ").Append(itemType).Append('.')
            .Append(GeneratorUtilities.EscapeIdentifier(methodName!)).Append("(field)");
    }

    private static void EmitDiagnosticsManifest(StringBuilder builder, SchemaModel schema, string itemType)
    {
        builder.AppendLine()
            .AppendLine("        public static global::Avalonia.Controls.DataGridGeneratedDiagnosticsManifest Diagnostics { get; } =")
            .AppendLine("            new global::Avalonia.Controls.DataGridGeneratedDiagnosticsManifest(")
            .AppendLine("                SchemaId,")
            .AppendLine("                SchemaHash,")
            .Append("                typeof(").Append(itemType).AppendLine("),")
            .Append("                ").Append(schema.Strict ? "true" : "false").AppendLine(",")
            .Append("                ").Append(schema.Streaming ? "true" : "false").AppendLine(",")
            .Append("                (global::Avalonia.Controls.DataGridGeneratedPerformanceProfile)").Append(schema.PerformanceProfile.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                ").Append(schema.KeyMember == null ? "false" : "true").AppendLine(",")
            .AppendLine("                new global::Avalonia.Controls.DataGridGeneratedDiagnosticField[]")
            .AppendLine("                {");
        foreach (ColumnModel column in schema.Columns)
        {
            int filterEditor = 0;
            if (column.Options.TryGetValue("FilterEditor", out TypedConstant editorConstant) && editorConstant.Value is int configuredEditor)
            {
                filterEditor = configuredEditor;
            }
            int analyticsRoles = 0;
            foreach (AnalyticsRoleModel role in column.AnalyticsRoles) analyticsRoles |= role.Role;
            builder.Append("                    new global::Avalonia.Controls.DataGridGeneratedDiagnosticField(")
                .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", typeof(")
                .Append(column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedFormat)).Append("), ")
                .Append(CanEdit(column) ? "true" : "false").Append(", ")
                .Append(column.IsSearchable ? "true" : "false").Append(", (global::Avalonia.Controls.DataGridGeneratedFilterEditorKind)")
                .Append(filterEditor.ToString(CultureInfo.InvariantCulture)).Append(", (global::Avalonia.Controls.DataGridGeneratedAnalyticsRole)")
                .Append(analyticsRoles.ToString(CultureInfo.InvariantCulture)).AppendLine("),");
        }
        builder.AppendLine("                },")
            .AppendLine("                new string[]")
            .AppendLine("                {");
        if (!schema.Strict)
        {
            builder.AppendLine("                    \"RuntimeCompatibility\",");
        }
        builder.AppendLine("                },")
            .AppendLine("                new string[]")
            .AppendLine("                {")
            .AppendLine("                    \"prodatagrid.rows.realized.count\",")
            .AppendLine("                    \"prodatagrid.rows.recycled.count\",")
            .AppendLine("                    \"prodatagrid.rows.display.update.time\",")
            .AppendLine("                    \"generated.search.index.items\",")
            .AppendLine("                    \"generated.pipeline.revision\",");
        if (schema.Streaming)
        {
            builder.AppendLine("                    \"generated.stream.queued\",");
        }
        if (schema.HierarchicalRows)
        {
            builder.AppendLine("                    \"generated.hierarchy.flattened.items\",");
        }
        builder.AppendLine("                });");
    }

    private static void EmitSummary(StringBuilder builder, ColumnModel column, SummaryModel summary, string itemType)
    {
        string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        ITypeSymbol effectiveType = UnwrapNullable(column.Property.Type);
        bool numeric = IsNumeric(effectiveType) && SymbolEqualityComparer.Default.Equals(effectiveType, column.Property.Type);
        string itemAccess = GetColumnAccessExpression(column, "item");
        builder.Append("                new global::Avalonia.Controls.DataGridGeneratedSummary<")
            .Append(itemType).Append(", ").Append(valueType).Append(">(")
            .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", (global::Avalonia.Controls.DataGridAggregateType)")
            .Append(summary.Aggregate.ToString(CultureInfo.InvariantCulture)).Append(", (global::Avalonia.Controls.DataGridSummaryScope)")
            .Append(summary.Scope.ToString(CultureInfo.InvariantCulture)).Append(", static item => ")
            .Append(itemAccess);
        if (numeric)
        {
            builder.Append(", default, static (left, right) => left + right, static (left, right) => left - right, ");
            if (effectiveType.SpecialType == SpecialType.System_Decimal)
            {
                builder.Append("static (sum, count) => count == 0 ? null! : sum / count");
            }
            else
            {
                builder.Append("static (sum, count) => count == 0 ? null! : (object)((double)sum / count)");
            }
        }
        builder.AppendLine("),");
    }

    private static void EmitConditionalRule(
        StringBuilder builder,
        SchemaModel schema,
        ColumnModel column,
        ConditionalRuleModel rule,
        string itemType)
    {
        string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string itemAccess = GetColumnAccessExpression(column, "item");
        builder.Append("                new global::Avalonia.Controls.DataGridGeneratedConditionalRule<")
            .Append(itemType).Append(", ").Append(valueType).Append(">(")
            .Append(GeneratorUtilities.EscapeString(rule.RuleId)).Append(", ")
            .Append(GeneratorUtilities.EscapeString(column.ColumnKey)).Append(", static item => ")
            .Append(itemAccess).Append(", ");
        if (!string.IsNullOrEmpty(rule.PredicateMethod))
        {
            builder.Append(EmitOptionalMethod(schema, rule.PredicateMethod));
        }
        else
        {
            EmitConditionalPredicate(builder, column.Property.Type, rule);
        }
        builder.Append(", ").Append(GeneratorUtilities.EscapeString(rule.ThemeKey)).Append(", ")
            .Append(rule.Priority.ToString(CultureInfo.InvariantCulture)).Append(", ")
            .Append(rule.StopIfTrue ? "true" : "false").Append(", (global::Avalonia.Controls.DataGridConditionalFormatting.ConditionalFormattingTarget)")
            .Append(rule.Target.ToString(CultureInfo.InvariantCulture)).AppendLine("),");
    }

    private static void EmitConditionalPredicate(StringBuilder builder, ITypeSymbol type, ConditionalRuleModel rule)
    {
        string valueType = type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        if (rule.Condition == 6)
        {
            builder.Append("static (item, value) => ((global::System.Object?)value) is null");
            return;
        }
        if (rule.Condition == 7)
        {
            builder.Append("static (item, value) => ((global::System.Object?)value) is not null");
            return;
        }
        if (rule.Condition is 10 or 11 or 12)
        {
            string method = rule.Condition switch
            {
                10 => "Contains",
                11 => "StartsWith",
                _ => "EndsWith"
            };
            builder.Append("static (item, value) => value is not null && value.")
                .Append(method).Append('(').Append(GeneratorUtilities.EscapeString(rule.Operand))
                .Append(", (global::System.StringComparison)")
                .Append(rule.StringComparison.ToString(CultureInfo.InvariantCulture)).Append(')');
            return;
        }
        if (!TryEmitOperand(type, rule.Operand, out string operand))
        {
            builder.Append("static (item, value) => false");
            return;
        }
        if (rule.Condition == 9)
        {
            TryEmitOperand(type, rule.Operand2, out string operand2);
            builder.Append("static (item, value) => global::System.Collections.Generic.Comparer<")
                .Append(valueType).Append(">.Default.Compare(value, ").Append(operand)
                .Append(") >= 0 && global::System.Collections.Generic.Comparer<")
                .Append(valueType).Append(">.Default.Compare(value, ").Append(operand2).Append(") <= 0");
            return;
        }
        string comparison = rule.Condition switch
        {
            0 => "== 0",
            1 => "!= 0",
            2 => "> 0",
            3 => ">= 0",
            4 => "< 0",
            5 => "<= 0",
            _ => "== 0"
        };
        builder.Append("static (item, value) => global::System.Collections.Generic.Comparer<")
            .Append(valueType).Append(">.Default.Compare(value, ").Append(operand).Append(") ").Append(comparison);
    }

    private static bool TryEmitOperand(ITypeSymbol type, string? text, out string expression)
    {
        ITypeSymbol effective = UnwrapNullable(type);
        if (effective.SpecialType == SpecialType.System_String)
        {
            expression = GeneratorUtilities.EscapeString(text);
            return true;
        }
        if (effective.SpecialType == SpecialType.System_Boolean && bool.TryParse(text, out bool boolean))
        {
            expression = boolean ? "true" : "false";
            return true;
        }
        if (IsNumeric(effective) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
        {
            string valueType = type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            expression = "(" + valueType + ")" + number.ToString(CultureInfo.InvariantCulture) + "m";
            return true;
        }
        expression = "default";
        return false;
    }

    private static bool IsNumeric(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or
            SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or
            SpecialType.System_Double or SpecialType.System_Decimal;

    private static void EmitParser(StringBuilder builder, SchemaModel schema, ColumnModel column)
    {
        if (!string.IsNullOrEmpty(column.ParserMethod))
        {
            builder.Append(schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat)).Append('.')
                .Append(GeneratorUtilities.EscapeIdentifier(column.ParserMethod!));
            return;
        }

        ITypeSymbol propertyType = column.Property.Type;
        ITypeSymbol effectiveType = UnwrapNullable(propertyType);
        string valueType = propertyType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string effectiveTypeName = effectiveType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        bool nullableValue = !SymbolEqualityComparer.Default.Equals(propertyType, effectiveType);
        if (effectiveType.SpecialType == SpecialType.System_String)
        {
            builder.Append("static (global::System.ReadOnlySpan<char> text, global::System.IFormatProvider provider, out ")
                .Append(valueType).Append(" value) => { value = text.ToString(); return true; }");
            return;
        }

        bool supported = effectiveType.TypeKind == TypeKind.Enum ||
            effectiveType.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or
                SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or
                SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
                SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or
                SpecialType.System_Double or SpecialType.System_Decimal ||
            IsBuiltInParsable(effectiveType);
        if (!supported)
        {
            builder.Append("null");
            return;
        }

        builder.Append("static (global::System.ReadOnlySpan<char> text, global::System.IFormatProvider provider, out ")
            .Append(valueType).Append(" value) => { ");
        if (nullableValue)
        {
            builder.Append("if (text.IsEmpty) { value = null; return true; } ");
        }
        builder.Append(effectiveTypeName).Append(" parsed = default; bool success = ");
        if (effectiveType.TypeKind == TypeKind.Enum)
        {
            builder.Append("global::System.Enum.TryParse<").Append(effectiveTypeName).Append(">(text, true, out parsed)");
        }
        else if (effectiveType.SpecialType == SpecialType.System_Char)
        {
            builder.Append("text.Length == 1 && (parsed = text[0]) == text[0]");
        }
        else if (effectiveType.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append("bool.TryParse(text, out parsed)");
        }
        else
        {
            builder.Append(effectiveTypeName).Append(".TryParse(text, provider, out parsed)");
        }
        builder.Append("; value = success ? parsed : default; return success; }");
    }

    private static void EmitFormatter(StringBuilder builder, SchemaModel schema, ColumnModel column)
    {
        if (!string.IsNullOrEmpty(column.FormatterMethod))
        {
            builder.Append(schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat)).Append('.')
                .Append(GeneratorUtilities.EscapeIdentifier(column.FormatterMethod!));
            return;
        }

        string? format = GetStringOption(column.Options, "FormatString");
        builder.Append("static (value, provider) => ((global::System.Object?)value) is global::System.IFormattable formattable ? formattable.ToString(")
            .Append(GeneratorUtilities.EscapeString(format))
            .Append(", provider) ?? global::System.String.Empty : ((global::System.Object?)value) is null ? global::System.String.Empty : value.ToString() ?? global::System.String.Empty");
    }

    private static void EmitValidator(StringBuilder builder, SchemaModel schema, ColumnModel column)
    {
        if (!string.IsNullOrEmpty(column.ValidatorMethod))
        {
            builder.Append(EmitOptionalMethod(schema, column.ValidatorMethod));
            return;
        }

        AttributeData? required = FindColumnAttribute(column, "System.ComponentModel.DataAnnotations.RequiredAttribute");
        AttributeData? stringLength = FindColumnAttribute(column, "System.ComponentModel.DataAnnotations.StringLengthAttribute");
        AttributeData? minLength = FindColumnAttribute(column, "System.ComponentModel.DataAnnotations.MinLengthAttribute");
        AttributeData? maxLength = FindColumnAttribute(column, "System.ComponentModel.DataAnnotations.MaxLengthAttribute");
        AttributeData? range = FindColumnAttribute(column, "System.ComponentModel.DataAnnotations.RangeAttribute");
        bool isString = UnwrapNullable(column.Property.Type).SpecialType == SpecialType.System_String;
        string? minimumRange = null;
        string? maximumRange = null;
        bool hasNumericRange = range != null &&
            IsNumericType(UnwrapNullable(column.Property.Type)) &&
            TryGetRangeBounds(range, out minimumRange, out maximumRange);
        if (required == null &&
            (!isString || stringLength == null && minLength == null && maxLength == null) &&
            !hasNumericRange)
        {
            builder.Append("null");
            return;
        }

        builder.Append("static (item, value) => ");
        bool emitted = false;
        if (required != null)
        {
            builder.Append(isString
                ? "global::System.String.IsNullOrWhiteSpace(value) ? \"A value is required.\""
                : "((global::System.Object?)value) is null ? \"A value is required.\"");
            emitted = true;
        }
        if (isString && stringLength != null)
        {
            int maximum = GetAttributeInt32(stringLength, 0, int.MaxValue);
            int minimum = GetNamedAttributeInt32(stringLength, "MinimumLength", 0);
            if (emitted) builder.Append(" : ");
            builder.Append("value is not null && value.Length > ").Append(maximum.ToString(CultureInfo.InvariantCulture))
                .Append(" ? \"The value is too long.\"");
            if (minimum > 0)
            {
                builder.Append(" : value is not null && value.Length < ").Append(minimum.ToString(CultureInfo.InvariantCulture))
                    .Append(" ? \"The value is too short.\"");
            }
            emitted = true;
        }
        if (isString && minLength != null)
        {
            if (emitted) builder.Append(" : ");
            builder.Append("value is not null && value.Length < ").Append(GetAttributeInt32(minLength, 0, 0).ToString(CultureInfo.InvariantCulture))
                .Append(" ? \"The value is too short.\"");
            emitted = true;
        }
        if (isString && maxLength != null)
        {
            if (emitted) builder.Append(" : ");
            builder.Append("value is not null && value.Length > ").Append(GetAttributeInt32(maxLength, 0, int.MaxValue).ToString(CultureInfo.InvariantCulture))
                .Append(" ? \"The value is too long.\"");
            emitted = true;
        }
        if (hasNumericRange)
        {
            string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            if (emitted) builder.Append(" : ");
            builder.Append("value < (").Append(valueType).Append(')').Append(minimumRange)
                .Append(" ? \"The value is below the allowed range.\" : value > (").Append(valueType).Append(')')
                .Append(maximumRange).Append(" ? \"The value is above the allowed range.\"");
        }
        builder.Append(" : null");
    }

    private static AttributeData? FindAttribute(ISymbol symbol, string metadataName)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass != null &&
                string.Equals(GeneratorUtilities.GetMetadataName(attribute.AttributeClass), metadataName, StringComparison.Ordinal))
            {
                return attribute;
            }
        }
        return null;
    }

    private static AttributeData? FindColumnAttribute(ColumnModel column, string metadataName)
    {
        return FindAttribute(column.ConfigurationProperty, metadataName) ??
               FindAttribute(column.Property, metadataName);
    }

    private static int GetAttributeInt32(AttributeData attribute, int index, int fallback) =>
        attribute.ConstructorArguments.Length > index && attribute.ConstructorArguments[index].Value is int value ? value : fallback;

    private static int GetNamedAttributeInt32(AttributeData attribute, string name, int fallback)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is int value) return value;
        }
        return fallback;
    }

    private static bool TryGetRangeBounds(AttributeData attribute, out string? minimum, out string? maximum)
    {
        minimum = null;
        maximum = null;
        if (attribute.ConstructorArguments.Length != 2)
        {
            return false;
        }

        minimum = FormatNumericAttributeConstant(attribute.ConstructorArguments[0]);
        maximum = FormatNumericAttributeConstant(attribute.ConstructorArguments[1]);
        return minimum != null && maximum != null;
    }

    private static string? FormatNumericAttributeConstant(TypedConstant constant)
    {
        return constant.Value switch
        {
            int value => value.ToString(CultureInfo.InvariantCulture),
            double value => GeneratorUtilities.FormatDouble(value),
            _ => null
        };
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
            SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
    }

    private static string EmitOptionalMethod(SchemaModel schema, string? method) =>
        string.IsNullOrEmpty(method)
            ? "null"
            : schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat) + "." + GeneratorUtilities.EscapeIdentifier(method!);

    private static bool IsBuiltInParsable(ITypeSymbol type)
    {
        string metadataName = type is INamedTypeSymbol named
            ? GeneratorUtilities.GetMetadataName(named)
            : type.ToDisplayString();
        return metadataName is "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or
            "System.TimeOnly" or "System.TimeSpan" or "System.Guid";
    }

    private static void EmitColumnFactory(StringBuilder builder, SchemaModel schema, ColumnModel column, string itemType)
    {
        string definitionTypeName = GetDefinitionTypeName(column.Kind);
        string definitionType = "global::Avalonia.Controls." + definitionTypeName;
        string valueType = column.Property.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string runtimeValueType = UnwrapNullable(column.Property.Type).ToDisplayString(GeneratorUtilities.FullyQualifiedFormat);
        string fieldName = GetFieldName(column.Property);
        string methodSuffix = GetMethodSuffix(column.Property);
        bool canWrite = CanWrite(column.Property);

        builder.Append("        private static global::Avalonia.Controls.DataGridColumnDefinition Create")
            .Append(methodSuffix).Append("Column(global::Avalonia.Controls.DataGridColumnDefinitionBuilder<")
            .Append(itemType).AppendLine("> builder)")
            .AppendLine("        {")
            .Append("            ").Append(definitionType).Append(" column = ");

        if (!string.IsNullOrEmpty(column.FactoryMethod))
        {
            builder.Append('(').Append(definitionType).Append(')').Append(itemType).Append('.')
                .Append(GeneratorUtilities.EscapeIdentifier(column.FactoryMethod!)).AppendLine("();");
        }
        else
        {
            EmitBuilderCall(builder, column, itemType, valueType, fieldName, canWrite);
        }

        builder.Append("            column.ColumnKey = ").Append(GeneratorUtilities.EscapeString(column.ColumnKey)).AppendLine(";")
            .Append("            column.SortMemberPath = ")
            .Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "SortMemberPath") ?? column.Property.Name))
            .AppendLine(";")
            .Append("            column.ValueAccessor = ").Append(fieldName)
            .Append(schema.HierarchicalRows ? "HierarchicalAccessor" : "Accessor").AppendLine(";")
            .Append("            column.ValueType = typeof(").Append(runtimeValueType).AppendLine(");");

        if (schema.HierarchicalRows && column.Kind != "Template" && column.Kind != "Button" && column.Kind != "Formula")
        {
            builder.Append("            column.Binding = ").Append(fieldName).AppendLine("HierarchicalBinding;");
            builder.Append("            column.SortMemberPath = ")
                .Append(GeneratorUtilities.EscapeString("Item." + (GetStringOption(column.Options, "SortMemberPath") ?? column.Property.Name)))
                .AppendLine(";");
        }

        EmitCommonOptions(builder, column);
        EmitSummaryDefinitions(builder, column);
        EmitKindOptions(builder, column, itemType);
        EmitGeneratedTemplates(builder, column, itemType);

        if (!string.IsNullOrEmpty(column.ConfigureMethod))
        {
            builder.Append("            ").Append(itemType).Append('.').Append(GeneratorUtilities.EscapeIdentifier(column.ConfigureMethod!))
                .AppendLine("(column);");
        }

        builder.AppendLine("            return column;")
            .AppendLine("        }")
            .AppendLine();
    }

    private static void EmitBuilderCall(
        StringBuilder builder,
        ColumnModel column,
        string itemType,
        string valueType,
        string fieldName,
        bool canWrite)
    {
        string header = GetLocalizedTextExpression(
            itemType,
            column.HeaderProviderMethod,
            column.HeaderProviderAcceptsFormatProvider,
            column.Header);
        string itemAccess = GetColumnAccessExpression(column, "item");
        switch (column.Kind)
        {
            case "Template":
                builder.Append("builder.Template(").Append(header).Append(", ")
                    .Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "TemplateKey") ?? string.Empty))
                    .AppendLine(");");
                return;
            case "Button":
                builder.Append("builder.Button(").Append(header).Append(", ")
                    .Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "Content")))
                    .AppendLine(");");
                return;
            case "Formula":
                builder.Append("builder.Formula(").Append(header).Append(", ")
                    .Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "Formula") ?? string.Empty)).Append(", ")
                    .Append(GeneratorUtilities.EscapeString(GetStringOption(column.Options, "FormulaName")))
                    .AppendLine(");");
                return;
        }

        string builderMethod = column.Kind;
        builder.Append("builder.").Append(builderMethod).Append('<').Append(valueType).AppendLine(">(")
            .Append("                ").Append(header).AppendLine(",")
            .Append("                ").Append(fieldName).AppendLine("Property,")
            .Append("                static item => ").Append(itemAccess).AppendLine(",");
        if (canWrite)
        {
            builder.Append("                static (item, value) => ").Append(itemAccess).AppendLine(" = value);");
        }
        else
        {
            builder.AppendLine("                setter: null);");
        }
    }

    private static void EmitCommonOptions(StringBuilder builder, ColumnModel column)
    {
        EmitStringAssignment(builder, column.Options, "HeaderTemplateKey");
        EmitStringAssignment(builder, column.Options, "HeaderThemeKey");
        EmitStringAssignment(builder, column.Options, "CellThemeKey");
        EmitStringAssignment(builder, column.Options, "SummaryCellThemeKey");
        EmitStringAssignment(builder, column.Options, "FilterThemeKey");
        EmitStringAssignment(builder, column.Options, "FilterFlyoutKey");
        EmitStringAssignment(builder, column.Options, "WidthSharingGroup");
        EmitEnumAssignment(builder, column.Options, "DisplayMode", "global::Avalonia.Controls.DataGridColumnDisplayMode");
        EmitDoubleAssignment(builder, column.Options, "MinWidth");
        EmitDoubleAssignment(builder, column.Options, "MaxWidth");
        EmitBooleanAssignment(builder, column.Options, "CanUserSort");
        EmitBooleanAssignment(builder, column.Options, "CanUserHide");
        EmitBooleanAssignment(builder, column.Options, "CanUserResize");
        EmitBooleanAssignment(builder, column.Options, "CanUserReorder");
        EmitBooleanAssignment(builder, column.Options, "IsReadOnly");
        EmitBooleanAssignment(builder, column.Options, "IsVisible");
        EmitBooleanAssignment(builder, column.Options, "ShowFilterButton");

        if (column.Options.TryGetValue("DisplayIndex", out TypedConstant displayIndexConstant) &&
            displayIndexConstant.Value is int displayIndex && displayIndex >= 0)
        {
            builder.Append("            column.DisplayIndex = ")
                .Append(displayIndex.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        }

        if (column.Options.TryGetValue("Width", out TypedConstant widthConstant) && widthConstant.Value is string width)
        {
            builder.Append("            column.Width = ").Append(EmitWidth(width)).AppendLine(";");
        }

        if (column.Options.ContainsKey("IsSearchable") || column.Options.ContainsKey("SearchMemberPath"))
        {
            builder.AppendLine("            column.Options = new global::Avalonia.Controls.DataGridColumnDefinitionOptions")
                .AppendLine("            {")
                .Append("                IsSearchable = ").Append(column.IsSearchable ? "true" : "false").AppendLine(",");
            string? searchPath = GetStringOption(column.Options, "SearchMemberPath");
            if (searchPath != null)
            {
                builder.Append("                SearchMemberPath = ").Append(GeneratorUtilities.EscapeString(searchPath)).AppendLine(",");
            }

            builder.AppendLine("            };");
        }
    }

    private static void EmitSummaryDefinitions(StringBuilder builder, ColumnModel column)
    {
        if (column.Summaries.IsDefaultOrEmpty)
        {
            return;
        }

        builder.AppendLine("            column.SummaryDefinitions = new global::Avalonia.Controls.DataGridSummaryDefinition[]")
            .AppendLine("            {");
        foreach (SummaryModel summary in column.Summaries)
        {
            builder.Append("                new global::Avalonia.Controls.DataGridSummaryDefinition((global::Avalonia.Controls.DataGridAggregateType)")
                .Append(summary.Aggregate.ToString(CultureInfo.InvariantCulture))
                .Append(", (global::Avalonia.Controls.DataGridSummaryScope)")
                .Append(summary.Scope.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(GeneratorUtilities.EscapeString(summary.Format)).Append(", ")
                .Append(GeneratorUtilities.EscapeString(summary.Title)).AppendLine("),");
        }
        builder.AppendLine("            };");
    }

    private static void EmitKindOptions(StringBuilder builder, ColumnModel column, string itemType)
    {
        string? format = GetStringOption(column.Options, "FormatString");
        string? watermark = GetStringOption(column.Options, "Watermark");
        switch (column.Kind)
        {
            case "Numeric":
                EmitOptionalString(builder, "FormatString", format);
                EmitDecimalAssignment(builder, column.Options, "Minimum");
                EmitDecimalAssignment(builder, column.Options, "Maximum");
                EmitDecimalAssignment(builder, column.Options, "Increment");
                EmitOptionalString(builder, "Watermark", watermark);
                break;
            case "ProgressBar":
                EmitDoubleAssignment(builder, column.Options, "Minimum");
                EmitDoubleAssignment(builder, column.Options, "Maximum");
                EmitOptionalString(builder, "ProgressTextFormat", format);
                break;
            case "Slider":
                EmitDoubleAssignment(builder, column.Options, "Minimum");
                EmitDoubleAssignment(builder, column.Options, "Maximum");
                if (column.Options.TryGetValue("Increment", out TypedConstant increment) && increment.Value is double incrementValue)
                {
                    builder.Append("            column.SmallChange = ").Append(GeneratorUtilities.FormatDouble(incrementValue)).AppendLine(";");
                }
                EmitOptionalString(builder, "ValueTextFormat", format);
                break;
            case "TimePicker":
                EmitOptionalString(builder, "FormatString", format);
                break;
            case "MaskedText":
                EmitOptionalString(builder, "Mask", GetStringOption(column.Options, "Mask"));
                EmitOptionalString(builder, "Watermark", watermark);
                break;
            case "Text":
                EmitBooleanAssignment(builder, column.Options, "UseDirectTextCell");
                EmitBooleanAssignment(builder, column.Options, "UseDirectTextContent");
                EmitBooleanAssignment(builder, column.Options, "TrackDirectTextValueChanges");
                EmitOptionalString(builder, "Watermark", watermark);
                if (format != null)
                {
                    builder.Append("            column.Binding.StringFormat = ").Append(GeneratorUtilities.EscapeString(format)).AppendLine(";");
                }
                break;
            case "Hyperlink":
            case "Image":
            case "DatePicker":
                EmitOptionalString(builder, "Watermark", watermark);
                if (format != null && (column.Kind == "Text" || column.Kind == "Hyperlink" || column.Kind == "Image" || column.Kind == "DatePicker"))
                {
                    builder.Append("            column.Binding.StringFormat = ").Append(GeneratorUtilities.EscapeString(format)).AppendLine(";");
                }
                break;
            case "CheckBox":
            case "ToggleButton":
            case "ToggleSwitch":
                EmitBooleanAssignment(builder, column.Options, "IsThreeState");
                EmitToggleContentOptions(builder, column);
                EmitAuxiliaryColumnBindings(builder, column);
                break;
            case "AutoComplete":
                EmitOptionalString(builder, "Watermark", watermark);
                EmitItemsSource(builder, column, itemType);
                break;
            case "ComboBoxSelectedItem":
            case "ComboBoxSelectedValue":
            case "ComboBoxText":
                EmitBooleanAssignment(builder, column.Options, "IsEditable");
                EmitOptionalString(builder, "DisplayMemberPath", GetStringOption(column.Options, "DisplayMemberPath"));
                EmitOptionalString(builder, "SelectedValuePath", GetStringOption(column.Options, "SelectedValuePath"));
                EmitItemsSource(builder, column, itemType);
                if (!column.Options.ContainsKey("ItemsSourceMember") && UnwrapNullable(column.Property.Type).TypeKind == TypeKind.Enum)
                {
                    string enumType = UnwrapNullable(column.Property.Type).ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
                    builder.Append("            column.ItemsSource = global::System.Enum.GetValues<").Append(enumType).AppendLine(">();");
                }
                break;
            case "Template":
                EmitOptionalString(builder, "CellEditingTemplateKey", GetStringOption(column.Options, "EditingTemplateKey"));
                break;
            case "CustomDrawing":
                EmitBooleanAssignment(builder, column.Options, "UseDirectValueAccessor");
                EmitBooleanAssignment(builder, column.Options, "TrackDirectValueChanges");
                EmitEnumAssignment(builder, column.Options, "DrawingMode", "global::Avalonia.Controls.DataGridCustomDrawingMode");
                EmitEnumAssignment(builder, column.Options, "RenderBackend", "global::Avalonia.Controls.DataGridCustomDrawingRenderBackend");
                EmitEnumAssignment(builder, column.Options, "TextLayoutCacheMode", "global::Avalonia.Controls.DataGridCustomDrawingTextLayoutCacheMode");
                EmitInt32Assignment(builder, column.Options, "SharedTextLayoutCacheCapacity");
                EmitBooleanAssignment(builder, column.Options, "DrawOperationLayoutFastPath");
                if (column.DrawOperationFactoryType != null)
                {
                    builder.Append("            column.DrawOperationFactory = new ")
                        .Append(column.DrawOperationFactoryType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat))
                        .AppendLine("();");
                }
                else if (!string.IsNullOrEmpty(column.DrawOperationFactoryMethod))
                {
                    builder.Append("            column.DrawOperationFactory = ").Append(itemType).Append('.')
                        .Append(GeneratorUtilities.EscapeIdentifier(column.DrawOperationFactoryMethod!)).AppendLine("();");
                }
                break;
            case "Formula":
                break;
            case "Button":
                EmitAuxiliaryColumnBindings(builder, column);
                break;
            case "Hierarchical":
                EmitBooleanAssignment(builder, column.Options, "UseDirectCell");
                EmitBooleanAssignment(builder, column.Options, "UseDirectTextContent");
                EmitBooleanAssignment(builder, column.Options, "UseOptimizedPresenter");
                EmitBooleanAssignment(builder, column.Options, "TrackDirectTextValueChanges");
                EmitOptionalString(builder, "CellTemplateKey", GetStringOption(column.Options, "TemplateKey"));
                break;
        }
    }

    private static void EmitGeneratedTemplates(StringBuilder builder, ColumnModel column, string itemType)
    {
        if (column.Kind != "Template")
        {
            return;
        }

        if (column.Options.TryGetValue("ReuseCellContent", out TypedConstant reuseConstant) &&
            reuseConstant.Value is bool reuse)
        {
            builder.Append("            column.ReuseCellContent = ").Append(reuse ? "true" : "false").AppendLine(";");
        }
        EmitGeneratedTemplateAssignment(builder, itemType, "CellTemplate", column.TemplateFactoryMethod);
        EmitGeneratedTemplateAssignment(builder, itemType, "CellEditingTemplate", column.EditingTemplateFactoryMethod);
        EmitGeneratedTemplateAssignment(builder, itemType, "NewRowCellTemplate", column.NewRowTemplateFactoryMethod);
    }

    private static void EmitGeneratedTemplateAssignment(
        StringBuilder builder,
        string itemType,
        string propertyName,
        string? methodName)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            return;
        }
        builder.Append("            column.").Append(propertyName)
            .Append(" = new global::Avalonia.Controls.DataGridGeneratedFuncDataTemplate<")
            .Append(itemType).Append(">(").Append(itemType).Append('.')
            .Append(GeneratorUtilities.EscapeIdentifier(methodName!)).AppendLine(");");
    }

    private static void EmitToggleContentOptions(StringBuilder builder, ColumnModel column)
    {
        string? content = GetStringOption(column.Options, "Content");
        if (column.Kind == "ToggleSwitch")
        {
            EmitOptionalString(builder, "OnContent", GetStringOption(column.Options, "OnContent") ?? content);
            EmitOptionalString(builder, "OffContent", GetStringOption(column.Options, "OffContent"));
        }
        else if (column.Kind == "ToggleButton")
        {
            EmitOptionalString(builder, "Content", content);
            EmitOptionalString(builder, "CheckedContent", GetStringOption(column.Options, "CheckedContent"));
            EmitOptionalString(builder, "UncheckedContent", GetStringOption(column.Options, "UncheckedContent"));
        }
    }

    private static void EmitAuxiliaryColumnBindings(StringBuilder builder, ColumnModel column)
    {
        EmitAuxiliaryColumnBinding(builder, column, column.ContentMember, "Content");
        EmitAuxiliaryColumnBinding(builder, column, column.CheckedContentMember, "CheckedContent");
        EmitAuxiliaryColumnBinding(builder, column, column.UncheckedContentMember, "UncheckedContent");
        EmitAuxiliaryColumnBinding(builder, column, column.OnContentMember, "OnContent");
        EmitAuxiliaryColumnBinding(builder, column, column.OffContentMember, "OffContent");
        EmitAuxiliaryColumnBinding(builder, column, column.CommandMember, "Command");
        EmitAuxiliaryColumnBinding(builder, column, column.CommandParameterMember, "CommandParameter");
    }

    private static void EmitAuxiliaryColumnBinding(
        StringBuilder builder,
        ColumnModel column,
        IPropertySymbol? member,
        string role)
    {
        if (member == null)
        {
            return;
        }

        builder.Append("            column.").Append(role).Append("Binding = ")
            .Append(GetAuxiliaryBindingPrefix(column, role)).AppendLine("Binding;");
    }

    private static void EmitItemsSource(StringBuilder builder, ColumnModel column, string itemType)
    {
        string? member = GetStringOption(column.Options, "ItemsSourceMember");
        if (!string.IsNullOrEmpty(member))
        {
            builder.Append("            column.ItemsSource = ").Append(itemType).Append('.')
                .Append(GeneratorUtilities.EscapeIdentifier(member!)).AppendLine(";");
        }
    }

    private static string EmitViewModel(ViewModelModel model)
    {
        string namespaceName = model.ViewModelType.ContainingNamespace?.IsGlobalNamespace == false
            ? model.ViewModelType.ContainingNamespace.ToDisplayString()
            : string.Empty;
        string itemType = model.Schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string providerType = string.IsNullOrEmpty(model.Schema.ProviderNamespace)
            ? "global::" + model.Schema.ProviderName
            : "global::" + model.Schema.ProviderNamespace + "." + model.Schema.ProviderName;
        var builder = new StringBuilder(4096);
        AppendHeader(builder);
        OpenNamespace(builder, namespaceName);

        INamedTypeSymbol[] chain = GetContainingTypeChain(model.ViewModelType);
        int indent = 1;
        foreach (INamedTypeSymbol type in chain)
        {
            builder.Append(' ', indent * 4)
                .Append(GetAccessibility(type)).Append(" partial ").Append(GetTypeKeyword(type)).Append(' ')
                .Append(GeneratorUtilities.EscapeIdentifier(type.Name));
            if (type.TypeParameters.Length > 0)
            {
                builder.Append('<').Append(string.Join(", ", type.TypeParameters.Select(static parameter => parameter.Name))).Append('>');
            }

            builder.AppendLine()
                .Append(' ', indent * 4).AppendLine("{");
            indent++;
        }

        string prefix = new string(' ', indent * 4);
        if (model.GenerateSchemaProperty)
        {
            builder.Append(prefix).Append("public global::Avalonia.Controls.IDataGridGeneratedSchema<")
                .Append(itemType).Append("> ").Append(GeneratorUtilities.EscapeIdentifier(model.SchemaPropertyName))
                .Append(" { get; } = ").Append(providerType).AppendLine(".Instance;")
                .AppendLine();
        }

        if (model.GenerateColumnDefinitionsProperty)
        {
            builder.Append(prefix).Append("public global::Avalonia.Controls.DataGridColumnDefinitionList ")
                .Append(GeneratorUtilities.EscapeIdentifier(model.ColumnDefinitionsPropertyName))
                .Append(" { get; } = ").Append(providerType).AppendLine(".Instance.CreateColumnDefinitions();")
                .AppendLine();
        }

        if (model.GenerateFastPathOptionsProperty)
        {
            builder.Append(prefix).Append("public global::Avalonia.Controls.DataGridFastPathOptions ")
                .Append(GeneratorUtilities.EscapeIdentifier(model.FastPathOptionsPropertyName))
                .Append(" { get; } = ").Append(providerType).AppendLine(".Instance.CreateFastPathOptions();");
        }

        if (model.GenerateNavigationModelProperty)
        {
            if (model.GenerateFastPathOptionsProperty)
            {
                builder.AppendLine();
            }

            builder.Append(prefix).Append("public global::Avalonia.Controls.DataGridNavigation.DataGridNavigationModel ")
                .Append(GeneratorUtilities.EscapeIdentifier(model.NavigationModelPropertyName))
                .Append(" { get; } = ").Append(providerType).AppendLine(".CreateNavigationModel();");
        }

        if (model.GenerateNavigationInputModelProperty)
        {
            if (model.GenerateFastPathOptionsProperty || model.GenerateNavigationModelProperty)
            {
                builder.AppendLine();
            }

            builder.Append(prefix).Append("public global::Avalonia.Controls.DataGridNavigation.DataGridNavigationInputModel ")
                .Append(GeneratorUtilities.EscapeIdentifier(model.NavigationInputModelPropertyName))
                .Append(" { get; } = ").Append(providerType).AppendLine(".CreateNavigationInputModel();");
        }

        if (model.GenerateRouteContextFactoryProperty)
        {
            if (model.GenerateFastPathOptionsProperty ||
                model.GenerateNavigationModelProperty ||
                model.GenerateNavigationInputModelProperty)
            {
                builder.AppendLine();
            }

            builder.Append(prefix).Append("public global::Avalonia.Controls.DataGridNavigation.DataGridRouteContextFactory ")
                .Append(GeneratorUtilities.EscapeIdentifier(model.RouteContextFactoryPropertyName))
                .Append(" { get; } = ").Append(providerType).AppendLine(".CreateRouteContextFactory();");
        }

        for (int i = chain.Length - 1; i >= 0; i--)
        {
            indent--;
            builder.Append(' ', indent * 4).AppendLine("}");
        }

        CloseNamespace(builder, namespaceName);
        return builder.ToString();
    }

    private static string EmitController(ControllerModel model)
    {
        string namespaceName = model.ViewModelType.ContainingNamespace?.IsGlobalNamespace == false
            ? model.ViewModelType.ContainingNamespace.ToDisplayString()
            : string.Empty;
        string itemType = model.Schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string providerType = string.IsNullOrEmpty(model.Schema.ProviderNamespace)
            ? "global::" + model.Schema.ProviderName
            : "global::" + model.Schema.ProviderNamespace + "." + model.Schema.ProviderName;
        string propertyName = GeneratorUtilities.EscapeIdentifier(model.Name);
        string fieldName = "_" + char.ToLowerInvariant(model.Name[0]) + model.Name.Substring(1) + "GeneratedController";
        string controllerType = "global::Avalonia.Controls.DataGridGeneratedOperationController<" + itemType + ">";
        var builder = new StringBuilder(4096);
        AppendHeader(builder);
        bool emitsDynamicData = model.SourceKind == 2 || model.SourceKind == 3;
        bool emitsAsyncStream = model.SourceKind == 4 || model.SourceKind == 5;
        bool emitsRemoteQuery = model.SourceKind == 6;
        if (emitsDynamicData)
        {
            builder.AppendLine("using DynamicData;")
                .AppendLine("using System.Reactive.Linq;")
                .AppendLine();
        }
        OpenNamespace(builder, namespaceName);

        INamedTypeSymbol[] chain = GetContainingTypeChain(model.ViewModelType);
        int indent = 1;
        foreach (INamedTypeSymbol type in chain)
        {
            builder.Append(' ', indent * 4)
                .Append(GetAccessibility(type)).Append(" partial ").Append(GetTypeKeyword(type)).Append(' ')
                .Append(GeneratorUtilities.EscapeIdentifier(type.Name));
            if (type.TypeParameters.Length > 0)
            {
                builder.Append('<').Append(string.Join(", ", type.TypeParameters.Select(static parameter => parameter.Name))).Append('>');
            }

            builder.AppendLine()
                .Append(' ', indent * 4).AppendLine("{");
            indent++;
        }

        string prefix = new string(' ', indent * 4);
        builder.Append(prefix).Append("private ").Append(controllerType).Append("? ").Append(fieldName).AppendLine(";");
        if (emitsAsyncStream)
        {
            string keyType = model.Schema.KeyMember!.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append(prefix).Append("private global::Avalonia.Controls.DataGridGeneratedAsyncStreamPump<")
                .Append(itemType).Append(", ").Append(keyType).Append(">? ").Append(fieldName).AppendLine("StreamPump;")
                .Append(prefix).Append("private global::Avalonia.Controls.DataGridGeneratedStreamMetrics ")
                .Append(fieldName).AppendLine("LastStreamMetrics;");
        }
        if (emitsRemoteQuery)
        {
            string keyType = model.Schema.KeyMember!.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append(prefix).Append("private global::Avalonia.Controls.DataGridGeneratedRemoteQueryController<")
                .Append(itemType).Append(", ").Append(keyType).Append(">? ").Append(fieldName).AppendLine("RemoteQuery;");
        }
        if (emitsDynamicData)
        {
            string pipelineField = fieldName + "Pipeline";
            string sortField = fieldName + "Sort";
            string filterField = fieldName + "Filter";
            string searchField = fieldName + "Search";
            string errorsField = fieldName + "Errors";
            string completionField = fieldName + "Completion";
            builder.Append(prefix).Append("private global::System.Reactive.Disposables.CompositeDisposable? ").Append(pipelineField).AppendLine(";")
                .Append(prefix).Append("private global::System.Reactive.Subjects.BehaviorSubject<global::System.Collections.Generic.IComparer<")
                .Append(itemType).Append(">>? ").Append(sortField).AppendLine(";")
                .Append(prefix).Append("private global::System.Reactive.Subjects.BehaviorSubject<global::System.Func<")
                .Append(itemType).Append(", bool>>? ").Append(filterField).AppendLine(";")
                .Append(prefix).Append("private global::System.Reactive.Subjects.BehaviorSubject<global::System.Func<")
                .Append(itemType).Append(", bool>>? ").Append(searchField).AppendLine(";")
                .Append(prefix).Append("private readonly global::System.Reactive.Subjects.ReplaySubject<global::System.Exception> ")
                .Append(errorsField).AppendLine(" = new(1);")
                .Append(prefix).Append("private readonly global::System.Reactive.Subjects.AsyncSubject<global::System.Reactive.Unit> ")
                .Append(completionField).AppendLine(" = new();");
        }

        builder
            .AppendLine()
            .Append(prefix).Append("public ").Append(controllerType).Append(' ').Append(propertyName).AppendLine()
            .Append(prefix).Append("    => ").Append(fieldName)
            .Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated controller '" + model.Name + "' has not been initialized."))
            .AppendLine(");")
            .AppendLine()
            .Append(prefix).Append("public global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedOperationDescriptor> ")
            .Append(propertyName).Append("Descriptors => ").Append(propertyName).AppendLine(".Descriptors;")
            .AppendLine()
            .Append(prefix).Append("public global::Avalonia.Controls.DataGridGeneratedOperationCommandSet<").Append(itemType).Append("> ")
            .Append(propertyName).Append("Commands => ").Append(propertyName).AppendLine(".Commands;")
            .AppendLine()
            .Append(prefix).Append("public global::System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedOperationPreset> ")
            .Append(propertyName).Append("Presets => ").Append(providerType).AppendLine(".OperationPresets;")
            .AppendLine()
            .Append(prefix).Append("public bool Is").Append(model.Name).Append("Initialized => ").Append(fieldName).AppendLine(" != null;")
            .AppendLine()
            .Append(prefix).Append("public void Initialize").Append(model.Name).Append('(').Append(controllerType).AppendLine(" controller)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).AppendLine("    if (controller is null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        throw new global::System.ArgumentNullException(nameof(controller));")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    if (").Append(fieldName).AppendLine(" != null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated controller '" + model.Name + "' is already initialized."))
            .AppendLine(");")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    ").Append(fieldName).AppendLine(" = controller;")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("public static ").Append(controllerType).Append(" Create").Append(model.Name).AppendLine("Controller()")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    var options = new global::Avalonia.Controls.DataGridGeneratedControllerOptions<")
            .Append(itemType).AppendLine(">(")
            .Append(prefix).Append("        (global::Avalonia.Controls.DataGridOperationExecution)").Append(model.OperationExecution).AppendLine(",")
            .Append(prefix).Append("        (global::Avalonia.Controls.DataGridGeneratedFeatures)").Append(model.Features).AppendLine(");");
        if (!string.IsNullOrEmpty(model.ConfigureMethod))
        {
            builder.Append(prefix).Append("    ").Append(GeneratorUtilities.EscapeIdentifier(model.ConfigureMethod!))
                .AppendLine("(ref options);");
        }

        if (model.ImplementationType != null)
        {
            string implementationType = model.ImplementationType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append(prefix).Append("    var context = new global::Avalonia.Controls.DataGridGeneratedControllerContext<")
                .Append(itemType).Append(">(").Append(providerType).AppendLine(".Instance, options);")
                .Append(prefix).Append("    return new ").Append(implementationType).AppendLine("().Create(in context);");
        }
        else
        {
            builder.Append(prefix).Append("    return ").Append(providerType)
                .AppendLine(".CreateController(options.Execution, options.Features);");
        }

        builder.Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("public void Dispose").Append(model.Name).AppendLine("()")
            .Append(prefix).AppendLine("{")
            .Append(emitsDynamicData ? prefix + "    Disconnect" + model.Name + "Pipeline();\n" : string.Empty)
            .Append(emitsAsyncStream ? prefix + "    Stop" + model.Name + "Stream();\n" : string.Empty)
            .Append(emitsRemoteQuery ? prefix + "    Dispose" + model.Name + "RemoteQuery();\n" : string.Empty)
            .Append(prefix).Append("    ").Append(fieldName).AppendLine("?.Dispose();")
            .Append(prefix).Append("    ").Append(fieldName).AppendLine(" = null;")
            .Append(prefix).AppendLine("}");

        if (emitsDynamicData)
        {
            EmitDynamicDataPipeline(builder, model, prefix, itemType, fieldName);
        }
        if (emitsAsyncStream)
        {
            EmitAsyncStreamController(builder, model, prefix, itemType, providerType, fieldName);
        }
        if (emitsRemoteQuery)
        {
            EmitRemoteQueryController(builder, model, prefix, itemType, fieldName);
        }

        for (int i = chain.Length - 1; i >= 0; i--)
        {
            indent--;
            builder.Append(' ', indent * 4).AppendLine("}");
        }

        CloseNamespace(builder, namespaceName);
        return builder.ToString();
    }

    private static void EmitRemoteQueryController(
        StringBuilder builder,
        ControllerModel model,
        string prefix,
        string itemType,
        string controllerField)
    {
        string keyType = model.Schema.KeyMember!.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string remoteType = "global::Avalonia.Controls.DataGridGeneratedRemoteQueryController<" + itemType + ", " + keyType + ">";
        string pageType = "global::Avalonia.Controls.DataGridQueryPage<" + itemType + ", " + keyType + ">";
        string remoteField = controllerField + "RemoteQuery";
        string sourceMember = GeneratorUtilities.EscapeIdentifier(model.SourceMember!);

        builder.AppendLine()
            .Append(prefix).Append("public ").Append(remoteType).Append(' ').Append(model.Name).AppendLine("RemoteQuery")
            .Append(prefix).Append("    => ").Append(remoteField).Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated remote query controller '" + model.Name + "' has not been initialized."))
            .AppendLine(");")
            .AppendLine()
            .Append(prefix).Append("public ").Append(remoteType).Append(" Create").Append(model.Name).AppendLine("RemoteQueryController(")
            .Append(prefix).AppendLine("    global::System.TimeSpan debounce = default,")
            .Append(prefix).AppendLine("    int pageCacheCapacity = 0,")
            .Append(prefix).AppendLine("    global::System.Func<string, string>? fieldNameTranslator = null)")
            .Append(prefix).Append("    => new ").Append(remoteType).Append('(').Append(sourceMember)
            .AppendLine(", debounce, pageCacheCapacity, fieldNameTranslator);")
            .AppendLine()
            .Append(prefix).Append("public void Initialize").Append(model.Name).Append("RemoteQuery(").Append(remoteType).AppendLine(" controller)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).AppendLine("    if (controller is null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        throw new global::System.ArgumentNullException(nameof(controller));")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    if (").Append(remoteField).AppendLine(" != null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        throw new global::System.InvalidOperationException(\"The generated remote query controller is already initialized.\");")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    ").Append(remoteField).AppendLine(" = controller;")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("public global::System.Threading.Tasks.ValueTask<").Append(pageType).Append("?> Query")
            .Append(model.Name).AppendLine("Async(")
            .Append(prefix).AppendLine("    global::Avalonia.Controls.DataGridPageRequest page,")
            .Append(prefix).AppendLine("    global::System.Collections.Generic.IEnumerable<string>? groups = null,")
            .Append(prefix).AppendLine("    string? cacheKey = null,")
            .Append(prefix).AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    var remote = ").Append(remoteField).Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated remote query controller '" + model.Name + "' has not been initialized."))
            .AppendLine(");")
            .Append(prefix).Append("    var operations = ").Append(controllerField).Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated controller '" + model.Name + "' has not been initialized."))
            .AppendLine(");")
            .Append(prefix).AppendLine("    return remote.ExecuteLatestAsync(")
            .Append(prefix).Append("        revision => new global::Avalonia.Controls.DataGridRemoteQuery<").Append(itemType).AppendLine(">(")
            .Append(prefix).AppendLine("            revision,")
            .Append(prefix).AppendLine("            operations.SortingModel.Descriptors,")
            .Append(prefix).AppendLine("            operations.FilteringModel.Descriptors,")
            .Append(prefix).AppendLine("            operations.SearchModel.Descriptors,")
            .Append(prefix).AppendLine("            page,")
            .Append(prefix).AppendLine("            groups),")
            .Append(prefix).AppendLine("        cacheKey,")
            .Append(prefix).AppendLine("        cancellationToken);")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("public global::System.Threading.Tasks.ValueTask<bool> Prefetch").Append(model.Name).AppendLine("Async(")
            .Append(prefix).AppendLine("    global::Avalonia.Controls.DataGridPageRequest page,")
            .Append(prefix).AppendLine("    string cacheKey,")
            .Append(prefix).AppendLine("    global::System.Collections.Generic.IEnumerable<string>? groups = null,")
            .Append(prefix).AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    var remote = ").Append(remoteField).Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated remote query controller '" + model.Name + "' has not been initialized."))
            .AppendLine(");")
            .Append(prefix).Append("    var operations = ").Append(controllerField).Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated controller '" + model.Name + "' has not been initialized."))
            .AppendLine(");")
            .Append(prefix).AppendLine("    return remote.PrefetchAsync(")
            .Append(prefix).Append("        revision => new global::Avalonia.Controls.DataGridRemoteQuery<").Append(itemType).AppendLine(">(")
            .Append(prefix).AppendLine("            revision,")
            .Append(prefix).AppendLine("            operations.SortingModel.Descriptors,")
            .Append(prefix).AppendLine("            operations.FilteringModel.Descriptors,")
            .Append(prefix).AppendLine("            operations.SearchModel.Descriptors,")
            .Append(prefix).AppendLine("            page,")
            .Append(prefix).AppendLine("            groups),")
            .Append(prefix).AppendLine("        cacheKey,")
            .Append(prefix).AppendLine("        cancellationToken);")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("public void Dispose").Append(model.Name).AppendLine("RemoteQuery()")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    ").Append(remoteField).AppendLine("?.Dispose();")
            .Append(prefix).Append("    ").Append(remoteField).AppendLine(" = null;")
            .Append(prefix).AppendLine("}");
    }

    private static void EmitAsyncStreamController(
        StringBuilder builder,
        ControllerModel model,
        string prefix,
        string itemType,
        string providerType,
        string controllerField)
    {
        string keyType = model.Schema.KeyMember!.Type.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string pumpType = "global::Avalonia.Controls.DataGridGeneratedAsyncStreamPump<" + itemType + ", " + keyType + ">";
        string updateType = "global::Avalonia.Controls.DataGridGeneratedStreamUpdate<" + itemType + ", " + keyType + ">";
        string pumpField = controllerField + "StreamPump";
        string metricsField = controllerField + "LastStreamMetrics";
        string sourceMember = GeneratorUtilities.EscapeIdentifier(model.SourceMember!);

        builder.AppendLine()
            .Append(prefix).Append("public ").Append(pumpType).Append("? ").Append(model.Name)
            .Append("StreamPump => ").Append(pumpField).AppendLine(";")
            .AppendLine()
            .Append(prefix).Append("public global::Avalonia.Controls.DataGridGeneratedStreamMetrics ").Append(model.Name)
            .Append("StreamMetrics => ").Append(pumpField).Append("?.Metrics ?? ").Append(metricsField).AppendLine(";")
            .AppendLine()
            .Append(prefix).Append("public async global::System.Threading.Tasks.Task Run").Append(model.Name).AppendLine("StreamAsync(")
            .Append(prefix).Append("    global::System.Func<global::System.ReadOnlyMemory<").Append(updateType)
            .AppendLine(">, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask> applyBatch,")
            .Append(prefix).AppendLine("    int capacity = 1024,")
            .Append(prefix).AppendLine("    int batchSize = 128,")
            .Append(prefix).AppendLine("    global::Avalonia.Controls.DataGridGeneratedStreamOverflowPolicy overflowPolicy = global::Avalonia.Controls.DataGridGeneratedStreamOverflowPolicy.CoalesceByKey,")
            .Append(prefix).AppendLine("    global::Avalonia.Controls.DataGridGeneratedStreamUpdateKind mode = global::Avalonia.Controls.DataGridGeneratedStreamUpdateKind.Upsert,")
            .Append(prefix).AppendLine("    long initialRevision = 0,")
            .Append(prefix).AppendLine("    global::System.Action<global::System.Exception>? onError = null,")
            .Append(prefix).AppendLine("    global::System.Action? onCompleted = null,")
            .Append(prefix).AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    if (").Append(pumpField).AppendLine(" != null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated stream '" + model.Name + "' is already running."))
            .AppendLine(");")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    var pump = ").Append(providerType)
            .AppendLine(".CreateAsyncStreamPump(applyBatch, capacity, batchSize, overflowPolicy);")
            .Append(prefix).AppendLine("    if (onError != null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        pump.Faulted += onError;")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("    if (onCompleted != null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        pump.Completed += (_, _) => onCompleted();")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    ").Append(pumpField).AppendLine(" = pump;")
            .Append(prefix).AppendLine("    try")
            .Append(prefix).AppendLine("    {");
        if (model.SourceKind == 4)
        {
            builder.Append(prefix).Append("        await pump.RunAsync(").Append(sourceMember)
                .AppendLine(", mode, initialRevision, cancellationToken).ConfigureAwait(false);");
        }
        else
        {
            builder.Append(prefix).Append("        await pump.RunAsync(").Append(sourceMember)
                .AppendLine(", mode, initialRevision, cancellationToken).ConfigureAwait(false);");
        }

        builder.Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("    finally")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        ").Append(metricsField).AppendLine(" = pump.Metrics;")
            .Append(prefix).AppendLine("        pump.Dispose();")
            .Append(prefix).Append("        if (global::System.Object.ReferenceEquals(").Append(pumpField).AppendLine(", pump))")
            .Append(prefix).AppendLine("        {")
            .Append(prefix).Append("            ").Append(pumpField).AppendLine(" = null;")
            .Append(prefix).AppendLine("        }")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("public void Stop").Append(model.Name).AppendLine("Stream()")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    var pump = ").Append(pumpField).AppendLine(";")
            .Append(prefix).AppendLine("    if (pump == null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        return;")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    ").Append(metricsField).AppendLine(" = pump.Metrics;")
            .Append(prefix).AppendLine("    pump.Dispose();")
            .Append(prefix).Append("    ").Append(pumpField).AppendLine(" = null;")
            .Append(prefix).AppendLine("}");
    }

    private static void EmitDynamicDataPipeline(
        StringBuilder builder,
        ControllerModel model,
        string prefix,
        string itemType,
        string controllerField)
    {
        string pipelineField = controllerField + "Pipeline";
        string sortField = controllerField + "Sort";
        string filterField = controllerField + "Filter";
        string searchField = controllerField + "Search";
        string errorsField = controllerField + "Errors";
        string completionField = controllerField + "Completion";
        string sourceMember = GeneratorUtilities.EscapeIdentifier(model.SourceMember!);
        string handlerName = "On" + model.Name + "GeneratedOperationsChanged";

        builder.AppendLine()
            .Append(prefix).Append("public global::System.IObservable<global::System.Exception> ").Append(model.Name)
            .Append("Errors => ").Append(errorsField).AppendLine(";")
            .AppendLine()
            .Append(prefix).Append("public global::System.IObservable<global::System.Reactive.Unit> ").Append(model.Name)
            .Append("Completion => ").Append(completionField).AppendLine(";")
            .AppendLine()
            .Append(prefix).Append("public global::System.Collections.ObjectModel.ReadOnlyObservableCollection<")
            .Append(itemType).Append("> Connect").Append(model.Name)
            .AppendLine("Pipeline(global::System.Reactive.Concurrency.IScheduler? scheduler = null)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    if (").Append(pipelineField).AppendLine(" != null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated DynamicData pipeline '" + model.Name + "' is already connected."))
            .AppendLine(");")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).Append("    var controller = ").Append(controllerField)
            .Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(GeneratorUtilities.EscapeString("Generated controller '" + model.Name + "' has not been initialized."))
            .AppendLine(");")
            .Append(prefix).Append("    ").Append(sortField).Append(" = new(controller.SortComparer);").AppendLine()
            .Append(prefix).Append("    ").Append(filterField).Append(" = new(controller.FilterPredicate);").AppendLine()
            .Append(prefix).Append("    ").Append(searchField).Append(" = new(controller.SearchPredicate);").AppendLine()
            .Append(prefix).Append("    ").Append(pipelineField).AppendLine(" = new global::System.Reactive.Disposables.CompositeDisposable();");

        if (model.SourceKind == 3)
        {
            builder.Append(prefix).AppendLine("    var options = new global::DynamicData.Binding.SortAndBindOptions")
                .Append(prefix).AppendLine("    {")
                .Append(prefix).AppendLine("        UseReplaceForUpdates = true,")
                .Append(prefix).AppendLine("        Scheduler = scheduler")
                .Append(prefix).AppendLine("    };")
                .Append(prefix).Append("    var changes = ").Append(sourceMember).AppendLine(".Connect();");
            if (!string.IsNullOrEmpty(model.PipelineTransformMethod))
            {
                builder.Append(prefix).Append("    changes = ")
                    .Append(GeneratorUtilities.EscapeIdentifier(model.PipelineTransformMethod!)).AppendLine("(changes);");
            }
            builder.Append(prefix).Append("    var subscription = changes.Filter(").Append(filterField).AppendLine(")")
                .Append(prefix).Append("        .Filter(").Append(searchField).AppendLine(")")
                .Append(prefix).Append("        .SortAndBind(out global::System.Collections.ObjectModel.ReadOnlyObservableCollection<")
                .Append(itemType).Append("> items, ").Append(sortField).AppendLine(", options);");
        }
        else
        {
            builder.Append(prefix).Append("    var changes = ").Append(sourceMember).AppendLine(".Connect();");
            if (!string.IsNullOrEmpty(model.PipelineTransformMethod))
            {
                builder.Append(prefix).Append("    changes = ")
                    .Append(GeneratorUtilities.EscapeIdentifier(model.PipelineTransformMethod!)).AppendLine("(changes);");
            }
            builder.Append(prefix).Append("    var filteredChanges = changes.Filter(").Append(filterField).AppendLine(")")
                .Append(prefix).Append("        .Filter(").Append(searchField).AppendLine(")")
                .Append(prefix).Append("        .Sort(").Append(sortField).AppendLine(");")
                .Append(prefix).AppendLine("    if (scheduler != null)")
                .Append(prefix).AppendLine("    {")
                .Append(prefix).AppendLine("        filteredChanges = filteredChanges.ObserveOn(scheduler);")
                .Append(prefix).AppendLine("    }")
                .Append(prefix).Append("    var subscription = filteredChanges.Bind(out global::System.Collections.ObjectModel.ReadOnlyObservableCollection<")
                .Append(itemType).AppendLine("> items, new global::DynamicData.Binding.BindingOptions(")
                .Append(prefix).AppendLine("        global::DynamicData.Binding.BindingOptions.DefaultResetThreshold,")
                .Append(prefix).AppendLine("        true,")
                .Append(prefix).AppendLine("        global::DynamicData.Binding.BindingOptions.DefaultResetOnFirstTimeLoad));");
        }

        builder.Append(prefix).AppendLine("    var pipelineSubscription = global::System.ObservableExtensions.Subscribe(subscription,")
            .Append(prefix).AppendLine("        static _ => { },")
            .Append(prefix).AppendLine("        error =>")
            .Append(prefix).AppendLine("        {")
            .Append(prefix).Append("            ").Append(errorsField).AppendLine(".OnNext(error);")
            .Append(prefix).Append("            ").Append(errorsField).AppendLine(".OnCompleted();")
            .Append(prefix).AppendLine("        },")
            .Append(prefix).AppendLine("        () =>")
            .Append(prefix).AppendLine("        {")
            .Append(prefix).Append("            ").Append(completionField).AppendLine(".OnNext(global::System.Reactive.Unit.Default);")
            .Append(prefix).Append("            ").Append(completionField).AppendLine(".OnCompleted();")
            .Append(prefix).AppendLine("        });")
            .Append(prefix).Append("    controller.OperationsChanged += ").Append(handlerName).AppendLine(";")
            .Append(prefix).Append("    ").Append(pipelineField).AppendLine(".Add(pipelineSubscription);")
            .Append(prefix).Append("    ").Append(pipelineField).Append(".Add(global::System.Reactive.Disposables.Disposable.Create(() => controller.OperationsChanged -= ")
            .Append(handlerName).AppendLine("));")
            .Append(prefix).AppendLine("    return items;")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("private void ").Append(handlerName)
            .AppendLine("(object? sender, global::Avalonia.Controls.DataGridGeneratedOperationsChangedEventArgs args)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).AppendLine("    var controller = sender as global::Avalonia.Controls.DataGridGeneratedOperationController<" + itemType + ">;")
            .Append(prefix).AppendLine("    if (controller == null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        return;")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("    if ((args.Change & global::Avalonia.Controls.DataGridGeneratedOperationChange.Sorting) != 0)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        ").Append(sortField).AppendLine("?.OnNext(controller.SortComparer);")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("    if ((args.Change & global::Avalonia.Controls.DataGridGeneratedOperationChange.Filtering) != 0)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        ").Append(filterField).AppendLine("?.OnNext(controller.FilterPredicate);")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("    if ((args.Change & global::Avalonia.Controls.DataGridGeneratedOperationChange.Searching) != 0)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        ").Append(searchField).AppendLine("?.OnNext(controller.SearchPredicate);")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).Append("public void Disconnect").Append(model.Name).AppendLine("Pipeline()")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    ").Append(pipelineField).AppendLine("?.Dispose();")
            .Append(prefix).Append("    ").Append(pipelineField).AppendLine(" = null;")
            .Append(prefix).Append("    ").Append(sortField).AppendLine("?.Dispose();")
            .Append(prefix).Append("    ").Append(sortField).AppendLine(" = null;")
            .Append(prefix).Append("    ").Append(filterField).AppendLine("?.Dispose();")
            .Append(prefix).Append("    ").Append(filterField).AppendLine(" = null;")
            .Append(prefix).Append("    ").Append(searchField).AppendLine("?.Dispose();")
            .Append(prefix).Append("    ").Append(searchField).AppendLine(" = null;")
            .Append(prefix).AppendLine("}");
    }

    private static void EmitGeneratedClasses(
        StringBuilder builder,
        string controlExpression,
        ImmutableArray<string> classes,
        int indentation)
    {
        string prefix = new(' ', indentation);
        for (int index = 0; index < classes.Length; index++)
        {
            builder.Append(prefix).Append(controlExpression).Append(".Classes.Add(")
                .Append(GeneratorUtilities.EscapeString(classes[index])).AppendLine(");");
        }
    }

    private static void EmitGeneratedTheme(
        StringBuilder builder,
        string controlExpression,
        string keyConstant,
        string? themeKey,
        int indentation)
    {
        if (themeKey == null)
        {
            return;
        }

        string prefix = new(' ', indentation);
        string themedName = "themed" + char.ToUpperInvariant(controlExpression[0]) + controlExpression.Substring(1);
        builder.Append(prefix).Append("if (").Append(controlExpression)
            .Append(" is global::Avalonia.Controls.Primitives.TemplatedControl ").Append(themedName).AppendLine(")")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    ").Append(themedName)
            .Append(".Bind(global::Avalonia.Controls.Primitives.TemplatedControl.ThemeProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(")
            .Append(keyConstant).AppendLine("!));")
            .Append(prefix).AppendLine("}");
    }

    private static string EmitView(ViewModelViewModel model)
    {
        string viewModelType = model.ViewModelType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string accessibility = IsPubliclyAccessible(model.ViewModelType) ? "public" : "internal";
        string baseType = ViewGenerationStrategyRegistry.Get(model.Framework).GetBaseType(model);
        bool usesReactiveActivation = model.Framework == ViewFrameworkModel.ReactiveUI &&
            (model.RoutedEventCommand != null || !model.Interactions.IsDefaultOrEmpty ||
                model.NavigationInteraction != null || model.InputCommand != null || model.DiagnosticsSinkType != null);
        var builder = new StringBuilder(12288);
        AppendHeader(builder);
        OpenNamespace(builder, model.ViewNamespace);
        builder.Append("    ").Append(accessibility).Append(" class ")
            .Append(model.ViewName).Append(" : ").Append(baseType).AppendLine()
            .AppendLine("    {")
            .Append("        public const int GeneratedRecipe = ").Append(model.Recipe.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
            .Append("        public const string GeneratedAutomationId = ").Append(GeneratorUtilities.EscapeString(model.AutomationId)).AppendLine(";")
            .Append("        public const string? GeneratedControllerName = ").Append(GeneratorUtilities.EscapeString(model.ControllerName)).AppendLine(";")
            .Append("        public const global::Avalonia.Controls.DataGridGeneratedPerformanceProfile GeneratedPerformanceProfile = (global::Avalonia.Controls.DataGridGeneratedPerformanceProfile)")
            .Append(model.PerformanceProfile.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
            .Append("        public const string GeneratedDiagnosticsSchemaId = ")
            .Append(GeneratorUtilities.EscapeString(GeneratorUtilities.GetMetadataName(model.ItemType) + "/" + model.ViewName)).AppendLine(";")
            .Append("        public const string? GeneratedViewThemeKey = ").Append(GeneratorUtilities.EscapeString(model.ViewThemeKey)).AppendLine(";")
            .Append("        public const string? GeneratedDataGridThemeKey = ").Append(GeneratorUtilities.EscapeString(model.DataGridThemeKey)).AppendLine(";")
            .Append("        public const string? GeneratedToolbarThemeKey = ").Append(GeneratorUtilities.EscapeString(model.ToolbarThemeKey)).AppendLine(";")
            .Append("        public const string? GeneratedRecipeContentThemeKey = ").Append(GeneratorUtilities.EscapeString(model.RecipeContentThemeKey)).AppendLine(";")
            .AppendLine();

        EmitViewPropertyInfo(builder, model.Items, viewModelType, "Items");
        EmitViewPropertyInfo(builder, model.ColumnDefinitions, viewModelType, "ColumnDefinitions");
        EmitViewPropertyInfo(builder, model.FastPathOptions, viewModelType, "FastPathOptions");
        if (model.LayoutModel != null)
        {
            EmitViewPropertyInfo(builder, model.LayoutModel, viewModelType, "LayoutModel");
        }
        if (model.SortingModel != null)
        {
            EmitViewPropertyInfo(builder, model.SortingModel, viewModelType, "SortingModel");
        }
        if (model.FilteringModel != null)
        {
            EmitViewPropertyInfo(builder, model.FilteringModel, viewModelType, "FilteringModel");
        }
        if (model.SearchModel != null)
        {
            EmitViewPropertyInfo(builder, model.SearchModel, viewModelType, "SearchModel");
        }
        if (model.SearchText != null)
        {
            EmitViewPropertyInfo(builder, model.SearchText, viewModelType, "SearchText");
        }
        if (model.SelectionModel != null)
        {
            EmitViewPropertyInfo(builder, model.SelectionModel, viewModelType, "SelectionModel");
        }
        if (model.NavigationModel != null)
        {
            EmitViewPropertyInfo(builder, model.NavigationModel, viewModelType, "NavigationModel");
        }
        if (model.RouteNavigationModel != null)
        {
            EmitViewPropertyInfo(builder, model.RouteNavigationModel, viewModelType, "RouteNavigationModel");
        }
        if (model.NavigationInputModel != null)
        {
            EmitViewPropertyInfo(builder, model.NavigationInputModel, viewModelType, "NavigationInputModel");
        }
        if (model.RouteContextFactory != null)
        {
            EmitViewPropertyInfo(builder, model.RouteContextFactory, viewModelType, "RouteContextFactory");
        }
        if (model.ClipboardImportModel != null)
        {
            EmitViewPropertyInfo(builder, model.ClipboardImportModel, viewModelType, "ClipboardImportModel");
        }
        if (model.FillModel != null)
        {
            EmitViewPropertyInfo(builder, model.FillModel, viewModelType, "FillModel");
        }
        if (model.FormulaModel != null)
        {
            EmitViewPropertyInfo(builder, model.FormulaModel, viewModelType, "FormulaModel");
        }
        if (model.ConditionalFormattingModel != null)
        {
            EmitViewPropertyInfo(builder, model.ConditionalFormattingModel, viewModelType, "ConditionalFormattingModel");
        }
        if (model.HierarchicalModel != null)
        {
            EmitViewPropertyInfo(builder, model.HierarchicalModel, viewModelType, "HierarchicalModel");
        }
        if (model.StateController != null)
        {
            EmitViewPropertyInfo(builder, model.StateController, viewModelType, "StateController");
        }
        if (model.ViewState != null)
        {
            EmitViewPropertyInfo(builder, model.ViewState, viewModelType, "ViewState");
        }
        if (model.ErrorMessage != null)
        {
            EmitViewPropertyInfo(builder, model.ErrorMessage, viewModelType, "ErrorMessage");
        }
        if (model.RetryCommand != null)
        {
            EmitViewPropertyInfo(builder, model.RetryCommand, viewModelType, "RetryCommand");
        }
        if (model.DiagnosticsStatus != null)
        {
            EmitViewPropertyInfo(builder, model.DiagnosticsStatus, viewModelType, "DiagnosticsStatus");
        }

        if (model.ViewState != null)
        {
            builder.AppendLine("        private static readonly GeneratedViewStateVisibilityConverter s_contentStateConverter = new(global::Avalonia.Controls.DataGridGeneratedViewState.Content);")
                .AppendLine("        private static readonly GeneratedViewStateVisibilityConverter s_loadingStateConverter = new(global::Avalonia.Controls.DataGridGeneratedViewState.Loading);")
                .AppendLine("        private static readonly GeneratedViewStateVisibilityConverter s_emptyStateConverter = new(global::Avalonia.Controls.DataGridGeneratedViewState.Empty);")
                .AppendLine("        private static readonly GeneratedViewStateVisibilityConverter s_errorStateConverter = new(global::Avalonia.Controls.DataGridGeneratedViewState.Error);")
                .AppendLine();
        }
        if (model.ErrorMessage != null)
        {
            builder.Append("        private static readonly GeneratedErrorMessageConverter s_errorMessageConverter = new(")
                .Append(GeneratorUtilities.EscapeString(model.ErrorText)).AppendLine(");")
                .AppendLine();
        }

        builder.AppendLine("        private global::Avalonia.Controls.DataGrid? _generatedDataGrid;")
            .AppendLine("        private global::Avalonia.Controls.IDataGridGeneratedInputMap? _generatedInputMap;");
        if (model.DiagnosticsSinkType != null && model.Framework == ViewFrameworkModel.Avalonia)
        {
            builder.AppendLine("        private global::System.IDisposable? _generatedMetricsSubscription;");
        }
        builder
            .AppendLine()
            .AppendLine("        protected global::Avalonia.Controls.DataGrid GeneratedDataGrid")
            .AppendLine("            => _generatedDataGrid ?? throw new global::System.InvalidOperationException(\"Generated DataGrid is not initialized.\");")
            .AppendLine();

        builder.AppendLine("        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"The generated view uses compiled binding indexers and activation delegates; it does not evaluate reflection-based member expressions.\")]")
            .Append("        public ").Append(model.ViewName).AppendLine("()")
            .AppendLine("        {")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(this, GeneratedAutomationId + \"-view\");")
            .Append("            global::Avalonia.Automation.AutomationProperties.SetName(this, ").Append(GeneratorUtilities.EscapeString(model.Title)).AppendLine(");");
        if (model.ViewThemeKey != null)
        {
            builder.AppendLine("            this.Bind(global::Avalonia.Controls.Primitives.TemplatedControl.ThemeProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(GeneratedViewThemeKey!));");
        }
        EmitGeneratedClasses(builder, "this", model.ViewClasses, 12);
        builder.AppendLine("            Content = CreateGeneratedContent();");
        if (usesReactiveActivation)
        {
            builder.AppendLine("            ConfigureGeneratedReactiveActivation(GeneratedDataGrid);");
        }
        else if (model.DiagnosticsSinkType != null)
        {
            builder.AppendLine("            ConfigureGeneratedAvaloniaMetricsLifetime();");
        }
        builder.AppendLine("        }")
            .AppendLine()
            .Append("        public ").Append(model.ViewName).Append('(').Append(viewModelType).AppendLine(" viewModel)")
            .AppendLine("            : this()")
            .AppendLine("        {")
            .AppendLine("            DataContext = viewModel ?? throw new global::System.ArgumentNullException(nameof(viewModel));")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual global::Avalonia.Controls.Control CreateGeneratedContent()")
            .AppendLine("        {")
            .AppendLine("            var title = new global::Avalonia.Controls.TextBlock")
            .AppendLine("            {")
            .Append("                Text = ").Append(GeneratorUtilities.EscapeString(model.Title)).AppendLine(",")
            .AppendLine("                FontSize = 22d,")
            .AppendLine("                FontWeight = global::Avalonia.Media.FontWeight.SemiBold")
            .AppendLine("            };")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(title, GeneratedAutomationId + \"-title\");")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(title, title.Text);")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetHeadingLevel(title, 1);")
            .AppendLine("            var header = new global::Avalonia.Controls.StackPanel")
            .AppendLine("            {")
            .AppendLine("                Spacing = 6d,")
            .AppendLine("                Children =")
            .AppendLine("                {")
            .AppendLine("                    title")
            .AppendLine("                }")
            .AppendLine("            };");

        if (model.DiagnosticsStatus != null)
        {
            builder.AppendLine("            var diagnosticsStatus = new global::Avalonia.Controls.TextBlock")
                .AppendLine("            {")
                .AppendLine("                Name = \"GeneratedDiagnosticsStatus\",")
                .AppendLine("                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap")
                .AppendLine("            };")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(diagnosticsStatus, GeneratedAutomationId + \"-diagnostics-status\");")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(diagnosticsStatus, \"Grid diagnostics status\");")
                .AppendLine("            diagnosticsStatus[!global::Avalonia.Controls.TextBlock.TextProperty] = CreateBinding(s_diagnosticsStatusProperty, global::Avalonia.Data.BindingMode.OneWay);")
                .AppendLine("            header.Children.Add(diagnosticsStatus);");
        }

        if (model.SearchText != null)
        {
            builder.AppendLine("            var searchBox = new global::Avalonia.Controls.TextBox")
                .AppendLine("            {")
                .AppendLine("                Name = \"GeneratedSearchBox\",")
                .AppendLine("                PlaceholderText = \"Search\",")
                .AppendLine("                Width = 280d,")
                .AppendLine("                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left")
                .AppendLine("            };")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(searchBox, GeneratedAutomationId + \"-search\");")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(searchBox, \"Search\");")
                .AppendLine("            searchBox[!global::Avalonia.Controls.TextBox.TextProperty] = CreateBinding(s_searchTextProperty, global::Avalonia.Data.BindingMode.TwoWay);")
                .AppendLine("            header.Children.Add(searchBox);");
        }

        if (model.Recipe >= 2)
        {
            builder.AppendLine("            var toolbar = CreateGeneratedToolbar();")
                .AppendLine("            if (toolbar is not null)")
                .AppendLine("            {");
            EmitGeneratedTheme(builder, "toolbar", "GeneratedToolbarThemeKey", model.ToolbarThemeKey, 16);
            EmitGeneratedClasses(builder, "toolbar", model.ToolbarClasses, 16);
            builder.AppendLine("            }");
        }
        if (model.Recipe >= 3)
        {
            builder.AppendLine("            var recipeContent = CreateGeneratedRecipeContent();")
                .AppendLine("            if (recipeContent is not null)")
                .AppendLine("            {");
            EmitGeneratedTheme(builder, "recipeContent", "GeneratedRecipeContentThemeKey", model.RecipeContentThemeKey, 16);
            EmitGeneratedClasses(builder, "recipeContent", model.RecipeContentClasses, 16);
            builder.AppendLine("            }");
        }

        builder.AppendLine("            var dataGrid = CreateGeneratedDataGrid();")
            .AppendLine("            _generatedDataGrid = dataGrid;")
            .AppendLine(model.ViewState != null
                ? "            var generatedStateHost = CreateGeneratedViewStateHost(dataGrid);"
                : string.Empty)
            .AppendLine("            var layout = new global::Avalonia.Controls.Grid")
            .AppendLine("            {")
            .AppendLine("                Margin = new global::Avalonia.Thickness(12d),")
            .Append("                RowDefinitions = new global::Avalonia.Controls.RowDefinitions(")
            .Append(GeneratorUtilities.EscapeString(model.Recipe >= 3 ? "Auto,Auto,*,Auto" : model.Recipe >= 2 ? "Auto,Auto,*" : "Auto,*")).AppendLine("),")
            .AppendLine("                RowSpacing = 8d")
            .AppendLine("            };")
            .Append("            global::Avalonia.Controls.Grid.SetRow(")
            .Append(model.ViewState != null ? "generatedStateHost" : "dataGrid")
            .Append(", ").Append(model.Recipe >= 2 ? "2" : "1").AppendLine(");")
            .AppendLine("            layout.Children.Add(header);");
        if (model.Recipe >= 2)
        {
            builder.AppendLine("            if (toolbar is not null)")
                .AppendLine("            {")
                .AppendLine("                global::Avalonia.Controls.Grid.SetRow(toolbar, 1);")
                .AppendLine("                layout.Children.Add(toolbar);")
                .AppendLine("            }");
        }
        if (model.Recipe >= 3)
        {
            builder.AppendLine("            if (recipeContent is not null)")
                .AppendLine("            {")
                .AppendLine("                global::Avalonia.Controls.Grid.SetRow(recipeContent, 3);")
                .AppendLine("                layout.Children.Add(recipeContent);")
                .AppendLine("            }");
        }
        builder
            .Append("            layout.Children.Add(")
            .Append(model.ViewState != null ? "generatedStateHost" : "dataGrid")
            .AppendLine(");")
            .AppendLine("            return layout;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"The generated DataGrid is paired with strict generated column definitions and fast-path accessors before it evaluates row members.\")]")
            .AppendLine("        protected virtual global::Avalonia.Controls.DataGrid CreateGeneratedDataGrid()")
            .AppendLine("        {")
            .AppendLine("            var dataGrid = new global::Avalonia.Controls.DataGrid")
            .AppendLine("            {")
            .AppendLine("                Name = \"GeneratedDataGrid\",")
            .AppendLine("                AutoGenerateColumns = false,")
            .AppendLine("                CanUserSortColumns = true,")
            .Append("                IsReadOnly = ").Append(model.IsReadOnly ? "true" : "false").AppendLine(",")
            .Append("                CanUserAddRows = ").Append(model.CanUserAddRows ? "true" : "false").AppendLine(",")
            .Append("                CanUserDeleteRows = ").Append(model.CanUserDeleteRows ? "true" : "false").AppendLine(",")
            .Append("                EditTriggers = (global::Avalonia.Controls.DataGridEditTriggers)")
            .Append(model.EditTriggers.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                RestrictTextInputEditToCells = ").Append(model.RestrictTextInputEditToCells ? "true" : "false").AppendLine(",")
            .Append("                ClipboardCopyMode = (global::Avalonia.Controls.DataGridClipboardCopyMode)")
            .Append(model.ClipboardCopyMode.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                ShowTotalSummary = ").Append(model.ShowTotalSummary ? "true" : "false").AppendLine(",")
            .Append("                ShowGroupSummary = ").Append(model.ShowGroupSummary ? "true" : "false").AppendLine(",")
            .Append("                TotalSummaryPosition = (global::Avalonia.Controls.DataGridSummaryRowPosition)")
            .Append(model.TotalSummaryPosition.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                GroupSummaryPosition = (global::Avalonia.Controls.DataGridGroupSummaryPosition)")
            .Append(model.GroupSummaryPosition.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .AppendLine("                GridLinesVisibility = global::Avalonia.Controls.DataGridGridLinesVisibility.Horizontal")
            .AppendLine("            };")
            .AppendLine("            var editingInteractionModelFactory = CreateGeneratedEditingInteractionModelFactory();")
            .AppendLine("            dataGrid.EditingInteractionModelFactory = editingInteractionModelFactory;")
            .AppendLine("            dataGrid.EditingInteractionModel = editingInteractionModelFactory.Create();")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(dataGrid, GeneratedAutomationId);")
            .Append("            global::Avalonia.Automation.AutomationProperties.SetName(dataGrid, ").Append(GeneratorUtilities.EscapeString(model.Title)).AppendLine(");")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetHelpText(dataGrid, \"Generated reflection-free ProDataGrid view.\");");
        if (model.DataGridThemeKey != null)
        {
            builder.AppendLine("            dataGrid.Bind(global::Avalonia.Controls.Primitives.TemplatedControl.ThemeProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(GeneratedDataGridThemeKey!));");
        }
        EmitGeneratedClasses(builder, "dataGrid", model.DataGridClasses, 12);
        if (model.HierarchicalModel == null)
        {
            builder.AppendLine("            dataGrid[!global::Avalonia.Controls.DataGrid.ItemsSourceProperty] = CreateBinding(s_itemsProperty, global::Avalonia.Data.BindingMode.OneWay);");
        }
        builder
            .AppendLine("            dataGrid[!global::Avalonia.Controls.DataGrid.ColumnDefinitionsSourceProperty] = CreateBinding(s_columnDefinitionsProperty, global::Avalonia.Data.BindingMode.OneWay);")
            .AppendLine("            dataGrid[!global::Avalonia.Controls.DataGrid.FastPathOptionsProperty] = CreateBinding(s_fastPathOptionsProperty, global::Avalonia.Data.BindingMode.OneWay);");

        if (model.LayoutModel != null)
        {
            builder.AppendLine("            dataGrid.UseLogicalScrollable = true;");
            EmitOptionalGridBinding(builder, model.LayoutModel, "LayoutModel", "s_layoutModelProperty");
        }
        else
        {
            EmitGeneratedLayout(builder, model);
        }
        EmitLayoutItemTemplateConfiguration(builder, model);

        EmitOptionalGridBinding(builder, model.SortingModel, "SortingModel", "s_sortingModelProperty");
        if (model.HierarchicalModel == null)
        {
            EmitOptionalGridBinding(builder, model.FilteringModel, "FilteringModel", "s_filteringModelProperty");
        }
        EmitOptionalGridBinding(builder, model.SearchModel, "SearchModel", "s_searchModelProperty");
        if (model.ConfigureSelection)
        {
            builder.Append("            dataGrid.SelectionMode = (global::Avalonia.Controls.DataGridSelectionMode)")
                .Append(model.SelectionMode.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
                .Append("            dataGrid.SelectionUnit = (global::Avalonia.Controls.DataGridSelectionUnit)")
                .Append(model.SelectionUnit.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        }
        EmitOptionalGridBinding(builder, model.SelectionModel, "Selection", "s_selectionModelProperty");
        EmitOptionalGridBinding(builder, model.NavigationModel, "NavigationModel", "s_navigationModelProperty");
        EmitOptionalGridBinding(builder, model.RouteNavigationModel, "RouteNavigationModel", "s_routeNavigationModelProperty");
        EmitOptionalGridBinding(builder, model.NavigationInputModel, "NavigationInputModel", "s_navigationInputModelProperty");
        EmitOptionalGridBinding(builder, model.RouteContextFactory, "RouteContextFactory", "s_routeContextFactoryProperty");
        EmitOptionalGridBinding(builder, model.ClipboardImportModel, "ClipboardImportModel", "s_clipboardImportModelProperty");
        EmitOptionalGridBinding(builder, model.FillModel, "FillModel", "s_fillModelProperty");
        EmitOptionalGridBinding(builder, model.FormulaModel, "FormulaModel", "s_formulaModelProperty");
        EmitOptionalGridBinding(
            builder,
            model.ConditionalFormattingModel,
            "ConditionalFormattingModel",
            "s_conditionalFormattingModelProperty");
        EmitOptionalGridBinding(builder, model.HierarchicalModel, "HierarchicalModel", "s_hierarchicalModelProperty");
        if (model.HierarchicalModel != null)
        {
            builder.AppendLine("            dataGrid.HierarchicalRowsEnabled = true;")
                .AppendLine("            dataGrid.Classes.Add(\"hierarchical\");");
        }
        if (model.HierarchicalModel != null && model.FilteringModel != null)
        {
            builder.AppendLine("            dataGrid.FilteringAdapterFactory = CreateGeneratedHierarchicalFilteringAdapterFactory();");
            EmitOptionalGridBinding(builder, model.FilteringModel, "FilteringModel", "s_filteringModelProperty");
        }
        EmitRowDetailsConfiguration(builder, model);

        builder.AppendLine("            ConfigureGeneratedPerformanceAndInput(dataGrid);")
            .AppendLine("            ConfigureGeneratedDataGrid(dataGrid);");
        if (model.RoutedEventCommand != null && model.Framework != ViewFrameworkModel.ReactiveUI)
        {
            builder.AppendLine("            ConfigureGeneratedRoutedEventCommands(dataGrid);");
        }
        if (model.InputCommand != null && model.Framework != ViewFrameworkModel.ReactiveUI)
        {
            builder.AppendLine("            ConfigureGeneratedInputCommand(dataGrid);");
        }
        builder
            .AppendLine("            return dataGrid;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual void ConfigureGeneratedDataGrid(global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("        {")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual global::Avalonia.Controls.DataGridEditing.IDataGridEditingInteractionModelFactory CreateGeneratedEditingInteractionModelFactory()")
            .AppendLine("            => new global::Avalonia.Controls.DataGridEditing.DataGridGeneratedEditingInteractionModelFactory(")
            .AppendLine("                new global::Avalonia.Controls.DataGridEditing.DataGridGeneratedEditingInteractionProfile(")
            .Append("                    (global::Avalonia.Controls.DataGridEditTriggers)").Append(model.EditTriggers.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                    ").Append(model.RestrictTextInputEditToCells ? "true" : "false").AppendLine(",")
            .Append("                    (global::Avalonia.Input.KeyModifiers)").Append(model.RequiredPointerEditModifiers.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
            .Append("                    ").Append(model.RequireExactPointerEditModifiers ? "true" : "false").AppendLine("));")
            .AppendLine();

        if (model.HierarchicalModel != null && model.FilteringModel != null)
        {
            builder.AppendLine("        protected virtual global::Avalonia.Controls.DataGridFiltering.IDataGridFilteringAdapterFactory CreateGeneratedHierarchicalFilteringAdapterFactory()")
                .AppendLine("            => new global::Avalonia.Controls.DataGridFiltering.DataGridHierarchicalFilteringAdapterFactory")
                .AppendLine("            {")
                .Append("                Policy = (global::Avalonia.Controls.DataGridFiltering.DataGridHierarchyFilterPolicy)")
                .Append(model.HierarchyFilterPolicy.ToString(CultureInfo.InvariantCulture)).AppendLine()
                .AppendLine("            };")
                .AppendLine();
        }

        EmitGeneratedRoutedEventMembers(builder, model);
        EmitGeneratedPerformanceIntegrationMembers(builder, model);
        EmitGeneratedReactiveActivationMembers(builder, model);
        EmitGeneratedViewStateMembers(builder, model);

        builder.AppendLine("        protected virtual global::Avalonia.Controls.Control? CreateGeneratedToolbar()")
            .AppendLine("        {");
        if (model.Recipe < 2)
        {
            builder.AppendLine("            return null;");
        }
        else
        {
            builder.AppendLine("            var slot = new global::Avalonia.Controls.ContentControl { Name = \"GeneratedToolbarSlot\" };")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(slot, GeneratedAutomationId + \"-toolbar\");")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(slot, \"Grid operations\");")
                .AppendLine("            return slot;");
        }
        builder.AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual global::Avalonia.Controls.Control? CreateGeneratedRecipeContent()")
            .AppendLine("        {");
        string? recipeSlotName = model.Recipe switch
        {
            3 => "GeneratedExplorerSlot",
            4 => "GeneratedFormulaBarSlot",
            5 => "GeneratedAnalyticsSlot",
            6 => "GeneratedDetailsSlot",
            _ => null
        };
        if (recipeSlotName == null)
        {
            builder.AppendLine("            return null;");
        }
        else
        {
            builder.Append("            var slot = new global::Avalonia.Controls.ContentControl { Name = ")
                .Append(GeneratorUtilities.EscapeString(recipeSlotName)).AppendLine(" };")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(slot, GeneratedAutomationId + \"-recipe\");")
                .Append("            global::Avalonia.Automation.AutomationProperties.SetName(slot, ")
                .Append(GeneratorUtilities.EscapeString(recipeSlotName)).AppendLine(");")
                .AppendLine("            return slot;");
        }
        builder.AppendLine("        }");

        if (model.StateController != null)
        {
            string stateProperty = GeneratorUtilities.EscapeIdentifier(model.StateController.PropertyName);
            builder.AppendLine()
                .AppendLine("        public global::Avalonia.Controls.DataGridGeneratedStateEnvelope CaptureGeneratedState(")
                .AppendLine("            global::Avalonia.Controls.DataGridStateSections sections = global::Avalonia.Controls.DataGridStateSections.All)")
                .AppendLine("        {")
                .Append("            var viewModel = DataContext as ").Append(viewModelType)
                .AppendLine(" ?? throw new global::System.InvalidOperationException(\"Generated view requires its declared view model.\");")
                .Append("            return ((global::Avalonia.Controls.DataGridGeneratedStateController)(object)viewModel.")
                .Append(stateProperty).AppendLine(").Capture(GeneratedDataGrid, sections);")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        public void RestoreGeneratedState(")
                .AppendLine("            global::Avalonia.Controls.DataGridGeneratedStateEnvelope envelope,")
                .AppendLine("            global::Avalonia.Controls.DataGridStateSections sections = global::Avalonia.Controls.DataGridStateSections.All)")
                .AppendLine("        {")
                .Append("            var viewModel = DataContext as ").Append(viewModelType)
                .AppendLine(" ?? throw new global::System.InvalidOperationException(\"Generated view requires its declared view model.\");")
                .Append("            ((global::Avalonia.Controls.DataGridGeneratedStateController)(object)viewModel.")
                .Append(stateProperty).AppendLine(").Restore(GeneratedDataGrid, envelope, sections);")
                .AppendLine("        }");
        }

        EmitGeneratedRowDetailsMembers(builder, model);

        if (model.ViewState != null)
        {
            builder.AppendLine()
                .AppendLine("        private sealed class GeneratedViewStateVisibilityConverter : global::Avalonia.Data.Converters.IValueConverter")
                .AppendLine("        {")
                .AppendLine("            private readonly global::Avalonia.Controls.DataGridGeneratedViewState _expected;")
                .AppendLine()
                .AppendLine("            public GeneratedViewStateVisibilityConverter(global::Avalonia.Controls.DataGridGeneratedViewState expected)")
                .AppendLine("            {")
                .AppendLine("                _expected = expected;")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            public object Convert(object? value, global::System.Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)")
                .AppendLine("                => value is global::Avalonia.Controls.DataGridGeneratedViewState state && state == _expected;")
                .AppendLine()
                .AppendLine("            public object ConvertBack(object? value, global::System.Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)")
                .AppendLine("                => global::Avalonia.AvaloniaProperty.UnsetValue;")
                .AppendLine("        }");
        }

        if (model.ErrorMessage != null)
        {
            builder.AppendLine()
                .AppendLine("        private sealed class GeneratedErrorMessageConverter : global::Avalonia.Data.Converters.IValueConverter")
                .AppendLine("        {")
                .AppendLine("            private readonly string _fallback;")
                .AppendLine()
                .AppendLine("            public GeneratedErrorMessageConverter(string fallback)")
                .AppendLine("            {")
                .AppendLine("                _fallback = fallback;")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            public object Convert(object? value, global::System.Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)")
                .AppendLine("                => value as string ?? _fallback;")
                .AppendLine()
                .AppendLine("            public object ConvertBack(object? value, global::System.Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)")
                .AppendLine("                => global::Avalonia.AvaloniaProperty.UnsetValue;")
                .AppendLine("        }");
        }

        builder.AppendLine()
            .AppendLine("        private static global::Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindingExtension CreateBinding(")
            .AppendLine("            global::Avalonia.Data.Core.IPropertyInfo property,")
            .AppendLine("            global::Avalonia.Data.BindingMode mode,")
            .AppendLine("            global::Avalonia.Data.Converters.IValueConverter? converter = null)")
            .AppendLine("        {")
            .AppendLine("            return new global::Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindingExtension")
            .AppendLine("            {")
            .Append("                DataType = typeof(").Append(viewModelType).AppendLine("),")
            .AppendLine("                Mode = mode,")
            .AppendLine("                Converter = converter,")
            .AppendLine("                Path = new global::Avalonia.Data.CompiledBindingPathBuilder()")
            .AppendLine("                    .Property(property, global::Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings.PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)")
            .AppendLine("                    .Build()")
            .AppendLine("            };")
            .AppendLine("        }")
            .AppendLine("    }");
        CloseNamespace(builder, model.ViewNamespace);
        return builder.ToString();
    }

    private static void EmitGeneratedRoutedEventMembers(StringBuilder builder, ViewModelViewModel model)
    {
        if (model.RoutedEventCommand == null)
        {
            return;
        }

        const int selectionChanged = 1 << 0;
        const int currentCellChanged = 1 << 1;
        const int sorting = 1 << 2;
        const int beginningEdit = 1 << 3;
        const int cellEditEnding = 1 << 4;
        const int cellEditEnded = 1 << 5;
        const int rowEditEnding = 1 << 6;
        const int rowEditEnded = 1 << 7;
        const int selectionChanging = 1 << 8;
        const int cellPrepared = 1 << 9;
        const int cellClearing = 1 << 10;
        const int cellValueChanged = 1 << 11;
        string itemType = model.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string viewModelType = model.ViewModelType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string commandProperty = GeneratorUtilities.EscapeIdentifier(model.RoutedEventCommand.PropertyName);

        builder.AppendLine("        protected virtual void ConfigureGeneratedRoutedEventCommands(global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("        {");
        if ((model.RoutedEvents & selectionChanged) != 0)
        {
            builder.AppendLine("            dataGrid.SelectionChanged += OnGeneratedSelectionChanged;");
        }
        if ((model.RoutedEvents & currentCellChanged) != 0)
        {
            builder.AppendLine("            dataGrid.CurrentCellChanged += OnGeneratedCurrentCellChanged;");
        }
        if ((model.RoutedEvents & sorting) != 0)
        {
            builder.AppendLine("            dataGrid.Sorting += OnGeneratedSorting;");
        }
        if ((model.RoutedEvents & beginningEdit) != 0)
        {
            builder.AppendLine("            dataGrid.BeginningEdit += OnGeneratedBeginningEdit;");
        }
        if ((model.RoutedEvents & cellEditEnding) != 0)
        {
            builder.AppendLine("            dataGrid.CellEditEnding += OnGeneratedCellEditEnding;");
        }
        if ((model.RoutedEvents & cellEditEnded) != 0)
        {
            builder.AppendLine("            dataGrid.CellEditEnded += OnGeneratedCellEditEnded;");
        }
        if ((model.RoutedEvents & rowEditEnding) != 0)
        {
            builder.AppendLine("            dataGrid.RowEditEnding += OnGeneratedRowEditEnding;");
        }
        if ((model.RoutedEvents & rowEditEnded) != 0)
        {
            builder.AppendLine("            dataGrid.RowEditEnded += OnGeneratedRowEditEnded;");
        }
        if ((model.RoutedEvents & selectionChanging) != 0)
        {
            builder.AppendLine("            dataGrid.SelectionChanging += OnGeneratedSelectionChanging;");
        }
        if ((model.RoutedEvents & cellPrepared) != 0)
        {
            builder.AppendLine("            dataGrid.CellPrepared += OnGeneratedCellPrepared;");
        }
        if ((model.RoutedEvents & cellClearing) != 0)
        {
            builder.AppendLine("            dataGrid.CellClearing += OnGeneratedCellClearing;");
        }
        if ((model.RoutedEvents & cellValueChanged) != 0)
        {
            builder.AppendLine("            dataGrid.CellValueChanged += OnGeneratedCellValueChanged;");
        }
        builder.AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual void DetachGeneratedRoutedEventCommands(global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("        {");
        if ((model.RoutedEvents & selectionChanged) != 0)
        {
            builder.AppendLine("            dataGrid.SelectionChanged -= OnGeneratedSelectionChanged;");
        }
        if ((model.RoutedEvents & currentCellChanged) != 0)
        {
            builder.AppendLine("            dataGrid.CurrentCellChanged -= OnGeneratedCurrentCellChanged;");
        }
        if ((model.RoutedEvents & sorting) != 0)
        {
            builder.AppendLine("            dataGrid.Sorting -= OnGeneratedSorting;");
        }
        if ((model.RoutedEvents & beginningEdit) != 0)
        {
            builder.AppendLine("            dataGrid.BeginningEdit -= OnGeneratedBeginningEdit;");
        }
        if ((model.RoutedEvents & cellEditEnding) != 0)
        {
            builder.AppendLine("            dataGrid.CellEditEnding -= OnGeneratedCellEditEnding;");
        }
        if ((model.RoutedEvents & cellEditEnded) != 0)
        {
            builder.AppendLine("            dataGrid.CellEditEnded -= OnGeneratedCellEditEnded;");
        }
        if ((model.RoutedEvents & rowEditEnding) != 0)
        {
            builder.AppendLine("            dataGrid.RowEditEnding -= OnGeneratedRowEditEnding;");
        }
        if ((model.RoutedEvents & rowEditEnded) != 0)
        {
            builder.AppendLine("            dataGrid.RowEditEnded -= OnGeneratedRowEditEnded;");
        }
        if ((model.RoutedEvents & selectionChanging) != 0)
        {
            builder.AppendLine("            dataGrid.SelectionChanging -= OnGeneratedSelectionChanging;");
        }
        if ((model.RoutedEvents & cellPrepared) != 0)
        {
            builder.AppendLine("            dataGrid.CellPrepared -= OnGeneratedCellPrepared;");
        }
        if ((model.RoutedEvents & cellClearing) != 0)
        {
            builder.AppendLine("            dataGrid.CellClearing -= OnGeneratedCellClearing;");
        }
        if ((model.RoutedEvents & cellValueChanged) != 0)
        {
            builder.AppendLine("            dataGrid.CellValueChanged -= OnGeneratedCellValueChanged;");
        }
        builder.AppendLine("        }")
            .AppendLine()
            .Append("        private void ExecuteGeneratedRoutedEventCommand(global::Avalonia.Controls.DataGridGeneratedViewEvent<")
            .Append(itemType).AppendLine("> eventData)")
            .AppendLine("        {")
            .Append("            if (DataContext is not ").Append(viewModelType).AppendLine(" viewModel)")
            .AppendLine("            {")
            .AppendLine("                return;")
            .AppendLine("            }")
            .AppendLine()
            .Append("            global::System.Windows.Input.ICommand? command = viewModel.").Append(commandProperty).AppendLine(";")
            .AppendLine("            if (command is not null && command.CanExecute(eventData))")
            .AppendLine("            {")
            .AppendLine("                command.Execute(eventData);")
            .AppendLine("            }")
            .AppendLine("        }")
            .AppendLine()
            .Append("        private static ").Append(itemType).AppendLine(" GetGeneratedEventItem(object? value)")
            .Append("            => value is ").Append(itemType).AppendLine(" item ? item : default!;")
            .AppendLine()
            .AppendLine("        private static string GetGeneratedEventColumnKey(global::Avalonia.Controls.DataGridColumn? column)")
            .AppendLine("            => column?.ColumnKey?.ToString() ?? column?.SortMemberPath ?? string.Empty;")
            .AppendLine();

        if ((model.RoutedEvents & selectionChanged) != 0)
        {
            builder.AppendLine("        private void OnGeneratedSelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)")
                .AppendLine("        {")
                .AppendLine("            var details = e as global::Avalonia.Controls.DataGridSelectionChangedEventArgs;")
                .Append("            var eventData = global::Avalonia.Controls.DataGridGeneratedViewEvent<").Append(itemType).AppendLine(">.CreateSelectionChanged(")
                .AppendLine("                e.AddedItems,")
                .AppendLine("                e.RemovedItems,")
                .AppendLine("                details?.Source ?? global::Avalonia.Controls.DataGridSelectionChangeSource.Unknown,")
                .AppendLine("                details?.IsUserInitiated ?? false);")
                .AppendLine("            eventData.Handled = e.Handled;")
                .AppendLine("            ExecuteGeneratedRoutedEventCommand(eventData);")
                .AppendLine("            e.Handled = eventData.Handled;")
                .AppendLine("        }")
                .AppendLine();
        }

        if ((model.RoutedEvents & currentCellChanged) != 0)
        {
            builder.AppendLine("        private void OnGeneratedCurrentCellChanged(object? sender, global::Avalonia.Controls.DataGridCurrentCellChangedEventArgs e)")
                .AppendLine("        {")
                .Append("            var eventData = global::Avalonia.Controls.DataGridGeneratedViewEvent<").Append(itemType).AppendLine(">.CreateCurrentCellChanged(")
                .AppendLine("                GetGeneratedEventItem(e.OldItem),")
                .AppendLine("                GetGeneratedEventColumnKey(e.OldColumn),")
                .AppendLine("                GetGeneratedEventItem(e.NewItem),")
                .AppendLine("                GetGeneratedEventColumnKey(e.NewColumn));")
                .AppendLine("            eventData.Handled = e.Handled;")
                .AppendLine("            ExecuteGeneratedRoutedEventCommand(eventData);")
                .AppendLine("            e.Handled = eventData.Handled;")
                .AppendLine("        }")
                .AppendLine();
        }

        if ((model.RoutedEvents & sorting) != 0)
        {
            builder.AppendLine("        private void OnGeneratedSorting(object? sender, global::Avalonia.Controls.DataGridColumnEventArgs e)")
                .AppendLine("        {")
                .Append("            var eventData = global::Avalonia.Controls.DataGridGeneratedViewEvent<").Append(itemType).AppendLine(">.CreateSorting(")
                .AppendLine("                GetGeneratedEventColumnKey(e.Column));")
                .AppendLine("            eventData.Handled = e.Handled;")
                .AppendLine("            ExecuteGeneratedRoutedEventCommand(eventData);")
                .AppendLine("            e.Handled = eventData.Handled;")
                .AppendLine("        }")
                .AppendLine();
        }

        if ((model.RoutedEvents & beginningEdit) != 0)
        {
            EmitGeneratedEditEventHandler(
                builder,
                itemType,
                "BeginningEdit",
                "DataGridBeginningEditEventArgs",
                "BeginningEdit",
                "null",
                hasColumn: true,
                isCancelable: true);
        }
        if ((model.RoutedEvents & cellEditEnding) != 0)
        {
            EmitGeneratedEditEventHandler(
                builder,
                itemType,
                "CellEditEnding",
                "DataGridCellEditEndingEventArgs",
                "CellEditEnding",
                "e.EditAction",
                hasColumn: true,
                isCancelable: true);
        }
        if ((model.RoutedEvents & cellEditEnded) != 0)
        {
            EmitGeneratedEditEventHandler(
                builder,
                itemType,
                "CellEditEnded",
                "DataGridCellEditEndedEventArgs",
                "CellEditEnded",
                "e.EditAction",
                hasColumn: true,
                isCancelable: false);
        }
        if ((model.RoutedEvents & rowEditEnding) != 0)
        {
            EmitGeneratedEditEventHandler(
                builder,
                itemType,
                "RowEditEnding",
                "DataGridRowEditEndingEventArgs",
                "RowEditEnding",
                "e.EditAction",
                hasColumn: false,
                isCancelable: true);
        }
        if ((model.RoutedEvents & rowEditEnded) != 0)
        {
            EmitGeneratedEditEventHandler(
                builder,
                itemType,
                "RowEditEnded",
                "DataGridRowEditEndedEventArgs",
                "RowEditEnded",
                "e.EditAction",
                hasColumn: false,
                isCancelable: false);
        }
        if ((model.RoutedEvents & selectionChanging) != 0)
        {
            builder.AppendLine("        private void OnGeneratedSelectionChanging(object? sender, global::Avalonia.Controls.DataGridSelectionChangingEventArgs e)")
                .AppendLine("        {")
                .Append("            var eventData = global::Avalonia.Controls.DataGridGeneratedViewEvent<").Append(itemType).AppendLine(">.CreateSelectionChanging(e);")
                .AppendLine("            ExecuteGeneratedRoutedEventCommand(eventData);")
                .AppendLine("            e.Cancel = eventData.Cancel;")
                .AppendLine("            if (e.TriggerEvent is not null)")
                .AppendLine("            {")
                .AppendLine("                e.TriggerEvent.Handled = eventData.Handled;")
                .AppendLine("            }")
                .AppendLine("        }")
                .AppendLine();
        }
        if ((model.RoutedEvents & cellPrepared) != 0)
        {
            EmitGeneratedCellLifecycleEventHandler(builder, itemType, "CellPrepared");
        }
        if ((model.RoutedEvents & cellClearing) != 0)
        {
            EmitGeneratedCellLifecycleEventHandler(builder, itemType, "CellClearing");
        }
        if ((model.RoutedEvents & cellValueChanged) != 0)
        {
            builder.AppendLine("        private void OnGeneratedCellValueChanged(object? sender, global::Avalonia.Controls.DataGridCellValueChangedEventArgs e)")
                .AppendLine("        {")
                .Append("            var eventData = global::Avalonia.Controls.DataGridGeneratedViewEvent<").Append(itemType).AppendLine(">.CreateCellValueChanged(")
                .AppendLine("                e,")
                .AppendLine("                GetGeneratedEventColumnKey(e.Column));")
                .AppendLine("            ExecuteGeneratedRoutedEventCommand(eventData);")
                .AppendLine("        }")
                .AppendLine();
        }
    }

    private static void EmitGeneratedCellLifecycleEventHandler(
        StringBuilder builder,
        string itemType,
        string eventName)
    {
        builder.Append("        private void OnGenerated").Append(eventName)
            .AppendLine("(object? sender, global::Avalonia.Controls.DataGridCellLifecycleEventArgs e)")
            .AppendLine("        {")
            .Append("            var eventData = global::Avalonia.Controls.DataGridGeneratedViewEvent<").Append(itemType)
            .AppendLine(">.CreateCellLifecycle(")
            .Append("                global::Avalonia.Controls.DataGridGeneratedViewEventKinds.").Append(eventName).AppendLine(",")
            .AppendLine("                e,")
            .AppendLine("                GetGeneratedEventColumnKey(e.Column));")
            .AppendLine("            ExecuteGeneratedRoutedEventCommand(eventData);")
            .AppendLine("        }")
            .AppendLine();
    }

    private static void EmitGeneratedPerformanceIntegrationMembers(StringBuilder builder, ViewModelViewModel model)
    {
        string itemType = model.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string viewModelType = model.ViewModelType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        builder.AppendLine("        protected virtual global::Avalonia.Controls.DataGridGeneratedPerformanceOptions CreateGeneratedPerformanceOptions()")
            .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedPerformanceOptions.Create(GeneratedPerformanceProfile);")
            .AppendLine()
            .AppendLine("        protected virtual global::Avalonia.Controls.IDataGridGeneratedInputMap CreateGeneratedInputMap()");
        if (model.InputMapType == null)
        {
            builder.AppendLine("            => global::Avalonia.Controls.DataGridGeneratedInputMap.Create(GeneratedPerformanceProfile);");
        }
        else
        {
            builder.Append("            => new ")
                .Append(model.InputMapType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat)).AppendLine("();");
        }
        builder.AppendLine()
            .AppendLine("        private void ConfigureGeneratedPerformanceAndInput(global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("        {")
            .AppendLine("            CreateGeneratedPerformanceOptions().Apply(dataGrid);")
            .AppendLine("            global::Avalonia.Controls.IDataGridGeneratedInputMap inputMap = CreateGeneratedInputMap()")
            .AppendLine("                ?? throw new global::System.InvalidOperationException(\"Generated input map factory returned null.\");")
            .AppendLine("            _generatedInputMap = inputMap;")
            .AppendLine("            global::Avalonia.Input.KeyModifiers commandModifiers = GetGeneratedCommandModifiers();")
            .AppendLine("            dataGrid.KeyboardGestureOverrides = inputMap.CreateKeyboardGestureOverrides(commandModifiers);")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        private global::Avalonia.Input.KeyModifiers GetGeneratedCommandModifiers()")
            .AppendLine("        {")
            .AppendLine("            global::Avalonia.Platform.IPlatformSettings? platformSettings =")
            .AppendLine("                global::Avalonia.VisualTree.VisualExtensions.GetPlatformSettings(this) ?? global::Avalonia.Application.Current?.PlatformSettings;")
            .AppendLine("            return platformSettings?.HotkeyConfiguration.CommandModifiers ?? global::Avalonia.Input.KeyModifiers.Control;")
            .AppendLine("        }")
            .AppendLine();

        if (model.InputCommand != null)
        {
            string propertyName = GeneratorUtilities.EscapeIdentifier(model.InputCommand.PropertyName);
            builder.AppendLine("        protected virtual void ConfigureGeneratedInputCommand(global::Avalonia.Controls.DataGrid dataGrid)")
                .AppendLine("        {")
                .AppendLine("            dataGrid.KeyDown += OnGeneratedInputKeyDown;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        protected virtual void DetachGeneratedInputCommand(global::Avalonia.Controls.DataGrid dataGrid)")
                .AppendLine("        {")
                .AppendLine("            dataGrid.KeyDown -= OnGeneratedInputKeyDown;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        private void OnGeneratedInputKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs args)")
                .AppendLine("        {")
                .AppendLine("            if (args.Handled || sender is not global::Avalonia.Controls.DataGrid dataGrid ||")
                .AppendLine("                _generatedInputMap is not { } inputMap ||")
                .AppendLine("                !inputMap.TryMatch(args.Key, args.KeyModifiers, GetGeneratedCommandModifiers(), out global::Avalonia.Controls.DataGridGeneratedInputAction action) ||")
                .Append("                DataContext is not ").Append(viewModelType).AppendLine(" viewModel)")
                .AppendLine("            {")
                .AppendLine("                return;")
                .AppendLine("            }")
                .AppendLine()
                .Append("            ").Append(itemType).AppendLine(" item = dataGrid.SelectedItem is " + itemType + " typedItem ? typedItem : default!;")
                .Append("            var input = new global::Avalonia.Controls.DataGridGeneratedInputEvent<")
                .Append(itemType).AppendLine(">(")
                .AppendLine("                action,")
                .AppendLine("                args.Key,")
                .AppendLine("                args.KeyModifiers,")
                .AppendLine("                item,")
                .AppendLine("                dataGrid.SelectedIndex,")
                .AppendLine("                dataGrid.CurrentColumn?.DisplayIndex ?? -1);")
                .Append("            global::System.Windows.Input.ICommand command = viewModel.").Append(propertyName).AppendLine(";")
                .AppendLine("            if (command.CanExecute(input))")
                .AppendLine("            {")
                .AppendLine("                command.Execute(input);")
                .AppendLine("                args.Handled = input.Handled;")
                .AppendLine("            }")
                .AppendLine("        }")
                .AppendLine();
        }

        if (model.DiagnosticsSinkType != null)
        {
            builder.AppendLine("        protected virtual global::Avalonia.Controls.IDataGridGeneratedMetricsSink CreateGeneratedMetricsSink()")
                .Append("            => new ")
                .Append(model.DiagnosticsSinkType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat)).AppendLine("();")
                .AppendLine()
                .AppendLine("        private global::System.IDisposable CreateGeneratedMetricsSubscription()")
                .AppendLine("            => global::Avalonia.Controls.DataGridGeneratedMetricsBridge.Subscribe(")
                .AppendLine("                GeneratedDiagnosticsSchemaId,")
                .AppendLine("                GeneratedPerformanceProfile,")
                .AppendLine("                CreateGeneratedMetricsSink() ?? throw new global::System.InvalidOperationException(\"Generated metrics sink factory returned null.\"));")
                .AppendLine();
            if (model.Framework == ViewFrameworkModel.Avalonia)
            {
                builder.AppendLine("        private void ConfigureGeneratedAvaloniaMetricsLifetime()")
                    .AppendLine("        {")
                    .AppendLine("            AttachedToVisualTree += OnGeneratedMetricsAttached;")
                    .AppendLine("            DetachedFromVisualTree += OnGeneratedMetricsDetached;")
                    .AppendLine("        }")
                    .AppendLine()
                    .AppendLine("        private void OnGeneratedMetricsAttached(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs args)")
                    .AppendLine("        {")
                    .AppendLine("            _generatedMetricsSubscription?.Dispose();")
                    .AppendLine("            _generatedMetricsSubscription = CreateGeneratedMetricsSubscription();")
                    .AppendLine("        }")
                    .AppendLine()
                    .AppendLine("        private void OnGeneratedMetricsDetached(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs args)")
                    .AppendLine("        {")
                    .AppendLine("            _generatedMetricsSubscription?.Dispose();")
                    .AppendLine("            _generatedMetricsSubscription = null;")
                    .AppendLine("        }")
                    .AppendLine();
            }
        }
    }

    private static void EmitGeneratedReactiveActivationMembers(StringBuilder builder, ViewModelViewModel model)
    {
        if (model.Framework != ViewFrameworkModel.ReactiveUI ||
            (model.RoutedEventCommand == null && model.Interactions.IsDefaultOrEmpty &&
                model.NavigationInteraction == null && model.InputCommand == null && model.DiagnosticsSinkType == null))
        {
            return;
        }

        builder.AppendLine("        private void ConfigureGeneratedReactiveActivation(global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("        {")
            .AppendLine("            global::ReactiveUI.ViewForMixins.WhenActivated(")
            .AppendLine("                (global::ReactiveUI.IActivatableView)this,")
            .AppendLine("                (global::System.Action<global::System.Action<global::System.IDisposable>>)(dispose =>")
            .AppendLine("                {");
        if (model.RoutedEventCommand != null)
        {
            builder.AppendLine("                    ConfigureGeneratedRoutedEventCommands(dataGrid);")
                .AppendLine("                    dispose(new GeneratedRoutedEventSubscription(this, dataGrid));");
        }
        if (!model.Interactions.IsDefaultOrEmpty)
        {
            builder.AppendLine("                    dispose(new GeneratedInteractionSubscription(this, dataGrid));");
        }
        if (model.NavigationInteraction != null)
        {
            builder.AppendLine("                    dispose(new GeneratedNavigationInteractionSubscription(this, dataGrid));");
        }
        if (model.InputCommand != null)
        {
            builder.AppendLine("                    ConfigureGeneratedInputCommand(dataGrid);")
                .AppendLine("                    dispose(new GeneratedInputSubscription(this, dataGrid));");
        }
        if (model.DiagnosticsSinkType != null)
        {
            builder.AppendLine("                    dispose(CreateGeneratedMetricsSubscription());");
        }
        builder.AppendLine("                }));")
            .AppendLine("        }")
            .AppendLine();

        if (!model.Interactions.IsDefaultOrEmpty)
        {
            EmitGeneratedInteractionMembers(builder, model);
        }
        if (model.NavigationInteraction != null)
        {
            EmitGeneratedNavigationInteractionMembers(builder, model);
        }

        if (model.RoutedEventCommand != null)
        {
            builder.AppendLine("        private sealed class GeneratedRoutedEventSubscription : global::System.IDisposable")
                .AppendLine("        {")
                .Append("            private ").Append(model.ViewName).AppendLine("? _view;")
                .AppendLine("            private global::Avalonia.Controls.DataGrid? _dataGrid;")
                .AppendLine()
                .Append("            public GeneratedRoutedEventSubscription(").Append(model.ViewName)
                .AppendLine(" view, global::Avalonia.Controls.DataGrid dataGrid)")
                .AppendLine("            {")
                .AppendLine("                _view = view;")
                .AppendLine("                _dataGrid = dataGrid;")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            public void Dispose()")
                .AppendLine("            {")
                .Append("                ").Append(model.ViewName).AppendLine("? view = _view;")
                .AppendLine("                global::Avalonia.Controls.DataGrid? dataGrid = _dataGrid;")
                .AppendLine("                _view = null;")
                .AppendLine("                _dataGrid = null;")
                .AppendLine("                if (view is not null && dataGrid is not null)")
                .AppendLine("                {")
                .AppendLine("                    view.DetachGeneratedRoutedEventCommands(dataGrid);")
                .AppendLine("                }")
                .AppendLine("            }")
                .AppendLine("        }")
                .AppendLine();
        }

        if (model.InputCommand != null)
        {
            builder.AppendLine("        private sealed class GeneratedInputSubscription : global::System.IDisposable")
                .AppendLine("        {")
                .Append("            private ").Append(model.ViewName).AppendLine("? _view;")
                .AppendLine("            private global::Avalonia.Controls.DataGrid? _dataGrid;")
                .AppendLine()
                .Append("            public GeneratedInputSubscription(").Append(model.ViewName)
                .AppendLine(" view, global::Avalonia.Controls.DataGrid dataGrid)")
                .AppendLine("            {")
                .AppendLine("                _view = view;")
                .AppendLine("                _dataGrid = dataGrid;")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            public void Dispose()")
                .AppendLine("            {")
                .Append("                ").Append(model.ViewName).AppendLine("? view = _view;")
                .AppendLine("                global::Avalonia.Controls.DataGrid? dataGrid = _dataGrid;")
                .AppendLine("                _view = null;")
                .AppendLine("                _dataGrid = null;")
                .AppendLine("                if (view is not null && dataGrid is not null)")
                .AppendLine("                {")
                .AppendLine("                    view.DetachGeneratedInputCommand(dataGrid);")
                .AppendLine("                }")
                .AppendLine("            }")
                .AppendLine("        }")
                .AppendLine();
        }
    }

    private static void EmitGeneratedInteractionMembers(StringBuilder builder, ViewModelViewModel model)
    {
        string viewModelType = model.ViewModelType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        for (int index = 0; index < model.Interactions.Length; index++)
        {
            ViewInteractionModel interaction = model.Interactions[index];
            string inputType = interaction.InputType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string outputType = interaction.OutputType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string handlerType = interaction.HandlerType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append("        protected virtual global::Avalonia.Controls.IDataGridGeneratedViewInteractionHandler<")
                .Append(inputType).Append(", ").Append(outputType).Append("> CreateGeneratedInteractionHandler")
                .Append(index).AppendLine("()")
                .Append("            => new ").Append(handlerType).AppendLine("();")
                .AppendLine();
        }

        builder.AppendLine("        private sealed class GeneratedInteractionSubscription : global::System.IObserver<object?>, global::System.IDisposable")
            .AppendLine("        {")
            .Append("            private ").Append(model.ViewName).AppendLine("? _view;")
            .AppendLine("            private global::Avalonia.Controls.DataGrid? _dataGrid;")
            .AppendLine("            private global::System.IDisposable? _dataContextSubscription;")
            .AppendLine("            private GeneratedInteractionLifetime? _interactionLifetime;");
        for (int index = 0; index < model.Interactions.Length; index++)
        {
            ViewInteractionModel interaction = model.Interactions[index];
            string inputType = interaction.InputType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string outputType = interaction.OutputType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            builder.Append("            private global::System.IDisposable? _registration").Append(index).AppendLine(";")
                .Append("            private global::Avalonia.Controls.IDataGridGeneratedViewInteractionHandler<")
                .Append(inputType).Append(", ").Append(outputType).Append(">? _handler").Append(index).AppendLine(";");
        }
        builder.AppendLine()
            .Append("            public GeneratedInteractionSubscription(").Append(model.ViewName)
            .AppendLine(" view, global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("            {")
            .AppendLine("                _view = view;")
            .AppendLine("                _dataGrid = dataGrid;")
            .AppendLine("                _dataContextSubscription = global::Avalonia.AvaloniaObjectExtensions")
            .AppendLine("                    .GetObservable(view, global::Avalonia.StyledElement.DataContextProperty)")
            .AppendLine("                    .Subscribe(this);")
            .AppendLine("            }")
            .AppendLine()
            .AppendLine("            public void OnNext(object? value)")
            .AppendLine("            {")
            .AppendLine("                DisconnectCurrent();")
            .AppendLine("                if (_view is not { } view || _dataGrid is not { } dataGrid ||")
            .Append("                    value is not ").Append(viewModelType).AppendLine(" viewModel)")
            .AppendLine("                {")
            .AppendLine("                    return;")
            .AppendLine("                }")
            .AppendLine()
            .AppendLine("                var interactionLifetime = new GeneratedInteractionLifetime();")
            .AppendLine("                _interactionLifetime = interactionLifetime;");
        for (int index = 0; index < model.Interactions.Length; index++)
        {
            ViewInteractionModel interaction = model.Interactions[index];
            string inputType = interaction.InputType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
            string propertyName = GeneratorUtilities.EscapeIdentifier(interaction.PropertyName);
            builder.Append("                var handler").Append(index).Append(" = view.CreateGeneratedInteractionHandler")
                .Append(index).AppendLine("();")
                .Append("                _handler").Append(index).Append(" = handler").Append(index).AppendLine(";")
                .Append("                _registration").Append(index).Append(" = viewModel.").Append(propertyName)
                .AppendLine(".RegisterHandler(async context =>")
                .AppendLine("                {")
                .Append("                    var output = await handler").Append(index)
                .Append(".HandleAsync(new global::Avalonia.Controls.DataGridGeneratedViewInteractionContext<")
                .Append(inputType).AppendLine(">(")
                .AppendLine("                        view,")
                .AppendLine("                        dataGrid,")
                .AppendLine("                        context.Input,")
                .AppendLine("                        interactionLifetime.CancellationToken));")
                .AppendLine("                    context.SetOutput(output);")
                .AppendLine("                });");
        }
        builder.AppendLine("            }")
            .AppendLine()
            .AppendLine("            public void OnError(global::System.Exception error) => Dispose();")
            .AppendLine()
            .AppendLine("            public void OnCompleted() => Dispose();")
            .AppendLine()
            .AppendLine("            public void Dispose()")
            .AppendLine("            {")
            .AppendLine("                global::System.IDisposable? subscription = _dataContextSubscription;")
            .AppendLine("                _dataContextSubscription = null;")
            .AppendLine("                _view = null;")
            .AppendLine("                _dataGrid = null;")
            .AppendLine("                subscription?.Dispose();")
            .AppendLine("                DisconnectCurrent();")
            .AppendLine("            }")
            .AppendLine()
            .AppendLine("            private void DisconnectCurrent()")
            .AppendLine("            {");
        for (int index = 0; index < model.Interactions.Length; index++)
        {
            builder.Append("                _registration").Append(index).AppendLine("?.Dispose();")
                .Append("                _registration").Append(index).AppendLine(" = null;");
        }
        builder.AppendLine("                _interactionLifetime?.Dispose();")
            .AppendLine("                _interactionLifetime = null;");
        for (int index = 0; index < model.Interactions.Length; index++)
        {
            builder.Append("                if (_handler").Append(index).AppendLine(" is global::System.IDisposable disposableHandler)")
                .AppendLine("                {")
                .AppendLine("                    disposableHandler.Dispose();")
                .AppendLine("                }")
                .Append("                _handler").Append(index).AppendLine(" = null;");
        }
        builder.AppendLine("            }")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        private sealed class GeneratedInteractionLifetime : global::System.IDisposable")
            .AppendLine("        {")
            .AppendLine("            private global::System.Threading.CancellationTokenSource? _source = new();")
            .AppendLine()
            .AppendLine("            public global::System.Threading.CancellationToken CancellationToken")
            .AppendLine("                => _source?.Token ?? new global::System.Threading.CancellationToken(canceled: true);")
            .AppendLine()
            .AppendLine("            public void Dispose()")
            .AppendLine("            {")
            .AppendLine("                global::System.Threading.CancellationTokenSource? source =")
            .AppendLine("                    global::System.Threading.Interlocked.Exchange(ref _source, null);")
            .AppendLine("                if (source is null)")
            .AppendLine("                {")
            .AppendLine("                    return;")
            .AppendLine("                }")
            .AppendLine()
            .AppendLine("                source.Cancel();")
            .AppendLine("                source.Dispose();")
            .AppendLine("            }")
            .AppendLine("        }")
            .AppendLine();
    }

    private static void EmitGeneratedNavigationInteractionMembers(StringBuilder builder, ViewModelViewModel model)
    {
        string itemType = model.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string viewModelType = model.ViewModelType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string propertyName = GeneratorUtilities.EscapeIdentifier(model.NavigationInteraction!.PropertyName);
        string requestType = "global::Avalonia.Controls.DataGridGeneratedNavigationRequest<" + itemType + ">";
        string resultType = "global::Avalonia.Controls.DataGridGeneratedNavigationResult<" + itemType + ">";
        string handlerType = "global::Avalonia.Controls.IDataGridGeneratedViewInteractionHandler<" + requestType + ", " + resultType + ">";

        builder.Append("        protected virtual ").Append(handlerType).AppendLine(" CreateGeneratedNavigationInteractionHandler()")
            .Append("            => new global::Avalonia.Controls.DataGridGeneratedNavigationHandler<").Append(itemType).AppendLine(">();")
            .AppendLine()
            .AppendLine("        private sealed class GeneratedNavigationInteractionSubscription : global::System.IObserver<object?>, global::System.IDisposable")
            .AppendLine("        {")
            .Append("            private ").Append(model.ViewName).AppendLine("? _view;")
            .AppendLine("            private global::Avalonia.Controls.DataGrid? _dataGrid;")
            .AppendLine("            private global::System.IDisposable? _dataContextSubscription;")
            .AppendLine("            private global::System.IDisposable? _registration;")
            .Append("            private ").Append(handlerType).AppendLine("? _handler;")
            .AppendLine("            private global::System.Threading.CancellationTokenSource? _lifetime;")
            .AppendLine()
            .Append("            public GeneratedNavigationInteractionSubscription(").Append(model.ViewName)
            .AppendLine(" view, global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("            {")
            .AppendLine("                _view = view;")
            .AppendLine("                _dataGrid = dataGrid;")
            .AppendLine("                _dataContextSubscription = global::Avalonia.AvaloniaObjectExtensions")
            .AppendLine("                    .GetObservable(view, global::Avalonia.StyledElement.DataContextProperty)")
            .AppendLine("                    .Subscribe(this);")
            .AppendLine("            }")
            .AppendLine()
            .AppendLine("            public void OnNext(object? value)")
            .AppendLine("            {")
            .AppendLine("                DisconnectCurrent();")
            .AppendLine("                if (_view is not { } view || _dataGrid is not { } dataGrid ||")
            .Append("                    value is not ").Append(viewModelType).AppendLine(" viewModel)")
            .AppendLine("                {")
            .AppendLine("                    return;")
            .AppendLine("                }")
            .AppendLine()
            .AppendLine("                var lifetime = new global::System.Threading.CancellationTokenSource();")
            .AppendLine("                _lifetime = lifetime;")
            .AppendLine("                var handler = view.CreateGeneratedNavigationInteractionHandler();")
            .AppendLine("                _handler = handler;")
            .Append("                _registration = viewModel.").Append(propertyName).AppendLine(".RegisterHandler(async context =>")
            .AppendLine("                {")
            .AppendLine("                    var output = await handler.HandleAsync(")
            .Append("                        new global::Avalonia.Controls.DataGridGeneratedViewInteractionContext<").Append(requestType).AppendLine(">(")
            .AppendLine("                            view,")
            .AppendLine("                            dataGrid,")
            .AppendLine("                            context.Input,")
            .AppendLine("                            lifetime.Token));")
            .AppendLine("                    context.SetOutput(output);")
            .AppendLine("                });")
            .AppendLine("            }")
            .AppendLine()
            .AppendLine("            public void OnError(global::System.Exception error) => Dispose();")
            .AppendLine()
            .AppendLine("            public void OnCompleted() => Dispose();")
            .AppendLine()
            .AppendLine("            public void Dispose()")
            .AppendLine("            {")
            .AppendLine("                global::System.IDisposable? subscription = _dataContextSubscription;")
            .AppendLine("                _dataContextSubscription = null;")
            .AppendLine("                _view = null;")
            .AppendLine("                _dataGrid = null;")
            .AppendLine("                subscription?.Dispose();")
            .AppendLine("                DisconnectCurrent();")
            .AppendLine("            }")
            .AppendLine()
            .AppendLine("            private void DisconnectCurrent()")
            .AppendLine("            {")
            .AppendLine("                _registration?.Dispose();")
            .AppendLine("                _registration = null;")
            .AppendLine("                global::System.Threading.CancellationTokenSource? lifetime = _lifetime;")
            .AppendLine("                _lifetime = null;")
            .AppendLine("                if (lifetime is not null)")
            .AppendLine("                {")
            .AppendLine("                    lifetime.Cancel();")
            .AppendLine("                    lifetime.Dispose();")
            .AppendLine("                }")
            .AppendLine("                if (_handler is global::System.IDisposable disposableHandler)")
            .AppendLine("                {")
            .AppendLine("                    disposableHandler.Dispose();")
            .AppendLine("                }")
            .AppendLine("                _handler = null;")
            .AppendLine("            }")
            .AppendLine("        }")
            .AppendLine();
    }

    private static void EmitGeneratedEditEventHandler(
        StringBuilder builder,
        string itemType,
        string methodSuffix,
        string eventArgsType,
        string eventKind,
        string editAction,
        bool hasColumn,
        bool isCancelable)
    {
        builder.Append("        private void OnGenerated").Append(methodSuffix)
            .Append("(object? sender, global::Avalonia.Controls.").Append(eventArgsType).AppendLine(" e)")
            .AppendLine("        {")
            .Append("            var eventData = global::Avalonia.Controls.DataGridGeneratedViewEvent<").Append(itemType).AppendLine(">.CreateEdit(")
            .Append("                global::Avalonia.Controls.DataGridGeneratedViewEventKinds.").Append(eventKind).AppendLine(",")
            .AppendLine("                GetGeneratedEventItem(e.Row.DataContext),")
            .AppendLine("                e.Row.Index,")
            .Append("                ").Append(hasColumn ? "GetGeneratedEventColumnKey(e.Column)" : "string.Empty").AppendLine(",")
            .Append("                ").Append(editAction).AppendLine(",")
            .Append("                ").Append(isCancelable ? "e.Cancel" : "false").AppendLine(");")
            .AppendLine("            eventData.Handled = e.Handled;")
            .AppendLine("            ExecuteGeneratedRoutedEventCommand(eventData);");
        if (isCancelable)
        {
            builder.AppendLine("            e.Cancel = eventData.Cancel;");
        }
        builder.AppendLine("            e.Handled = eventData.Handled;")
            .AppendLine("        }")
            .AppendLine();
    }

    private static void EmitGeneratedViewStateMembers(StringBuilder builder, ViewModelViewModel model)
    {
        if (model.ViewState == null)
        {
            return;
        }

        builder.AppendLine("        protected virtual global::Avalonia.Controls.Control CreateGeneratedViewStateHost(global::Avalonia.Controls.DataGrid dataGrid)")
            .AppendLine("        {")
            .AppendLine("            var host = new global::Avalonia.Controls.Grid { Name = \"GeneratedViewStateHost\" };")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(host, GeneratedAutomationId + \"-state-host\");")
            .AppendLine("            dataGrid[!global::Avalonia.Visual.IsVisibleProperty] = CreateBinding(s_viewStateProperty, global::Avalonia.Data.BindingMode.OneWay, s_contentStateConverter);")
            .AppendLine("            host.Children.Add(dataGrid);")
            .AppendLine("            var loading = CreateGeneratedLoadingContent();")
            .AppendLine("            loading[!global::Avalonia.Visual.IsVisibleProperty] = CreateBinding(s_viewStateProperty, global::Avalonia.Data.BindingMode.OneWay, s_loadingStateConverter);")
            .AppendLine("            host.Children.Add(loading);")
            .AppendLine("            var empty = CreateGeneratedEmptyContent();")
            .AppendLine("            empty[!global::Avalonia.Visual.IsVisibleProperty] = CreateBinding(s_viewStateProperty, global::Avalonia.Data.BindingMode.OneWay, s_emptyStateConverter);")
            .AppendLine("            host.Children.Add(empty);")
            .AppendLine("            var error = CreateGeneratedErrorContent();")
            .AppendLine("            error[!global::Avalonia.Visual.IsVisibleProperty] = CreateBinding(s_viewStateProperty, global::Avalonia.Data.BindingMode.OneWay, s_errorStateConverter);")
            .AppendLine("            host.Children.Add(error);")
            .AppendLine("            return host;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual global::Avalonia.Controls.Control CreateGeneratedLoadingContent()")
            .AppendLine("        {")
            .AppendLine("            var content = new global::Avalonia.Controls.StackPanel")
            .AppendLine("            {")
            .AppendLine("                Name = \"GeneratedLoadingState\",")
            .AppendLine("                Spacing = 10d,")
            .AppendLine("                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,")
            .AppendLine("                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,")
            .AppendLine("                Children =")
            .AppendLine("                {")
            .AppendLine("                    new global::Avalonia.Controls.ProgressBar { IsIndeterminate = true, Width = 180d },")
            .Append("                    new global::Avalonia.Controls.TextBlock { Text = ").Append(GeneratorUtilities.EscapeString(model.LoadingText)).AppendLine(" }")
            .AppendLine("                }")
            .AppendLine("            };")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(content, GeneratedAutomationId + \"-loading\");")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(content, \"Loading\");")
            .Append("            global::Avalonia.Automation.AutomationProperties.SetHelpText(content, ").Append(GeneratorUtilities.EscapeString(model.LoadingText)).AppendLine(");")
            .AppendLine("            return content;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual global::Avalonia.Controls.Control CreateGeneratedEmptyContent()")
            .AppendLine("        {")
            .AppendLine("            var content = new global::Avalonia.Controls.TextBlock")
            .AppendLine("            {")
            .AppendLine("                Name = \"GeneratedEmptyState\",")
            .Append("                Text = ").Append(GeneratorUtilities.EscapeString(model.EmptyText)).AppendLine(",")
            .AppendLine("                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,")
            .AppendLine("                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center")
            .AppendLine("            };")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(content, GeneratedAutomationId + \"-empty\");")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(content, \"Empty\");")
            .Append("            global::Avalonia.Automation.AutomationProperties.SetHelpText(content, ").Append(GeneratorUtilities.EscapeString(model.EmptyText)).AppendLine(");")
            .AppendLine("            return content;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        protected virtual global::Avalonia.Controls.Control CreateGeneratedErrorContent()")
            .AppendLine("        {")
            .AppendLine("            var message = new global::Avalonia.Controls.TextBlock")
            .AppendLine("            {")
            .AppendLine("                Name = \"GeneratedErrorMessage\",")
            .Append("                Text = ").Append(GeneratorUtilities.EscapeString(model.ErrorText)).AppendLine(",")
            .AppendLine("                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,")
            .AppendLine("                MaxWidth = 520d,")
            .AppendLine("                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center")
            .AppendLine("            };");
        if (model.ErrorMessage != null)
        {
            builder.AppendLine("            message[!global::Avalonia.Controls.TextBlock.TextProperty] = CreateBinding(s_errorMessageProperty, global::Avalonia.Data.BindingMode.OneWay, s_errorMessageConverter);");
        }
        builder.AppendLine("            var content = new global::Avalonia.Controls.StackPanel")
            .AppendLine("            {")
            .AppendLine("                Name = \"GeneratedErrorState\",")
            .AppendLine("                Spacing = 10d,")
            .AppendLine("                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,")
            .AppendLine("                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center")
            .AppendLine("            };")
            .AppendLine("            content.Children.Add(message);");
        if (model.RetryCommand != null)
        {
            builder.AppendLine("            var retry = new global::Avalonia.Controls.Button")
                .AppendLine("            {")
                .AppendLine("                Name = \"GeneratedRetryButton\",")
                .Append("                Content = ").Append(GeneratorUtilities.EscapeString(model.RetryText)).AppendLine(",")
                .AppendLine("                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center")
                .AppendLine("            };")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(retry, GeneratedAutomationId + \"-retry\");")
                .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(retry, retry.Content?.ToString());")
                .AppendLine("            retry[!global::Avalonia.Controls.Button.CommandProperty] = CreateBinding(s_retryCommandProperty, global::Avalonia.Data.BindingMode.OneWay);")
                .AppendLine("            content.Children.Add(retry);");
        }
        builder.AppendLine("            global::Avalonia.Automation.AutomationProperties.SetAutomationId(content, GeneratedAutomationId + \"-error\");")
            .AppendLine("            global::Avalonia.Automation.AutomationProperties.SetName(content, \"Error\");")
            .Append("            global::Avalonia.Automation.AutomationProperties.SetHelpText(content, ").Append(GeneratorUtilities.EscapeString(model.ErrorText)).AppendLine(");")
            .AppendLine("            return content;")
            .AppendLine("        }")
            .AppendLine();
    }

    private static void EmitRowDetailsConfiguration(StringBuilder builder, ViewModelViewModel model)
    {
        RowDetailsViewModel? rowDetails = model.RowDetails;
        if (rowDetails == null)
        {
            return;
        }

        string itemType = model.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        switch (rowDetails.Source)
        {
            case RowDetailsTemplateSourceModel.Resource:
                builder.Append("            dataGrid.Bind(global::Avalonia.Controls.DataGrid.RowDetailsTemplateProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(")
                    .Append(GeneratorUtilities.EscapeString(rowDetails.ResourceKey)).AppendLine("));");
                break;
            case RowDetailsTemplateSourceModel.Implementation:
                builder.Append("            dataGrid.RowDetailsTemplate = new ")
                    .Append(rowDetails.ImplementationType!.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat))
                    .AppendLine("();");
                break;
            case RowDetailsTemplateSourceModel.FactoryMethod:
                builder.Append("            dataGrid.RowDetailsTemplate = new global::Avalonia.Controls.DataGridGeneratedFuncDataTemplate<")
                    .Append(itemType).Append(">(").Append(itemType).Append('.')
                    .Append(GeneratorUtilities.EscapeIdentifier(rowDetails.FactoryMethod!)).AppendLine(");");
                break;
            case RowDetailsTemplateSourceModel.NestedGrid:
                builder.Append("            dataGrid.RowDetailsTemplate = new global::Avalonia.Controls.DataGridGeneratedFuncDataTemplate<")
                    .Append(itemType).AppendLine(">(CreateGeneratedRowDetails);");
                break;
        }

        builder.Append("            dataGrid.RowDetailsVisibilityMode = (global::Avalonia.Controls.DataGridRowDetailsVisibilityMode)")
            .Append(rowDetails.VisibilityMode.ToString(CultureInfo.InvariantCulture)).AppendLine(";")
            .Append("            dataGrid.AreRowDetailsFrozen = ").Append(rowDetails.AreFrozen ? "true" : "false").AppendLine(";");
    }

    private static void EmitGeneratedRowDetailsMembers(StringBuilder builder, ViewModelViewModel model)
    {
        RowDetailsViewModel? rowDetails = model.RowDetails;
        if (rowDetails?.Source != RowDetailsTemplateSourceModel.NestedGrid)
        {
            return;
        }

        string itemType = model.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string nestedItemType = rowDetails.NestedItemType!.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        string nestedProviderType = string.IsNullOrEmpty(rowDetails.NestedProviderNamespace)
            ? "global::" + rowDetails.NestedProviderName
            : "global::" + rowDetails.NestedProviderNamespace + "." + rowDetails.NestedProviderName;
        string nestedItemsProperty = GeneratorUtilities.EscapeIdentifier(rowDetails.NestedItemsProperty!.Name);

        builder.AppendLine()
            .Append("        private static global::Avalonia.Controls.Control CreateGeneratedRowDetails(")
            .Append(itemType).AppendLine(" item, global::Avalonia.Controls.Control? existing)")
            .AppendLine("        {")
            .AppendLine("            var presenter = existing as GeneratedRowDetailsPresenter ?? new GeneratedRowDetailsPresenter();")
            .AppendLine("            presenter.Update(item);")
            .AppendLine("            return presenter;")
            .AppendLine("        }")
            .AppendLine()
            .AppendLine("        private sealed class GeneratedRowDetailsPresenter : global::Avalonia.Controls.Border")
            .AppendLine("        {")
            .AppendLine("            private readonly global::Avalonia.Controls.TextBlock? _summary;")
            .AppendLine("            private readonly global::Avalonia.Controls.DataGrid _nestedGrid;")
            .AppendLine()
            .AppendLine("            public GeneratedRowDetailsPresenter()")
            .AppendLine("            {")
            .AppendLine("                Padding = new global::Avalonia.Thickness(10d);")
            .Append("                global::Avalonia.Automation.AutomationProperties.SetAutomationId(this, ")
            .Append(GeneratorUtilities.EscapeString(rowDetails.AutomationId + "-host")).AppendLine(");")
            .AppendLine("                global::Avalonia.Automation.AutomationProperties.SetName(this, \"Row details\");")
            .AppendLine("                global::Avalonia.Automation.AutomationProperties.SetHelpText(this, \"Generated typed row details.\");")
            .AppendLine("                var content = new global::Avalonia.Controls.StackPanel { Spacing = 8d };");

        if (rowDetails.SummaryProperty != null)
        {
            builder.AppendLine("                _summary = new global::Avalonia.Controls.TextBlock")
                .AppendLine("                {")
                .AppendLine("                    Name = \"GeneratedRowDetailsSummary\",")
                .AppendLine("                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap")
                .AppendLine("                };")
                .AppendLine("                global::Avalonia.Automation.AutomationProperties.SetAutomationId(_summary, GeneratedAutomationId + \"-details-summary\");")
                .AppendLine("                global::Avalonia.Automation.AutomationProperties.SetName(_summary, \"Row details summary\");")
                .AppendLine("                content.Children.Add(_summary);");
        }
        else
        {
            builder.AppendLine("                _summary = null;");
        }

        builder.AppendLine("                _nestedGrid = new global::Avalonia.Controls.DataGrid")
            .AppendLine("                {")
            .AppendLine("                    Name = \"GeneratedNestedDataGrid\",")
            .AppendLine("                    AutoGenerateColumns = false,")
            .AppendLine("                    CanUserAddRows = false,")
            .AppendLine("                    CanUserDeleteRows = false,")
            .AppendLine("                    CanUserReorderColumns = false,")
            .AppendLine("                    HeadersVisibility = global::Avalonia.Controls.DataGridHeadersVisibility.Column,")
            .AppendLine("                    GridLinesVisibility = global::Avalonia.Controls.DataGridGridLinesVisibility.Horizontal,")
            .AppendLine("                    IsReadOnly = true,")
            .Append("                    ColumnDefinitionsSource = ").Append(nestedProviderType).AppendLine(".Instance.CreateColumnDefinitions(),")
            .Append("                    FastPathOptions = ").Append(nestedProviderType).AppendLine(".Instance.CreateFastPathOptions()")
            .AppendLine("                };")
            .Append("                global::Avalonia.Automation.AutomationProperties.SetAutomationId(_nestedGrid, ")
            .Append(GeneratorUtilities.EscapeString(rowDetails.AutomationId)).AppendLine(");")
            .AppendLine("                global::Avalonia.Automation.AutomationProperties.SetName(_nestedGrid, \"Nested row details\");")
            .AppendLine("                global::Avalonia.Automation.AutomationProperties.SetHelpText(_nestedGrid, \"Generated reflection-free nested ProDataGrid.\");")
            .AppendLine("                content.Children.Add(_nestedGrid);")
            .AppendLine("                Child = content;")
            .AppendLine("            }")
            .AppendLine()
            .Append("            public void Update(").Append(itemType).AppendLine(" item)")
            .AppendLine("            {");

        if (rowDetails.SummaryProperty != null)
        {
            builder.Append("                _summary!.Text = item.")
                .Append(GeneratorUtilities.EscapeIdentifier(rowDetails.SummaryProperty.Name)).AppendLine(";");
        }

        builder.Append("                _nestedGrid.ItemsSource = (global::System.Collections.Generic.IEnumerable<")
            .Append(nestedItemType).Append(">)item.").Append(nestedItemsProperty).AppendLine(";")
            .AppendLine("            }")
            .AppendLine("        }");
    }

    private static void EmitViewPropertyInfo(
        StringBuilder builder,
        ViewBindingModel binding,
        string viewModelType,
        string role)
    {
        string fieldName = "s_" + char.ToLowerInvariant(role[0]) + role.Substring(1) + "Property";
        string propertyName = GeneratorUtilities.EscapeIdentifier(binding.PropertyName);
        builder.Append("        private static readonly global::Avalonia.Data.Core.IPropertyInfo ")
            .Append(fieldName).AppendLine(" =")
            .AppendLine("            new global::Avalonia.Data.Core.ClrPropertyInfo(")
            .Append("                ").Append(GeneratorUtilities.EscapeString(binding.PropertyName)).AppendLine(",")
            .Append("                static target => target is ").Append(viewModelType).Append(" viewModel ? viewModel.")
            .Append(propertyName).Append(" : default(").Append(binding.PropertyType).AppendLine("),");
        if (binding.CanWrite)
        {
            builder.AppendLine("                static (target, value) =>")
                .AppendLine("                {")
                .Append("                    if (target is ").Append(viewModelType).AppendLine(" viewModel)")
                .AppendLine("                    {")
                .Append("                        viewModel.").Append(propertyName).Append(" = value is null ? default! : (")
                .Append(binding.PropertyType).AppendLine(")value;")
                .AppendLine("                    }")
                .AppendLine("                },");
        }
        else
        {
            builder.AppendLine("                setter: null,");
        }

        builder.Append("                typeof(").Append(binding.RuntimePropertyType).AppendLine("));")
            .AppendLine();
    }

    private static void EmitOptionalGridBinding(
        StringBuilder builder,
        ViewBindingModel? binding,
        string propertyName,
        string fieldName)
    {
        if (binding == null)
        {
            return;
        }

        builder.Append("            dataGrid[!global::Avalonia.Controls.DataGrid.").Append(propertyName)
            .Append("Property] = CreateBinding(").Append(fieldName)
            .AppendLine(", global::Avalonia.Data.BindingMode.OneWay);");
    }

    private static void EmitGeneratedLayout(StringBuilder builder, ViewModelViewModel model)
    {
        if (model.Layout == 0)
        {
            return;
        }

        string typeName = model.Layout switch
        {
            1 => "DataGridStackLayoutModel",
            2 => "DataGridNonVirtualizingStackLayoutModel",
            3 => "DataGridUniformGridLayoutModel",
            4 => "DataGridWrapLayoutModel",
            _ => string.Empty
        };
        if (typeName.Length == 0)
        {
            return;
        }

        builder.Append("            dataGrid.LayoutModel = new global::Avalonia.Controls.DataGridLayouts.")
            .Append(typeName).AppendLine()
            .AppendLine("            {");
        if (model.LayoutPresentation == 1)
        {
            builder.AppendLine("                PresentationMode = global::Avalonia.Controls.DataGridLayouts.DataGridLayoutPresentationMode.Items,")
                .Append("                ItemSizeEstimate = new global::Avalonia.Size(")
                .Append(GeneratorUtilities.FormatDouble(model.LayoutItemWidthEstimate)).Append(", ")
                .Append(GeneratorUtilities.FormatDouble(model.LayoutItemHeightEstimate)).AppendLine("),");
        }
        if (model.LayoutOrientation != 0)
        {
            builder.Append("                Orientation = global::Avalonia.Controls.DataGridLayouts.DataGridLayoutOrientation.")
                .Append(model.LayoutOrientation == 1 ? "Horizontal" : "Vertical").AppendLine(",");
        }

        if (model.Layout is 1 or 2)
        {
            builder.Append("                Spacing = ").Append(GeneratorUtilities.FormatDouble(model.LayoutSpacing)).AppendLine(",");
            if (model.Layout == 1)
            {
                builder.Append("                DisableVirtualization = ").Append(model.LayoutDisableVirtualization ? "true" : "false").AppendLine(",");
            }
        }
        else if (model.Layout == 3)
        {
            builder.Append("                MinColumnSpacing = ").Append(GeneratorUtilities.FormatDouble(model.LayoutHorizontalSpacing)).AppendLine(",")
                .Append("                MinRowSpacing = ").Append(GeneratorUtilities.FormatDouble(model.LayoutVerticalSpacing)).AppendLine(",");
        }
        else
        {
            builder.Append("                HorizontalSpacing = ").Append(GeneratorUtilities.FormatDouble(model.LayoutHorizontalSpacing)).AppendLine(",")
                .Append("                VerticalSpacing = ").Append(GeneratorUtilities.FormatDouble(model.LayoutVerticalSpacing)).AppendLine(",");
        }

        if (model.Layout == 3)
        {
            builder.Append("                MinItemWidth = ").Append(GeneratorUtilities.FormatDouble(model.LayoutMinItemWidth)).AppendLine(",")
                .Append("                MinItemHeight = ").Append(GeneratorUtilities.FormatDouble(model.LayoutMinItemHeight)).AppendLine(",")
                .Append("                MaximumRowsOrColumns = ").Append(model.LayoutMaximumRowsOrColumns.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
                .Append("                ItemsJustification = (global::Avalonia.Controls.DataGridLayouts.DataGridUniformGridItemsJustification)")
                .Append(model.LayoutItemsJustification.ToString(CultureInfo.InvariantCulture)).AppendLine(",")
                .Append("                ItemsStretch = (global::Avalonia.Controls.DataGridLayouts.DataGridUniformGridItemsStretch)")
                .Append(model.LayoutItemsStretch.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
        }
        else if (model.Layout == 4)
        {
            builder.Append("                MaximumCachedLines = ").Append(model.LayoutMaximumCachedLines.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
        }

        builder.AppendLine("            };")
            .AppendLine("            dataGrid.UseLogicalScrollable = true;");
    }

    private static void EmitLayoutItemTemplateConfiguration(StringBuilder builder, ViewModelViewModel model)
    {
        LayoutItemTemplateViewModel? template = model.LayoutItemTemplate;
        if (template == null)
        {
            return;
        }

        string itemType = model.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat);
        switch (template.Source)
        {
            case LayoutItemTemplateSourceModel.Resource:
                builder.Append("            dataGrid.Bind(global::Avalonia.Controls.DataGrid.ItemTemplateProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(")
                    .Append(GeneratorUtilities.EscapeString(template.ResourceKey)).AppendLine("));");
                break;
            case LayoutItemTemplateSourceModel.Implementation:
                builder.Append("            dataGrid.ItemTemplate = new ")
                    .Append(template.ImplementationType!.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat))
                    .AppendLine("();");
                break;
            case LayoutItemTemplateSourceModel.FactoryMethod:
                builder.Append("            dataGrid.ItemTemplate = new global::Avalonia.Controls.DataGridGeneratedFuncDataTemplate<")
                    .Append(itemType).Append(">(").Append(itemType).Append('.')
                    .Append(GeneratorUtilities.EscapeIdentifier(template.FactoryMethod!)).AppendLine(");");
                break;
        }
    }

    private static void EmitStringAssignment(StringBuilder builder, ImmutableDictionary<string, TypedConstant> options, string propertyName)
    {
        if (options.TryGetValue(propertyName, out TypedConstant value) && value.Value is string text)
        {
            builder.Append("            column.").Append(propertyName).Append(" = ")
                .Append(GeneratorUtilities.EscapeString(text)).AppendLine(";");
        }
    }

    private static void EmitOptionalString(StringBuilder builder, string propertyName, string? value)
    {
        if (value != null)
        {
            builder.Append("            column.").Append(propertyName).Append(" = ")
                .Append(GeneratorUtilities.EscapeString(value)).AppendLine(";");
        }
    }

    private static void EmitBooleanAssignment(StringBuilder builder, ImmutableDictionary<string, TypedConstant> options, string propertyName)
    {
        if (options.TryGetValue(propertyName, out TypedConstant value) && value.Value is bool boolean)
        {
            builder.Append("            column.").Append(propertyName).Append(" = ")
                .Append(boolean ? "true" : "false").AppendLine(";");
        }
    }

    private static void EmitInt32Assignment(StringBuilder builder, ImmutableDictionary<string, TypedConstant> options, string propertyName)
    {
        if (options.TryGetValue(propertyName, out TypedConstant value) && value.Value is int number)
        {
            builder.Append("            column.").Append(propertyName).Append(" = ")
                .Append(number.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        }
    }

    private static void EmitEnumAssignment(
        StringBuilder builder,
        ImmutableDictionary<string, TypedConstant> options,
        string propertyName,
        string enumType)
    {
        if (options.TryGetValue(propertyName, out TypedConstant value) && value.Value is int number)
        {
            builder.Append("            column.").Append(propertyName).Append(" = (").Append(enumType).Append(')')
                .Append(number.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        }
    }

    private static void EmitDoubleAssignment(StringBuilder builder, ImmutableDictionary<string, TypedConstant> options, string propertyName)
    {
        if (options.TryGetValue(propertyName, out TypedConstant value) && value.Value is double number)
        {
            builder.Append("            column.").Append(propertyName).Append(" = ")
                .Append(GeneratorUtilities.FormatDouble(number)).AppendLine(";");
        }
    }

    private static void EmitDecimalAssignment(StringBuilder builder, ImmutableDictionary<string, TypedConstant> options, string propertyName)
    {
        if (options.TryGetValue(propertyName, out TypedConstant value) && value.Value is double number)
        {
            builder.Append("            column.").Append(propertyName).Append(" = (decimal)")
                .Append(GeneratorUtilities.FormatDouble(number)).AppendLine(";");
        }
    }

    private static string? GetStringOption(ImmutableDictionary<string, TypedConstant> options, string name)
    {
        return options.TryGetValue(name, out TypedConstant value) ? value.Value as string : null;
    }

    private static string EmitWidth(string width)
    {
        string trimmed = width.Trim();
        if (string.Equals(trimmed, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return "global::Avalonia.Controls.DataGridLength.Auto";
        }

        if (trimmed.EndsWith("*", StringComparison.Ordinal))
        {
            string factorText = trimmed.Substring(0, trimmed.Length - 1);
            double factor = string.IsNullOrWhiteSpace(factorText)
                ? 1d
                : double.TryParse(factorText, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 1d;
            return "new global::Avalonia.Controls.DataGridLength(" + GeneratorUtilities.FormatDouble(factor) +
                   ", global::Avalonia.Controls.DataGridLengthUnitType.Star)";
        }

        double pixels = double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedPixels)
            ? parsedPixels
            : 100d;
        return "new global::Avalonia.Controls.DataGridLength(" + GeneratorUtilities.FormatDouble(pixels) +
               ", global::Avalonia.Controls.DataGridLengthUnitType.Pixel)";
    }

    private static bool CanWrite(IPropertySymbol property)
    {
        return property.SetMethod != null &&
               !property.SetMethod.IsInitOnly &&
               GeneratorUtilities.IsAccessibleFromGeneratedCode(property.SetMethod);
    }

    private static string GetColumnAccessExpression(ColumnModel column, string receiver)
    {
        return GetMemberAccessExpression(column.Property, column.AccessReceiverType, receiver);
    }

    private static string GetKeyAccessExpression(SchemaModel schema, string receiver)
    {
        KeyMemberModel key = schema.KeyMember!;
        if (key.Kind == KeyAccessorKind.ReferenceIdentity)
        {
            return receiver;
        }

        if (key.Kind == KeyAccessorKind.StaticMethod)
        {
            return schema.ItemType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat) + "." +
                   GeneratorUtilities.EscapeIdentifier(key.Member.Name) + "(" + receiver + ")";
        }

        return GetMemberAccessExpression(key.Member, key.AccessReceiverType, receiver);
    }

    private static string GetKeyName(SchemaModel schema) => schema.KeyMember!.Kind switch
    {
        KeyAccessorKind.ReferenceIdentity => "$reference",
        KeyAccessorKind.StaticMethod => schema.KeyMember.Member.Name + "()",
        _ => schema.KeyMember.Member.Name
    };

    private static string GetMemberAccessExpression(
        ISymbol member,
        INamedTypeSymbol? accessReceiverType,
        string receiver)
    {
        string target = accessReceiverType == null
            ? receiver
            : "((" + accessReceiverType.ToDisplayString(GeneratorUtilities.FullyQualifiedNullableFormat) + ")" + receiver + ")";
        return target + "." + GeneratorUtilities.EscapeIdentifier(member.Name);
    }

    private static bool IsRuntimeDefinedImplementation(SchemaModel schema)
    {
        return schema.ImplementationType != null &&
               ImplementsInterface(schema.ImplementationType, "Avalonia.Controls.IDataGridRuntimeDefinedSchema");
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, string metadataName)
    {
        return type.AllInterfaces.Any(candidate =>
            string.Equals(GeneratorUtilities.GetMetadataName(candidate), metadataName, StringComparison.Ordinal));
    }

    private static bool CanEdit(ColumnModel column)
    {
        return CanWrite(column.Property) &&
               (!column.Options.TryGetValue("IsReadOnly", out TypedConstant value) ||
                value.Value is not bool isReadOnly ||
                !isReadOnly);
    }

    private static string GetDefinitionTypeName(string kind)
    {
        return kind switch
        {
            "Text" => "DataGridTextColumnDefinition",
            "CheckBox" => "DataGridCheckBoxColumnDefinition",
            "Hyperlink" => "DataGridHyperlinkColumnDefinition",
            "Image" => "DataGridImageColumnDefinition",
            "Numeric" => "DataGridNumericColumnDefinition",
            "ProgressBar" => "DataGridProgressBarColumnDefinition",
            "Slider" => "DataGridSliderColumnDefinition",
            "DatePicker" => "DataGridDatePickerColumnDefinition",
            "TimePicker" => "DataGridTimePickerColumnDefinition",
            "MaskedText" => "DataGridMaskedTextColumnDefinition",
            "AutoComplete" => "DataGridAutoCompleteColumnDefinition",
            "ToggleButton" => "DataGridToggleButtonColumnDefinition",
            "ToggleSwitch" => "DataGridToggleSwitchColumnDefinition",
            "Hierarchical" => "DataGridHierarchicalColumnDefinition",
            "CustomDrawing" => "DataGridCustomDrawingColumnDefinition",
            "ComboBoxSelectedItem" or "ComboBoxSelectedValue" or "ComboBoxText" => "DataGridComboBoxColumnDefinition",
            "Template" => "DataGridTemplateColumnDefinition",
            "Button" => "DataGridButtonColumnDefinition",
            "Formula" => "DataGridFormulaColumnDefinition",
            _ => "DataGridTextColumnDefinition"
        };
    }

    private static string GetFieldName(IPropertySymbol property)
    {
        return "s_" + GeneratorUtilities.SanitizeIdentifier(property.Name).TrimStart('@');
    }

    private static string GetAuxiliaryBindingPrefix(ColumnModel column, string role) =>
        GetFieldName(column.Property) + GeneratorUtilities.SanitizeIdentifier(role).TrimStart('@');

    private static string GetEditFieldName(IPropertySymbol property)
    {
        return "s_" + GeneratorUtilities.SanitizeIdentifier(property.Name).TrimStart('@') + "EditField";
    }

    private static string GetTypedFieldName(IPropertySymbol property)
    {
        string name = GeneratorUtilities.SanitizeIdentifier(property.Name).TrimStart('@');
        switch (name)
        {
            case "Instance":
            case "ManifestVersion":
            case "SchemaId":
            case "SchemaHash":
            case "Manifest":
            case "Fields":
            case "TryGetField":
            case "GetKey":
            case "KeyComparer":
            case "CreateItemIndex":
            case "CreateController":
            case "CreateColumnDefinitions":
            case "CreateSortComparer":
            case "CreateFilterPredicate":
            case "CreateSearchPredicate":
            case "CreateFastPathOptions":
                name += "Field";
                break;
        }

        return GeneratorUtilities.EscapeIdentifier(name);
    }

    private static string GetMethodSuffix(IPropertySymbol property)
    {
        return GeneratorUtilities.SanitizeIdentifier(property.Name).TrimStart('@');
    }

    private static string CreateHintName(string namespaceName, string name, string suffix)
    {
        string raw = namespaceName + "." + name + "." + suffix;
        var builder = new StringBuilder(raw.Length + 5);
        for (int i = 0; i < raw.Length; i++)
        {
            char value = raw[i];
            builder.Append(char.IsLetterOrDigit(value) || value == '_' ? value : '_');
        }

        return builder.Append(".g.cs").ToString();
    }

    private static bool IsPubliclyAccessible(INamedTypeSymbol type)
    {
        INamedTypeSymbol? current = type;
        while (current != null)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            current = current.ContainingType;
        }

        return true;
    }

    private static INamedTypeSymbol[] GetContainingTypeChain(INamedTypeSymbol type)
    {
        var stack = new Stack<INamedTypeSymbol>();
        INamedTypeSymbol? current = type;
        while (current != null)
        {
            stack.Push(current);
            current = current.ContainingType;
        }

        return stack.ToArray();
    }

    private static string GetAccessibility(INamedTypeSymbol type)
    {
        return type.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private"
        };
    }

    private static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
        }

        return type.TypeKind == TypeKind.Struct ? "struct" : "class";
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

    private static void AppendHeader(StringBuilder builder)
    {
        builder.AppendLine("// <auto-generated/>")
            .AppendLine("#nullable enable")
            .AppendLine();
    }

    private static void OpenNamespace(StringBuilder builder, string namespaceName)
    {
        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine()
                .AppendLine("{");
        }
    }

    private static void CloseNamespace(StringBuilder builder, string namespaceName)
    {
        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder.AppendLine("}");
        }
    }
}
