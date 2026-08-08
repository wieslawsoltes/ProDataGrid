// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedSchemaManifestTests
{
    private static readonly DataGridColumnValueAccessor<Row, int> s_idAccessor = new(static row => row.Id);
    private static readonly DataGridColumnValueAccessor<Row, string> s_nameAccessor = new(static row => row.Name);

    [Fact]
    public void Manifest_preserves_identity_key_and_field_order()
    {
        DataGridGeneratedField[] fields =
        {
            new(0, "id", nameof(Row.Id), typeof(int), s_idAccessor, true),
            new(1, "name", nameof(Row.Name), typeof(string), s_nameAccessor, false)
        };

        var manifest = new DataGridGeneratedSchemaManifest(
            1,
            "tests/row/v1",
            "0123456789abcdef",
            typeof(Row),
            fields,
            nameof(Row.Id),
            typeof(int));

        Assert.Equal(1, manifest.FormatVersion);
        Assert.Equal("tests/row/v1", manifest.SchemaId);
        Assert.Equal("0123456789abcdef", manifest.SchemaHash);
        Assert.Equal(typeof(Row), manifest.ItemType);
        Assert.True(manifest.HasKey);
        Assert.Equal(nameof(Row.Id), manifest.KeyMemberName);
        Assert.Equal(typeof(int), manifest.KeyType);
        Assert.Equal(fields, manifest.Fields);
    }

    [Fact]
    public void TryGetField_accepts_stable_key_and_property_alias()
    {
        var field = new DataGridGeneratedField(0, "row-name", nameof(Row.Name), typeof(string), s_nameAccessor, true);
        var manifest = new DataGridGeneratedSchemaManifest(1, "row/v1", "hash", typeof(Row), new[] { field });

        Assert.True(manifest.TryGetField("row-name", out DataGridGeneratedField byKey));
        Assert.Same(field, byKey);
        Assert.True(manifest.TryGetField(nameof(Row.Name), out DataGridGeneratedField byName));
        Assert.Same(field, byName);
        Assert.False(manifest.TryGetField("missing", out _));
    }

    [Fact]
    public void Constructor_rejects_non_contiguous_ordinals()
    {
        var field = new DataGridGeneratedField(1, "id", nameof(Row.Id), typeof(int), s_idAccessor, true);

        Assert.Throws<ArgumentException>(() =>
            new DataGridGeneratedSchemaManifest(1, "row/v1", "hash", typeof(Row), new[] { field }));
    }

    [Fact]
    public void Field_exposes_canonical_cross_feature_metadata()
    {
        var metadata = new DataGridGeneratedFieldMetadata(
            exportFormat: "N2",
            backendFieldName: "row_name",
            filterEditor: DataGridGeneratedFilterEditorKind.Distinct,
            automationId: "row-name",
            isSensitive: true);
        var field = new DataGridGeneratedField(
            0, "name", nameof(Row.Name), typeof(string), s_nameAccessor, true, metadata);

        Assert.Same(metadata, field.Metadata);
        Assert.Equal("N2", field.Metadata.ExportFormat);
        Assert.Equal("row_name", field.Metadata.BackendFieldName);
        Assert.Equal(DataGridGeneratedFilterEditorKind.Distinct, field.Metadata.FilterEditor);
        Assert.True(field.Metadata.IsSensitive);
    }

    [Fact]
    public void Field_metadata_resolves_localized_text_through_direct_providers()
    {
        var metadata = new DataGridGeneratedFieldMetadata(
            header: "Amount",
            description: "Fallback description",
            headerProvider: static provider => provider is CultureInfo culture ? $"Amount ({culture.Name})" : "Amount",
            descriptionProvider: static _ => "Localized description");

        Assert.Equal("Amount (fr-FR)", metadata.ResolveHeader(CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("Localized description", metadata.ResolveDescription(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Typed_fields_create_type_safe_sort_and_filter_descriptors()
    {
        var id = new DataGridGeneratedComparableField<Row, int>(0, "id", nameof(Row.Id), s_idAccessor, true);
        var name = new DataGridGeneratedStringField<Row, string>(1, "name", nameof(Row.Name), s_nameAccessor, true);

        var descending = id.Descending();
        FilteringDescriptor range = id.Between(10, 20);
        FilteringDescriptor contains = name.Contains("desk", StringComparison.OrdinalIgnoreCase);
        SearchDescriptor search = name.Search("alpha", SearchMatchMode.StartsWith);

        Assert.Equal(ListSortDirection.Descending, descending.Direction);
        Assert.Equal("id", descending.ColumnId);
        Assert.Equal(FilteringOperator.Between, range.Operator);
        Assert.Equal(new object[] { 10, 20 }, range.Values);
        Assert.Equal(FilteringOperator.Contains, contains.Operator);
        Assert.Equal(StringComparison.OrdinalIgnoreCase, contains.StringComparisonMode);
        Assert.Equal(SearchScope.ExplicitColumns, search.Scope);
        Assert.Equal(new object[] { "name" }, search.ColumnIds);
    }

    private sealed record Row(int Id, string Name);
}
