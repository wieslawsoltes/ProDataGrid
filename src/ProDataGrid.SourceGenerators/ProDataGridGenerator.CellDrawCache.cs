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

internal static partial class Discovery
{
    public static CellDrawCacheCandidate? CreateCellDrawCacheCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetType || context.Attributes.Length == 0)
        {
            return null;
        }

        AttributeData attribute = context.Attributes[0];
        return new CellDrawCacheCandidate
        {
            TargetType = targetType,
            Attribute = attribute,
            CacheKey = CreateDirectSchemaCacheKey(targetType, ImmutableArray.Create(attribute))
        };
    }

    public static CellDrawCacheGenerationResult BuildCellDrawCache(
        CellDrawCacheCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        INamedTypeSymbol targetType = candidate.TargetType;
        Location location = candidate.Attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ??
            targetType.Locations.FirstOrDefault() ?? Location.None;

        if (targetType.TypeKind != TypeKind.Class || targetType.IsStatic || !AllContainingTypesArePartial(targetType))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidTarget,
                location,
                targetType.ToDisplayString(),
                "generated cell draw caches require a non-static partial class and partial containing types"));
            return new CellDrawCacheGenerationResult(null, diagnostics.ToImmutable());
        }

        if (ImplementsMetadataName(targetType, "Avalonia.Controls.IDataGridCellDrawOperationItemCache"))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidTarget,
                location,
                targetType.ToDisplayString(),
                "the type already implements IDataGridCellDrawOperationItemCache"));
            return new CellDrawCacheGenerationResult(null, diagnostics.ToImmutable());
        }

        string[] generatedMembers =
        {
            "_generatedCellDrawCacheEntries",
            "TryGetCellDrawCacheEntry",
            "SetCellDrawCacheEntry",
            "ClearGeneratedCellDrawCache"
        };
        foreach (string memberName in generatedMembers)
        {
            if (targetType.GetMembers(memberName).Length == 0)
            {
                continue;
            }

            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.MemberCollision,
                location,
                targetType.ToDisplayString(),
                memberName));
            return new CellDrawCacheGenerationResult(null, diagnostics.ToImmutable());
        }

        Dictionary<string, TypedConstant> arguments = GeneratorUtilities.GetNamedArguments(candidate.Attribute);
        int configuredCapacity = GeneratorUtilities.GetInt32(arguments, "InitialCapacity", 0);
        int maximumCapacity = GeneratorUtilities.GetInt32(arguments, "MaximumCapacity", 256);
        if (configuredCapacity < 0 || maximumCapacity <= 0 || configuredCapacity > maximumCapacity)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidStateMetadata,
                location,
                targetType.ToDisplayString(),
                "cell draw cache capacities require 0 <= InitialCapacity <= MaximumCapacity and MaximumCapacity > 0"));
            return new CellDrawCacheGenerationResult(null, diagnostics.ToImmutable());
        }

        ImmutableArray<IPropertySymbol> customDrawingProperties = targetType.GetMembers()
            .OfType<IPropertySymbol>()
            .Select(static property => new
            {
                Property = property,
                Attribute = property.GetAttributes().FirstOrDefault(static attribute =>
                    attribute.AttributeClass != null &&
                    string.Equals(
                        GeneratorUtilities.GetMetadataName(attribute.AttributeClass),
                        ProDataGridGenerator.ColumnAttributeName,
                        StringComparison.Ordinal))
            })
            .Where(static item => item.Attribute != null)
            .Select(item => new
            {
                item.Property,
                Kind = GetColumnKind(item.Property.Type, item.Attribute, GeneratorUtilities.GetNamedArguments(item.Attribute)),
                Arguments = GeneratorUtilities.GetNamedArguments(item.Attribute)
            })
            .Where(static item => string.Equals(item.Kind, "CustomDrawing", StringComparison.Ordinal))
            .OrderBy(static item => GeneratorUtilities.GetInt32(item.Arguments, "Order", 0))
            .ThenBy(static item => item.Property.Locations.FirstOrDefault(static itemLocation => itemLocation.IsInSource)?.SourceSpan.Start ?? int.MaxValue)
            .ThenBy(static item => item.Property.Name, StringComparer.Ordinal)
            .Select(static item => item.Property)
            .ToImmutableArray();

        bool generateSlotConstants = GeneratorUtilities.GetBoolean(arguments, "GenerateSlotConstants", true);
        if (customDrawingProperties.Length > maximumCapacity)
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidStateMetadata,
                location,
                targetType.ToDisplayString(),
                "cell draw cache MaximumCapacity is smaller than the generated custom-drawing slot count"));
            return new CellDrawCacheGenerationResult(null, diagnostics.ToImmutable());
        }
        if (generateSlotConstants)
        {
            foreach (IPropertySymbol property in customDrawingProperties)
            {
                string slotName = GeneratorUtilities.SanitizeIdentifier(property.Name + "CellDrawCacheSlot").TrimStart('@');
                if (targetType.GetMembers(slotName).Length == 0)
                {
                    continue;
                }

                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.MemberCollision,
                    location,
                    targetType.ToDisplayString(),
                    slotName));
                return new CellDrawCacheGenerationResult(null, diagnostics.ToImmutable());
            }
        }
        int initialCapacity = Math.Max(configuredCapacity, customDrawingProperties.Length);
        string source = EmitCellDrawCache(
            targetType,
            customDrawingProperties,
            initialCapacity,
            maximumCapacity,
            generateSlotConstants);
        string metadataName = GeneratorUtilities.GetMetadataName(targetType).Replace('+', '.');
        string hintName = metadataName.Replace('.', '_').Replace('`', '_') + ".CellDrawCache.g.cs";
        return new CellDrawCacheGenerationResult(new GeneratedSource(hintName, source), diagnostics.ToImmutable());
    }

    private static string EmitCellDrawCache(
        INamedTypeSymbol targetType,
        ImmutableArray<IPropertySymbol> customDrawingProperties,
        int initialCapacity,
        int maximumCapacity,
        bool generateSlotConstants)
    {
        string namespaceName = targetType.ContainingNamespace?.IsGlobalNamespace == false
            ? targetType.ContainingNamespace.ToDisplayString()
            : string.Empty;
        INamedTypeSymbol[] chain = GetContainingTypeChainForCache(targetType);
        var builder = new StringBuilder(6144);
        builder.AppendLine("// <auto-generated />")
            .AppendLine("#nullable enable");
        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine()
                .AppendLine("{");
        }

        int indent = string.IsNullOrEmpty(namespaceName) ? 0 : 1;
        foreach (INamedTypeSymbol type in chain)
        {
            builder.Append(' ', indent * 4)
                .Append(GetCacheAccessibility(type)).Append(" partial ").Append(GetCacheTypeKeyword(type)).Append(' ')
                .Append(GeneratorUtilities.EscapeIdentifier(type.Name));
            if (type.TypeParameters.Length > 0)
            {
                builder.Append('<').Append(string.Join(", ", type.TypeParameters.Select(static parameter => parameter.Name))).Append('>');
            }
            if (SymbolEqualityComparer.Default.Equals(type, targetType))
            {
                builder.Append(" : global::Avalonia.Controls.IDataGridCellDrawOperationItemCache");
            }
            builder.AppendLine()
                .Append(' ', indent * 4).AppendLine("{");
            indent++;
        }

        string prefix = new(' ', indent * 4);
        if (generateSlotConstants)
        {
            for (int index = 0; index < customDrawingProperties.Length; index++)
            {
                builder.Append(prefix).Append("public const int ")
                    .Append(GeneratorUtilities.SanitizeIdentifier(customDrawingProperties[index].Name + "CellDrawCacheSlot"))
                    .Append(" = ").Append(index.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
            }
            if (customDrawingProperties.Length > 0)
            {
                builder.AppendLine();
            }
        }

        builder.Append(prefix).AppendLine("private struct GeneratedCellDrawCacheEntry")
            .Append(prefix).AppendLine("{")
            .Append(prefix).AppendLine("    public bool HasValue;")
            .Append(prefix).AppendLine("    public int CacheKey;")
            .Append(prefix).AppendLine("    public object? Value;")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).AppendLine("private GeneratedCellDrawCacheEntry[]? _generatedCellDrawCacheEntries;")
            .AppendLine()
            .Append(prefix).AppendLine("public bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).AppendLine("    GeneratedCellDrawCacheEntry[]? entries = _generatedCellDrawCacheEntries;")
            .Append(prefix).AppendLine("    if (entries is not null && (uint)cacheSlot < (uint)entries.Length)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        GeneratedCellDrawCacheEntry entry = entries[cacheSlot];")
            .Append(prefix).AppendLine("        if (entry.HasValue && entry.CacheKey == cacheKey && entry.Value is not null)")
            .Append(prefix).AppendLine("        {")
            .Append(prefix).AppendLine("            value = entry.Value;")
            .Append(prefix).AppendLine("            return true;")
            .Append(prefix).AppendLine("        }")
            .Append(prefix).AppendLine("    }")
            .AppendLine()
            .Append(prefix).AppendLine("    value = null!;")
            .Append(prefix).AppendLine("    return false;")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).AppendLine("public void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).Append("    if ((uint)cacheSlot >= ").Append(maximumCapacity.ToString(CultureInfo.InvariantCulture)).AppendLine("u)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        return;")
            .Append(prefix).AppendLine("    }")
            .AppendLine()
            .Append(prefix).AppendLine("    GeneratedCellDrawCacheEntry[]? entries = _generatedCellDrawCacheEntries;")
            .Append(prefix).AppendLine("    if (entries is null)")
            .Append(prefix).Append("    {").AppendLine()
            .Append(prefix).Append("        entries = new GeneratedCellDrawCacheEntry[global::System.Math.Max(cacheSlot + 1, ")
            .Append(Math.Max(1, initialCapacity).ToString(CultureInfo.InvariantCulture)).AppendLine(")];")
            .Append(prefix).AppendLine("        _generatedCellDrawCacheEntries = entries;")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("    else if (cacheSlot >= entries.Length)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).Append("        int newLength = global::System.Math.Min(")
            .Append(maximumCapacity.ToString(CultureInfo.InvariantCulture))
            .AppendLine(", global::System.Math.Max(cacheSlot + 1, entries.Length * 2));")
            .Append(prefix).AppendLine("        global::System.Array.Resize(ref entries, newLength);")
            .Append(prefix).AppendLine("        _generatedCellDrawCacheEntries = entries;")
            .Append(prefix).AppendLine("    }")
            .AppendLine()
            .Append(prefix).AppendLine("    entries[cacheSlot] = new GeneratedCellDrawCacheEntry")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        HasValue = true,")
            .Append(prefix).AppendLine("        CacheKey = cacheKey,")
            .Append(prefix).AppendLine("        Value = value")
            .Append(prefix).AppendLine("    };")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).AppendLine("public void ClearGeneratedCellDrawCache()")
            .Append(prefix).AppendLine("{")
            .Append(prefix).AppendLine("    GeneratedCellDrawCacheEntry[]? entries = _generatedCellDrawCacheEntries;")
            .Append(prefix).AppendLine("    if (entries is not null)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        global::System.Array.Clear(entries, 0, entries.Length);")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("}")
            .AppendLine()
            .Append(prefix).AppendLine("public void ClearGeneratedCellDrawCache(int cacheSlot)")
            .Append(prefix).AppendLine("{")
            .Append(prefix).AppendLine("    GeneratedCellDrawCacheEntry[]? entries = _generatedCellDrawCacheEntries;")
            .Append(prefix).AppendLine("    if (entries is not null && (uint)cacheSlot < (uint)entries.Length)")
            .Append(prefix).AppendLine("    {")
            .Append(prefix).AppendLine("        entries[cacheSlot] = default;")
            .Append(prefix).AppendLine("    }")
            .Append(prefix).AppendLine("}");

        for (int index = chain.Length - 1; index >= 0; index--)
        {
            indent--;
            builder.Append(' ', indent * 4).AppendLine("}");
        }
        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    private static INamedTypeSymbol[] GetContainingTypeChainForCache(INamedTypeSymbol type)
    {
        var chain = new Stack<INamedTypeSymbol>();
        INamedTypeSymbol? current = type;
        while (current != null)
        {
            chain.Push(current);
            current = current.ContainingType;
        }
        return chain.ToArray();
    }

    private static string GetCacheAccessibility(INamedTypeSymbol type) => type.DeclaredAccessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "private"
    };

    private static string GetCacheTypeKeyword(INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return type.TypeKind == TypeKind.Struct ? "record struct" : "record class";
        }
        return type.TypeKind == TypeKind.Struct ? "struct" : "class";
    }
}
