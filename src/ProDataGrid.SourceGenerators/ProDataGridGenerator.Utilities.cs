// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ProDataGrid.SourceGenerators;

internal static class GeneratorUtilities
{
    public static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (INamespaceOrTypeSymbol member in namespaceSymbol.GetMembers().OrderBy(static member => member.Name, StringComparer.Ordinal))
        {
            if (member is INamespaceSymbol childNamespace)
            {
                foreach (INamedTypeSymbol type in EnumerateTypes(childNamespace))
                {
                    yield return type;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                foreach (INamedTypeSymbol nested in EnumerateTypeAndNested(type))
                {
                    yield return nested;
                }
            }
        }
    }

    public static string GetMetadataName(INamedTypeSymbol type)
    {
        var parts = new Stack<string>();
        ISymbol? current = type;
        while (current is INamedTypeSymbol named)
        {
            parts.Push(named.MetadataName);
            current = named.ContainingType;
        }

        string typeName = string.Join("+", parts);
        string namespaceName = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName;
    }

    public static string GetDefaultProviderName(INamedTypeSymbol type)
    {
        var builder = new StringBuilder();
        if (type.ContainingType != null)
        {
            builder.Append(SanitizeIdentifier(type.ContainingType.Name));
        }

        builder.Append(SanitizeIdentifier(type.Name));
        builder.Append("DataGridSchema");
        return builder.ToString();
    }

    public static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "Generated";
        }

        var builder = new StringBuilder(value.Length + 1);
        if (!SyntaxFacts.IsIdentifierStartCharacter(value[0]))
        {
            builder.Append('_');
        }

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            builder.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');
        }

        string result = builder.ToString();
        return SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None ? "@" + result : result;
    }

    public static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ? "@" + value : value;
    }

    public static string EscapeString(string? value)
    {
        if (value == null)
        {
            return "null";
        }

        return SymbolDisplay.FormatLiteral(value, quote: true);
    }

    public static string ToHeader(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        var builder = new StringBuilder(propertyName.Length + 4);
        for (int i = 0; i < propertyName.Length; i++)
        {
            char current = propertyName[i];
            if (i > 0 && char.IsUpper(current) &&
                (char.IsLower(propertyName[i - 1]) ||
                 (i + 1 < propertyName.Length && char.IsLower(propertyName[i + 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    public static Location GetLocation(ISymbol symbol)
    {
        for (int i = 0; i < symbol.Locations.Length; i++)
        {
            if (symbol.Locations[i].IsInSource)
            {
                return symbol.Locations[i];
            }
        }

        return Location.None;
    }

    public static AttributeData? FindAttribute(ISymbol symbol, string metadataName)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass != null &&
                string.Equals(GetMetadataName(attribute.AttributeClass), metadataName, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    public static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return FindAttribute(symbol, metadataName) != null;
    }

    public static IEnumerable<AttributeData> FindAttributes(ISymbol symbol, string metadataName)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass != null &&
                string.Equals(GetMetadataName(attribute.AttributeClass), metadataName, StringComparison.Ordinal))
            {
                yield return attribute;
            }
        }
    }

    public static Dictionary<string, TypedConstant> GetNamedArguments(AttributeData? attribute)
    {
        var result = new Dictionary<string, TypedConstant>(StringComparer.Ordinal);
        if (attribute == null)
        {
            return result;
        }

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            result[argument.Key] = argument.Value;
        }

        return result;
    }

    public static bool GetBoolean(Dictionary<string, TypedConstant> arguments, string name, bool fallback)
    {
        return arguments.TryGetValue(name, out TypedConstant value) && value.Value is bool boolean ? boolean : fallback;
    }

    public static string? GetString(Dictionary<string, TypedConstant> arguments, string name)
    {
        return arguments.TryGetValue(name, out TypedConstant value) ? value.Value as string : null;
    }

    public static ImmutableArray<string> GetStringArray(Dictionary<string, TypedConstant> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out TypedConstant value) || value.Kind != TypedConstantKind.Array)
        {
            return ImmutableArray<string>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<string>(value.Values.Length);
        foreach (TypedConstant element in value.Values)
        {
            if (element.Value is string text)
            {
                builder.Add(text);
            }
        }
        return builder.ToImmutable();
    }

    public static INamedTypeSymbol? GetType(Dictionary<string, TypedConstant> arguments, string name)
    {
        return arguments.TryGetValue(name, out TypedConstant value) ? value.Value as INamedTypeSymbol : null;
    }

    public static ImmutableArray<INamedTypeSymbol> GetTypeArray(
        Dictionary<string, TypedConstant> arguments,
        string name)
    {
        if (!arguments.TryGetValue(name, out TypedConstant value) || value.Kind != TypedConstantKind.Array)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(value.Values.Length);
        foreach (TypedConstant element in value.Values)
        {
            if (element.Value is INamedTypeSymbol type)
            {
                builder.Add(type);
            }
        }
        return builder.ToImmutable();
    }

    public static int GetInt32(Dictionary<string, TypedConstant> arguments, string name, int fallback)
    {
        return arguments.TryGetValue(name, out TypedConstant value) && value.Value is int number ? number : fallback;
    }

    public static double GetDouble(Dictionary<string, TypedConstant> arguments, string name, double fallback)
    {
        return arguments.TryGetValue(name, out TypedConstant value) && value.Value is double number ? number : fallback;
    }

    public static bool IsPartial(INamedTypeSymbol type)
    {
        foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax declaration &&
                declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAccessibleFromGeneratedCode(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Public ||
               symbol.DeclaredAccessibility == Accessibility.Internal ||
               symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal;
    }

    public static string FormatDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return "double.NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "double.PositiveInfinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "double.NegativeInfinity";
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypeAndNested(INamedTypeSymbol type)
    {
        yield return type;
        foreach (INamedTypeSymbol nested in type.GetTypeMembers().OrderBy(static nested => nested.Name, StringComparer.Ordinal))
        {
            foreach (INamedTypeSymbol descendant in EnumerateTypeAndNested(nested))
            {
                yield return descendant;
            }
        }
    }
}
