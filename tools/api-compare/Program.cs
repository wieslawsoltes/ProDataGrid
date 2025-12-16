using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mono.Cecil;

internal static class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Starting API extraction...");
        var options = CliOptions.Parse(args);

        ApiSurface wpfSurface;
        ApiSurface proSurface;

        try
        {
            Console.WriteLine($"Reading WPF assembly: {options.WpfAssembly}");
            wpfSurface = ApiSurface.Extract(options.WpfAssembly);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read WPF assembly: {ex}");
            return 1;
        }

        try
        {
            Console.WriteLine($"Reading ProDataGrid assembly: {options.ProAssembly}");
            proSurface = ApiSurface.Extract(options.ProAssembly);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read ProDataGrid assembly: {ex}");
            return 1;
        }

        Console.WriteLine("Loaded:");
        Console.WriteLine($"  WPF: {options.WpfAssembly}");
        Console.WriteLine($"  ProDataGrid: {options.ProAssembly}");
        Console.WriteLine();

        var diffs = ApiDiff.Compute(wpfSurface, proSurface);
        Directory.CreateDirectory(options.OutputDirectory);

        File.WriteAllText(Path.Combine(options.OutputDirectory, "wpf-api.json"), JsonSerializer.Serialize(wpfSurface, JsonOptions));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "prodatagrid-api.json"), JsonSerializer.Serialize(proSurface, JsonOptions));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "api-diff.json"), JsonSerializer.Serialize(diffs, JsonOptions));

        Console.WriteLine($"Wrote API data to {options.OutputDirectory}");
        Console.WriteLine();

        foreach (var diff in diffs.OrderBy(d => d.TypeName, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Type: {diff.TypeName} ({diff.Status})");

            if (diff.MissingMembers.Count > 0)
            {
                Console.WriteLine("  Missing in ProDataGrid:");
                foreach (var member in diff.MissingMembers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"    - {member}");
            }

            if (diff.ExtraMembers.Count > 0)
            {
                Console.WriteLine("  Extra in ProDataGrid:");
                foreach (var member in diff.ExtraMembers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"    + {member}");
            }

            Console.WriteLine();
        }

        return 0;
    }

    static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

record CliOptions(string WpfAssembly, string ProAssembly, string OutputDirectory)
{
    public static CliOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 < args.Length && args[i].StartsWith("--"))
                map[args[i]] = args[i + 1];
        }

        var repoRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", ".."));
        var proDefault = Path.Combine(repoRoot, "src", "Avalonia.Controls.DataGrid", "bin", "Release", "net8.0", "Avalonia.Controls.DataGrid.dll");

        var nugetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var wpfDefault = Path.Combine(nugetRoot, "microsoft.windowsdesktop.app.ref",
            "8.0.22", "ref", "net8.0", "PresentationFramework.dll");

        var wpfAssembly = map.GetValueOrDefault("--wpf") ?? wpfDefault;
        var proAssembly = map.GetValueOrDefault("--pro") ?? proDefault;
        var output = map.GetValueOrDefault("--out") ?? Path.Combine(repoRoot, "artifacts", "api-diff");

        return new CliOptions(wpfAssembly, proAssembly, output);
    }
}

record ApiSurface(List<ApiType> Types)
{
    public static ApiSurface Extract(string assemblyPath)
    {
        var readerParams = new ReaderParameters
        {
            ReadSymbols = false,
            ReadingMode = ReadingMode.Deferred
        };

        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParams);
        var types = GetAllTypes(assembly.MainModule.Types)
            .Where(t => (t.IsPublic || t.IsNestedPublic) && t.Name.StartsWith("DataGrid", StringComparison.Ordinal))
            .Select(ApiType.FromDefinition)
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ApiSurface(types);
    }

    private static IEnumerable<TypeDefinition> GetAllTypes(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;

            foreach (var nested in GetAllTypes(type.NestedTypes))
                yield return nested;
        }
    }
}

record ApiType(string Name, string Namespace, string Kind, string? BaseType, List<ApiMember> Members)
{
    public static ApiType FromDefinition(TypeDefinition type)
    {
        var members = new List<ApiMember>();

        foreach (var field in type.Fields)
        {
            if (field.IsPublic)
                members.Add(ApiMember.ForField(field));
        }

        foreach (var prop in type.Properties)
        {
            var getter = prop.GetMethod;
            var setter = prop.SetMethod;
            if ((getter != null && (getter.IsPublic || getter.IsFamily || getter.IsFamilyOrAssembly)) ||
                (setter != null && (setter.IsPublic || setter.IsFamily || setter.IsFamilyOrAssembly)))
            {
                members.Add(ApiMember.ForProperty(prop));
            }
        }

        foreach (var evt in type.Events)
        {
            var add = evt.AddMethod;
            if (add != null && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly))
                members.Add(ApiMember.ForEvent(evt));
        }

