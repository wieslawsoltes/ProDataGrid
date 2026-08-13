// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ProDataGrid.SourceGenerators.UnitTests;

public sealed class ProDataGridGeneratorTests
{
    [Fact]
    public void Property_attribute_generates_schema_and_typed_accessor()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn(Header = "Display name", Width = "2*")]
                public string Name { get; set; } = string.Empty;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class RowDataGridSchema", result.CombinedSource);
        Assert.Contains("DataGridColumnValueAccessor<global::Demo.Row, string>", result.CombinedSource);
        Assert.Contains("DataGridGeneratedDistinctValueProvider<global::Demo.Row, string> NameDistinctValues", result.CombinedSource);
        Assert.Contains("CreateNameRemoteDistinctValues", result.CombinedSource);
        Assert.Contains("Generated collection views install typed group and sort descriptions", result.CombinedSource);
        Assert.Contains("Display name", result.CombinedSource);
        Assert.Contains("DataGridLengthUnitType.Star", result.CombinedSource);
    }

    [Fact]
    public void Type_attribute_discovers_public_properties_and_default_kinds()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                public string Name { get; set; } = string.Empty;
                public bool Enabled { get; set; }
                public decimal Amount { get; set; }
                public DateTime Date { get; set; }
                public Status Status { get; set; }
            }
            public enum Status { New, Done }
            """);

        AssertNoErrors(result);
        Assert.Contains("builder.Text<string>", result.CombinedSource);
        Assert.Contains("builder.CheckBox<bool>", result.CombinedSource);
        Assert.Contains("builder.Numeric<decimal>", result.CombinedSource);
        Assert.Contains("builder.DatePicker<global::System.DateTime>", result.CombinedSource);
        Assert.Contains("builder.ComboBoxSelectedItem<global::Demo.Status>", result.CombinedSource);
        Assert.Contains("Enum.GetValues<global::Demo.Status>()", result.CombinedSource);
    }

    [Fact]
    public void Schema_emits_validated_named_operation_presets()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.ComponentModel;
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridSorting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridColumns(OperationPresetMethods = new[] { nameof(CreateRiskPreset) })]
            public sealed class Row
            {
                public decimal Price { get; set; }

                public static DataGridGeneratedOperationPreset CreateRiskPreset() => new(
                    "risk",
                    sorting: new[] { new SortingDescriptor("Price", ListSortDirection.Descending) });
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedOperationPreset> OperationPresets", result.CombinedSource);
        Assert.Contains("global::Demo.Row.CreateRiskPreset()", result.CombinedSource);
        Assert.Contains("TryGetOperationPreset(string name", result.CombinedSource);
    }

    [Fact]
    public void Invalid_operation_preset_method_reports_PDGSG004()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(OperationPresetMethods = new[] { "Missing", "Missing" })]
            public sealed class Row { public decimal Price { get; set; } }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == "PDGSG004"));
        Assert.DoesNotContain("global::Demo.Row.Missing()", result.CombinedSource);
    }

    [Fact]
    public void Interface_schema_generates_typed_inherited_accessors()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IEntity
            {
                [DataGridKey]
                int Id { get; }
            }

            [GenerateDataGridColumns]
            public interface ITrade : IEntity
            {
                string Symbol { get; set; }
                decimal Price { get; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("IDataGridGeneratedSchema<global::Demo.ITrade>", result.CombinedSource);
        Assert.Contains("DataGridColumnValueAccessor<global::Demo.ITrade, int>", result.CombinedSource);
        Assert.Contains("static item => item.Id", result.CombinedSource);
        Assert.Contains("static (item, value) => item.Symbol = value", result.CombinedSource);
        Assert.Contains("IDataGridItemKey<global::Demo.ITrade, int>", result.CombinedSource);
    }

    [Fact]
    public void Interface_schema_can_exclude_inherited_properties()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IBaseRow
            {
                int BaseValue { get; }
            }

            [GenerateDataGridColumns(IncludeInherited = false)]
            public interface IRow : IBaseRow
            {
                int OwnValue { get; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateOwnValueColumn", result.CombinedSource);
        Assert.DoesNotContain("CreateBaseValueColumn", result.CombinedSource);
    }

    [Fact]
    public void Namespace_policy_includes_interface_item_types()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridColumnsForNamespace("Demo.Contracts")]
            namespace Demo.Contracts
            {
                public interface IRow { int Id { get; } }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class IRowDataGridSchema", result.CombinedSource);
        Assert.Contains("IDataGridGeneratedSchema<global::Demo.Contracts.IRow>", result.CombinedSource);
    }

    [Fact]
    public void Interface_schema_reports_unrelated_inherited_property_ambiguity()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface ITextValue { string Value { get; } }
            public interface INumericValue { int Value { get; } }

            [GenerateDataGridColumns]
            public interface IRow : ITextValue, INumericValue
            {
                int Id { get; }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            result.GeneratorDiagnostics.Where(static value => value.Id == "PDGSG132"));
        Assert.Contains("redeclare the property", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("CreateIdColumn", result.CombinedSource);
        Assert.DoesNotContain("CreateValueColumn", result.CombinedSource);
    }

    [Fact]
    public void Interface_schema_redeclaration_resolves_inherited_property_ambiguity()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IFirstValue { string Value { get; } }
            public interface ISecondValue { string Value { get; } }

            [GenerateDataGridColumns]
            public interface IRow : IFirstValue, ISecondValue
            {
                new string Value { get; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateValueColumn", result.CombinedSource);
    }

    [Fact]
    public void Explicit_interface_properties_generate_typed_cast_accessors_and_key()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IRowContract
            {
                [DataGridKey]
                int Id { get; }

                [DataGridColumn(Header = "Contract name", Order = 1)]
                string Name { get; set; }

                decimal Hidden { get; }
            }

            [GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly)]
            public sealed class Row : IRowContract
            {
                int IRowContract.Id => 42;
                string IRowContract.Name { get; set; } = "Ada";
                decimal IRowContract.Hidden => 10m;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("static item => ((global::Demo.IRowContract)item).Name", result.CombinedSource);
        Assert.Contains("static (item, value) => ((global::Demo.IRowContract)item).Name = value", result.CombinedSource);
        Assert.Contains("=> ((global::Demo.IRowContract)item).Id;", result.CombinedSource);
        Assert.Contains("Contract name", result.CombinedSource);
        Assert.DoesNotContain("CreateHiddenColumn", result.CombinedSource);
    }

    [Fact]
    public void Explicit_interface_implementation_metadata_drives_edit_field()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.ComponentModel.DataAnnotations;
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IRowContract { string Name { get; set; } }

            [GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly)]
            public sealed class Row : IRowContract
            {
                [Required]
                [DataGridColumn]
                string IRowContract.Name { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridGeneratedEditField<global::Demo.Row, string>", result.CombinedSource);
        Assert.Contains("String.IsNullOrWhiteSpace(value)", result.CombinedSource);
        Assert.Contains("((global::Demo.IRowContract)item).Name", result.CombinedSource);
    }

    [Fact]
    public void Explicit_interface_property_attribute_directly_triggers_schema()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IRowContract { string Name { get; } }
            public sealed class Row : IRowContract
            {
                [DataGridColumn]
                string IRowContract.Name => "Direct";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class RowDataGridSchema", result.CombinedSource);
        Assert.Contains("static item => ((global::Demo.IRowContract)item).Name", result.CombinedSource);
    }

    [Fact]
    public void Explicit_interface_property_reuses_cast_accessor_for_advanced_metadata()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridConditionalFormatting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IRowContract { decimal Value { get; } }

            [GenerateDataGridColumns]
            public sealed class Row : IRowContract
            {
                [DataGridColumn(HeaderProviderMethod = nameof(GetHeader), DescriptionProviderMethod = nameof(GetDescription))]
                [DataGridGroup]
                [DataGridSummary(DataGridAggregateType.Sum)]
                [DataGridConditionalFormat(DataGridCondition.GreaterThan, Operand = "10")]
                decimal IRowContract.Value => 20m;

                public static string GetHeader(IFormatProvider provider) => "Value";
                public static string GetDescription() => "Contract value";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("global::Demo.Row.GetHeader(provider)", result.CombinedSource);
        Assert.Contains("global::Demo.Row.GetDescription()", result.CombinedSource);
        Assert.Contains("static item => ((global::Demo.IRowContract)item).Value", result.CombinedSource);
        Assert.Contains("DataGridGeneratedGroupField<global::Demo.Row, decimal>", result.CombinedSource);
        Assert.Contains("DataGridGeneratedSummary<global::Demo.Row, decimal>", result.CombinedSource);
        Assert.Contains("DataGridGeneratedConditionalRule<global::Demo.Row, decimal>", result.CombinedSource);
    }

    [Fact]
    public void Public_property_wins_over_same_name_explicit_interface_property()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IRowContract { string Name { get; } }

            [GenerateDataGridColumns]
            public sealed class Row : IRowContract
            {
                public string Name { get; set; } = "Public";
                string IRowContract.Name => "Explicit";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("static item => item.Name", result.CombinedSource);
        Assert.DoesNotContain("((global::Demo.IRowContract)item).Name", result.CombinedSource);
    }

    [Fact]
    public void Same_name_explicit_interface_properties_report_ambiguity()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public interface IFirst { string Value { get; } }
            public interface ISecond { int Value { get; } }

            [GenerateDataGridColumns]
            public sealed class Row : IFirst, ISecond
            {
                string IFirst.Value => "One";
                int ISecond.Value => 2;
                public int Id { get; set; }
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG133");
        Assert.Contains("CreateIdColumn", result.CombinedSource);
        Assert.DoesNotContain("CreateValueColumn", result.CombinedSource);
    }

    [Fact]
    public void Assembly_attribute_targets_item_type()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridColumns(typeof(Demo.Row), ProviderName = "AssemblyColumns")]
            namespace Demo { public sealed class Row { public int Id { get; set; } } }
            """);

        AssertNoErrors(result);
        Assert.Contains("class AssemblyColumns", result.CombinedSource);
    }

    [Fact]
    public void Namespace_attribute_generates_all_matching_models()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridColumnsForNamespace("Demo.Models")]
            namespace Demo.Models
            {
                public sealed class First { public int Id { get; set; } }
                public sealed class Second { public string Name { get; set; } = ""; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class FirstDataGridSchema", result.CombinedSource);
        Assert.Contains("class SecondDataGridSchema", result.CombinedSource);
    }

    [Fact]
    public void Assembly_registry_exposes_cross_assembly_manifest_lookup()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridRegistry(RegistryNamespace = "Demo.Registration", RegistryName = "GridSchemas")]
            namespace Demo.Models
            {
                [GenerateDataGridColumns(SchemaId = "demo/row/v1")]
                public sealed class Row { public int Id { get; set; } }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("namespace Demo.Registration", result.CombinedSource);
        Assert.Contains("public static class GridSchemas", result.CombinedSource);
        Assert.Contains("IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedSchemaManifestProvider> Schemas", result.CombinedSource);
        Assert.Contains("TryGetSchema(", result.CombinedSource);
        Assert.Contains("itemType == typeof(global::Demo.Models.Row)", result.CombinedSource);
        Assert.Contains("RowDataGridSchema.SchemaId", result.CombinedSource);
    }

    [Fact]
    public void Assembly_registry_emits_optional_microsoft_di_registration_when_available()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridRegistry(RegistryNamespace = "Demo.Registration", RegistryName = "GridSchemas")]
            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection { void Add(ServiceDescriptor descriptor); }
                public sealed class ServiceDescriptor
                {
                    public static ServiceDescriptor Singleton(Type serviceType, object instance) => new();
                }
            }
            namespace Demo.Models
            {
                [GenerateDataGridColumns]
                public sealed class Row { public int Id { get; set; } }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("AddGeneratedProDataGrids", result.CombinedSource);
        Assert.Contains("IDataGridGeneratedSchema<global::Demo.Models.Row>", result.CombinedSource);
        Assert.Contains("ServiceDescriptor.Singleton", result.CombinedSource);
    }

    [Fact]
    public void Assembly_registry_generates_reflection_free_user_view_mappings()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridRegistry]
            [assembly: DataGridViewRegistration(typeof(Demo.RowsViewModel), typeof(Demo.RowsView))]

            namespace Demo
            {
                [GenerateDataGridColumns]
                public sealed class Row { public int Id { get; set; } }
                public sealed class RowsViewModel { }
                public sealed class RowsView : Avalonia.Controls.UserControl { }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("public static bool TryCreateView", result.CombinedSource);
        Assert.Contains("viewModel is global::Demo.RowsViewModel typedViewModel0", result.CombinedSource);
        Assert.Contains("new global::Demo.RowsView { DataContext = typedViewModel0 }", result.CombinedSource);
        Assert.DoesNotContain("Activator.CreateInstance", result.CombinedSource);
    }

    [Fact]
    public void Partial_view_model_receives_columns_schema_and_fast_options()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row), Streaming = true)]
            public sealed partial class RowsViewModel { }
            """);

        AssertNoErrors(result);
        Assert.Contains("partial class RowsViewModel", result.CombinedSource);
        Assert.Contains("DataGridColumnDefinitionList ColumnDefinitions", result.CombinedSource);
        Assert.Contains("IDataGridGeneratedSchema<global::Demo.Row> DataGridSchema", result.CombinedSource);
        Assert.Contains("DataGridFastPathOptions FastPathOptions", result.CombinedSource);
        Assert.Contains("HighPerformanceSearchTrackItemChanges = false", result.CombinedSource);
    }

    [Fact]
    public void Partial_view_model_can_receive_multiple_named_grid_schemas()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridColumns] public sealed class Metric { public double Value { get; set; } }
            [GenerateDataGridColumns] public sealed class Activity { public string Name { get; set; } = ""; }

            [GenerateDataGridViewModel(
                typeof(Metric),
                ColumnDefinitionsPropertyName = "MetricColumns",
                SchemaPropertyName = "MetricSchema",
                FastPathOptionsPropertyName = "MetricFastPath")]
            [GenerateDataGridViewModel(
                typeof(Activity),
                ColumnDefinitionsPropertyName = "ActivityColumns",
                SchemaPropertyName = "ActivitySchema",
                FastPathOptionsPropertyName = "ActivityFastPath")]
            public sealed partial class DashboardViewModel { }
            """);

        AssertNoErrors(result);
        Assert.Contains("MetricColumns", result.CombinedSource);
        Assert.Contains("ActivityColumns", result.CombinedSource);
        Assert.Contains("MetricFastPath", result.CombinedSource);
        Assert.Contains("ActivityFastPath", result.CombinedSource);
    }

    [Fact]
    public void Multiple_view_model_schemas_report_default_member_collisions()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Metric { public double Value { get; set; } }
            public sealed class Activity { public string Name { get; set; } = ""; }

            [GenerateDataGridViewModel(typeof(Metric))]
            [GenerateDataGridViewModel(typeof(Activity))]
            public sealed partial class DashboardViewModel { }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG006");
    }

    [Fact]
    public void Assembly_view_model_attribute_generates_partial_members()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridViewModel(typeof(Demo.RowsViewModel), typeof(Demo.Row))]
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                public sealed partial class RowsViewModel { }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("partial class RowsViewModel", result.CombinedSource);
    }

    [Fact]
    public void Namespace_view_model_attribute_infers_items_type()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridViewModelsForNamespace("Demo.ViewModels")]
            namespace Demo.Models { public sealed class Row { public int Id { get; set; } } }
            namespace Demo.ViewModels
            {
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Demo.Models.Row> Items { get; } = new List<Demo.Models.Row>();
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("IDataGridGeneratedSchema<global::Demo.Models.Row>", result.CombinedSource);
    }

    [Fact]
    public void Ignore_attribute_and_attributed_only_discovery_are_honored()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly)]
            public sealed class Row
            {
                [DataGridColumn] public int Included { get; set; }
                [DataGridIgnoreColumn] public int Ignored { get; set; }
                public int NotAttributed { get; set; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateIncludedColumn", result.CombinedSource);
        Assert.DoesNotContain("CreateIgnoredColumn", result.CombinedSource);
        Assert.DoesNotContain("CreateNotAttributedColumn", result.CombinedSource);
    }

    [Fact]
    public void Hierarchical_rows_emit_wrapper_aware_compiled_bindings()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridColumns(
                Discovery = DataGridColumnDiscovery.AttributedOnly,
                HierarchicalRows = true)]
            public sealed class Row
            {
                [DataGridColumn(DataGridColumnKind.Hierarchical, SortMemberPath = nameof(DisplayName))]
                public Row Item => this;

                public string DisplayName { get; init; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridColumnValueAccessor<global::Avalonia.Controls.DataGridHierarchical.HierarchicalNode, global::Demo.Row>", result.CombinedSource);
        Assert.Contains("node.Item is global::Demo.Row item ? item.Item : default!", result.CombinedSource);
        Assert.Contains("column.Binding = s_ItemHierarchicalBinding", result.CombinedSource);
        Assert.Contains("column.SortMemberPath = \"Item.DisplayName\"", result.CombinedSource);
    }

    [Fact]
    public void Read_only_property_does_not_generate_setter()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public string Display => "value"; }
            """);

        AssertNoErrors(result);
        Assert.Contains("setter: null", result.CombinedSource);
        Assert.Contains("static item => item.Display", result.CombinedSource);
    }

    [Fact]
    public void Configure_and_factory_methods_are_called_directly()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(ConfigureMethod = nameof(ConfigureColumns))]
            public sealed class Row
            {
                [DataGridColumn(Kind = DataGridColumnKind.Text, ConfigureMethod = nameof(ConfigureName))]
                public string Name { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.Button, FactoryMethod = nameof(CreateAction))]
                public string Action { get; } = "Run";

                public static void ConfigureName(DataGridTextColumnDefinition column) => column.Watermark = "name";
                public static DataGridButtonColumnDefinition CreateAction() => new();
                public static void ConfigureColumns(DataGridColumnDefinitionList columns) { }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("global::Demo.Row.ConfigureName(column);", result.CombinedSource);
        Assert.Contains("global::Demo.Row.CreateAction();", result.CombinedSource);
        Assert.Contains("global::Demo.Row.ConfigureColumns(columns);", result.CombinedSource);
    }

    [Fact]
    public void All_column_kinds_emit_builder_or_definition_paths()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly)]
            public sealed class Row
            {
                [DataGridColumn(Kind = DataGridColumnKind.Text)] public string Text { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.CheckBox)] public bool Check { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.Hyperlink)] public string Link { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.Image)] public object Image { get; set; } = new();
                [DataGridColumn(Kind = DataGridColumnKind.Numeric)] public decimal Number { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.ProgressBar)] public double Progress { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.Slider)] public double Slider { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.DatePicker)] public System.DateTime Date { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.TimePicker)] public System.TimeSpan Time { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.MaskedText, Mask = "000")] public string Masked { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.AutoComplete)] public string Auto { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.ToggleButton)] public bool Toggle { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.ToggleSwitch)] public bool Switch { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.Hierarchical)] public string Tree { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.CustomDrawing)] public string Draw { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.ComboBoxSelectedItem)] public string ComboItem { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.ComboBoxSelectedValue)] public int ComboValue { get; set; }
                [DataGridColumn(Kind = DataGridColumnKind.ComboBoxText)] public string ComboText { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.Template, TemplateKey = "CellTemplate")] public string Template { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.Button, Content = "Run")] public string Button { get; set; } = "";
                [DataGridColumn(Kind = DataGridColumnKind.Formula, Formula = "=A1")] public double Formula { get; set; }
            }
            """);

        AssertNoErrors(result);
        foreach (string marker in new[]
                 {
                     "builder.Text<", "builder.CheckBox<", "builder.Hyperlink<", "builder.Image<", "builder.Numeric<",
                     "builder.ProgressBar<", "builder.Slider<", "builder.DatePicker<", "builder.TimePicker<", "builder.MaskedText<",
                     "builder.AutoComplete<", "builder.ToggleButton<", "builder.ToggleSwitch<", "builder.Hierarchical<",
                     "builder.CustomDrawing<", "builder.ComboBoxSelectedItem<", "builder.ComboBoxSelectedValue<",
                     "builder.ComboBoxText<", "builder.Template(", "builder.Button(", "builder.Formula("
                 })
        {
            Assert.Contains(marker, result.CombinedSource);
        }
    }

    [Fact]
    public void Button_and_toggle_members_generate_cached_direct_bindings()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Windows.Input;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly)]
            public sealed class Row
            {
                [DataGridColumn(
                    Kind = DataGridColumnKind.Button,
                    ContentMember = nameof(ActionLabel),
                    CommandMember = nameof(ActionCommand),
                    CommandParameterMember = nameof(ActionParameter))]
                public string Action { get; } = "Run";

                [DataGridColumn(
                    Kind = DataGridColumnKind.ToggleButton,
                    ContentMember = nameof(ToggleLabel),
                    CheckedContentMember = nameof(CheckedLabel),
                    UncheckedContentMember = nameof(UncheckedLabel),
                    CommandMember = nameof(ToggleCommand),
                    CommandParameterMember = nameof(ToggleParameter))]
                public bool Enabled { get; set; }

                [DataGridColumn(
                    Kind = DataGridColumnKind.ToggleSwitch,
                    OnContentMember = nameof(OnLabel),
                    OffContentMember = nameof(OffLabel),
                    CommandMember = nameof(ToggleCommand))]
                public bool Online { get; set; }

                public string ActionLabel { get; } = "Execute";
                public object ActionParameter { get; } = new();
                public ICommand ActionCommand { get; } = new TestCommand();
                public string ToggleLabel { get; } = "State";
                public string CheckedLabel { get; } = "Enabled";
                public string UncheckedLabel { get; } = "Disabled";
                public string OnLabel { get; } = "Online";
                public string OffLabel { get; } = "Offline";
                public object ToggleParameter { get; } = new();
                public ICommand ToggleCommand { get; } = new TestCommand();
            }
            public sealed class TestCommand : ICommand
            {
                public event EventHandler? CanExecuteChanged;
                public bool CanExecute(object? parameter) => true;
                public void Execute(object? parameter) { }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("static item => item.ActionLabel", result.CombinedSource);
        Assert.Contains("static item => item.ActionCommand", result.CombinedSource);
        Assert.Contains("static item => item.ActionParameter", result.CombinedSource);
        Assert.Contains("column.ContentBinding = s_ActionContentBinding", result.CombinedSource);
        Assert.Contains("column.CommandBinding = s_ActionCommandBinding", result.CombinedSource);
        Assert.Contains("column.CommandParameterBinding = s_ActionCommandParameterBinding", result.CombinedSource);
        Assert.Contains("column.CheckedContentBinding = s_EnabledCheckedContentBinding", result.CombinedSource);
        Assert.Contains("column.UncheckedContentBinding = s_EnabledUncheckedContentBinding", result.CombinedSource);
        Assert.Contains("column.OnContentBinding = s_OnlineOnContentBinding", result.CombinedSource);
        Assert.Contains("column.OffContentBinding = s_OnlineOffContentBinding", result.CombinedSource);
        Assert.DoesNotContain("GetProperty(", result.CombinedSource);
    }

    [Fact]
    public void Missing_auxiliary_member_reports_PDGSG124()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn(Kind = DataGridColumnKind.Button, ContentMember = "Missing")]
                public string Action { get; } = "Run";
            }
            """);

        Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics.Where(static item => item.Id == "PDGSG124"));
        Assert.Contains("accessible readable instance property", diagnostic.GetMessage());
    }

    [Fact]
    public void Unsupported_or_conflicting_auxiliary_members_report_PDGSG124()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                public string Label { get; } = "Label";

                [DataGridColumn(Kind = DataGridColumnKind.Text, ContentMember = nameof(Label))]
                public string Name { get; set; } = "";

                [DataGridColumn(Kind = DataGridColumnKind.ToggleSwitch, Content = "On", OnContentMember = nameof(Label))]
                public bool Online { get; set; }
            }
            """);

        Diagnostic[] diagnostics = result.GeneratorDiagnostics.Where(static item => item.Id == "PDGSG124").ToArray();
        Assert.Equal(2, diagnostics.Length);
        Assert.Contains(diagnostics, static item => item.GetMessage().Contains("does not support this binding", StringComparison.Ordinal));
        Assert.Contains(diagnostics, static item => item.GetMessage().Contains("cannot be combined with static Content", StringComparison.Ordinal));
    }

    [Fact]
    public void Non_command_auxiliary_member_reports_PDGSG124()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                public string NotACommand { get; } = "Run";

                [DataGridColumn(Kind = DataGridColumnKind.Button, CommandMember = nameof(NotACommand))]
                public string Action { get; } = "Run";
            }
            """);

        Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics.Where(static item => item.Id == "PDGSG124"));
        Assert.Contains("must implement System.Windows.Input.ICommand", diagnostic.GetMessage());
    }

    [Fact]
    public void User_defined_schema_implementation_is_forwarded_without_reflection()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridFiltering;
            using Avalonia.Controls.DataGridSearching;
            using Avalonia.Controls.DataGridSorting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(ImplementationType = typeof(CustomSchema), ProviderName = "GeneratedFacade")]
            public sealed class Row { public int Id { get; set; } }
            public sealed class CustomSchema : IDataGridGeneratedSchema<Row>
            {
                public DataGridColumnDefinitionList CreateColumnDefinitions() => new();
                public IComparer<Row> CreateSortComparer(IReadOnlyList<SortingDescriptor> descriptors) => Comparer<Row>.Default;
                public Func<Row, bool> CreateFilterPredicate(IReadOnlyList<FilteringDescriptor> descriptors) => static _ => true;
                public Func<Row, bool> CreateSearchPredicate(IReadOnlyList<SearchDescriptor> descriptors) => static _ => true;
                public DataGridFastPathOptions CreateFastPathOptions() => new();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class GeneratedFacade", result.CombinedSource);
        Assert.Contains("new global::Demo.CustomSchema()", result.CombinedSource);
        Assert.Contains("_implementation.CreateSearchPredicate(descriptors)", result.CombinedSource);
    }

    [Fact]
    public void Runtime_defined_schema_implementation_forwards_manifest_and_marker()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridColumns(
                Discovery = DataGridColumnDiscovery.AttributedOnly,
                ImplementationType = typeof(RuntimeSchema),
                ProviderName = "GeneratedRuntimeFacade")]
            public sealed class RuntimeRow : Dictionary<string, object?> { }

            public sealed class RuntimeSchema : DataGridRuntimeSchemaAdapter<RuntimeRow>
            {
                public RuntimeSchema() : base(new Provider()) { }

                private sealed class Provider : IDataGridRuntimeSchemaProvider<RuntimeRow>
                {
                    public string SchemaId => "demo/runtime/v1";
                    public IReadOnlyList<DataGridRuntimeSchemaField<RuntimeRow>> CreateFields() =>
                    [
                        new(
                            "value",
                            "Value",
                            new DataGridColumnValueAccessor<RuntimeRow, object?>(static row => row.TryGetValue("Value", out object? value) ? value : null),
                            static () => new DataGridTextColumnDefinition())
                    ];
                    public DataGridFastPathOptions CreateFastPathOptions() =>
                        new() { UseAccessorsOnly = true, ThrowOnMissingAccessor = true };
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("global::Avalonia.Controls.IDataGridRuntimeDefinedSchema", result.CombinedSource);
        Assert.Contains("Manifest => _implementation.Manifest", result.CombinedSource);
        Assert.Contains("=> _implementation.RuntimeFields", result.CombinedSource);
        Assert.Contains("Fields => Instance.Manifest.Fields", result.CombinedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG002");
    }

    [Fact]
    public void Runtime_defined_shape_without_provider_reports_PDGSG134()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridColumns]
            public sealed class RuntimeBag : Dictionary<string, object?> { }
            """);

        Diagnostic diagnostic = Assert.Single(
            result.GeneratorDiagnostics.Where(static value => value.Id == "PDGSG134"));
        Assert.Contains("ImplementationType", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("class RuntimeBagDataGridSchema", result.CombinedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, static value => value.Id == "PDGSG002");
    }

    [Fact]
    public void Inherited_public_properties_are_included_by_default()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public class BaseRow { public int Id { get; set; } }
            [GenerateDataGridColumns]
            public sealed class Row : BaseRow { public string Name { get; set; } = ""; }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateIdColumn", result.CombinedSource);
        Assert.Contains("CreateNameColumn", result.CombinedSource);
    }

    [Fact]
    public void Avalonia_view_generation_emits_code_only_layout_and_compiled_binding_indexers()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), ViewName = "RowsPage", ViewNamespace = "Demo.Views", Title = "Rows")]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class RowsPage : global::Avalonia.Controls.UserControl", result.CombinedSource);
        Assert.Contains("dataGrid[!global::Avalonia.Controls.DataGrid.ItemsSourceProperty]", result.CombinedSource);
        Assert.Contains("CompiledBindingPathBuilder", result.CombinedSource);
        Assert.Contains("protected virtual void ConfigureGeneratedDataGrid", result.CombinedSource);
        Assert.Contains("AutomationProperties.SetAutomationId(dataGrid, GeneratedAutomationId)", result.CombinedSource);
        Assert.Contains("AutomationProperties.SetHeadingLevel(title, 1)", result.CombinedSource);
        Assert.Contains("The generated view uses compiled binding indexers and activation delegates", result.CombinedSource);
        Assert.Contains("The generated DataGrid is paired with strict generated column definitions and fast-path accessors", result.CombinedSource);
    }

    [Fact]
    public void Reactive_ui_view_strategy_uses_typed_reactive_user_control()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl { }
            }
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(typeof(Row), Framework = DataGridViewFramework.ReactiveUI)]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains(
            "ReactiveUserControl<global::Demo.RowsViewModel>",
            result.CombinedSource);
    }

    [Fact]
    public void Reactive_ui_view_generates_typed_activation_scoped_interaction_adapters()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI
            {
                public interface IActivatableView { }
                public interface IInteractionContext<out TInput, in TOutput>
                {
                    TInput Input { get; }
                    void SetOutput(TOutput output);
                }
                public interface IInteraction<TInput, TOutput>
                {
                    IDisposable RegisterHandler(Func<IInteractionContext<TInput, TOutput>, Task> handler);
                }
                public static class ViewForMixins
                {
                    public static void WhenActivated(IActivatableView view, Action<Action<IDisposable>> block) { }
                }
            }
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl, global::ReactiveUI.IActivatableView { }
            }
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                public sealed class ConfirmHandler : IDataGridGeneratedViewInteractionHandler<string, bool>, IDisposable
                {
                    public ValueTask<bool> HandleAsync(DataGridGeneratedViewInteractionContext<string> context) => new(true);
                    public void Dispose() { }
                }
                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(
                    typeof(Row),
                    Framework = DataGridViewFramework.ReactiveUI,
                    InteractionPropertyNames = new[] { nameof(Confirm) },
                    InteractionHandlerTypes = new[] { typeof(ConfirmHandler) })]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<string, bool> Confirm { get; } = null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("ConfigureGeneratedReactiveActivation(GeneratedDataGrid)", result.CombinedSource);
        Assert.Contains("ViewForMixins.WhenActivated", result.CombinedSource);
        Assert.Contains("viewModel.Confirm.RegisterHandler", result.CombinedSource);
        Assert.Contains("CreateGeneratedInteractionHandler0", result.CombinedSource);
        Assert.Contains("=> new global::Demo.ConfirmHandler()", result.CombinedSource);
        Assert.Contains("GetObservable(view, global::Avalonia.StyledElement.DataContextProperty)", result.CombinedSource);
        Assert.Contains("DataGridGeneratedViewInteractionContext<string>", result.CombinedSource);
        Assert.Contains("_handler0 is global::System.IDisposable disposableHandler", result.CombinedSource);
        Assert.Contains("interactionLifetime.CancellationToken", result.CombinedSource);
    }

    [Fact]
    public void Assembly_and_namespace_view_policies_share_typed_interaction_and_conditional_formatting_options()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridConditionalFormatting;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridView(
                typeof(Demo.AssemblyRowsViewModel),
                typeof(Demo.Models.Row),
                ViewName = "AssemblyRowsView",
                Framework = DataGridViewFramework.ReactiveUI,
                ConditionalFormattingModelPropertyName = "Formatting",
                NavigationInteractionPropertyName = "Navigation",
                InteractionPropertyNames = new[] { "Confirm" },
                InteractionHandlerTypes = new[] { typeof(Demo.Handlers.ConfirmHandler) })]
            [assembly: GenerateDataGridViewsForNamespace(
                "Demo.NamespaceViewModels",
                Framework = DataGridViewFramework.ReactiveUI,
                ConditionalFormattingModelPropertyName = "Formatting",
                NavigationInteractionPropertyName = "Navigation",
                InteractionPropertyNames = new[] { "Confirm" },
                InteractionHandlerTypes = new[] { typeof(Demo.Handlers.ConfirmHandler) })]
            namespace ReactiveUI
            {
                public interface IActivatableView { }
                public interface IInteractionContext<out TInput, in TOutput>
                {
                    TInput Input { get; }
                    void SetOutput(TOutput output);
                }
                public interface IInteraction<TInput, TOutput>
                {
                    IDisposable RegisterHandler(Func<IInteractionContext<TInput, TOutput>, Task> handler);
                }
                public static class ViewForMixins
                {
                    public static void WhenActivated(IActivatableView view, Action<Action<IDisposable>> block) { }
                }
            }
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl, global::ReactiveUI.IActivatableView { }
            }
            namespace Demo.Models
            {
                public sealed class Row { public int Id { get; set; } }
            }
            namespace Demo.Handlers
            {
                public sealed class ConfirmHandler : IDataGridGeneratedViewInteractionHandler<string, bool>
                {
                    public ValueTask<bool> HandleAsync(DataGridGeneratedViewInteractionContext<string> context) => new(true);
                }
            }
            namespace Demo
            {
                [GenerateDataGridViewModel(typeof(Models.Row))]
                public sealed partial class AssemblyRowsViewModel
                {
                    public IReadOnlyList<Models.Row> Items { get; } = new List<Models.Row>();
                    public IConditionalFormattingModel Formatting { get; } = new ConditionalFormattingModel();
                    public global::ReactiveUI.IInteraction<string, bool> Confirm { get; } = null!;
                    public global::ReactiveUI.IInteraction<
                        DataGridGeneratedNavigationRequest<Models.Row>,
                        DataGridGeneratedNavigationResult<Models.Row>> Navigation { get; } = null!;
                }
            }
            namespace Demo.NamespaceViewModels
            {
                [GenerateDataGridViewModel(typeof(global::Demo.Models.Row))]
                public sealed partial class PolicyRowsViewModel
                {
                    public IReadOnlyList<global::Demo.Models.Row> Items { get; } = new List<global::Demo.Models.Row>();
                    public IConditionalFormattingModel Formatting { get; } = new ConditionalFormattingModel();
                    public global::ReactiveUI.IInteraction<string, bool> Confirm { get; } = null!;
                    public global::ReactiveUI.IInteraction<
                        DataGridGeneratedNavigationRequest<global::Demo.Models.Row>,
                        DataGridGeneratedNavigationResult<global::Demo.Models.Row>> Navigation { get; } = null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class AssemblyRowsView :", result.CombinedSource);
        Assert.Contains("class PolicyRowsView :", result.CombinedSource);
        Assert.Equal(
            2,
            result.CombinedSource.Split(
                "=> new global::Demo.Handlers.ConfirmHandler();",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            2,
            result.CombinedSource.Split(
                "DataGridGeneratedNavigationHandler<global::Demo.Models.Row>()",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            2,
            result.CombinedSource.Split(
                "DataGrid.ConditionalFormattingModelProperty",
                StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Reactive_ui_view_generates_dedicated_typed_navigation_interaction()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI
            {
                public interface IActivatableView { }
                public interface IInteractionContext<out TInput, in TOutput>
                {
                    TInput Input { get; }
                    void SetOutput(TOutput output);
                }
                public interface IInteraction<TInput, TOutput>
                {
                    IDisposable RegisterHandler(Func<IInteractionContext<TInput, TOutput>, Task> handler);
                }
                public static class ViewForMixins
                {
                    public static void WhenActivated(IActivatableView view, Action<Action<IDisposable>> block) { }
                }
            }
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl, global::ReactiveUI.IActivatableView { }
            }
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(
                    typeof(Row),
                    Framework = DataGridViewFramework.ReactiveUI,
                    NavigationInteractionPropertyName = nameof(Navigation))]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<
                        DataGridGeneratedNavigationRequest<Row>,
                        DataGridGeneratedNavigationResult<Row>> Navigation { get; } = null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateGeneratedNavigationInteractionHandler", result.CombinedSource);
        Assert.Contains("DataGridGeneratedNavigationHandler<global::Demo.Row>()", result.CombinedSource);
        Assert.Contains("viewModel.Navigation.RegisterHandler", result.CombinedSource);
        Assert.Contains("GeneratedNavigationInteractionSubscription", result.CombinedSource);
        Assert.Contains("DataGridGeneratedNavigationRequest<global::Demo.Row>", result.CombinedSource);
        Assert.Contains("lifetime.Token", result.CombinedSource);
    }

    [Fact]
    public void Navigation_models_generate_factories_view_model_member_and_typed_view_bindings()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridNavigation;
            using ProDataGrid.SourceGeneration;
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }

                [GenerateDataGridViewModel(
                    typeof(Row),
                    GenerateNavigationModel = true,
                    NavigationModelPropertyName = nameof(CellNavigation))]
                [GenerateDataGridView(
                    typeof(Row),
                    NavigationModelPropertyName = nameof(CellNavigation),
                    RouteNavigationModelPropertyName = nameof(RouteNavigation))]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public IDataGridRouteNavigationModel RouteNavigation { get; } = null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridNavigationModel CellNavigation", result.CombinedSource);
        Assert.Contains("CreateNavigationModel()", result.CombinedSource);
        Assert.Contains("CreateRouteNavigationModel(", result.CombinedSource);
        Assert.Contains("DataGrid.NavigationModelProperty", result.CombinedSource);
        Assert.Contains("DataGrid.RouteNavigationModelProperty", result.CombinedSource);
        Assert.Contains("viewModel.CellNavigation", result.CombinedSource);
        Assert.Contains("viewModel.RouteNavigation", result.CombinedSource);
    }

    [Fact]
    public void Invalid_navigation_model_bindings_report_PDGSG141()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }

                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(
                    typeof(Row),
                    NavigationModelPropertyName = nameof(CellNavigation),
                    RouteNavigationModelPropertyName = nameof(RouteNavigation))]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public object CellNavigation { get; } = new object();
                    public object RouteNavigation { get; } = new object();
                }
            }
            """);

        Assert.Equal(
            2,
            result.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == "PDGSG141"));
        Assert.DoesNotContain("DataGrid.NavigationModelProperty", result.CombinedSource);
        Assert.DoesNotContain("DataGrid.RouteNavigationModelProperty", result.CombinedSource);
    }

    [Fact]
    public void Invalid_navigation_contracts_and_unbounded_high_frequency_details_are_rejected()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI
            {
                public interface IActivatableView { }
                public interface IInteraction<TInput, TOutput> { }
            }
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl, global::ReactiveUI.IActivatableView { }
            }
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }

                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(
                    typeof(Row),
                    ViewName = "PlainNavigationView",
                    NavigationInteractionPropertyName = nameof(Navigation))]
                public sealed partial class PlainViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<
                        DataGridGeneratedNavigationRequest<Row>,
                        DataGridGeneratedNavigationResult<Row>> Navigation { get; } = null!;
                }

                [GenerateDataGridViewModel(
                    typeof(Row),
                    ColumnDefinitionsPropertyName = "Columns2",
                    FastPathOptionsPropertyName = "Fast2")]
                [GenerateDataGridView(
                    typeof(Row),
                    ViewName = "WrongNavigationView",
                    Framework = DataGridViewFramework.ReactiveUI,
                    ColumnDefinitionsPropertyName = "Columns2",
                    FastPathOptionsPropertyName = "Fast2",
                    NavigationInteractionPropertyName = nameof(Navigation))]
                public sealed partial class WrongViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<
                        DataGridGeneratedNavigationRequest<Row>,
                        bool> Navigation { get; } = null!;
                }

                [GenerateDataGridViewModel(
                    typeof(Row),
                    ColumnDefinitionsPropertyName = "Columns3",
                    FastPathOptionsPropertyName = "Fast3")]
                [GenerateDataGridView(
                    typeof(Row),
                    ViewName = "UnboundedDetailsView",
                    ColumnDefinitionsPropertyName = "Columns3",
                    FastPathOptionsPropertyName = "Fast3",
                    PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming,
                    RowDetailsTemplateKey = "AlwaysVisibleDetails",
                    RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible)]
                public sealed partial class UnboundedDetailsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                }
            }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == "PDGSG127"));
        Diagnostic performance = Assert.Single(
            result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Id == "PDGSG128"));
        Assert.Contains("RowDetailsVisibilityMode.Visible", performance.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("class UnboundedDetailsView :", result.CombinedSource);
    }

    [Fact]
    public void Invalid_generated_view_interaction_contracts_report_PDGSG127()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI
            {
                public interface IActivatableView { }
                public interface IInteraction<TInput, TOutput> { }
            }
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl, global::ReactiveUI.IActivatableView { }
            }
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                public sealed class WrongHandler : IDataGridGeneratedViewInteractionHandler<int, bool>
                {
                    public ValueTask<bool> HandleAsync(DataGridGeneratedViewInteractionContext<int> context) => new(true);
                }

                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(
                    typeof(Row),
                    ViewName = "PlainInteractionView",
                    InteractionPropertyNames = new[] { nameof(Confirm) },
                    InteractionHandlerTypes = new[] { typeof(WrongHandler) })]
                public sealed partial class PlainViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<string, bool> Confirm { get; } = null!;
                }

                [GenerateDataGridViewModel(typeof(Row), ColumnDefinitionsPropertyName = "Columns2", FastPathOptionsPropertyName = "Fast2")]
                [GenerateDataGridView(
                    typeof(Row),
                    ViewName = "MismatchedInteractionView",
                    Framework = DataGridViewFramework.ReactiveUI,
                    ColumnDefinitionsPropertyName = "Columns2",
                    FastPathOptionsPropertyName = "Fast2",
                    InteractionPropertyNames = new[] { nameof(Confirm) },
                    InteractionHandlerTypes = new Type[0])]
                public sealed partial class MismatchedViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<string, bool> Confirm { get; } = null!;
                }

                [GenerateDataGridViewModel(typeof(Row), ColumnDefinitionsPropertyName = "Columns3", FastPathOptionsPropertyName = "Fast3")]
                [GenerateDataGridView(
                    typeof(Row),
                    ViewName = "WrongHandlerView",
                    Framework = DataGridViewFramework.ReactiveUI,
                    ColumnDefinitionsPropertyName = "Columns3",
                    FastPathOptionsPropertyName = "Fast3",
                    InteractionPropertyNames = new[] { nameof(Confirm) },
                    InteractionHandlerTypes = new[] { typeof(WrongHandler) })]
                public sealed partial class WrongHandlerViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<string, bool> Confirm { get; } = null!;
                }
            }
            """);

        Diagnostic[] diagnostics = result.GeneratorDiagnostics
            .Where(static diagnostic => diagnostic.Id == "PDGSG127")
            .ToArray();
        Assert.Equal(3, diagnostics.Length);
        Assert.DoesNotContain("class PlainInteractionView :", result.CombinedSource);
        Assert.DoesNotContain("class MismatchedInteractionView :", result.CombinedSource);
        Assert.DoesNotContain("class WrongHandlerView :", result.CombinedSource);
    }

    [Fact]
    public void Reactive_activation_features_require_activatable_custom_base()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI
            {
                public interface IActivatableView { }
                public interface IInteraction<TInput, TOutput> { }
            }
            namespace Demo
            {
                public class PassiveBase : UserControl { }
                public sealed class Row { public int Id { get; set; } }
                public sealed class ConfirmHandler : IDataGridGeneratedViewInteractionHandler<string, bool>
                {
                    public ValueTask<bool> HandleAsync(DataGridGeneratedViewInteractionContext<string> context) => new(true);
                }
                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(
                    typeof(Row),
                    Framework = DataGridViewFramework.ReactiveUI,
                    BaseType = typeof(PassiveBase),
                    InteractionPropertyNames = new[] { nameof(Confirm) },
                    InteractionHandlerTypes = new[] { typeof(ConfirmHandler) })]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public global::ReactiveUI.IInteraction<string, bool> Confirm { get; } = null!;
                }
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG013");
        Assert.DoesNotContain("class RowsView :", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_emits_compiled_loading_empty_error_and_retry_projections()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using System.Windows.Input;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                ViewStatePropertyName = nameof(ViewState),
                ErrorMessagePropertyName = nameof(ErrorMessage),
                RetryCommandPropertyName = nameof(RetryCommand),
                LoadingText = "Fetching rows",
                EmptyText = "Nothing matched",
                ErrorText = "Rows unavailable",
                RetryText = "Try again")]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public DataGridGeneratedViewState ViewState { get; set; }
                public string? ErrorMessage { get; set; }
                public ICommand RetryCommand { get; } = null!;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateGeneratedViewStateHost", result.CombinedSource);
        Assert.Contains("CreateGeneratedLoadingContent", result.CombinedSource);
        Assert.Contains("CreateGeneratedEmptyContent", result.CombinedSource);
        Assert.Contains("CreateGeneratedErrorContent", result.CombinedSource);
        Assert.Contains("s_viewStateProperty", result.CombinedSource);
        Assert.Contains("s_errorMessageProperty", result.CombinedSource);
        Assert.Contains("s_retryCommandProperty", result.CombinedSource);
        Assert.Contains("Button.CommandProperty", result.CombinedSource);
        Assert.Contains("GeneratedViewStateVisibilityConverter", result.CombinedSource);
        Assert.Contains("GeneratedErrorMessageConverter", result.CombinedSource);
        Assert.Contains("s_errorMessageConverter", result.CombinedSource);
        Assert.Contains("\"Fetching rows\"", result.CombinedSource);
        Assert.Contains("\"Nothing matched\"", result.CombinedSource);
        Assert.Contains("\"Rows unavailable\"", result.CombinedSource);
        Assert.Contains("\"Try again\"", result.CombinedSource);
    }

    [Fact]
    public void Invalid_generated_view_state_member_types_report_PDGSG125()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                ViewStatePropertyName = nameof(ViewState),
                ErrorMessagePropertyName = nameof(ErrorMessage),
                RetryCommandPropertyName = nameof(RetryCommand))]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public string ViewState { get; } = "Loading";
                public int ErrorMessage { get; }
                public object RetryCommand { get; } = new object();
            }
            """);

        Diagnostic[] diagnostics = result.GeneratorDiagnostics
            .Where(static diagnostic => diagnostic.Id == "PDGSG125")
            .ToArray();
        Assert.Equal(3, diagnostics.Length);
        Assert.DoesNotContain("class RowsView :", result.CombinedSource);
    }

    [Fact]
    public void State_projection_options_without_state_member_report_PDGSG125()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using System.Windows.Input;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), RetryCommandPropertyName = nameof(RetryCommand))]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public ICommand RetryCommand { get; } = null!;
            }
            """);

        Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics.Where(static item => item.Id == "PDGSG125"));
        Assert.Contains("ViewStatePropertyName", diagnostic.GetMessage());
    }

    [Fact]
    public void Generated_view_emits_only_selected_typed_routed_event_command_bridges()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using System.Windows.Input;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanged |
                               DataGridGeneratedViewEventKinds.SelectionChanging |
                               DataGridGeneratedViewEventKinds.BeginningEdit |
                               DataGridGeneratedViewEventKinds.RowEditEnded |
                               DataGridGeneratedViewEventKinds.CellLifecycle |
                               DataGridGeneratedViewEventKinds.CellValueChanged,
                RoutedEventCommandPropertyName = nameof(GridEventCommand))]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public ICommand GridEventCommand { get; } = null!;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("ConfigureGeneratedRoutedEventCommands", result.CombinedSource);
        Assert.Contains("dataGrid.SelectionChanged += OnGeneratedSelectionChanged", result.CombinedSource);
        Assert.Contains("dataGrid.SelectionChanging += OnGeneratedSelectionChanging", result.CombinedSource);
        Assert.Contains("dataGrid.BeginningEdit += OnGeneratedBeginningEdit", result.CombinedSource);
        Assert.Contains("dataGrid.RowEditEnded += OnGeneratedRowEditEnded", result.CombinedSource);
        Assert.Contains("dataGrid.CellPrepared += OnGeneratedCellPrepared", result.CombinedSource);
        Assert.Contains("dataGrid.CellClearing += OnGeneratedCellClearing", result.CombinedSource);
        Assert.Contains("dataGrid.CellValueChanged += OnGeneratedCellValueChanged", result.CombinedSource);
        Assert.Contains("DataGridGeneratedViewEvent<global::Demo.Row>", result.CombinedSource);
        Assert.Contains("viewModel.GridEventCommand", result.CombinedSource);
        Assert.Contains("e.Cancel = eventData.Cancel", result.CombinedSource);
        Assert.Contains("CreateSelectionChanging(e)", result.CombinedSource);
        Assert.Contains("CreateCellLifecycle(", result.CombinedSource);
        Assert.Contains("CreateCellValueChanged(", result.CombinedSource);
        Assert.DoesNotContain("OnGeneratedCurrentCellChanged", result.CombinedSource);
        Assert.DoesNotContain("OnGeneratedCellEditEnding", result.CombinedSource);
    }

    [Fact]
    public void Invalid_generated_view_routed_event_contracts_report_PDGSG126()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }

            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                ViewName = "MissingCommandView",
                RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanged)]
            public sealed partial class MissingCommandViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
            }

            [GenerateDataGridViewModel(typeof(Row), ColumnDefinitionsPropertyName = "Columns2", FastPathOptionsPropertyName = "Fast2")]
            [GenerateDataGridView(
                typeof(Row),
                ViewName = "MissingEventsView",
                ColumnDefinitionsPropertyName = "Columns2",
                FastPathOptionsPropertyName = "Fast2",
                RoutedEventCommandPropertyName = nameof(GridEventCommand))]
            public sealed partial class MissingEventsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public object GridEventCommand { get; } = new object();
            }

            [GenerateDataGridViewModel(typeof(Row), ColumnDefinitionsPropertyName = "Columns3", FastPathOptionsPropertyName = "Fast3")]
            [GenerateDataGridView(
                typeof(Row),
                ViewName = "InvalidCommandView",
                ColumnDefinitionsPropertyName = "Columns3",
                FastPathOptionsPropertyName = "Fast3",
                RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanged,
                RoutedEventCommandPropertyName = nameof(GridEventCommand))]
            public sealed partial class InvalidCommandViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public object GridEventCommand { get; } = new object();
            }

            [GenerateDataGridViewModel(typeof(Row), ColumnDefinitionsPropertyName = "Columns4", FastPathOptionsPropertyName = "Fast4")]
            [GenerateDataGridView(
                typeof(Row),
                ViewName = "InvalidFlagsView",
                ColumnDefinitionsPropertyName = "Columns4",
                FastPathOptionsPropertyName = "Fast4",
                RoutedEvents = (DataGridGeneratedViewEventKinds)4096,
                RoutedEventCommandPropertyName = nameof(GridEventCommand))]
            public sealed partial class InvalidFlagsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public object GridEventCommand { get; } = new object();
            }
            """);

        Diagnostic[] diagnostics = result.GeneratorDiagnostics
            .Where(static diagnostic => diagnostic.Id == "PDGSG126")
            .ToArray();
        Assert.Equal(4, diagnostics.Length);
        Assert.DoesNotContain("class MissingCommandView :", result.CombinedSource);
        Assert.DoesNotContain("class MissingEventsView :", result.CombinedSource);
        Assert.DoesNotContain("class InvalidCommandView :", result.CombinedSource);
        Assert.DoesNotContain("class InvalidFlagsView :", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_emits_typed_nested_grid_row_details()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(ProviderName = "DetailSchema")]
            public sealed class Detail { public string Name { get; set; } = ""; }
            public sealed class Row
            {
                public string Summary { get; set; } = "";
                public IReadOnlyList<Detail> Details { get; } = new List<Detail>();
            }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected,
                AreRowDetailsFrozen = true,
                RowDetailsNestedItemType = typeof(Detail),
                RowDetailsNestedItemsMember = nameof(Row.Details),
                RowDetailsNestedProviderName = "DetailSchema",
                RowDetailsSummaryMember = nameof(Row.Summary),
                RowDetailsAutomationId = "row-detail-grid")]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridGeneratedFuncDataTemplate<global::Demo.Row>(CreateGeneratedRowDetails)", result.CombinedSource);
        Assert.Contains("class GeneratedRowDetailsPresenter", result.CombinedSource);
        Assert.Contains("global::Demo.DetailSchema.Instance.CreateColumnDefinitions()", result.CombinedSource);
        Assert.Contains("_nestedGrid.ItemsSource = (global::System.Collections.Generic.IEnumerable<global::Demo.Detail>)item.Details", result.CombinedSource);
        Assert.Contains("_summary!.Text = item.Summary", result.CombinedSource);
        Assert.Contains("dataGrid.AreRowDetailsFrozen = true", result.CombinedSource);
        Assert.Contains("\"row-detail-grid\"", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_supports_row_details_resource_factory_and_implementation_sources()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using Avalonia.Controls.Templates;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                public int Id { get; set; }
                public static Control CreateDetails(Row item, Control existing) => existing ?? new TextBlock { Text = item.Id.ToString() };
            }
            public sealed class DetailsTemplate : IDataTemplate
            {
                public Control Build(object data) => new TextBlock();
                public bool Match(object data) => data is Row;
            }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), ViewName = "ResourceView", RowDetailsTemplateKey = "RowDetails")]
            [GenerateDataGridView(typeof(Row), ViewName = "FactoryView", RowDetailsTemplateFactoryMethod = nameof(Row.CreateDetails))]
            [GenerateDataGridView(typeof(Row), ViewName = "ImplementationView", RowDetailsTemplateImplementationType = typeof(DetailsTemplate))]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DynamicResourceExtension(\"RowDetails\")", result.CombinedSource);
        Assert.Contains("DataGridGeneratedFuncDataTemplate<global::Demo.Row>(global::Demo.Row.CreateDetails)", result.CombinedSource);
        Assert.Contains("dataGrid.RowDetailsTemplate = new global::Demo.DetailsTemplate()", result.CombinedSource);
    }

    [Fact]
    public void Conflicting_row_details_sources_report_PDGSG123()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Detail { }
            public sealed class Row { public IReadOnlyList<Detail> Details { get; } = new List<Detail>(); }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                RowDetailsTemplateKey = "Details",
                RowDetailsNestedItemType = typeof(Detail),
                RowDetailsNestedItemsMember = nameof(Row.Details))]
            public sealed partial class RowsViewModel { public IReadOnlyList<Row> Items { get; } = new List<Row>(); }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG123");
        Assert.DoesNotContain("class RowsView :", result.CombinedSource);
    }

    [Fact]
    public void Invalid_nested_row_details_member_reports_PDGSG123()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Detail { }
            public sealed class Row { public string Details { get; } = ""; }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                RowDetailsNestedItemType = typeof(Detail),
                RowDetailsNestedItemsMember = nameof(Row.Details))]
            public sealed partial class RowsViewModel { public IReadOnlyList<Row> Items { get; } = new List<Row>(); }
            """);

        Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Id == "PDGSG123"));
        Assert.Contains("IEnumerable<Demo.Detail>", diagnostic.GetMessage());
    }

    [Fact]
    public void Nested_row_type_edit_invalidates_direct_view_composition()
    {
        const string before = """
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Detail { public string Name { get; set; } = ""; }
            public sealed class Row { public IReadOnlyList<Detail> Details { get; } = new List<Detail>(); }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), RowDetailsNestedItemType = typeof(Detail), RowDetailsNestedItemsMember = nameof(Row.Details))]
            public sealed partial class RowsViewModel { public IReadOnlyList<Row> Items { get; } = new List<Row>(); }
            """;
        const string after = """
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Detail { public string Label { get; set; } = ""; }
            public sealed class Row { public IReadOnlyList<Detail> Details { get; } = new List<Detail>(); }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), RowDetailsNestedItemType = typeof(Detail), RowDetailsNestedItemsMember = nameof(Row.Details))]
            public sealed partial class RowsViewModel { public IReadOnlyList<Row> Items { get; } = new List<Row>(); }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { before },
            new[] { after },
            "DirectViewComposition");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
    }

    [Fact]
    public void Assembly_view_attribute_supports_row_details_metadata()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridView(
                typeof(Demo.RowsViewModel),
                typeof(Demo.Row),
                ViewName = "AssemblyRowsView",
                RowDetailsTemplateKey = "AssemblyDetails")]
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                public sealed class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                    public Avalonia.Controls.DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                    public Avalonia.Controls.DataGridFastPathOptions FastPathOptions { get; } = new();
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class AssemblyRowsView", result.CombinedSource);
        Assert.Contains("DynamicResourceExtension(\"AssemblyDetails\")", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_supports_custom_base_and_search_binding()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public class GridViewBase : UserControl { }
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), BaseType = typeof(GridViewBase), SearchTextPropertyName = nameof(Query))]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public string Query { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class RowsView : global::Demo.GridViewBase", result.CombinedSource);
        Assert.Contains("TextBox.TextProperty", result.CombinedSource);
        Assert.Contains("BindingMode.TwoWay", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_binds_shared_selection_and_emits_state_adapter()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using Avalonia.Controls.Selection;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { [DataGridKey] public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                SelectionModelPropertyName = nameof(Selection),
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
                StateControllerPropertyName = nameof(GridState))]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public ISelectionModel Selection { get; } = null!;
                public DataGridGeneratedStateController GridState { get; } = null!;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGrid.SelectionProperty", result.CombinedSource);
        Assert.Contains("dataGrid.SelectionMode = (global::Avalonia.Controls.DataGridSelectionMode)0", result.CombinedSource);
        Assert.Contains("dataGrid.SelectionUnit = (global::Avalonia.Controls.DataGridSelectionUnit)2", result.CombinedSource);
        Assert.Contains("CaptureGeneratedState(", result.CombinedSource);
        Assert.Contains("RestoreGeneratedState(", result.CombinedSource);
        Assert.Contains("GridState).Capture(GeneratedDataGrid", result.CombinedSource);
        string viewSource = result.Sources.Single(static source => source.Contains("class RowsView :", StringComparison.Ordinal));
        Assert.True(
            viewSource.IndexOf("dataGrid.SelectionMode =", StringComparison.Ordinal) <
            viewSource.IndexOf("dataGrid[!global::Avalonia.Controls.DataGrid.SelectionProperty]", StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_view_binds_hierarchical_model_and_enables_hierarchical_rows()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridFiltering;
            using Avalonia.Controls.DataGridHierarchical;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(HierarchicalRows = true)]
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridChildren] public List<Row> Children { get; } = new();
                [DataGridColumn(DataGridColumnKind.Hierarchical)] public Row Item => this;
            }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                HierarchicalModelPropertyName = nameof(Tree),
                FilteringModelPropertyName = nameof(Filtering),
                HierarchyFilterPolicy = DataGridHierarchyFilterPolicy.KeepAncestorsOfMatches |
                                        DataGridHierarchyFilterPolicy.KeepDescendantsOfMatches)]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
                public HierarchicalModel<Row> Tree { get; } = new();
                public IFilteringModel Filtering { get; } = new FilteringModel();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGrid.HierarchicalModelProperty", result.CombinedSource);
        Assert.Contains("dataGrid.HierarchicalRowsEnabled = true", result.CombinedSource);
        Assert.Contains("dataGrid.Classes.Add(\"hierarchical\")", result.CombinedSource);
        Assert.Contains("CreateGeneratedHierarchicalFilteringAdapterFactory", result.CombinedSource);
        Assert.Contains("DataGridHierarchicalFilteringAdapterFactory", result.CombinedSource);
        Assert.Contains("Policy = (global::Avalonia.Controls.DataGridFiltering.DataGridHierarchyFilterPolicy)3", result.CombinedSource);
        Assert.DoesNotContain("DataGrid.ItemsSourceProperty", result.CombinedSource);
        string viewSource = result.Sources.Single(static source => source.Contains("class RowsView :", StringComparison.Ordinal));
        Assert.True(
            viewSource.IndexOf("DataGrid.HierarchicalModelProperty", StringComparison.Ordinal) <
            viewSource.IndexOf("DataGrid.FilteringModelProperty", StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_view_rejects_unknown_hierarchy_filter_policy_flags()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls.DataGridFiltering;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), HierarchyFilterPolicy = (DataGridHierarchyFilterPolicy)4)]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new List<Row>();
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG128");
        Assert.DoesNotContain("class RowsView :", result.CombinedSource);
    }

    [Fact]
    public void Assembly_view_attribute_and_namespace_view_attribute_are_supported()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridView(typeof(Demo.FirstViewModel), typeof(Demo.Row), ViewName = "FirstGrid")]
            [assembly: GenerateDataGridViewsForNamespace("Demo.Generated")]
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                [GenerateDataGridViewModel(typeof(Row))]
                public sealed partial class FirstViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                }
            }
            namespace Demo.Generated
            {
                public sealed class Item { public int Id { get; set; } }
                [GenerateDataGridViewModel(typeof(Item))]
                public sealed partial class SecondViewModel
                {
                    public IReadOnlyList<Item> Items { get; } = new List<Item>();
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class FirstGrid", result.CombinedSource);
        Assert.Contains("class SecondView", result.CombinedSource);
    }

    [Fact]
    public void Namespace_view_policy_applies_typed_state_projection_options()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using System.Windows.Input;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridViewsForNamespace(
                "Demo.ViewModels",
                IncludeNestedNamespaces = false,
                ViewStatePropertyName = "ViewState",
                ErrorMessagePropertyName = "ErrorMessage",
                RetryCommandPropertyName = "RetryCommand",
                EmptyText = "No namespace rows")]
            namespace Demo.Models
            {
                public sealed class Row { public int Id { get; set; } }
            }
            namespace Demo.ViewModels
            {
                public sealed class RowsViewModel
                {
                    public IReadOnlyList<Demo.Models.Row> Items { get; } = new List<Demo.Models.Row>();
                    public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                    public DataGridFastPathOptions FastPathOptions { get; } = new();
                    public DataGridGeneratedViewState ViewState { get; set; }
                    public string? ErrorMessage { get; set; }
                    public ICommand RetryCommand { get; } = null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class RowsView", result.CombinedSource);
        Assert.Contains("CreateGeneratedViewStateHost", result.CombinedSource);
        Assert.Contains("\"No namespace rows\"", result.CombinedSource);
    }

    [Fact]
    public void Namespace_view_policy_applies_routed_event_command_bridge_options()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using System.Windows.Input;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridViewsForNamespace(
                "Demo.ViewModels",
                IncludeNestedNamespaces = false,
                RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanged |
                               DataGridGeneratedViewEventKinds.CurrentCellChanged,
                RoutedEventCommandPropertyName = "GridEventCommand")]
            namespace Demo.Models
            {
                public sealed class Row { public int Id { get; set; } }
            }
            namespace Demo.ViewModels
            {
                public sealed class RowsViewModel
                {
                    public IReadOnlyList<Demo.Models.Row> Items { get; } = new List<Demo.Models.Row>();
                    public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                    public DataGridFastPathOptions FastPathOptions { get; } = new();
                    public ICommand GridEventCommand { get; } = null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class RowsView", result.CombinedSource);
        Assert.Contains("OnGeneratedSelectionChanged", result.CombinedSource);
        Assert.Contains("OnGeneratedCurrentCellChanged", result.CombinedSource);
        Assert.DoesNotContain("OnGeneratedSorting", result.CombinedSource);
    }

    [Fact]
    public void Missing_generated_view_member_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridView(typeof(Row), SearchTextPropertyName = "Missing")]
            public sealed class RowsViewModel { public Row[] Items { get; } = []; }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG012");
    }

    [Fact]
    public void Multiple_view_attributes_generate_independent_framework_views()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl { }
            }
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(typeof(Row), ViewName = "PlainRowsView")]
                [GenerateDataGridView(typeof(Row), ViewName = "ReactiveRowsView", Framework = DataGridViewFramework.ReactiveUI)]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = new List<Row>();
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class PlainRowsView", result.CombinedSource);
        Assert.Contains("class ReactiveRowsView", result.CombinedSource);
    }

    [Fact]
    public void Invalid_custom_view_base_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public class NotAControl { }
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), BaseType = typeof(NotAControl))]
            public sealed partial class RowsViewModel { public Row[] Items { get; } = []; }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG013");
    }

    [Fact]
    public void Reactive_source_generated_fields_are_recognized_as_view_binding_members()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI.SourceGenerators
            {
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class ReactiveAttribute : Attribute { }
            }
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridView(typeof(Row), SearchTextPropertyName = "Query")]
                public sealed partial class RowsViewModel
                {
                    public Row[] Items { get; } = [];
                    [ReactiveUI.SourceGenerators.Reactive] private string _query = "";
                }
            }
            """);

        Assert.DoesNotContain(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG012");
        Assert.Contains("viewModel.Query", result.CombinedSource);
    }

    [Fact]
    public void Non_partial_view_model_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            public sealed class RowsViewModel { }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG005");
    }

    [Fact]
    public void Invalid_template_configuration_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn(Kind = DataGridColumnKind.Template)]
                public string Value { get; set; } = "";
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG009");
    }

    [Fact]
    public void Unsupported_attributed_property_reports_PDGSG003()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn]
                public static string Name { get; set; } = "";
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG003");
    }

    [Fact]
    public void Invalid_schema_implementation_reports_PDGSG007()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class InvalidSchema { }
            [GenerateDataGridColumns(ImplementationType = typeof(InvalidSchema))]
            public sealed class Row { public int Id { get; set; } }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG007");
    }

    [Fact]
    public void Empty_namespace_policy_reports_PDGSG008()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridColumnsForNamespace("Missing.Models")]
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG008");
    }

    [Fact]
    public void Inaccessible_attributed_property_reports_PDGSG010()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn]
                private string Name { get; set; } = "";
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG010");
        Assert.DoesNotContain(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG003");
    }

    [Fact]
    public void Namespace_view_model_without_unambiguous_items_reports_PDGSG011()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridViewModelsForNamespace("Demo.ViewModels")]
            namespace Demo.ViewModels;
            public sealed partial class RowsViewModel { }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG011");
    }

    [Fact]
    public void Missing_reactive_view_framework_reports_PDGSG014()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), Framework = DataGridViewFramework.ReactiveUI)]
            public sealed partial class RowsViewModel { public Row[] Items { get; } = []; }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG014");
    }

    [Fact]
    public void Schema_emits_canonical_manifest_and_typed_item_key()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(SchemaId = "orders/v2")]
            public sealed class Row
            {
                [DataGridKey]
                public int Id { get; init; }

                [DataGridColumn(ColumnKey = "display-name")]
                public string Name { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("IDataGridGeneratedSchemaManifestProvider", result.CombinedSource);
        Assert.Contains("IDataGridItemKey<global::Demo.Row, int>", result.CombinedSource);
        Assert.Contains("public const int ManifestVersion = 1", result.CombinedSource);
        Assert.Contains("public const string SchemaId = \"orders/v2\"", result.CombinedSource);
        Assert.Contains("DataGridGeneratedComparableField<global::Demo.Row, int> Id", result.CombinedSource);
        Assert.Contains("DataGridGeneratedStringField<global::Demo.Row, string> Name", result.CombinedSource);
        Assert.Contains("(0, \"Id\", \"Id\"", result.CombinedSource);
        Assert.Contains("(1, \"display-name\", \"Name\"", result.CombinedSource);
        Assert.Contains("public int GetKey(global::Demo.Row item)", result.CombinedSource);
        Assert.Contains("=> item.Id;", result.CombinedSource);
        Assert.Contains("CreateItemIndex()", result.CombinedSource);
        Assert.Contains("IEqualityComparer<int> KeyComparer", result.CombinedSource);
        Assert.Contains("CreateIdentitySelectionModel()", result.CombinedSource);
        Assert.Contains("DataGridStateOptions CreateStateOptions", result.CombinedSource);
        Assert.Contains("ItemKeySelector = static item => ((global::Demo.Row)item).Id", result.CombinedSource);
        Assert.Contains("Array.AsReadOnly(s_fields)", result.CombinedSource);
        Assert.DoesNotContain("CreateStreamBuffer", result.CombinedSource);
    }

    [Fact]
    public void Field_can_define_typed_item_key()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridKey]
                public readonly long Id;

                public string Name { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("IDataGridItemKey<global::Demo.Row, long>", result.CombinedSource);
        Assert.Contains("public long GetKey(global::Demo.Row item)", result.CombinedSource);
    }

    [Fact]
    public void Static_key_selector_generates_composite_key_for_every_keyed_adapter()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(KeySelectorMethod = nameof(CreateKey))]
            public sealed class Row
            {
                public int TenantId { get; init; }
                public long OrderId { get; init; }
                public string Name { get; set; } = "";

                public static (int TenantId, long OrderId) CreateKey(Row item) =>
                    (item.TenantId, item.OrderId);
            }
            """);

        AssertNoErrors(result);
        Assert.Contains(
            "IDataGridItemKey<global::Demo.Row, (int TenantId, long OrderId)>",
            result.CombinedSource);
        Assert.Contains("=> global::Demo.Row.CreateKey(item);", result.CombinedSource);
        Assert.Contains("CreateItemIndex()", result.CombinedSource);
        Assert.Contains("CreateIdentitySelectionModel()", result.CombinedSource);
        Assert.Contains("ItemKeySelector = static item => global::Demo.Row.CreateKey(((global::Demo.Row)item))!", result.CombinedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG101");
    }

    [Fact]
    public void Reference_identity_key_uses_reference_comparer_and_direct_item_selector()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(UseReferenceIdentityKey = true)]
            public sealed class Row
            {
                public string Name { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("IDataGridItemKey<global::Demo.Row, global::Demo.Row>", result.CombinedSource);
        Assert.Contains("ReferenceEqualityComparer.Instance", result.CombinedSource);
        Assert.Contains("public global::Demo.Row GetKey(global::Demo.Row item)", result.CombinedSource);
        Assert.Contains("=> item;", result.CombinedSource);
        Assert.Contains("\"$reference\"", result.CombinedSource);
    }

    [Fact]
    public void Controller_can_select_static_composite_key_method()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn] public int TenantId { get; init; }
                [DataGridColumn] public long OrderId { get; init; }
                public static (int, long) CreateKey(Row item) => (item.TenantId, item.OrderId);
            }
            [GenerateDataGridController(typeof(Row), "Rows", KeySelectorMethod = nameof(Row.CreateKey))]
            public sealed partial class RowsViewModel
            {
                public IEnumerable<Row> Items { get; } = new List<Row>();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("global::Demo.Row.CreateKey(item)", result.CombinedSource);
        Assert.Contains("DataGridGeneratedItemIndex<global::Demo.Row, (int, long)>", result.CombinedSource);
    }

    [Fact]
    public void Invalid_or_conflicting_generated_key_modes_report_PDGSG101()
    {
        GeneratorTestResult invalidMethod = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(KeySelectorMethod = nameof(CreateKey))]
            public sealed class Row
            {
                public int Id { get; init; }
                private int CreateKey() => Id;
            }
            """);
        GeneratorTestResult invalidReference = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(UseReferenceIdentityKey = true)]
            public struct Row
            {
                public int Id { get; init; }
            }
            """);
        GeneratorTestResult conflicting = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(KeySelectorMethod = nameof(CreateKey), UseReferenceIdentityKey = true)]
            public sealed class Row
            {
                public int Id { get; init; }
                public static int CreateKey(Row item) => item.Id;
            }
            """);

        Assert.Contains(invalidMethod.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG101");
        Assert.Contains(invalidReference.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG101");
        Assert.Contains(conflicting.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG101");
    }

    [Fact]
    public void Streaming_keyed_schema_generates_bounded_buffer_factory()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(Streaming = true)]
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                public string Name { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridGeneratedStreamBuffer<global::Demo.Row, int> CreateStreamBuffer", result.CombinedSource);
        Assert.Contains("DataGridGeneratedAsyncStreamPump<global::Demo.Row, int> CreateAsyncStreamPump", result.CombinedSource);
        Assert.Contains("DataGridGeneratedStreamOverflowPolicy.CoalesceByKey", result.CombinedSource);
    }

    [Fact]
    public void Duplicate_item_keys_report_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridKey] public int AlternateId { get; init; }
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG101");
        Assert.DoesNotContain("IDataGridItemKey<", result.CombinedSource);
    }

    [Fact]
    public void Duplicate_column_keys_report_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(ColumnKey = "value")] public int First { get; set; }
                [DataGridColumn(ColumnKey = "value")] public int Second { get; set; }
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG100");
    }

    [Fact]
    public void Nullable_item_key_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridKey] public string? Id { get; init; }
                public string Name { get; set; } = "";
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG101");
    }

    [Fact]
    public void Generated_output_is_deterministic()
    {
        const string source = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } public string Name { get; set; } = ""; }
            """;

        GeneratorTestResult first = GeneratorTestHelper.Run(source);
        GeneratorTestResult second = GeneratorTestHelper.Run(source);
        Assert.Equal(first.CombinedSource, second.CombinedSource);
    }

    [Fact]
    public void Direct_only_compilation_skips_compilation_wide_semantic_model()
    {
        const string directSchema = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class DirectRow { public int Id { get; set; } }
            public sealed class PropertyRow
            {
                [DataGridColumn] public string Name { get; set; } = "";
            }
            """;
        const string unrelatedBefore = """
            namespace Demo;
            public sealed class Unrelated { public int Value { get; set; } }
            """;
        const string unrelatedAfter = """
            namespace Demo;
            public sealed class Unrelated { public string Value { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { directSchema, unrelatedBefore },
            new[] { directSchema, unrelatedAfter },
            "SemanticModel");

        Assert.Empty(result.Reasons);
        Assert.Contains(result.Sources, static source =>
            source.Contains("class DirectRowDataGridSchema", StringComparison.Ordinal));
        Assert.Contains(result.Sources, static source =>
            source.Contains("class PropertyRowDataGridSchema", StringComparison.Ordinal));
    }

    [Fact]
    public void Assembly_policy_keeps_compilation_wide_semantic_model_active()
    {
        const string policy = """
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridColumnsForNamespace("Demo.Models")]
            """;
        const string rowBefore = """
            namespace Demo.Models;
            public sealed class Row { public int Id { get; set; } }
            """;
        const string rowAfter = """
            namespace Demo.Models;
            public sealed class Row { public int Id { get; set; } public decimal Amount { get; set; } }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { policy, rowBefore },
            new[] { policy, rowAfter },
            "SemanticModel");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(result.Sources, static source =>
            source.Contains("class RowDataGridSchema", StringComparison.Ordinal));
    }

    [Fact]
    public void Unchanged_schema_generation_is_reused_when_another_schema_changes()
    {
        const string firstSource = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class First { public int Id { get; set; } }
            [GenerateDataGridColumns]
            public sealed class Second { public string Name { get; set; } = ""; }
            """;
        const string secondSource = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class First { public int Id { get; set; } public decimal Amount { get; set; } }
            [GenerateDataGridColumns]
            public sealed class Second { public string Name { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            firstSource,
            secondSource,
            "DirectSchemaGeneration");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(result.Reasons, static reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
        Assert.Equal(3, result.Sources.Count); // injected attributes plus two schemas
    }

    [Fact]
    public void Unchanged_schema_semantic_build_is_reused_when_another_schema_changes()
    {
        const string firstBefore = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class First { public int Id { get; set; } }
            """;
        const string firstAfter = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class First { public int Id { get; set; } public decimal Amount { get; set; } }
            """;
        const string unchangedSecond = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Second { public string Name { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { firstBefore, unchangedSecond },
            new[] { firstAfter, unchangedSecond },
            "DirectSchemaGeneration");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(result.Reasons, static reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Interface_schema_semantic_build_tracks_inherited_contract_changes()
    {
        const string before = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public interface IEntity { int Id { get; } }
            [GenerateDataGridColumns]
            public interface IRow : IEntity { string Name { get; } }
            """;
        const string after = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public interface IEntity { long Id { get; } }
            [GenerateDataGridColumns]
            public interface IRow : IEntity { string Name { get; } }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            before,
            after,
            "DirectSchemaGeneration");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(result.Sources, static source =>
            source.Contains("DataGridColumnValueAccessor<global::Demo.IRow, long>", StringComparison.Ordinal));
    }

    [Fact]
    public void Explicit_interface_schema_semantic_build_tracks_contract_changes()
    {
        const string before = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public interface IRowContract { string Value { get; } }
            [GenerateDataGridColumns]
            public sealed class Row : IRowContract { string IRowContract.Value => "Before"; }
            """;
        const string after = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public interface IRowContract { int Value { get; } }
            [GenerateDataGridColumns]
            public sealed class Row : IRowContract { int IRowContract.Value => 42; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            before,
            after,
            "DirectSchemaGeneration");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(result.Sources, static source =>
            source.Contains("DataGridColumnValueAccessor<global::Demo.Row, int>", StringComparison.Ordinal));
    }

    [Fact]
    public void Direct_schema_semantic_build_is_invalidated_when_owner_options_change()
    {
        const string before = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row), Strict = true)]
            public sealed partial class RowsViewModel { }
            """;
        const string after = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row), Strict = false)]
            public sealed partial class RowsViewModel { }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            before,
            after,
            "DirectSchemaGeneration");

        Assert.Contains(result.Sources, static source =>
            source.Contains("UseAccessorsOnly = false", StringComparison.Ordinal));
        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
    }

    [Fact]
    public void Direct_schema_semantic_build_is_invalidated_when_provider_collision_appears()
    {
        const string first = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(ProviderName = "SharedSchema")]
            public sealed class First { public int Id { get; set; } }
            """;
        const string secondBefore = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(ProviderName = "SecondSchema")]
            public sealed class Second { public int Id { get; set; } }
            """;
        const string secondAfter = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(ProviderName = "SharedSchema")]
            public sealed class Second { public int Id { get; set; } }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { first, secondBefore },
            new[] { first, secondAfter },
            "DirectSchemaGeneration");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.DoesNotContain(result.Sources, static source =>
            source.Contains("class SharedSchema :", StringComparison.Ordinal));
        Assert.Equal(2, result.Sources.Count(static source =>
            source.Contains("class SharedSchema_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Property_only_schema_semantic_build_is_invalidated_when_property_changes()
    {
        const string before = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn] public int Id { get; set; }
            }
            """;
        const string after = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn] public long Id { get; set; }
            }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            before,
            after,
            "DirectSchemaGeneration");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
    }

    [Fact]
    public void Unchanged_direct_schema_semantic_candidate_is_reused()
    {
        const string firstBefore = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class First { public int Id { get; set; } }
            """;
        const string firstAfter = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class First { public int Id { get; set; } public decimal Amount { get; set; } }
            """;
        const string unchangedSecond = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Second { public string Name { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { firstBefore, unchangedSecond },
            new[] { firstAfter, unchangedSecond },
            "DirectSchemaCandidates");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(IncrementalStepRunReason.Unchanged, result.Reasons);
    }

    [Fact]
    public void Unrelated_type_edit_does_not_invalidate_property_schema_composition()
    {
        const string propertySchema = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn] public int Id { get; set; }
            }
            """;
        const string unrelatedBefore = """
            namespace Demo;
            public sealed class Unrelated { public int Value { get; set; } }
            """;
        const string unrelatedAfter = """
            namespace Demo;
            public sealed class Unrelated { public string Value { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { propertySchema, unrelatedBefore },
            new[] { propertySchema, unrelatedAfter },
            "DirectSchemaComposition");

        Assert.DoesNotContain(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.DoesNotContain(IncrementalStepRunReason.New, result.Reasons);
        Assert.Contains(result.Reasons, static reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Property_schema_candidate_is_invalidated_when_attributed_row_changes()
    {
        const string before = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn] public int Id { get; set; }
            }
            """;
        const string after = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn] public long Id { get; set; }
            }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            before,
            after,
            "DirectPropertySchemaCandidates");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
    }

    [Fact]
    public void Unchanged_generated_view_output_is_reused_when_another_view_changes()
    {
        const string firstBefore = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class FirstRow { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(FirstRow))]
            [GenerateDataGridView(typeof(FirstRow), Title = "First")]
            public sealed partial class FirstViewModel { public FirstRow[] Items { get; } = []; }
            """;
        const string firstAfter = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class FirstRow { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(FirstRow))]
            [GenerateDataGridView(typeof(FirstRow), Title = "Updated first")]
            public sealed partial class FirstViewModel { public FirstRow[] Items { get; } = []; }
            """;
        const string unchangedSecond = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class SecondRow { public string Name { get; set; } = ""; }
            [GenerateDataGridViewModel(typeof(SecondRow))]
            [GenerateDataGridView(typeof(SecondRow), Title = "Second")]
            public sealed partial class SecondViewModel { public SecondRow[] Items { get; } = []; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { firstBefore, unchangedSecond },
            new[] { firstAfter, unchangedSecond },
            "DirectViewSources");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(IncrementalStepRunReason.Unchanged, result.Reasons);
        Assert.Equal(7, result.Sources.Count); // injected attributes, two schemas, two view-models, and two views.
    }

    [Fact]
    public void Unchanged_direct_view_semantic_candidate_is_reused()
    {
        const string firstBefore = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class FirstRow { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(FirstRow))]
            [GenerateDataGridView(typeof(FirstRow), Title = "First")]
            public sealed partial class FirstViewModel { public FirstRow[] Items { get; } = []; }
            """;
        const string firstAfter = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class FirstRow { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(FirstRow))]
            [GenerateDataGridView(typeof(FirstRow), Title = "Updated first")]
            public sealed partial class FirstViewModel { public FirstRow[] Items { get; } = []; }
            """;
        const string unchangedSecond = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class SecondRow { public string Name { get; set; } = ""; }
            [GenerateDataGridViewModel(typeof(SecondRow))]
            [GenerateDataGridView(typeof(SecondRow), Title = "Second")]
            public sealed partial class SecondViewModel { public SecondRow[] Items { get; } = []; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { firstBefore, unchangedSecond },
            new[] { firstAfter, unchangedSecond },
            "DirectViewCandidates");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.Contains(IncrementalStepRunReason.Unchanged, result.Reasons);
    }

    [Fact]
    public void Unrelated_type_edit_does_not_invalidate_direct_view_composition()
    {
        const string viewSource = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), Title = "Rows")]
            public sealed partial class RowsViewModel { public Row[] Items { get; } = []; }
            """;
        const string unrelatedBefore = """
            namespace Demo;
            public sealed class Unrelated { public int Value { get; set; } }
            """;
        const string unrelatedAfter = """
            namespace Demo;
            public sealed class Unrelated { public string Value { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { viewSource, unrelatedBefore },
            new[] { viewSource, unrelatedAfter },
            "DirectViewComposition");

        Assert.DoesNotContain(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.DoesNotContain(IncrementalStepRunReason.New, result.Reasons);
        Assert.Contains(result.Reasons, static reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Direct_view_candidate_is_invalidated_when_target_view_type_appears()
    {
        const string viewSource = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), Title = "Rows")]
            public sealed partial class RowsViewModel { public Row[] Items { get; } = []; }
            """;
        const string unrelatedBefore = """
            namespace Demo;
            public sealed class Unrelated { }
            """;
        const string conflictingAfter = """
            namespace Demo;
            public sealed class RowsView : Avalonia.Controls.UserControl { }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { viewSource, unrelatedBefore },
            new[] { viewSource, conflictingAfter },
            "DirectViewCandidates");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.DoesNotContain(result.Sources, static source => source.Contains("partial class RowsView :", StringComparison.Ordinal));
    }

    [Fact]
    public void Unrelated_type_edit_does_not_invalidate_direct_view_model_composition()
    {
        const string viewModelSource = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            public sealed partial class RowsViewModel { }
            """;
        const string unrelatedBefore = """
            namespace Demo;
            public sealed class Unrelated { public int Value { get; set; } }
            """;
        const string unrelatedAfter = """
            namespace Demo;
            public sealed class Unrelated { public string Value { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { viewModelSource, unrelatedBefore },
            new[] { viewModelSource, unrelatedAfter },
            "DirectViewModelComposition");

        Assert.DoesNotContain(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.DoesNotContain(IncrementalStepRunReason.New, result.Reasons);
        Assert.Contains(result.Reasons, static reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Direct_view_model_candidate_tracks_referenced_item_changes()
    {
        const string before = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            public sealed partial class RowsViewModel { }
            """;
        const string after = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public string Id { get; set; } = ""; }
            [GenerateDataGridViewModel(typeof(Row))]
            public sealed partial class RowsViewModel { }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            before,
            after,
            "DirectViewModelCandidates");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
    }

    [Fact]
    public void Direct_view_model_uses_referenced_schema_provider_configuration()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(ProviderName = "ConfiguredRowSchema")]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            public sealed partial class RowsViewModel { }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("global::Demo.ConfiguredRowSchema.Instance.CreateColumnDefinitions()", result.CombinedSource);
        Assert.Contains("global::Demo.ConfiguredRowSchema.Instance.CreateFastPathOptions()", result.CombinedSource);
    }

    [Fact]
    public void Unrelated_type_edit_does_not_invalidate_direct_controller_composition()
    {
        const string controllerSource = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridController(typeof(Row), "Rows")]
            public sealed partial class RowsViewModel { }
            """;
        const string unrelatedBefore = """
            namespace Demo;
            public sealed class Unrelated { public int Value { get; set; } }
            """;
        const string unrelatedAfter = """
            namespace Demo;
            public sealed class Unrelated { public string Value { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { controllerSource, unrelatedBefore },
            new[] { controllerSource, unrelatedAfter },
            "DirectControllerComposition");

        Assert.DoesNotContain(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.DoesNotContain(IncrementalStepRunReason.New, result.Reasons);
        Assert.Contains(result.Reasons, static reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Direct_controller_candidate_tracks_referenced_item_changes()
    {
        const string before = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridController(typeof(Row), "Rows")]
            public sealed partial class RowsViewModel { }
            """;
        const string after = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public string Id { get; set; } = ""; }
            [GenerateDataGridController(typeof(Row), "Rows")]
            public sealed partial class RowsViewModel { }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            before,
            after,
            "DirectControllerCandidates");

        Assert.Contains(IncrementalStepRunReason.Modified, result.Reasons);
    }

    [Fact]
    public void Indexed_column_family_generates_typed_method_backed_factories()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridIndexedColumns(
                Name = "Cells",
                GetterMethod = nameof(GetCell),
                SetterMethod = nameof(SetCell),
                NotificationNameMethod = nameof(GetCellName))]
            public sealed class SheetRow
            {
                public object? GetCell(int index) => null;
                public void SetCell(int index, object? value) { }
                public static string GetCellName(int index) => "Cell" + index;
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("public static class SheetRowCells", result.CombinedSource);
        Assert.Contains("CreateColumn<TValue>", result.CombinedSource);
        Assert.Contains("item => (TValue)item.GetCell(index)!", result.CombinedSource);
        Assert.Contains("SheetRow.GetCellName(index)", result.CombinedSource);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Indexed_column_family_supports_formula_options_through_the_same_typed_factory()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridIndexedColumns(
                Name = "Cells",
                GetterMethod = nameof(GetCell),
                NotificationNameMethod = nameof(GetCellName))]
            public sealed class SheetRow
            {
                public object? GetCell(int index) => null;
                public static string GetCellName(int index) => "Cell" + index;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateColumn<TValue>", result.CombinedSource);
        Assert.Contains("DataGridGeneratedIndexedColumnOptions<TValue>", result.CombinedSource);
        Assert.Contains("DataGridGeneratedIndexedColumnFactory.Create<", result.CombinedSource);
    }

    [Fact]
    public void Canonical_manifest_contains_export_editor_remote_and_accessibility_metadata()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(
                    ColumnKey = "amount",
                    Header = "Amount fallback",
                    Description = "Description fallback",
                    HeaderProviderMethod = nameof(GetAmountHeader),
                    DescriptionProviderMethod = nameof(GetAmountDescription),
                    ExportFormat = "N2",
                    ExportNullText = "-",
                    BackendFieldName = "total_amount",
                    FilterEditor = DataGridGeneratedFilterEditorKind.Range,
                    FilterEditorResourceKey = "AmountEditor",
                    HeaderResourceKey = "AmountHeader",
                    DescriptionResourceKey = "AmountDescription",
                    AutomationId = "amount-cell",
                    AutomationName = "Amount",
                    AutomationHelpText = "Order amount",
                    IsSensitive = true)]
                public decimal Amount { get; set; }

                public static string GetAmountHeader(System.IFormatProvider provider) => "Amount";
                public static string GetAmountDescription() => "Order amount";
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("exportFormat: \"N2\"", result.CombinedSource);
        Assert.Contains("backendFieldName: \"total_amount\"", result.CombinedSource);
        Assert.Contains("filterEditor: (global::Avalonia.Controls.DataGridGeneratedFilterEditorKind)6", result.CombinedSource);
        Assert.Contains("automationId: \"amount-cell\"", result.CombinedSource);
        Assert.Contains("isSensitive: true", result.CombinedSource);
        Assert.Contains("headerProvider: static provider => global::Demo.Row.GetAmountHeader(provider)", result.CombinedSource);
        Assert.Contains("descriptionProvider: static provider => global::Demo.Row.GetAmountDescription()", result.CombinedSource);
        Assert.Contains("global::Demo.Row.GetAmountHeader(global::System.Globalization.CultureInfo.CurrentUICulture)", result.CombinedSource);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Template_factory_methods_generate_typed_recycling_templates()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(
                    DataGridColumnKind.Template,
                    TemplateFactoryMethod = nameof(BuildCell),
                    EditingTemplateFactoryMethod = nameof(BuildEditor),
                    ReuseCellContent = true)]
                public string Name { get; set; } = "";

                public static Control BuildCell(Row item, Control existing) => existing ?? new TextBlock();
                public static Control BuildEditor(Row item, Control existing) => existing ?? new TextBox();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("column.CellTemplate = new global::Avalonia.Controls.DataGridGeneratedFuncDataTemplate", result.CombinedSource);
        Assert.Contains("column.CellEditingTemplate = new global::Avalonia.Controls.DataGridGeneratedFuncDataTemplate", result.CombinedSource);
        Assert.Contains("column.ReuseCellContent = true", result.CombinedSource);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Custom_drawing_factory_type_and_fast_options_are_generated()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            using Avalonia.Rendering.SceneGraph;
            namespace Demo;

            public sealed class DrawFactory : IDataGridCellDrawOperationFactory
            {
                public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context) => null!;
            }

            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(
                    DataGridColumnKind.CustomDrawing,
                    DrawOperationFactoryType = typeof(DrawFactory),
                    DrawingMode = DataGridCustomDrawingMode.DrawOperation,
                    RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
                    TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
                    SharedTextLayoutCacheCapacity = 2048,
                    DrawOperationLayoutFastPath = true)]
                public string Name { get; set; } = "";
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("column.DrawOperationFactory = new global::Demo.DrawFactory();", result.CombinedSource);
        Assert.Contains("column.DrawingMode = (global::Avalonia.Controls.DataGridCustomDrawingMode)1;", result.CombinedSource);
        Assert.Contains("column.RenderBackend = (global::Avalonia.Controls.DataGridCustomDrawingRenderBackend)1;", result.CombinedSource);
        Assert.Contains("column.TextLayoutCacheMode = (global::Avalonia.Controls.DataGridCustomDrawingTextLayoutCacheMode)1;", result.CombinedSource);
        Assert.Contains("column.SharedTextLayoutCacheCapacity = 2048;", result.CombinedSource);
        Assert.Contains("column.DrawOperationLayoutFastPath = true;", result.CombinedSource);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Optimized_column_realization_options_are_generated_by_kind()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            namespace Demo;

            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(DataGridColumnKind.Text,
                    DisplayMode = DataGridColumnDisplayMode.Drawn,
                    UseDirectTextCell = true,
                    UseDirectTextContent = true,
                    TrackDirectTextValueChanges = false)]
                public string Name { get; set; } = "";

                [DataGridColumn(DataGridColumnKind.Hierarchical,
                    DisplayMode = DataGridColumnDisplayMode.Drawn,
                    UseDirectCell = true,
                    UseDirectTextContent = true,
                    UseOptimizedPresenter = true,
                    TrackDirectTextValueChanges = false)]
                public string TreeName { get; set; } = "";

                [DataGridColumn(DataGridColumnKind.CustomDrawing,
                    UseDirectValueAccessor = true,
                    TrackDirectValueChanges = false)]
                public double Activity { get; set; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("column.DisplayMode = (global::Avalonia.Controls.DataGridColumnDisplayMode)1;", result.CombinedSource);
        Assert.Contains("column.UseDirectTextCell = true;", result.CombinedSource);
        Assert.Contains("column.UseDirectCell = true;", result.CombinedSource);
        Assert.Contains("column.UseDirectTextContent = true;", result.CombinedSource);
        Assert.Contains("column.UseOptimizedPresenter = true;", result.CombinedSource);
        Assert.Contains("column.TrackDirectTextValueChanges = false;", result.CombinedSource);
        Assert.Contains("column.UseDirectValueAccessor = true;", result.CombinedSource);
        Assert.Contains("column.TrackDirectValueChanges = false;", result.CombinedSource);
    }

    [Theory]
    [InlineData("UseDirectTextCell = true")]
    [InlineData("UseDirectCell = true")]
    [InlineData("UseDirectTextContent = true")]
    [InlineData("UseOptimizedPresenter = true")]
    [InlineData("TrackDirectTextValueChanges = false")]
    [InlineData("UseDirectValueAccessor = true")]
    [InlineData("TrackDirectValueChanges = false")]
    public void Optimized_options_on_unsupported_column_kinds_report_diagnostic(string option)
    {
        GeneratorTestResult result = GeneratorTestHelper.Run($$"""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(DataGridColumnKind.Numeric, {{option}})]
                public double Value { get; set; }
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG009");
    }

    [Fact]
    public void Custom_drawing_factory_method_is_generated_and_validated()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            using Avalonia.Rendering.SceneGraph;
            namespace Demo;

            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(DataGridColumnKind.CustomDrawing, DrawOperationFactoryMethod = nameof(CreateFactory))]
                public string Name { get; set; } = "";

                public static IDataGridCellDrawOperationFactory CreateFactory() => new DrawFactory();
            }

            public sealed class DrawFactory : IDataGridCellDrawOperationFactory
            {
                public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context) => null!;
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("column.DrawOperationFactory = global::Demo.Row.CreateFactory();", result.CombinedSource);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Conflicting_custom_drawing_factories_report_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            using Avalonia.Rendering.SceneGraph;
            namespace Demo;

            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(
                    DataGridColumnKind.CustomDrawing,
                    DrawOperationFactoryType = typeof(DrawFactory),
                    DrawOperationFactoryMethod = nameof(CreateFactory))]
                public string Name { get; set; } = "";

                public static IDataGridCellDrawOperationFactory CreateFactory() => new DrawFactory();
            }

            public sealed class DrawFactory : IDataGridCellDrawOperationFactory
            {
                public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context) => null!;
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG122");
        Assert.DoesNotContain("column.DrawOperationFactory =", result.CombinedSource);
    }

    [Fact]
    public void Cell_draw_cache_generates_contract_storage_and_stable_slots()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            namespace Demo;

            [GenerateDataGridColumns]
            [GenerateDataGridCellDrawCache(InitialCapacity = 8, MaximumCapacity = 16)]
            public sealed partial class Row
            {
                [DataGridColumn(DataGridColumnKind.CustomDrawing, Order = 20)]
                public string Notes { get; set; } = "";

                [DataGridColumn(DataGridColumnKind.CustomDrawing, Order = 10)]
                public string Title { get; set; } = "";

                [DataGridColumn]
                public int Id { get; set; }

                public static bool VerifyGeneratedContract()
                {
                    var row = new Row();
                    IDataGridCellDrawOperationItemCache cache = row;
                    cache.SetCellDrawCacheEntry(TitleCellDrawCacheSlot, 42, "cached");
                    return cache.TryGetCellDrawCacheEntry(TitleCellDrawCacheSlot, 42, out object value) &&
                        (string)value == "cached";
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("partial class Row : global::Avalonia.Controls.IDataGridCellDrawOperationItemCache", result.CombinedSource);
        Assert.Contains("public const int TitleCellDrawCacheSlot = 0;", result.CombinedSource);
        Assert.Contains("public const int NotesCellDrawCacheSlot = 1;", result.CombinedSource);
        Assert.Contains("Math.Max(cacheSlot + 1, 8)", result.CombinedSource);
        Assert.Contains("if ((uint)cacheSlot >= 16u)", result.CombinedSource);
        Assert.Contains("Math.Min(16, global::System.Math.Max", result.CombinedSource);
        Assert.Contains("public void ClearGeneratedCellDrawCache(int cacheSlot)", result.CombinedSource);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Cell_draw_cache_requires_partial_class()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridCellDrawCache]
            public sealed class Row
            {
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG001");
        Assert.DoesNotContain("IDataGridCellDrawOperationItemCache", result.CombinedSource);
    }

    [Fact]
    public void Cell_draw_cache_rejects_capacity_smaller_than_generated_slots()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            [GenerateDataGridCellDrawCache(MaximumCapacity = 1)]
            public sealed partial class Row
            {
                [DataGridColumn(DataGridColumnKind.CustomDrawing)] public string First { get; set; } = "";
                [DataGridColumn(DataGridColumnKind.CustomDrawing)] public string Second { get; set; } = "";
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG118");
        Assert.DoesNotContain("partial class Row : global::Avalonia.Controls.IDataGridCellDrawOperationItemCache", result.CombinedSource);
    }

    [Fact]
    public void Unrelated_type_edit_does_not_invalidate_cell_draw_cache_generation()
    {
        const string cacheSource = """
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridCellDrawCache]
            public sealed partial class Row
            {
                [DataGridColumn(DataGridColumnKind.CustomDrawing)] public string Value { get; set; } = "";
            }
            """;
        const string unrelatedBefore = """
            namespace Demo;
            public sealed class Unrelated { public int Value { get; set; } }
            """;
        const string unrelatedAfter = """
            namespace Demo;
            public sealed class Unrelated { public string Value { get; set; } = ""; }
            """;

        IncrementalRunResult result = GeneratorTestHelper.RunIncremental(
            new[] { cacheSource, unrelatedBefore },
            new[] { cacheSource, unrelatedAfter },
            "CellDrawCacheGeneration");

        Assert.DoesNotContain(IncrementalStepRunReason.Modified, result.Reasons);
        Assert.DoesNotContain(IncrementalStepRunReason.New, result.Reasons);
        Assert.Contains(result.Reasons, static reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Analytics_attributes_generate_typed_pivot_chart_outline_and_formula_roles()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridPivoting;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridPivotAxis(DataGridGeneratedAnalyticsRole.PivotRow, Order = 0, Name = "Desk")]
                [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartCategory, Order = 0)]
                [DataGridOutlineField(DataGridGeneratedAnalyticsRole.OutlineGroup, Order = 0)]
                public string Desk { get; set; } = "";

                [DataGridPivotValue(PivotAggregateType.Sum, Format = "N2", DisplayMode = PivotValueDisplayMode.PercentOfGrandTotal)]
                [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartValue, Series = "Amount")]
                [DataGridFormulaField("Amount", Dependencies = new[] { "Desk" })]
                public decimal Amount { get; set; }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("IDataGridGeneratedAnalyticsField[] s_analyticsFields", result.CombinedSource);
        Assert.Contains("DataGridGeneratedAnalyticsRole)1", result.CombinedSource);
        Assert.Contains("DataGridGeneratedAnalyticsRole)8", result.CombinedSource);
        Assert.Contains("DataGridGeneratedAnalyticsRole)2048", result.CombinedSource);
        Assert.Contains("CreatePivotAxisFields", result.CombinedSource);
        Assert.Contains("CreatePivotValueFields", result.CombinedSource);
        Assert.Contains("CreatePivotTableModel", result.CombinedSource);
        Assert.Contains("DataGridGeneratedPivotAdapter.CreateModel(items, AnalyticsFields, model =>", result.CombinedSource);
        Assert.Contains("CreateOutlineReportModel", result.CombinedSource);
        Assert.Contains("DataGridGeneratedOutlineAdapter.CreateModel(items, AnalyticsFields, model =>", result.CombinedSource);
        Assert.Contains("item is global::Demo.Row typed ? (double?)typed.Amount : null", result.CombinedSource);
        Assert.Contains("DataGridGeneratedDiagnosticsManifest Diagnostics", result.CombinedSource);
        Assert.Contains("DataGridGeneratedAnalyticsRole)2120", result.CombinedSource);
        Assert.Contains("CreateColumnLayoutController", result.CombinedSource);
        Assert.Contains("CreateHeaderCommandController", result.CombinedSource);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Advanced_analytics_emit_calculated_custom_and_schema_configuration_hooks()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridPivoting;
            using Avalonia.Controls.DataGridReporting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;

            public sealed class Aggregator : IPivotAggregator
            {
                public PivotAggregateType AggregateType => PivotAggregateType.Custom;
                public string Name => "Custom";
                public IPivotAggregationState CreateState() => null!;
            }

            [GenerateDataGridColumns(
                PivotConfigureMethod = nameof(ConfigurePivot),
                OutlineConfigureMethod = nameof(ConfigureOutline))]
            public sealed class Row
            {
                [DataGridPivotAxis(
                    DataGridGeneratedAnalyticsRole.PivotRow,
                    ConfigureMethod = nameof(ConfigureAxis))]
                [DataGridOutlineField(
                    DataGridGeneratedAnalyticsRole.OutlineGroup,
                    ConfigureMethod = nameof(ConfigureOutlineGroup))]
                public string Desk { get; set; } = "";

                [DataGridPivotValue(
                    PivotAggregateType.Custom,
                    CustomAggregatorFactoryMethod = nameof(CreateAggregator),
                    ConfigureMethod = nameof(ConfigureValue))]
                [DataGridOutlineField(
                    DataGridGeneratedAnalyticsRole.OutlineDetail,
                    Aggregate = DataGridAggregateType.Custom,
                    CustomAggregatorFactoryMethod = nameof(CreateAggregator),
                    ConfigureMethod = nameof(ConfigureOutlineValue))]
                public decimal Amount { get; set; }

                [DataGridPivotValue(
                    PivotAggregateType.None,
                    Formula = "[Amount] * 2",
                    Dependencies = new[] { "Amount" })]
                public decimal DoubleAmount { get; set; }

                public static IPivotAggregator CreateAggregator() => new Aggregator();
                public static void ConfigureAxis(PivotAxisField field) => field.ShowItemsWithNoData = true;
                public static void ConfigureValue(PivotValueField field) => field.DisplayMode = PivotValueDisplayMode.Index;
                public static void ConfigureOutlineGroup(OutlineGroupField field) => field.ShowSubtotals = false;
                public static void ConfigureOutlineValue(OutlineValueField field) => field.StringFormat = "N1";
                public static void ConfigurePivot(PivotTableModel model) => model.Layout.ShowRowSubtotals = false;
                public static void ConfigureOutline(OutlineReportModel model) => model.Layout.ShowSubtotals = false;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("global::Demo.Row.ConfigurePivot(model)", result.CombinedSource);
        Assert.Contains("global::Demo.Row.ConfigureOutline(model)", result.CombinedSource);
        Assert.Contains("static () => global::Demo.Row.CreateAggregator()", result.CombinedSource);
        Assert.Contains("static field => global::Demo.Row.ConfigureAxis(field)", result.CombinedSource);
        Assert.Contains("static field => global::Demo.Row.ConfigureValue(field)", result.CombinedSource);
        Assert.Contains("static field => global::Demo.Row.ConfigureOutlineGroup(field)", result.CombinedSource);
        Assert.Contains("static field => global::Demo.Row.ConfigureOutlineValue(field)", result.CombinedSource);
        Assert.Contains("new global::Avalonia.Controls.DataGridGeneratedAdvancedAnalyticsOptions", result.CombinedSource);
        Assert.Contains("\"[Amount] * 2\"", result.CombinedSource);
        Assert.Contains("new string[] { \"Amount\" }", result.CombinedSource);
    }

    [Fact]
    public void Invalid_advanced_analytics_hooks_and_dependencies_report_diagnostics()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridPivoting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(PivotConfigureMethod = nameof(Invalid))]
            public sealed class Row
            {
                [DataGridPivotValue(
                    PivotAggregateType.Custom,
                    CustomAggregatorFactoryMethod = nameof(Invalid),
                    Formula = "[Missing]",
                    Dependencies = new[] { "Missing" },
                    ConfigureMethod = nameof(Invalid))]
                public decimal Amount { get; set; }

                public static int Invalid(int value) => value;
            }
            """);

        Assert.True(result.GeneratorDiagnostics.Count(diagnostic => diagnostic.Id == "PDGSG004") >= 3);
        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG121");
        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG009");
    }

    [Fact]
    public void Namespace_policy_applies_advanced_analytics_model_hooks()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridPivoting;
            using Avalonia.Controls.DataGridReporting;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridColumnsForNamespace(
                "Demo.Models",
                PivotConfigureMethod = "ConfigurePivot",
                OutlineConfigureMethod = "ConfigureOutline")]

            namespace Demo.Models
            {
                public sealed class Row
                {
                    [DataGridPivotAxis(DataGridGeneratedAnalyticsRole.PivotRow)]
                    [DataGridOutlineField(DataGridGeneratedAnalyticsRole.OutlineGroup)]
                    public string Desk { get; set; } = "";

                    [DataGridPivotValue(PivotAggregateType.Sum)]
                    [DataGridOutlineField(
                        DataGridGeneratedAnalyticsRole.OutlineDetail,
                        Aggregate = DataGridAggregateType.Sum)]
                    public decimal Amount { get; set; }

                    public static void ConfigurePivot(PivotTableModel model) => model.Layout.ShowRowSubtotals = false;
                    public static void ConfigureOutline(OutlineReportModel model) => model.Layout.ShowSubtotals = false;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("global::Demo.Models.Row.ConfigurePivot(model)", result.CombinedSource);
        Assert.Contains("global::Demo.Models.Row.ConfigureOutline(model)", result.CombinedSource);
    }

    [Fact]
    public void Schema_hash_includes_group_summary_band_and_analytics_semantics()
    {
        const string source = """
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridPivoting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridGroup(Order = 0)]
                [DataGridBand("Market/Core", Order = 0)]
                public string Desk { get; set; } = "";

                [DataGridSummary(DataGridAggregateType.Sum, Title = "Total")]
                [DataGridPivotValue(PivotAggregateType.Sum, DisplayMode = PivotValueDisplayMode.Value)]
                public decimal Amount { get; set; }
            }
            """;

        string baseline = GetGeneratedSchemaHash(GeneratorTestHelper.Run(source));
        string groupChanged = GetGeneratedSchemaHash(GeneratorTestHelper.Run(source.Replace("[DataGridGroup(Order = 0)]", "[DataGridGroup(Order = 1)]", StringComparison.Ordinal)));
        string summaryChanged = GetGeneratedSchemaHash(GeneratorTestHelper.Run(source.Replace("Title = \"Total\"", "Title = \"Grand total\"", StringComparison.Ordinal)));
        string bandChanged = GetGeneratedSchemaHash(GeneratorTestHelper.Run(source.Replace("Market/Core", "Market/Measures", StringComparison.Ordinal)));
        string analyticsChanged = GetGeneratedSchemaHash(GeneratorTestHelper.Run(source.Replace("PivotValueDisplayMode.Value", "PivotValueDisplayMode.Index", StringComparison.Ordinal)));

        Assert.NotEqual(baseline, groupChanged);
        Assert.NotEqual(baseline, summaryChanged);
        Assert.NotEqual(baseline, bandChanged);
        Assert.NotEqual(baseline, analyticsChanged);
    }

    [Fact]
    public void Invalid_formula_dependency_and_duplicate_name_report_diagnostics()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridFormulaField("Value", Dependencies = new[] { "missing" })]
                public decimal Amount { get; set; }

                [DataGridFormulaField("Value")]
                public decimal Total { get; set; }
            }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(diagnostic => diagnostic.Id == "PDGSG121"));
    }

    [Fact]
    public void Formula_column_uses_shared_compile_time_syntax_validation()
    {
        GeneratorTestResult valid = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class ValidRow
            {
                [DataGridColumn(DataGridColumnKind.Formula, Formula = "=SUM([@Amount], 1)")]
                public decimal Total { get; set; }

                public decimal Amount { get; set; }
            }
            """);
        GeneratorTestResult invalid = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class InvalidRow
            {
                [DataGridColumn(DataGridColumnKind.Formula, Formula = "=SUM([@Amount],")]
                public decimal Total { get; set; }

                public decimal Amount { get; set; }
            }
            """);

        Assert.DoesNotContain(valid.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG138");
        Diagnostic diagnostic = Assert.Single(invalid.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG138");
        Assert.Contains("position", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(valid.Errors);
    }

    [Fact]
    public void Invalid_indexed_column_method_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridIndexedColumns(GetterMethod = "Missing")]
            public sealed class SheetRow { }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG004");
    }

    [Fact]
    public void Named_controller_generates_grouped_lifetime_api_and_schema()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            using Avalonia.Controls;
            namespace Demo;
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                public string Name { get; init; } = "";
            }

            [GenerateDataGridController(
                typeof(Row),
                "Trades",
                Features = DataGridGeneratedFeatures.Columns | DataGridGeneratedFeatures.Sorting | DataGridGeneratedFeatures.Searching,
                OperationExecution = DataGridOperationExecution.ExternalPipeline)]
            public sealed partial class TradingViewModel
            {
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class RowDataGridSchema", result.CombinedSource);
        Assert.Contains("DataGridGeneratedOperationController<global::Demo.Row> Trades", result.CombinedSource);
        Assert.Contains("IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedOperationDescriptor> TradesDescriptors", result.CombinedSource);
        Assert.Contains("DataGridGeneratedOperationCommandSet<global::Demo.Row> TradesCommands", result.CombinedSource);
        Assert.Contains("IReadOnlyList<global::Avalonia.Controls.DataGridGeneratedOperationPreset> TradesPresets", result.CombinedSource);
        Assert.Contains("InitializeTrades", result.CombinedSource);
        Assert.Contains("CreateTradesController", result.CombinedSource);
        Assert.Contains("DisposeTrades", result.CombinedSource);
        Assert.Contains("(global::Avalonia.Controls.DataGridOperationExecution)1", result.CombinedSource);
        Assert.Contains("(global::Avalonia.Controls.DataGridGeneratedFeatures)11", result.CombinedSource);
    }

    [Fact]
    public void Multiple_named_controllers_are_supported_but_duplicate_names_report_diagnostic()
    {
        GeneratorTestResult valid = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridController(typeof(Row), "Primary")]
            [GenerateDataGridController(typeof(Row), "Secondary")]
            public sealed partial class DashboardViewModel { }
            """);
        AssertNoErrors(valid);
        Assert.Contains("InitializePrimary", valid.CombinedSource);
        Assert.Contains("InitializeSecondary", valid.CombinedSource);

        GeneratorTestResult duplicate = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridController(typeof(Row), "Grid")]
            [GenerateDataGridController(typeof(Row), "Grid")]
            public sealed partial class DashboardViewModel { }
            """);
        Assert.Contains(duplicate.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG117");
    }

    [Fact]
    public void Missing_controller_source_member_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridController(typeof(Row), "Rows", SourceMember = "Missing")]
            public sealed partial class RowsViewModel { }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG103");
    }

    [Fact]
    public void Incompatible_controller_source_type_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridController(typeof(Row), "Rows", SourceMember = nameof(Source))]
            public sealed partial class RowsViewModel
            {
                public int Source { get; } = 42;
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG103");
    }

    [Fact]
    public void Stream_source_requires_external_operation_execution()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridController(
                typeof(Row),
                "Rows",
                SourceMember = nameof(Source),
                SourceKind = DataGridGeneratedSourceKind.AsyncEnumerable,
                OperationExecution = DataGridOperationExecution.View)]
            public sealed partial class RowsViewModel
            {
                public IAsyncEnumerable<Row> Source { get; } = null!;
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG104");
    }

    [Fact]
    public void Hierarchy_attributes_generate_typed_options_and_parent_key_accessor()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Node
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridParentKey] public int? ParentId { get; init; }
                [DataGridChildren] public List<Node> Children { get; } = new();
                [DataGridExpanded] public bool IsExpanded { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("HierarchicalOptions<global::Demo.Node> CreateHierarchicalOptions", result.CombinedSource);
        Assert.Contains("ChildrenSelector = static item => item.Children", result.CombinedSource);
        Assert.Contains("IsExpandedSetter = static (item, value) => item.IsExpanded = value", result.CombinedSource);
        Assert.Contains("ExpandedStateKeySelector = static item => item.Id", result.CombinedSource);
        Assert.Contains("CreateHierarchyController()", result.CombinedSource);
        Assert.Contains("CreateHierarchicalModel()", result.CombinedSource);
        Assert.Contains("CreateHierarchicalAdapter(", result.CombinedSource);
        Assert.Contains("CreateHierarchicalFilteringAdapter(", result.CombinedSource);
        Assert.Contains("CreateHierarchicalFilteringAdapterFactory(", result.CombinedSource);
        Assert.Contains("CreateSelectionController(", result.CombinedSource);
        Assert.Contains("int? GetParentKey", result.CombinedSource);
    }

    [Fact]
    public void Hierarchy_child_loader_is_validated_and_emitted_for_options_and_controller()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Node
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridChildren(LoaderMethod = nameof(LoadChildrenAsync))]
                public List<Node> Children { get; } = new();
                public ValueTask<IReadOnlyList<Node>> LoadChildrenAsync(CancellationToken cancellationToken) =>
                    ValueTask.FromResult<IReadOnlyList<Node>>(Children);
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("ChildrenSelectorAsync = static async (item, cancellationToken) => await item.LoadChildrenAsync", result.CombinedSource);
        Assert.Contains("static (item, cancellationToken) => item.LoadChildrenAsync(cancellationToken)", result.CombinedSource);
    }

    [Fact]
    public void Invalid_hierarchy_child_loader_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Node
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridChildren(LoaderMethod = nameof(LoadChildren))]
                public List<Node> Children { get; } = new();
                public List<Node> LoadChildren() => Children;
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG109");
    }

    [Fact]
    public void Invalid_hierarchy_member_reports_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Node
            {
                [DataGridChildren] public string Children { get; set; } = "";
                [DataGridExpanded] public bool IsExpanded { get; init; }
                public string Name { get; set; } = "";
            }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(diagnostic => diagnostic.Id == "PDGSG109"));
    }

    [Fact]
    public void State_version_and_column_aliases_emit_versioned_state_controller()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(SchemaId = "trades", StateVersion = 3)]
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridColumn(ColumnKey = "amount", PreviousColumnKeys = new[] { "price", "value" })]
                public decimal Amount { get; init; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("public const int StateVersion = 3", result.CombinedSource);
        Assert.Contains("[\"price\"] = \"amount\"", result.CombinedSource);
        Assert.Contains("[\"value\"] = \"amount\"", result.CombinedSource);
        Assert.Contains("CreateStateController(", result.CombinedSource);
    }

    [Fact]
    public void Duplicate_column_alias_reports_state_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridColumn(PreviousColumnKeys = new[] { "legacy" })] public string First { get; init; } = "";
                [DataGridColumn(PreviousColumnKeys = new[] { "legacy" })] public string Second { get; init; } = "";
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG118");
    }

    [Fact]
    public void Controller_key_member_generates_typed_identity_without_item_attribute()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                public int Id { get; init; }
                public string Name { get; init; } = "";
            }

            [GenerateDataGridController(typeof(Row), "Rows", KeyMember = nameof(Row.Id))]
            public sealed partial class RowsViewModel { }
            """);

        AssertNoErrors(result);
        Assert.Contains("IDataGridItemKey<global::Demo.Row, int>", result.CombinedSource);
        Assert.Contains("CreateItemIndex", result.CombinedSource);
    }

    [Fact]
    public void Direct_schema_controller_key_member_is_composed_into_canonical_schema()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; init; } }
            [GenerateDataGridController(typeof(Row), "Rows", KeyMember = nameof(Row.Id))]
            public sealed partial class RowsViewModel { }
            """);

        AssertNoErrors(result);
        Assert.Contains("IDataGridItemKey<global::Demo.Row, int>", result.CombinedSource);
        Assert.Contains("public int GetKey(global::Demo.Row item)", result.CombinedSource);
    }

    [Fact]
    public void Controller_supports_validated_factory_and_configure_hook()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }

            public sealed class Factory : IDataGridGeneratedControllerFactory<Row>
            {
                public DataGridGeneratedOperationController<Row> Create(
                    in DataGridGeneratedControllerContext<Row> context) =>
                    new(context.Schema, context.Options.Execution, context.Options.Features);
            }

            [GenerateDataGridController(
                typeof(Row),
                "Rows",
                ImplementationType = typeof(Factory),
                ConfigureMethod = nameof(ConfigureRows))]
            public sealed partial class RowsViewModel
            {
                private static void ConfigureRows(ref DataGridGeneratedControllerOptions<Row> options)
                {
                    options.Features = DataGridGeneratedFeatures.Columns;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("ConfigureRows(ref options)", result.CombinedSource);
        Assert.Contains("return new global::Demo.Factory().Create(in context)", result.CombinedSource);
    }

    [Fact]
    public void Source_cache_controller_emits_owned_dynamic_data_pipeline()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo
            {
                public sealed class Row
                {
                    [DataGridKey] public int Id { get; init; }
                    public string Name { get; init; } = "";
                }

                [GenerateDataGridController(
                    typeof(Row),
                    "Rows",
                    SourceMember = nameof(Source),
                    PipelineTransformMethod = nameof(TransformRows),
                    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceCache,
                    OperationExecution = DataGridOperationExecution.ExternalPipeline)]
                public sealed partial class RowsViewModel
                {
                    private readonly global::DynamicData.SourceCache<Row, int> Source = new(static row => row.Id);
                    private global::System.IObservable<global::DynamicData.IChangeSet<Row, int>> TransformRows(
                        global::System.IObservable<global::DynamicData.IChangeSet<Row, int>> changes) => changes;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("ConnectRowsPipeline", result.CombinedSource);
        Assert.Contains("SortAndBind", result.CombinedSource);
        Assert.Contains("UseReplaceForUpdates = true", result.CombinedSource);
        Assert.Contains("changes = TransformRows(changes)", result.CombinedSource);
        Assert.Contains("RowsErrors", result.CombinedSource);
        Assert.Contains("DisconnectRowsPipeline", result.CombinedSource);
    }

    [Fact]
    public void Source_cache_controller_requires_matching_stable_key()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo
            {
                public sealed class Row { public int Id { get; init; } }
                [GenerateDataGridController(
                    typeof(Row),
                    "Rows",
                    SourceMember = nameof(Source),
                    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceCache,
                    OperationExecution = DataGridOperationExecution.ExternalPipeline)]
                public sealed partial class RowsViewModel
                {
                    private readonly global::DynamicData.SourceCache<Row, int> Source = new(static row => row.Id);
                }
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG101");
    }

    [Fact]
    public void Source_list_controller_emits_compilable_owned_pipeline()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo
            {
                public sealed class Row { public int Id { get; init; } }
                [GenerateDataGridController(
                    typeof(Row),
                    "Rows",
                    SourceMember = nameof(Source),
                    PipelineTransformMethod = nameof(TransformRows),
                    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceList,
                    OperationExecution = DataGridOperationExecution.ExternalPipeline)]
                public sealed partial class RowsViewModel
                {
                    private readonly global::DynamicData.SourceList<Row> Source = new();
                    private static global::System.IObservable<global::DynamicData.IChangeSet<Row>> TransformRows(
                        global::System.IObservable<global::DynamicData.IChangeSet<Row>> changes) => changes;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("changes = TransformRows(changes)", result.CombinedSource);
        Assert.Contains("filteredChanges.ObserveOn(scheduler)", result.CombinedSource);
        Assert.Contains("ReadOnlyObservableCollection<global::Demo.Row> items", result.CombinedSource);
    }

    [Fact]
    public void Dynamic_data_pipeline_transform_requires_exact_change_set_shape()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; init; } }
            [GenerateDataGridController(
                typeof(Row),
                "Rows",
                SourceMember = nameof(Source),
                PipelineTransformMethod = nameof(TransformRows),
                SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceList,
                OperationExecution = DataGridOperationExecution.ExternalPipeline)]
            public sealed partial class RowsViewModel
            {
                private readonly global::DynamicData.SourceList<Row> Source = new();
                private static int TransformRows(int value) => value;
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG004");
    }

    [Fact]
    public void Async_enumerable_controller_emits_bounded_stream_lifecycle()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                public string Name { get; init; } = "";
            }

            [GenerateDataGridController(
                typeof(Row),
                "Rows",
                SourceMember = nameof(Source),
                SourceKind = DataGridGeneratedSourceKind.AsyncEnumerable,
                OperationExecution = DataGridOperationExecution.ExternalPipeline)]
            public sealed partial class RowsViewModel
            {
                private readonly IAsyncEnumerable<Row> Source = null!;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("RunRowsStreamAsync", result.CombinedSource);
        Assert.Contains("CreateAsyncStreamPump", result.CombinedSource);
        Assert.Contains("RowsStreamMetrics", result.CombinedSource);
        Assert.Contains("StopRowsStream", result.CombinedSource);
    }

    [Fact]
    public void Remote_controller_emits_query_lifecycle_and_validates_provider_key()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                public string Name { get; init; } = "";
            }

            public sealed class Provider : IDataGridQueryProvider<Row, int>
            {
                public ValueTask<DataGridQueryPage<Row, int>> ExecuteAsync(
                    DataGridRemoteQuery<Row> query,
                    CancellationToken cancellationToken) =>
                    ValueTask.FromResult(new DataGridQueryPage<Row, int>(query.Revision, new Row[0]));
            }

            [GenerateDataGridController(
                typeof(Row),
                "Rows",
                SourceMember = nameof(Source),
                SourceKind = DataGridGeneratedSourceKind.Remote,
                OperationExecution = DataGridOperationExecution.Remote)]
            public sealed partial class RowsViewModel
            {
                private readonly Provider Source = new();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateRowsRemoteQueryController", result.CombinedSource);
        Assert.Contains("QueryRowsAsync", result.CombinedSource);
        Assert.Contains("PrefetchRowsAsync", result.CombinedSource);
        Assert.Contains("DataGridRemoteQuery<global::Demo.Row>", result.CombinedSource);
        Assert.Contains("DisposeRowsRemoteQuery", result.CombinedSource);
    }

    [Fact]
    public void Editable_columns_generate_typed_fields_hooks_and_keyed_controller()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridKey] public int Id { get; init; }
                [DataGridColumn(
                    FormatString = "0.00",
                    ParserMethod = nameof(ParseAmount),
                    FormatterMethod = nameof(FormatAmount),
                    ValidatorMethod = nameof(ValidateAmount),
                    AsyncValidatorMethod = nameof(ValidateAmountAsync),
                    CoerceMethod = nameof(CoerceAmount),
                    CanEditMethod = nameof(CanEditAmount))]
                public decimal Amount { get; set; }

                public static bool ParseAmount(ReadOnlySpan<char> text, IFormatProvider provider, out decimal value) =>
                    decimal.TryParse(text, provider, out value);
                public static string FormatAmount(decimal value, IFormatProvider provider) => value.ToString("0.00", provider);
                public static string? ValidateAmount(Row item, decimal value) => value < 0 ? "negative" : null;
                public static ValueTask<string?> ValidateAmountAsync(Row item, decimal value, CancellationToken cancellationToken) =>
                    ValueTask.FromResult<string?>(null);
                public static decimal CoerceAmount(Row item, decimal value) => decimal.Round(value, 2);
                public static bool CanEditAmount(Row item) => true;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridGeneratedEditField<global::Demo.Row, decimal>", result.CombinedSource);
        Assert.Contains("global::Demo.Row.ParseAmount", result.CombinedSource);
        Assert.Contains("global::Demo.Row.ValidateAmountAsync", result.CombinedSource);
        Assert.Contains("IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedEditField<global::Demo.Row>> EditFields", result.CombinedSource);
        Assert.Contains("CreateEditController", result.CombinedSource);
        Assert.Contains("CreateValidationProjection", result.CombinedSource);
        Assert.Contains("\"Amount\");", result.CombinedSource);
        Assert.Contains("CreateClipboardController", result.CombinedSource);
        Assert.Contains("CreateFillController", result.CombinedSource);
        Assert.Contains("CreateClipboardImportModel", result.CombinedSource);
        Assert.Contains("CreateFillModel", result.CombinedSource);
        Assert.Contains("CreateDragDropController", result.CombinedSource);
    }

    [Fact]
    public void Writable_read_only_columns_are_excluded_from_generated_edit_fields()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridKey]
                public int Id { get; init; }

                [DataGridColumn(ColumnKey = "editable")]
                public string Editable { get; set; } = "";

                [DataGridColumn(ColumnKey = "read-only", IsReadOnly = true)]
                public string ReadOnly { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("s_EditableEditField", result.CombinedSource);
        Assert.DoesNotContain("s_ReadOnlyEditField", result.CombinedSource);
        Assert.Contains("new global::Avalonia.Controls.DataGridGeneratedDiagnosticField(\"editable\", typeof(string), true", result.CombinedSource);
        Assert.Contains("new global::Avalonia.Controls.DataGridGeneratedDiagnosticField(\"read-only\", typeof(string), false", result.CombinedSource);
    }

    [Fact]
    public void Incompatible_edit_hook_reports_customization_diagnostic()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row
            {
                [DataGridColumn(ParserMethod = nameof(ParseAmount))]
                public decimal Amount { get; set; }
                public static decimal ParseAmount(string text) => 0m;
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG004");
    }

    [Fact]
    public void Common_data_annotations_compile_into_direct_edit_validation()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.ComponentModel.DataAnnotations;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [Required, StringLength(12, MinimumLength = 3)]
                public string Name { get; set; } = "";

                [Range(1, 500)]
                public int Quantity { get; set; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("String.IsNullOrWhiteSpace(value)", result.CombinedSource);
        Assert.Contains("value.Length > 12", result.CombinedSource);
        Assert.Contains("value.Length < 3", result.CombinedSource);
        Assert.Contains("value < (int)1", result.CombinedSource);
        Assert.Contains("value > (int)500", result.CombinedSource);
    }

    [Fact]
    public void Group_summary_conditional_format_and_band_metadata_share_typed_accessors()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridConditionalFormatting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridGroup(Order = 1)]
                [DataGridSummary(DataGridAggregateType.Sum, Scope = DataGridSummaryScope.Both, Format = "N2", Title = "Total: ")]
                [DataGridConditionalFormat(
                    DataGridCondition.GreaterThan,
                    Operand = "100",
                    CellThemeKey = "LargeValue",
                    Target = ConditionalFormattingTarget.Row)]
                [DataGridBand("Trading/Risk", Order = 2)]
                public decimal Value { get; set; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridGeneratedGroupField<global::Demo.Row, decimal>", result.CombinedSource);
        Assert.Contains("DataGridSortDescription.FromComparer(field.CreateSortComparer(), field.Direction, field.ColumnKey)", result.CombinedSource);
        Assert.Contains("DataGridGeneratedSummary<global::Demo.Row, decimal>", result.CombinedSource);
        Assert.Contains("column.SummaryDefinitions = new global::Avalonia.Controls.DataGridSummaryDefinition[]", result.CombinedSource);
        Assert.Contains("DataGridSummaryDefinition((global::Avalonia.Controls.DataGridAggregateType)1, (global::Avalonia.Controls.DataGridSummaryScope)2, \"N2\", \"Total: \")", result.CombinedSource);
        Assert.Contains("Comparer<decimal>.Default.Compare(value, (decimal)100m) > 0", result.CombinedSource);
        Assert.Contains("IReadOnlyList<global::Avalonia.Controls.IDataGridGeneratedConditionalRule> ConditionalRules", result.CombinedSource);
        Assert.Contains("CreateConditionalFormattingModel()", result.CombinedSource);
        Assert.Contains("(global::Avalonia.Controls.DataGridConditionalFormatting.ConditionalFormattingTarget)1", result.CombinedSource);
        Assert.Contains("DataGridGeneratedBandField(\"Value\", new string[] { \"Trading\", \"Risk\" }, 2)", result.CombinedSource);
    }

    [Fact]
    public void Conditional_formatting_emits_typed_range_and_ordinal_text_predicates()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridConditionalFormat(DataGridCondition.Between, Operand = "10", Operand2 = "20")]
                public int Score { get; set; }

                [DataGridConditionalFormat(
                    DataGridCondition.Contains,
                    Operand = "risk",
                    StringComparison = StringComparison.OrdinalIgnoreCase)]
                [DataGridConditionalFormat(DataGridCondition.StartsWith, Operand = "A")]
                [DataGridConditionalFormat(DataGridCondition.EndsWith, Operand = "Z")]
                public string Status { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("Comparer<int>.Default.Compare(value, (int)10m) >= 0", result.CombinedSource);
        Assert.Contains("Comparer<int>.Default.Compare(value, (int)20m) <= 0", result.CombinedSource);
        Assert.Contains("value.Contains(\"risk\", (global::System.StringComparison)5)", result.CombinedSource);
        Assert.Contains("value.StartsWith(\"A\", (global::System.StringComparison)4)", result.CombinedSource);
        Assert.Contains("value.EndsWith(\"Z\", (global::System.StringComparison)4)", result.CombinedSource);
    }

    [Fact]
    public void Conditional_formatting_rejects_invalid_typed_operands_and_text_targets()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridConditionalFormat(DataGridCondition.Between, Operand = "10", Operand2 = "not-an-int")]
                [DataGridConditionalFormat(DataGridCondition.Contains, Operand = "1")]
                public int Score { get; set; }
            }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(diagnostic => diagnostic.Id == "PDGSG009"));
    }

    [Fact]
    public void Column_layout_emits_display_indexes_and_left_right_frozen_defaults()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(DisplayIndex = 1)]
                public string Center { get; set; } = "";

                [DataGridColumn(FrozenPlacement = DataGridFrozenPlacement.Right)]
                public int Right { get; set; }

                [DataGridColumn(FrozenPlacement = DataGridFrozenPlacement.Left)]
                public int Left { get; set; }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("FrozenColumnCount = 1", result.CombinedSource);
        Assert.Contains("FrozenColumnCountRight = 1", result.CombinedSource);
        Assert.Contains("column.DisplayIndex = 1", result.CombinedSource);
        Assert.True(result.CombinedSource.IndexOf("columns.Add(CreateLeftColumn", StringComparison.Ordinal) <
                    result.CombinedSource.IndexOf("columns.Add(CreateCenterColumn", StringComparison.Ordinal));
        Assert.True(result.CombinedSource.IndexOf("columns.Add(CreateCenterColumn", StringComparison.Ordinal) <
                    result.CombinedSource.IndexOf("columns.Add(CreateRightColumn", StringComparison.Ordinal));
    }

    [Fact]
    public void Column_layout_rejects_invalid_display_index_and_frozen_placement()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row
            {
                [DataGridColumn(DisplayIndex = -2, FrozenPlacement = (DataGridFrozenPlacement)42)]
                public int Value { get; set; }
            }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(diagnostic => diagnostic.Id == "PDGSG009"));
    }

    [Fact]
    public void Generated_view_configures_total_and_group_summary_placement()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Value { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                ShowTotalSummary = true,
                ShowGroupSummary = true,
                TotalSummaryPosition = DataGridSummaryRowPosition.Top,
                GroupSummaryPosition = DataGridGroupSummaryPosition.Both)]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("ShowTotalSummary = true", result.CombinedSource);
        Assert.Contains("ShowGroupSummary = true", result.CombinedSource);
        Assert.Contains("TotalSummaryPosition = (global::Avalonia.Controls.DataGridSummaryRowPosition)0", result.CombinedSource);
        Assert.Contains("GroupSummaryPosition = (global::Avalonia.Controls.DataGridGroupSummaryPosition)2", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_binds_reflection_free_clipboard_and_fill_models()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using Avalonia.Controls.DataGridClipboard;
            using Avalonia.Controls.DataGridFilling;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { [DataGridKey] public int Id { get; init; } public int Value { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                ClipboardImportModelPropertyName = nameof(ClipboardImport),
                FillModelPropertyName = nameof(Fill),
                SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
                EditTriggers = DataGridEditTriggers.CellDoubleClick | DataGridEditTriggers.TextInput,
                RestrictTextInputEditToCells = true,
                RequiredPointerEditModifiers = Avalonia.Input.KeyModifiers.Alt,
                ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader)]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
                public IDataGridClipboardImportModel ClipboardImport { get; } = null!;
                public IDataGridFillModel Fill { get; } = null!;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGrid.ClipboardImportModelProperty", result.CombinedSource);
        Assert.Contains("DataGrid.FillModelProperty", result.CombinedSource);
        Assert.Contains("EditTriggers = (global::Avalonia.Controls.DataGridEditTriggers)6", result.CombinedSource);
        Assert.Contains("RestrictTextInputEditToCells = true", result.CombinedSource);
        Assert.Contains("DataGridGeneratedEditingInteractionModelFactory", result.CombinedSource);
        Assert.Contains(
            "(global::Avalonia.Input.KeyModifiers)" + ((int)Avalonia.Input.KeyModifiers.Alt).ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.CombinedSource);
        Assert.Contains("ClipboardCopyMode = (global::Avalonia.Controls.DataGridClipboardCopyMode)2", result.CombinedSource);
        Assert.Contains("dataGrid.SelectionUnit = (global::Avalonia.Controls.DataGridSelectionUnit)2", result.CombinedSource);
        Assert.Contains("CanUserAddRows = false", result.CombinedSource);
        Assert.Contains("CanUserDeleteRows = false", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_rejects_unknown_pointer_edit_modifier_flags()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Input;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Value { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), RequiredPointerEditModifiers = (KeyModifiers)32)]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "PDGSG128");
    }

    [Fact]
    public void Generated_view_binds_a_typed_formula_model()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls.DataGridFormulas;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } public object? GetCell(int index) => null; }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), FormulaModelPropertyName = nameof(Formulas))]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
                public IDataGridFormulaModel Formulas { get; } = new DataGridFormulaModel();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGrid.FormulaModelProperty", result.CombinedSource);
        Assert.Contains("s_formulaModelProperty", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_rejects_an_incompatible_formula_model_member()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public object? GetCell(int index) => null; }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), FormulaModelPropertyName = nameof(Formulas))]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
                public object Formulas { get; } = new();
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG130");
    }

    [Fact]
    public void Generated_view_binds_a_typed_conditional_formatting_model()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls.DataGridConditionalFormatting;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns]
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), ConditionalFormattingModelPropertyName = nameof(Formatting))]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
                public IConditionalFormattingModel Formatting { get; } = new ConditionalFormattingModel();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGrid.ConditionalFormattingModelProperty", result.CombinedSource);
        Assert.Contains("s_conditionalFormattingModelProperty", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_rejects_an_incompatible_conditional_formatting_model_member()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), ConditionalFormattingModelPropertyName = nameof(Formatting))]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
                public object Formatting { get; } = new();
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG131");
    }

    [Fact]
    public void Generated_view_rejects_incompatible_clipboard_and_fill_model_members()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Value { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(
                typeof(Row),
                ClipboardImportModelPropertyName = nameof(ClipboardImport),
                FillModelPropertyName = nameof(Fill))]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
                public object ClipboardImport { get; } = new();
                public object Fill { get; } = new();
            }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == "PDGSG129"));
    }

    [Fact]
    public void Generated_view_preserves_datagrid_selection_defaults_when_not_configured()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Value { get; set; } }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row))]
            public sealed partial class GridViewModel
            {
                public System.Collections.Generic.IReadOnlyList<Row> Items { get; } = System.Array.Empty<Row>();
            }
            """);

        AssertNoErrors(result);
        string viewSource = result.Sources.Single(static source => source.Contains("class GridView :", StringComparison.Ordinal));
        Assert.DoesNotContain("dataGrid.SelectionMode =", viewSource);
        Assert.DoesNotContain("dataGrid.SelectionUnit =", viewSource);
    }

    [Fact]
    public void Performance_profile_emits_explicit_options_and_streaming_search_policy()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming)]
            public sealed class Row { public int Id { get; set; } }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreatePerformanceOptions", result.CombinedSource);
        Assert.Contains("DataGridGeneratedPerformanceProfile)6", result.CombinedSource);
        Assert.Contains("HighPerformanceSearchTrackItemChanges = false", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_emits_typed_performance_input_and_metrics_integration()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Windows.Input;
            using Avalonia.Controls;
            using Avalonia.Input;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            public sealed class InputMap : IDataGridGeneratedInputMap
            {
                public DataGridKeyboardGestures CreateKeyboardGestureOverrides(KeyModifiers commandModifiers) => new();
                public bool TryMatch(Key key, KeyModifiers modifiers, KeyModifiers commandModifiers, out DataGridGeneratedInputAction action)
                {
                    action = DataGridGeneratedInputAction.Search;
                    return true;
                }
            }
            public sealed class MetricsSink : IDataGridGeneratedMetricsSink
            {
                public void Record(in DataGridGeneratedMetricMeasurement measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags) { }
                public void Dispose() { }
            }
            [GenerateDataGridView(
                typeof(Row),
                ViewName = "GeneratedVirtualizationProfilePage",
                PerformanceProfile = DataGridGeneratedPerformanceProfile.Spreadsheet,
                InputMapType = typeof(InputMap),
                InputCommandPropertyName = nameof(InputCommand),
                DiagnosticsSinkType = typeof(MetricsSink))]
            public sealed class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = Array.Empty<Row>();
                public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                public DataGridFastPathOptions FastPathOptions { get; } = new();
                public ICommand InputCommand => null!;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("GeneratedPerformanceProfile = (global::Avalonia.Controls.DataGridGeneratedPerformanceProfile)4", result.CombinedSource);
        Assert.Contains("protected virtual global::Avalonia.Controls.IDataGridGeneratedInputMap CreateGeneratedInputMap()", result.CombinedSource);
        Assert.Contains("=> new global::Demo.InputMap();", result.CombinedSource);
        Assert.Contains("DataGridGeneratedInputEvent<global::Demo.Row>", result.CombinedSource);
        Assert.Contains("ConfigureGeneratedAvaloniaMetricsLifetime", result.CombinedSource);
        Assert.Contains("DataGridGeneratedMetricsBridge.Subscribe", result.CombinedSource);
        Assert.Contains("protected virtual global::Avalonia.Controls.IDataGridGeneratedMetricsSink CreateGeneratedMetricsSink()", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_rejects_invalid_input_map_and_metrics_sink_implementations()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            public sealed class InvalidMap { }
            public abstract class InvalidSink : IDataGridGeneratedMetricsSink
            {
                public void Record(in DataGridGeneratedMetricMeasurement measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags) { }
                public void Dispose() { }
            }
            [GenerateDataGridView(typeof(Row), InputMapType = typeof(InvalidMap))]
            public sealed class InvalidMapViewModel
            {
                public IReadOnlyList<Row> Items { get; } = Array.Empty<Row>();
                public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                public DataGridFastPathOptions FastPathOptions { get; } = new();
            }
            [GenerateDataGridView(typeof(Row), DiagnosticsSinkType = typeof(InvalidSink))]
            public sealed class InvalidSinkViewModel
            {
                public IReadOnlyList<Row> Items { get; } = Array.Empty<Row>();
                public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                public DataGridFastPathOptions FastPathOptions { get; } = new();
            }
            """);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == "PDGSG128"));
    }

    [Fact]
    public void Assembly_and_namespace_view_policies_propagate_performance_input_and_metrics_options()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Windows.Input;
            using Avalonia.Controls;
            using Avalonia.Input;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridView(
                typeof(Demo.AssemblyRowsViewModel),
                typeof(Demo.Row),
                ViewName = "AssemblyPerformanceView",
                PerformanceProfile = DataGridGeneratedPerformanceProfile.Spreadsheet,
                InputMapType = typeof(Demo.InputMap),
                InputCommandPropertyName = "InputCommand",
                DiagnosticsSinkType = typeof(Demo.MetricsSink))]
            [assembly: GenerateDataGridViewsForNamespace(
                "Demo.NamespaceViewModels",
                IncludeNestedNamespaces = false,
                PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming,
                InputMapType = typeof(Demo.InputMap),
                InputCommandPropertyName = "InputCommand",
                DiagnosticsSinkType = typeof(Demo.MetricsSink))]
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                public sealed class InputMap : IDataGridGeneratedInputMap
                {
                    public DataGridKeyboardGestures CreateKeyboardGestureOverrides(KeyModifiers commandModifiers) => new();
                    public bool TryMatch(Key key, KeyModifiers modifiers, KeyModifiers commandModifiers, out DataGridGeneratedInputAction action)
                    {
                        action = DataGridGeneratedInputAction.Search;
                        return true;
                    }
                }
                public sealed class MetricsSink : IDataGridGeneratedMetricsSink
                {
                    public void Record(in DataGridGeneratedMetricMeasurement measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags) { }
                    public void Dispose() { }
                }
                public sealed class AssemblyRowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = Array.Empty<Row>();
                    public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                    public DataGridFastPathOptions FastPathOptions { get; } = new();
                    public ICommand InputCommand => null!;
                }
            }
            namespace Demo.NamespaceViewModels
            {
                public sealed class NamespaceRowsViewModel
                {
                    public IReadOnlyList<Demo.Row> Items { get; } = Array.Empty<Demo.Row>();
                    public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                    public DataGridFastPathOptions FastPathOptions { get; } = new();
                    public ICommand InputCommand => null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class AssemblyPerformanceView", result.CombinedSource);
        Assert.Contains("class NamespaceRowsView", result.CombinedSource);
        Assert.Contains("GeneratedPerformanceProfile = (global::Avalonia.Controls.DataGridGeneratedPerformanceProfile)4", result.CombinedSource);
        Assert.Contains("GeneratedPerformanceProfile = (global::Avalonia.Controls.DataGridGeneratedPerformanceProfile)6", result.CombinedSource);
        Assert.Contains("=> new global::Demo.InputMap();", result.CombinedSource);
        Assert.Contains("=> new global::Demo.MetricsSink();", result.CombinedSource);
        Assert.Contains("DataGridGeneratedInputEvent<global::Demo.Row>", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_recipe_emits_customizable_toolbar_and_stable_metadata()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridView(
                typeof(Row),
                Recipe = DataGridViewRecipe.Explorer,
                ControllerName = "Rows",
                AutomationId = "rows-grid")]
            public sealed class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new Row[0];
                public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                public DataGridFastPathOptions FastPathOptions { get; } = new();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("GeneratedRecipe = 3", result.CombinedSource);
        Assert.Contains("GeneratedAutomationId = \"rows-grid\"", result.CombinedSource);
        Assert.Contains("GeneratedControllerName = \"Rows\"", result.CombinedSource);
        Assert.Contains("CreateGeneratedToolbar", result.CombinedSource);
        Assert.Contains("GeneratedToolbarSlot", result.CombinedSource);
        Assert.Contains("CreateGeneratedRecipeContent", result.CombinedSource);
        Assert.Contains("GeneratedExplorerSlot", result.CombinedSource);
    }

    [Fact]
    public void Generated_view_emits_theme_classes_and_compiled_diagnostics_status()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridView(
                typeof(Row),
                Recipe = DataGridViewRecipe.Analytics,
                DiagnosticsStatusPropertyName = nameof(Status),
                ViewThemeKey = "RowsViewTheme",
                DataGridThemeKey = "RowsGridTheme",
                ToolbarThemeKey = "RowsToolbarTheme",
                RecipeContentThemeKey = "RowsAnalyticsTheme",
                ViewClasses = new[] { "generated-view", "dense" },
                DataGridClasses = new[] { "generated-grid", "striped" },
                ToolbarClasses = new[] { "generated-toolbar" },
                RecipeContentClasses = new[] { "generated-analytics" })]
            public sealed class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new Row[0];
                public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                public DataGridFastPathOptions FastPathOptions { get; } = new();
                public string Status { get; } = "Fast path active";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("GeneratedViewThemeKey = \"RowsViewTheme\"", result.CombinedSource);
        Assert.Contains("GeneratedDataGridThemeKey = \"RowsGridTheme\"", result.CombinedSource);
        Assert.Contains("GeneratedToolbarThemeKey = \"RowsToolbarTheme\"", result.CombinedSource);
        Assert.Contains("GeneratedRecipeContentThemeKey = \"RowsAnalyticsTheme\"", result.CombinedSource);
        Assert.Contains("this.Classes.Add(\"generated-view\")", result.CombinedSource);
        Assert.Contains("dataGrid.Classes.Add(\"striped\")", result.CombinedSource);
        Assert.Contains("toolbar.Classes.Add(\"generated-toolbar\")", result.CombinedSource);
        Assert.Contains("recipeContent.Classes.Add(\"generated-analytics\")", result.CombinedSource);
        Assert.Contains("DynamicResourceExtension(GeneratedViewThemeKey!)", result.CombinedSource);
        Assert.Contains("DynamicResourceExtension(GeneratedDataGridThemeKey!)", result.CombinedSource);
        Assert.Contains("s_diagnosticsStatusProperty", result.CombinedSource);
        Assert.Contains("GeneratedDiagnosticsStatus", result.CombinedSource);
        Assert.Contains("GeneratedAutomationId + \"-diagnostics-status\"", result.CombinedSource);
    }

    [Fact]
    public void Assembly_and_namespace_view_presentation_options_propagate()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridView(
                typeof(Demo.AssemblyRowsViewModel),
                typeof(Demo.Row),
                ViewName = "AssemblyPresentationView",
                DiagnosticsStatusPropertyName = "Status",
                DataGridThemeKey = "AssemblyGridTheme",
                DataGridClasses = new[] { "assembly-grid" })]
            [assembly: GenerateDataGridViewsForNamespace(
                "Demo.Policy",
                DiagnosticsStatusPropertyName = "Status",
                ViewThemeKey = "PolicyViewTheme",
                ViewClasses = new[] { "policy-view" })]
            namespace Demo
            {
                public sealed class Row { public int Id { get; set; } }
                public sealed class AssemblyRowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = Array.Empty<Row>();
                    public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                    public DataGridFastPathOptions FastPathOptions { get; } = new();
                    public string Status { get; } = "Assembly";
                }
            }
            namespace Demo.Policy
            {
                public sealed class PolicyRowsViewModel
                {
                    public IReadOnlyList<global::Demo.Row> Items { get; } = Array.Empty<global::Demo.Row>();
                    public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                    public DataGridFastPathOptions FastPathOptions { get; } = new();
                    public string Status { get; } = "Policy";
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class AssemblyPresentationView", result.CombinedSource);
        Assert.Contains("GeneratedDataGridThemeKey = \"AssemblyGridTheme\"", result.CombinedSource);
        Assert.Contains("dataGrid.Classes.Add(\"assembly-grid\")", result.CombinedSource);
        Assert.Contains("class PolicyRowsView", result.CombinedSource);
        Assert.Contains("GeneratedViewThemeKey = \"PolicyViewTheme\"", result.CombinedSource);
        Assert.Contains("this.Classes.Add(\"policy-view\")", result.CombinedSource);
        Assert.Equal(2, result.CombinedSource.Split("GeneratedDiagnosticsStatus", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Invalid_generated_view_presentation_reports_PDGSG139()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridView(
                typeof(Row),
                ViewName = "InvalidStyleView",
                ViewThemeKey = " ",
                ViewClasses = new[] { "valid", "invalid class" })]
            [GenerateDataGridView(
                typeof(Row),
                ViewName = "InvalidStatusView",
                DiagnosticsStatusPropertyName = nameof(Status))]
            public sealed class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new Row[0];
                public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                public DataGridFastPathOptions FastPathOptions { get; } = new();
                public int Status { get; } = 42;
            }
            """);

        Assert.Equal(3, result.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == "PDGSG139"));
        Assert.DoesNotContain("class InvalidStyleView", result.CombinedSource);
        Assert.DoesNotContain("class InvalidStatusView", result.CombinedSource);
    }

    [Fact]
    public void Multiple_generated_view_recipes_emit_independent_layout_contracts()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public sealed class Row { public int Id { get; set; } }
            [GenerateDataGridView(typeof(Row), ViewName = "GridOnlyView", Recipe = DataGridViewRecipe.GridOnly)]
            [GenerateDataGridView(typeof(Row), ViewName = "ExplorerView", Recipe = DataGridViewRecipe.Explorer, SearchTextPropertyName = nameof(Query))]
            [GenerateDataGridView(typeof(Row), ViewName = "SpreadsheetView", Recipe = DataGridViewRecipe.Spreadsheet)]
            [GenerateDataGridView(typeof(Row), ViewName = "AnalyticsView", Recipe = DataGridViewRecipe.Analytics, SearchTextPropertyName = nameof(Query))]
            public sealed class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = new Row[0];
                public DataGridColumnDefinitionList ColumnDefinitions { get; } = new();
                public DataGridFastPathOptions FastPathOptions { get; } = new();
                public string Query { get; set; } = "";
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("class GridOnlyView", result.CombinedSource);
        Assert.Contains("class ExplorerView", result.CombinedSource);
        Assert.Contains("class SpreadsheetView", result.CombinedSource);
        Assert.Contains("class AnalyticsView", result.CombinedSource);
        Assert.Contains("GeneratedRecipe = 0", result.CombinedSource);
        Assert.Contains("GeneratedRecipe = 3", result.CombinedSource);
        Assert.Contains("GeneratedRecipe = 4", result.CombinedSource);
        Assert.Contains("GeneratedRecipe = 5", result.CombinedSource);
        Assert.Contains("GeneratedExplorerSlot", result.CombinedSource);
        Assert.Contains("GeneratedFormulaBarSlot", result.CombinedSource);
        Assert.Contains("GeneratedAnalyticsSlot", result.CombinedSource);
    }

    [Fact]
    public void Custom_implementation_seams_compose_in_one_generated_schema_and_view()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            public class CustomBase : UserControl { }
            public sealed class Calculator : IDataGridSummaryCalculator
            {
                public string Name => "Direct";
                public bool SupportsIncremental => false;
                public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName) => 42;
                public IDataGridSummaryState? CreateState() => null;
            }
            [GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly, ConfigureMethod = nameof(ConfigureColumns))]
            public sealed class Row
            {
                [DataGridKey]
                [DataGridColumn(DataGridColumnKind.Numeric, IsReadOnly = true)]
                public int Id { get; set; }

                [DataGridColumn(FactoryMethod = nameof(CreateNameColumn))]
                public string Name { get; set; } = "";

                [DataGridColumn(DataGridColumnKind.Numeric, ValidatorMethod = nameof(ValidateValue), ConfigureMethod = nameof(ConfigureValue))]
                [DataGridSummary(DataGridAggregateType.Sum)]
                public int Value { get; set; }

                public static DataGridColumnDefinition CreateNameColumn() => new DataGridTextColumnDefinition();
                public static string? ValidateValue(Row item, int value) => value < 0 ? "negative" : null;
                public static void ConfigureValue(DataGridNumericColumnDefinition column)
                {
                    column.SummaryDefinitions[0].Factory = static () => new DataGridCustomSummaryDescription { Calculator = new Calculator() };
                }
                public static void ConfigureColumns(DataGridColumnDefinitionList columns) { }
            }
            [GenerateDataGridViewModel(typeof(Row))]
            [GenerateDataGridView(typeof(Row), ViewName = "CustomView", BaseType = typeof(CustomBase), Recipe = DataGridViewRecipe.OperationsToolbar)]
            public sealed partial class RowsViewModel
            {
                public IReadOnlyList<Row> Items { get; } = Array.Empty<Row>();
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("global::Demo.Row.CreateNameColumn()", result.CombinedSource);
        Assert.Contains("global::Demo.Row.ConfigureValue(column)", result.CombinedSource);
        Assert.Contains("global::Demo.Row.ConfigureColumns(columns)", result.CombinedSource);
        Assert.Contains("global::Demo.Row.ValidateValue", result.CombinedSource);
        Assert.Contains("class CustomView : global::Demo.CustomBase", result.CombinedSource);
        Assert.Contains("protected virtual void ConfigureGeneratedDataGrid", result.CombinedSource);
        Assert.Contains("GeneratedToolbarSlot", result.CombinedSource);
    }

    [Fact]
    public void Assembly_namespace_policies_apply_defaults_and_explicit_type_overrides_deterministically()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridRegistry(RegistryNamespace = "Demo.Generated", RegistryName = "PolicyRegistry")]
            [assembly: GenerateDataGridColumnsForNamespace(
                "Demo.Models",
                IncludeNestedNamespaces = false,
                Strict = true,
                Streaming = true,
                PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming)]
            [assembly: GenerateDataGridViewModelsForNamespace(
                "Demo.Policy.ViewModels",
                IncludeNestedNamespaces = false,
                Strict = true,
                Streaming = true)]
            [assembly: GenerateDataGridViewsForNamespace(
                "Demo.Policy.ViewModels",
                IncludeNestedNamespaces = false,
                Framework = DataGridViewFramework.ReactiveUI,
                Recipe = DataGridViewRecipe.GridOnly,
                IsReadOnly = true,
                PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming)]
            [assembly: DataGridViewRegistration(typeof(Demo.RootViewModel), typeof(Demo.RootView))]

            namespace ReactiveUI
            {
                public interface IActivatableView { }
            }
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl, global::ReactiveUI.IActivatableView { }
            }
            namespace Demo.Models
            {
                public sealed class NamespaceRow
                {
                    public int Id { get; set; }
                    public string Symbol { get; set; } = "";
                }

                [GenerateDataGridColumns(
                    ProviderName = "ExplicitRowSchema",
                    ProviderNamespace = "Demo.Generated",
                    SchemaId = "demo/explicit/v2",
                    StateVersion = 2,
                    Discovery = DataGridColumnDiscovery.AttributedOnly,
                    Strict = false,
                    Streaming = false,
                    PerformanceProfile = DataGridGeneratedPerformanceProfile.Spreadsheet)]
                public sealed class ExplicitRow
                {
                    [DataGridColumn(ColumnKey = "name")]
                    public string Name { get; set; } = "";
                    public string ExcludedByAttributedOnly { get; set; } = "";
                }
            }
            namespace Demo.Models.Nested
            {
                public sealed class ExcludedRow { public int Id { get; set; } }
            }
            namespace Demo.Policy.ViewModels
            {
                public sealed partial class NamespaceRowsViewModel
                {
                    public IReadOnlyList<global::Demo.Models.NamespaceRow> Items { get; } = Array.Empty<global::Demo.Models.NamespaceRow>();
                }

                [GenerateDataGridViewModel(typeof(global::Demo.Models.ExplicitRow), Strict = false, Streaming = false)]
                [GenerateDataGridView(
                    typeof(global::Demo.Models.ExplicitRow),
                    Framework = DataGridViewFramework.ReactiveUI,
                    Recipe = DataGridViewRecipe.Spreadsheet,
                    IsReadOnly = false,
                    PerformanceProfile = DataGridGeneratedPerformanceProfile.Spreadsheet)]
                public sealed partial class ExplicitRowsViewModel
                {
                    public IReadOnlyList<global::Demo.Models.ExplicitRow> Items { get; } = Array.Empty<global::Demo.Models.ExplicitRow>();
                }
            }
            namespace Demo
            {
                public sealed class RootViewModel { }
                public sealed class RootView : global::Avalonia.Controls.UserControl { }
            }
            """);

        AssertNoErrors(result);
        string namespaceSchema = Assert.Single(
            result.Sources,
            static source => source.Contains("class NamespaceRowDataGridSchema", StringComparison.Ordinal));
        Assert.Contains("UseAccessorsOnly = true", namespaceSchema);
        Assert.Contains("HighPerformanceSearchTrackItemChanges = false", namespaceSchema);

        string explicitSchema = Assert.Single(
            result.Sources,
            static source => source.Contains("class ExplicitRowSchema", StringComparison.Ordinal));
        Assert.Contains("public const string SchemaId = \"demo/explicit/v2\"", explicitSchema);
        Assert.Contains("public const int StateVersion = 2", explicitSchema);
        Assert.Contains("UseAccessorsOnly = false", explicitSchema);
        Assert.Contains("HighPerformanceSearchTrackItemChanges = true", explicitSchema);
        Assert.DoesNotContain("ExcludedByAttributedOnly", explicitSchema);

        Assert.DoesNotContain("class ExcludedRowDataGridSchema", result.CombinedSource);
        Assert.Contains("class NamespaceRowsView :", result.CombinedSource);
        Assert.Contains("class ExplicitRowsView :", result.CombinedSource);
        Assert.Contains("public const int GeneratedRecipe = 0", result.CombinedSource);
        Assert.Contains("public const int GeneratedRecipe = 4", result.CombinedSource);
        Assert.Contains("itemType == typeof(global::Demo.Models.NamespaceRow)", result.CombinedSource);
        Assert.Contains("itemType == typeof(global::Demo.Models.ExplicitRow)", result.CombinedSource);
        Assert.DoesNotContain("itemType == typeof(global::Demo.Models.Nested.ExcludedRow)", result.CombinedSource);
        Assert.Contains("new global::Demo.RootView { DataContext = typedViewModel0 }", result.CombinedSource);
    }

    [Fact]
    public void Header_filter_sample_surface_generates_typed_editors_distinct_values_commands_and_interaction()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace ReactiveUI
            {
                public interface IActivatableView { }
                public interface IInteractionContext<out TInput, in TOutput>
                {
                    TInput Input { get; }
                    void SetOutput(TOutput output);
                }
                public interface IInteraction<TInput, TOutput>
                {
                    IDisposable RegisterHandler(Func<IInteractionContext<TInput, TOutput>, Task> handler);
                }
                public static class ViewForMixins
                {
                    public static void WhenActivated(IActivatableView view, Action<Action<IDisposable>> block) { }
                }
            }
            namespace ReactiveUI.Avalonia
            {
                public class ReactiveUserControl<T> : global::Avalonia.Controls.UserControl, global::ReactiveUI.IActivatableView { }
            }
            namespace Demo
            {
                [GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly, Strict = true)]
                public sealed class Row
                {
                    [DataGridColumn(
                        ColumnKey = "desk",
                        FilterEditor = DataGridGeneratedFilterEditorKind.Distinct,
                        FilterFlyoutKey = "DeskDistinctFlyout")]
                    public string Desk { get; set; } = "";

                    [DataGridColumn(
                        DataGridColumnKind.Numeric,
                        ColumnKey = "price",
                        FilterEditor = DataGridGeneratedFilterEditorKind.Range)]
                    public decimal Price { get; set; }
                }

                public sealed class HeaderHandler :
                    IDataGridGeneratedViewInteractionHandler<DataGridGeneratedHeaderCommandRequest, bool>
                {
                    public ValueTask<bool> HandleAsync(
                        DataGridGeneratedViewInteractionContext<DataGridGeneratedHeaderCommandRequest> context) =>
                        new(true);
                }

                [GenerateDataGridViewModel(typeof(Row))]
                [GenerateDataGridController(
                    typeof(Row),
                    "Rows",
                    Features = DataGridGeneratedFeatures.Columns |
                               DataGridGeneratedFeatures.Sorting |
                               DataGridGeneratedFeatures.Filtering,
                    OperationExecution = DataGridOperationExecution.View)]
                [GenerateDataGridView(
                    typeof(Row),
                    Framework = DataGridViewFramework.ReactiveUI,
                    ControllerName = "Rows",
                    SortingModelPropertyName = nameof(SortingModel),
                    FilteringModelPropertyName = nameof(FilteringModel),
                    InteractionPropertyNames = new[] { nameof(HeaderInteraction) },
                    InteractionHandlerTypes = new[] { typeof(HeaderHandler) })]
                public sealed partial class RowsViewModel
                {
                    public IReadOnlyList<Row> Items { get; } = Array.Empty<Row>();
                    public global::Avalonia.Controls.DataGridSorting.SortingModel SortingModel => Rows.SortingModel;
                    public global::Avalonia.Controls.DataGridFiltering.FilteringModel FilteringModel => Rows.FilteringModel;
                    public global::ReactiveUI.IInteraction<DataGridGeneratedHeaderCommandRequest, bool> HeaderInteraction { get; } = null!;
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DataGridGeneratedDistinctValueProvider<global::Demo.Row, string> DeskDistinctValues", result.CombinedSource);
        Assert.Contains("CreateDeskRemoteDistinctValues", result.CombinedSource);
        Assert.Contains("filterEditor: (global::Avalonia.Controls.DataGridGeneratedFilterEditorKind)7", result.CombinedSource);
        Assert.Contains("filterEditor: (global::Avalonia.Controls.DataGridGeneratedFilterEditorKind)6", result.CombinedSource);
        Assert.Contains("FilterFlyoutKey = \"DeskDistinctFlyout\"", result.CombinedSource);
        Assert.Contains("CreateHeaderCommandController", result.CombinedSource);
        Assert.Contains("viewModel.HeaderInteraction.RegisterHandler", result.CombinedSource);
        Assert.Contains("new global::Demo.HeaderHandler()", result.CombinedSource);
    }

    [Fact]
    public void Schema_generates_injected_and_configured_domain_and_formula_services()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Avalonia.Controls;
            using ProDataGrid.FormulaEngine;
            using ProDataGrid.SourceGeneration;
            namespace Demo
            {
                public sealed class RowMutationHandler : IDataGridGeneratedCollectionMutationHandler<Row>
                {
                    public ValueTask AddAsync(int index, ReadOnlyMemory<Row> items, CancellationToken token) => default;
                    public ValueTask RemoveAsync(int index, ReadOnlyMemory<Row> items, CancellationToken token) => default;
                    public ValueTask ReplaceAsync(int index, ReadOnlyMemory<Row> oldItems, ReadOnlyMemory<Row> newItems, CancellationToken token) => default;
                    public ValueTask MoveAsync(int oldIndex, int newIndex, int count, CancellationToken token) => default;
                    public ValueTask ResetAsync(ReadOnlyMemory<Row> items, CancellationToken token) => default;
                }

                public sealed class RowFactory : IDataGridGeneratedNewRowFactory<Row>
                {
                    public ValueTask<Row> CreateAsync(CancellationToken token) => new(new Row());
                }

                public sealed class FormulaTranslator : IFormulaFillTranslator
                {
                    public bool TryTranslate(string formula, int sourceRow, int sourceColumn, int targetRow, int targetColumn, out string translated)
                    {
                        translated = formula;
                        return true;
                    }
                }

                [GenerateDataGridColumns(
                    MutationHandlerType = typeof(RowMutationHandler),
                    NewRowFactoryType = typeof(RowFactory),
                    FormulaFillTranslatorType = typeof(FormulaTranslator))]
                public sealed class Row
                {
                    [DataGridKey]
                    public int Id { get; set; }
                }
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("CreateCollectionMutationService(", result.CombinedSource);
        Assert.Contains("IDataGridGeneratedCollectionMutationHandler<global::Demo.Row> handler", result.CombinedSource);
        Assert.Contains("CreateNewRowService(", result.CombinedSource);
        Assert.Contains("CreateConfiguredCollectionMutationService(", result.CombinedSource);
        Assert.Contains("new global::Demo.RowMutationHandler()", result.CombinedSource);
        Assert.Contains("CreateConfiguredNewRowService()", result.CombinedSource);
        Assert.Contains("new global::Demo.RowFactory()", result.CombinedSource);
        Assert.Contains("IFormulaFillTranslator? formulaTranslator = null", result.CombinedSource);
        Assert.Contains("CreateConfiguredFormulaFillModel(", result.CombinedSource);
        Assert.Contains("new global::Demo.FormulaTranslator()", result.CombinedSource);
    }

    [Fact]
    public void Invalid_domain_mutation_service_types_report_deterministic_diagnostics()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo
            {
                public sealed class InvalidService { }

                [GenerateDataGridColumns(
                    MutationHandlerType = typeof(InvalidService),
                    NewRowFactoryType = typeof(InvalidService),
                    FormulaFillTranslatorType = typeof(InvalidService))]
                public sealed class Row
                {
                    public int Id { get; set; }
                }
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG135");
        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG136");
        Assert.Contains(result.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG137");
        Assert.DoesNotContain("CreateConfiguredCollectionMutationService", result.CombinedSource);
        Assert.DoesNotContain("CreateConfiguredNewRowService", result.CombinedSource);
        Assert.DoesNotContain("CreateConfiguredFormulaFillModel", result.CombinedSource);
    }

    [Fact]
    public void Generated_collection_view_defaults_emit_keyed_lifetime_controller()
    {
        GeneratorTestResult result = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(
                DefaultPageSize = 25,
                InitialPageIndex = 2,
                InitialCurrency = DataGridGeneratedInitialCurrency.First,
                PreserveCurrentItemByKey = true,
                PreserveSelectionByKey = false)]
            public sealed class Row
            {
                [DataGridKey]
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }
            """);

        AssertNoErrors(result);
        Assert.Contains("DefaultPageSize = 25", result.CombinedSource);
        Assert.Contains("InitialPageIndex = 2", result.CombinedSource);
        Assert.Contains("InitialCurrency = (global::Avalonia.Controls.DataGridGeneratedInitialCurrency)2", result.CombinedSource);
        Assert.Contains("PreserveCurrentItemByKey = true", result.CombinedSource);
        Assert.Contains("PreserveSelectionByKey = false", result.CombinedSource);
        Assert.Contains("CreateCollectionViewController(", result.CombinedSource);
        Assert.Contains("DataGridGeneratedCollectionViewController<global::Demo.Row, int>", result.CombinedSource);
        Assert.Contains("PreserveUnloadedKeys = PreserveSelectionByKey", result.CombinedSource);
        Assert.Contains("view.MoveToPage(initialPageIndex)", result.CombinedSource);
        Assert.Contains("view.MoveCurrentToFirst()", result.CombinedSource);
        Assert.Contains("ApplyCollectionViewSorting(", result.CombinedSource);
        Assert.Contains("DataGridGeneratedCollectionViewOperations.ApplySorting(view, Instance, descriptors)", result.CombinedSource);

        GeneratorTestResult changedDefault = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(DefaultPageSize = 26)]
            public sealed class Row
            {
                [DataGridKey]
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }
            """);
        Assert.NotEqual(GetGeneratedSchemaHash(result), GetGeneratedSchemaHash(changedDefault));
    }

    [Fact]
    public void Namespace_collection_view_defaults_propagate_and_invalid_defaults_report_PDGSG140()
    {
        GeneratorTestResult propagated = GeneratorTestHelper.Run("""
            using Avalonia.Controls;
            using ProDataGrid.SourceGeneration;
            [assembly: GenerateDataGridColumnsForNamespace(
                "Demo",
                DefaultPageSize = 10,
                InitialPageIndex = 1,
                InitialCurrency = DataGridGeneratedInitialCurrency.Last)]
            namespace Demo
            {
                public sealed class Row
                {
                    [DataGridKey]
                    public int Id { get; set; }
                }
            }
            """);

        AssertNoErrors(propagated);
        Assert.Contains("DefaultPageSize = 10", propagated.CombinedSource);
        Assert.Contains("InitialPageIndex = 1", propagated.CombinedSource);
        Assert.Contains("InitialCurrency = (global::Avalonia.Controls.DataGridGeneratedInitialCurrency)3", propagated.CombinedSource);

        GeneratorTestResult invalid = GeneratorTestHelper.Run("""
            using ProDataGrid.SourceGeneration;
            namespace Demo;
            [GenerateDataGridColumns(DefaultPageSize = 0, InitialPageIndex = 1)]
            public sealed class Row { public int Id { get; set; } }
            """);

        Assert.Contains(invalid.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "PDGSG140");
    }

    private static void AssertNoErrors(GeneratorTestResult result)
    {
        Assert.True(
            !result.Errors.Any(),
            string.Join(Environment.NewLine, result.Errors.Select(static diagnostic => diagnostic.ToString())) +
            Environment.NewLine + result.CombinedSource);
    }

    private static string GetGeneratedSchemaHash(GeneratorTestResult result)
    {
        AssertNoErrors(result);
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            result.CombinedSource,
            "public const string SchemaHash = \"(?<hash>[0-9a-f]+)\";",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(match.Success, result.CombinedSource);
        return match.Groups["hash"].Value;
    }
}
