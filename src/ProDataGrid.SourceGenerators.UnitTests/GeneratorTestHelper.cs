// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ProDataGrid.SourceGenerators.UnitTests;

internal static class GeneratorTestHelper
{
    public static GeneratorTestResult Run(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        CSharpCompilation compilation = CreateCompilation(syntaxTree);

        ISourceGenerator generator = new ProDataGridGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out ImmutableArray<Diagnostic> driverDiagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();
        ImmutableArray<Diagnostic> generatorDiagnostics = runResult.Results.SelectMany(static result => result.Diagnostics).ToImmutableArray();
        ImmutableArray<Diagnostic> compilationDiagnostics = updatedCompilation.GetDiagnostics();
        string[] generatedSources = runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .Select(static sourceResult => sourceResult.SourceText.ToString())
            .ToArray();

        return new GeneratorTestResult(
            generatedSources,
            generatorDiagnostics,
            driverDiagnostics,
            compilationDiagnostics);
    }

    public static IncrementalRunResult RunIncremental(
        string firstSource,
        string secondSource,
        string trackingName = "GeneratedSources") =>
        RunIncremental(new[] { firstSource }, new[] { secondSource }, trackingName);

    public static IncrementalRunResult RunIncremental(
        IReadOnlyList<string> firstSources,
        IReadOnlyList<string> secondSources,
        string trackingName)
    {
        if (firstSources.Count != secondSources.Count)
        {
            throw new ArgumentException("Incremental source collections must have the same number of entries.");
        }

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        SyntaxTree[] firstTrees = firstSources
            .Select((source, index) => CSharpSyntaxTree.ParseText(source, parseOptions, $"Source{index}.cs"))
            .ToArray();
        CSharpCompilation firstCompilation = CreateCompilation(firstTrees);
        var driverOptions = new GeneratorDriverOptions(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true,
            baseDirectory: AppContext.BaseDirectory);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new ProDataGridGenerator().AsSourceGenerator() },
            parseOptions: parseOptions,
            driverOptions: driverOptions);
        driver = driver.RunGenerators(firstCompilation);

        CSharpCompilation secondCompilation = firstCompilation;
        for (int index = 0; index < firstTrees.Length; index++)
        {
            if (string.Equals(firstSources[index], secondSources[index], StringComparison.Ordinal))
            {
                continue;
            }

            SyntaxTree secondTree = CSharpSyntaxTree.ParseText(secondSources[index], parseOptions, $"Source{index}.cs");
            secondCompilation = secondCompilation.ReplaceSyntaxTree(firstTrees[index], secondTree);
        }

        driver = driver.RunGenerators(secondCompilation);

        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        ImmutableArray<IncrementalGeneratorRunStep> steps = result.TrackedSteps.TryGetValue(
            trackingName,
            out ImmutableArray<IncrementalGeneratorRunStep> trackedSteps)
                ? trackedSteps
                : ImmutableArray<IncrementalGeneratorRunStep>.Empty;
        IncrementalStepRunReason[] reasons = steps
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();
        string[] sources = result.GeneratedSources
            .Select(static generated => generated.SourceText.ToString())
            .ToArray();
        return new IncrementalRunResult(reasons, sources);
    }

    private static CSharpCompilation CreateCompilation(params SyntaxTree[] syntaxTrees) =>
        CSharpCompilation.Create(
            "GeneratorTests",
            syntaxTrees,
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trusted))
        {
            foreach (string path in trusted.Split(Path.PathSeparator))
            {
                paths.Add(path);
            }
        }

        paths.Add(typeof(DataGrid).Assembly.Location);
        paths.Add(typeof(Avalonia.AvaloniaObject).Assembly.Location);
        paths.Add(typeof(Avalonia.Data.Core.ClrPropertyInfo).Assembly.Location);
        paths.Add(typeof(DynamicData.SourceCache<,>).Assembly.Location);
        paths.Add(typeof(System.Reactive.Subjects.BehaviorSubject<>).Assembly.Location);
        return paths.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();
    }
}

internal sealed class IncrementalRunResult
{
    public IncrementalRunResult(
        IReadOnlyList<IncrementalStepRunReason> reasons,
        IReadOnlyList<string> sources)
    {
        Reasons = reasons;
        Sources = sources;
    }

    public IReadOnlyList<IncrementalStepRunReason> Reasons { get; }

    public IReadOnlyList<string> Sources { get; }
}

internal sealed class GeneratorTestResult
{
    public GeneratorTestResult(
        IReadOnlyList<string> sources,
        ImmutableArray<Diagnostic> generatorDiagnostics,
        ImmutableArray<Diagnostic> driverDiagnostics,
        ImmutableArray<Diagnostic> compilationDiagnostics)
    {
        Sources = sources;
        GeneratorDiagnostics = generatorDiagnostics;
        DriverDiagnostics = driverDiagnostics;
        CompilationDiagnostics = compilationDiagnostics;
    }

    public IReadOnlyList<string> Sources { get; }

    public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

    public ImmutableArray<Diagnostic> DriverDiagnostics { get; }

    public ImmutableArray<Diagnostic> CompilationDiagnostics { get; }

    public string CombinedSource => string.Join("\n-----\n", Sources);

    public IEnumerable<Diagnostic> Errors => DriverDiagnostics
        .Concat(CompilationDiagnostics)
        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