        foreach (var method in type.Methods)
        {
            if (method.IsGetter || method.IsSetter || method.IsAddOn || method.IsRemoveOn)
                continue;

            if (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly)
                members.Add(ApiMember.ForMethod(method));
        }

        var kind = type.IsEnum ? "enum" : type.IsValueType ? "struct" : type.IsInterface ? "interface" : "class";

        return new ApiType(
            type.Name,
            type.Namespace ?? string.Empty,
            kind,
            type.BaseType?.FullName,
            members);
    }
}

record ApiMember(string Kind, string Name, string Signature, string MatchKey)
{
    public static ApiMember ForField(FieldDefinition field) =>
        new("field", field.Name, $"public {(field.IsStatic ? "static " : string.Empty)}{FormatType(field.FieldType)} {field.Name}", $"field:{field.Name}");

    public static ApiMember ForProperty(PropertyDefinition prop)
    {
        var accessor = prop.GetMethod ?? prop.SetMethod!;
        var key = $"property:{prop.Name}";
        var signature = $"{Access(accessor)}{Static(accessor)}{FormatType(prop.PropertyType)} {prop.Name}";
        return new("property", prop.Name, signature, key);
    }

    public static ApiMember ForEvent(EventDefinition evt)
    {
        var add = evt.AddMethod!;
        var key = $"event:{evt.Name}";
        var signature = $"{Access(add)}{Static(add)}event {FormatType(evt.EventType)} {evt.Name}";
        return new("event", evt.Name, signature, key);
    }

    public static ApiMember ForMethod(MethodDefinition method)
    {
        var parameters = method.Parameters;
        var paramTypes = string.Join(", ", parameters.Select(p => FormatType(p.ParameterType)));
        var keyParams = string.Join(",", parameters.Select(p => p.ParameterType.Name));
        var signature = $"{Access(method)}{Static(method)}{FormatType(method.ReturnType)} {method.Name}({paramTypes})";
        var key = $"method:{method.Name}({keyParams})";
        return new("method", method.Name, signature, key);
    }

    private static string Access(MethodDefinition method)
    {
        if (method.IsFamilyOrAssembly)
            return "protected internal ";
        if (method.IsFamily)
            return "protected ";
        return "public ";
    }

    private static string Static(MethodDefinition method) => method.IsStatic ? "static " : string.Empty;

    private static string FormatType(TypeReference type)
    {
        if (type is GenericInstanceType git)
        {
            var args = string.Join(", ", git.GenericArguments.Select(FormatType));
            return $"{git.Namespace}.{git.Name.Substring(0, git.Name.IndexOf('`'))}<{args}>";
        }

        return string.IsNullOrWhiteSpace(type.Namespace) ? type.Name : $"{type.Namespace}.{type.Name}";
    }
}

record ApiDiff(string TypeName, string Status, List<string> MissingMembers, List<string> ExtraMembers)
{
    public static List<ApiDiff> Compute(ApiSurface wpf, ApiSurface pro)
    {
        var proMap = new Dictionary<string, ApiType>(StringComparer.Ordinal);
        foreach (var type in pro.Types)
        {
            if (!proMap.ContainsKey(type.Name))
                proMap[type.Name] = type;
        }
        var diffs = new List<ApiDiff>();

        foreach (var wpfType in wpf.Types)
        {
            if (!proMap.TryGetValue(wpfType.Name, out var proType))
            {
                diffs.Add(new ApiDiff(wpfType.Name, "missing type", wpfType.Members.Select(m => m.Signature).ToList(), new List<string>()));
                continue;
            }

            var wpfMembers = wpfType.Members.ToDictionary(m => m.MatchKey, m => m.Signature, StringComparer.Ordinal);
            var proMembers = proType.Members.ToDictionary(m => m.MatchKey, m => m.Signature, StringComparer.Ordinal);

            var missing = wpfMembers.Keys.Where(k => !proMembers.ContainsKey(k)).Select(k => wpfMembers[k]).ToList();
            var extra = proMembers.Keys.Where(k => !wpfMembers.ContainsKey(k)).Select(k => proMembers[k]).ToList();

            diffs.Add(new ApiDiff(wpfType.Name, missing.Count == 0 ? "match" : "partial", missing, extra));
        }

        var wpfNames = new HashSet<string>(wpf.Types.Select(t => t.Name), StringComparer.Ordinal);
        foreach (var extraType in pro.Types.Where(t => !wpfNames.Contains(t.Name)))
        {
            diffs.Add(new ApiDiff(extraType.Name, "extra type", new List<string>(), extraType.Members.Select(m => m.Signature).ToList()));
        }

        return diffs;
    }
}
