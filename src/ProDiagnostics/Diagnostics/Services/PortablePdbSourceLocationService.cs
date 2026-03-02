using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Services
{
    internal sealed class PortablePdbSourceLocationService : ISourceLocationService
    {
        private readonly ConcurrentDictionary<Assembly, AssemblySymbols> _assemblySymbolsCache = new();
        private readonly ConcurrentDictionary<Type, SourceLocationInfo> _typeCache = new();

        public SourceLocationInfo Resolve(Type? type)
        {
            if (type is null)
            {
                return SourceLocationInfo.Empty;
            }

            var normalizedType = NormalizeType(type);
            return _typeCache.GetOrAdd(normalizedType, ResolveCore);
        }

        public SourceDocumentLocation? ResolveDocument(Assembly? assembly, string? documentHint, string? lineHint = null)
        {
            if (assembly is null || assembly.IsDynamic || string.IsNullOrWhiteSpace(documentHint))
            {
                return null;
            }

            if (!TryGetAssemblySymbolPaths(assembly, out var assemblyPath, out var pdbPath, out _))
            {
                return null;
            }

            try
            {
                var symbols = _assemblySymbolsCache.GetOrAdd(assembly, _ => new AssemblySymbols(assemblyPath, pdbPath));
                var location = symbols.ResolveDocument(documentHint);
                if (location is null || string.IsNullOrWhiteSpace(lineHint))
                {
                    return location;
                }

                return TryRefineDocumentLine(location, lineHint!) ?? location;
            }
            catch
            {
                return null;
            }
        }

        public SourceLocationInfo ResolveObject(object? source, string? documentHint = null, string? lineHint = null)
        {
            if (source is null)
            {
                return SourceLocationInfo.Empty;
            }

            if (source is Type type)
            {
                return Resolve(type);
            }

            var fallback = Resolve(source.GetType());
            var xaml = TryResolveObjectXamlLocation(source, fallback, documentHint, lineHint)
                ?? fallback.XamlLocation;

            var status = BuildObjectStatus(xaml, fallback.CodeLocation, fallback.Status);
            return new SourceLocationInfo(
                fallback.TargetType ?? source.GetType(),
                xaml,
                fallback.CodeLocation,
                status);
        }

        private SourceLocationInfo ResolveCore(Type type)
        {
            if (type.Assembly.IsDynamic)
            {
                return new SourceLocationInfo(type, xamlLocation: null, codeLocation: null, status: "Dynamic assemblies do not expose portable PDB symbols.");
            }

            if (!TryGetAssemblySymbolPaths(type.Assembly, out var assemblyPath, out var pdbPath, out var status))
            {
                return new SourceLocationInfo(type, xamlLocation: null, codeLocation: null, status: status);
            }

            try
            {
                var symbols = _assemblySymbolsCache.GetOrAdd(type.Assembly, _ => new AssemblySymbols(assemblyPath, pdbPath));
                return symbols.Resolve(type);
            }
            catch (Exception e)
            {
                return new SourceLocationInfo(
                    type,
                    xamlLocation: null,
                    codeLocation: null,
                    status: "Unable to read symbols: " + e.Message);
            }
        }

        private static bool TryGetAssemblySymbolPaths(
            Assembly assembly,
            out string assemblyPath,
            out string pdbPath,
            out string status)
        {
            assemblyPath = string.Empty;
            pdbPath = string.Empty;

            assemblyPath = assembly.Location;
            if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            {
                status = "Assembly path is unavailable.";
                return false;
            }

            pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (string.IsNullOrWhiteSpace(pdbPath) || !File.Exists(pdbPath))
            {
                status = "Portable PDB file not found.";
                return false;
            }

            status = string.Empty;
            return true;
        }

        private static Type NormalizeType(Type type)
        {
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                return type.GetGenericTypeDefinition();
            }

            return type;
        }

        private SourceDocumentLocation? TryResolveObjectXamlLocation(
            object source,
            SourceLocationInfo fallback,
            string? documentHint,
            string? lineHint)
        {
            var assembly = source.GetType().Assembly;
            var hints = BuildLineHints(source, lineHint);
            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in EnumerateDocumentCandidates(source, fallback, documentHint))
            {
                if (candidate is null || candidate.FilePath.Length == 0)
                {
                    continue;
                }

                if (!visitedPaths.Add(candidate.FilePath))
                {
                    continue;
                }

                if (TryResolveObjectLine(candidate.FilePath, source, hints, out var line))
                {
                    return new SourceDocumentLocation(candidate.FilePath, line, "XAML object");
                }
            }

            return null;
        }

        private IEnumerable<SourceDocumentLocation?> EnumerateDocumentCandidates(
            object source,
            SourceLocationInfo fallback,
            string? documentHint)
        {
            if (!string.IsNullOrWhiteSpace(documentHint))
            {
                yield return ResolveDocument(source.GetType().Assembly, documentHint);
            }

            var sourceHint = TryGetSourceHint(source);
            if (!string.IsNullOrWhiteSpace(sourceHint))
            {
                yield return ResolveDocument(source.GetType().Assembly, sourceHint);
            }

            if (fallback.XamlLocation is not null)
            {
                yield return fallback.XamlLocation;
            }

            foreach (var ownerType in EnumerateOwnerTypes(source))
            {
                var ownerInfo = Resolve(ownerType);
                if (ownerInfo.XamlLocation is not null)
                {
                    yield return ownerInfo.XamlLocation;
                }
            }
        }

        private static IEnumerable<Type> EnumerateOwnerTypes(object source)
        {
            if (source is not AvaloniaObject avaloniaObject)
            {
                yield break;
            }

            var yielded = new HashSet<Type>();
            var currentType = avaloniaObject.GetType();
            if (IsLikelyUserType(currentType) && yielded.Add(currentType))
            {
                yield return currentType;
            }

            if (source is StyledElement styledElement)
            {
                var currentLogical = (styledElement as ILogical)?.LogicalParent;
                while (currentLogical is AvaloniaObject logicalObject)
                {
                    var candidateType = logicalObject.GetType();
                    if (IsLikelyUserType(candidateType) && yielded.Add(candidateType))
                    {
                        yield return candidateType;
                    }

                    currentLogical = (logicalObject as ILogical)?.LogicalParent;
                }
            }

            if (source is Visual visual)
            {
                var currentVisual = visual.GetVisualParent();
                while (currentVisual is not null)
                {
                    var candidateType = currentVisual.GetType();
                    if (IsLikelyUserType(candidateType) && yielded.Add(candidateType))
                    {
                        yield return candidateType;
                    }

                    currentVisual = currentVisual.GetVisualParent();
                }
            }
        }

        private static bool IsLikelyUserType(Type type)
        {
            var assemblyName = type.Assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return false;
            }

            return !assemblyName.StartsWith("Avalonia", StringComparison.Ordinal)
                   && !assemblyName.StartsWith("System", StringComparison.Ordinal)
                   && !assemblyName.StartsWith("Microsoft", StringComparison.Ordinal)
                   && !assemblyName.StartsWith("mscorlib", StringComparison.Ordinal);
        }

        private static SourceDocumentLocation? TryRefineDocumentLine(SourceDocumentLocation location, string lineHint)
        {
            if (string.IsNullOrWhiteSpace(location.FilePath) || !File.Exists(location.FilePath))
            {
                return null;
            }

            var hints = BuildLineHints(source: null, lineHint);
            if (!TryResolveObjectLine(location.FilePath, source: null, hints, out var line))
            {
                return null;
            }

            return new SourceDocumentLocation(location.FilePath, line, location.MethodName);
        }

        private static bool TryResolveObjectLine(
            string filePath,
            object? source,
            IReadOnlyList<string> hints,
            out int line)
        {
            line = 0;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch
            {
                return false;
            }

            if (lines.Length == 0)
            {
                return false;
            }

            foreach (var hint in hints)
            {
                if (TryFindAttributeLine(lines, "x:Name", hint, out line)
                    || TryFindAttributeLine(lines, "Name", hint, out line)
                    || TryFindAttributeLine(lines, "x:Key", hint, out line)
                    || TryFindContainsLine(lines, hint, out line))
                {
                    return true;
                }
            }

            if (source is not null)
            {
                var typeName = NormalizeTypeName(source.GetType().Name);
                if (TryFindElementLine(lines, typeName, out line))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindElementLine(string[] lines, string typeName, out int line)
        {
            line = 0;
            if (typeName.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var current = lines[i];
                var index = current.IndexOf('<');
                while (index >= 0 && index < current.Length - 1)
                {
                    var start = index + 1;
                    while (start < current.Length && char.IsWhiteSpace(current[start]))
                    {
                        start++;
                    }

                    if (start >= current.Length || current[start] is '/' or '!' or '?')
                    {
                        index = current.IndexOf('<', index + 1);
                        continue;
                    }

                    var cursor = start;
                    while (cursor < current.Length &&
                           !char.IsWhiteSpace(current[cursor]) &&
                           current[cursor] != '>' &&
                           current[cursor] != '/')
                    {
                        cursor++;
                    }

                    if (cursor > start)
                    {
                        var rawTag = current.Substring(start, cursor - start);
                        var localTag = rawTag;
                        var prefixSeparator = rawTag.IndexOf(':');
                        if (prefixSeparator >= 0 && prefixSeparator < rawTag.Length - 1)
                        {
                            localTag = rawTag.Substring(prefixSeparator + 1);
                        }

                        if (string.Equals(localTag, typeName, StringComparison.OrdinalIgnoreCase))
                        {
                            line = i + 1;
                            return true;
                        }
                    }

                    index = current.IndexOf('<', index + 1);
                }
            }

            return false;
        }

        private static bool TryFindAttributeLine(string[] lines, string attributeName, string attributeValue, out int line)
        {
            line = 0;
            if (attributeName.Length == 0 || attributeValue.Length == 0)
            {
                return false;
            }

            var marker = attributeName + "=\"";
            for (var i = 0; i < lines.Length; i++)
            {
                var current = lines[i];
                var markerIndex = current.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                {
                    continue;
                }

                var valueStart = markerIndex + marker.Length;
                var valueEnd = current.IndexOf('"', valueStart);
                if (valueEnd < valueStart)
                {
                    continue;
                }

                var value = current.Substring(valueStart, valueEnd - valueStart);
                if (string.Equals(value, attributeValue, StringComparison.Ordinal))
                {
                    line = i + 1;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindContainsLine(string[] lines, string token, out int line)
        {
            line = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    line = i + 1;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            var tick = typeName.IndexOf('`');
            return tick > 0 ? typeName.Substring(0, tick) : typeName;
        }

        private static IReadOnlyList<string> BuildLineHints(object? source, string? lineHint)
        {
            var hints = new List<string>();

            if (!string.IsNullOrWhiteSpace(lineHint))
            {
                hints.Add(lineHint!.Trim());
            }

            if (source is INamed named && !string.IsNullOrWhiteSpace(named.Name))
            {
                hints.Add(named.Name!);
            }

            if (source is StyledElement { Name: { Length: > 0 } elementName })
            {
                hints.Add(elementName);
            }

            return hints
                .Where(static h => !string.IsNullOrWhiteSpace(h))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string? TryGetSourceHint(object source)
        {
            if (source is null)
            {
                return null;
            }

            var sourceProperty = source.GetType().GetProperty(
                "Source",
                BindingFlags.Instance | BindingFlags.Public);
            if (sourceProperty is null || !sourceProperty.CanRead)
            {
                return null;
            }

            object? sourceValue;
            try
            {
                sourceValue = sourceProperty.GetValue(source);
            }
            catch
            {
                return null;
            }

            if (sourceValue is Uri uri)
            {
                return uri.ToString();
            }

            return sourceValue as string;
        }

        private static string BuildObjectStatus(
            SourceDocumentLocation? xamlLocation,
            SourceDocumentLocation? codeLocation,
            string fallbackStatus)
        {
            if (xamlLocation is not null && codeLocation is not null)
            {
                return "XAML object source resolved; C# source resolved.";
            }

            if (xamlLocation is not null)
            {
                return "XAML object source resolved; C# source location unavailable.";
            }

            return fallbackStatus;
        }

        private sealed class AssemblySymbols
        {
            private readonly MetadataReader _peMetadataReader;
            private readonly MetadataReader _pdbMetadataReader;
            private readonly ConcurrentDictionary<int, string> _documentPathCache = new();
            private readonly object _documentIndexSync = new();
            private Dictionary<string, CandidateLocation>? _documentIndexByPath;
            private Dictionary<string, CandidateLocation>? _documentIndexByFileName;

            public AssemblySymbols(string assemblyPath, string pdbPath)
            {
                var peReader = new PEReader(new MemoryStream(File.ReadAllBytes(assemblyPath), writable: false));
                _peMetadataReader = peReader.GetMetadataReader();
                var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(
                    new MemoryStream(File.ReadAllBytes(pdbPath), writable: false),
                    MetadataStreamOptions.PrefetchMetadata);
                _pdbMetadataReader = pdbProvider.GetMetadataReader();
            }

            public SourceLocationInfo Resolve(Type type)
            {
                if (!TryGetTypeHandle(type, out var typeHandle))
                {
                    return new SourceLocationInfo(type, xamlLocation: null, codeLocation: null, status: "Type metadata token is unavailable.");
                }

                var typeDefinition = _peMetadataReader.GetTypeDefinition(typeHandle);
                var typeName = _peMetadataReader.GetString(typeDefinition.Name);

                var xaml = TryResolveTypeLocalLocation(typeDefinition.GetMethods(), LocationKind.Xaml)
                           ?? TryResolveAssemblyWideLocation(typeName, LocationKind.Xaml);
                var code = TryResolveTypeLocalLocation(typeDefinition.GetMethods(), LocationKind.Code)
                           ?? TryResolveAssemblyWideLocation(typeName, LocationKind.Code);

                var status = BuildStatus(xaml, code);
                return new SourceLocationInfo(
                    type,
                    xaml?.ToSourceDocumentLocation(),
                    code?.ToSourceDocumentLocation(),
                    status);
            }

            public SourceDocumentLocation? ResolveDocument(string documentHint)
            {
                if (string.IsNullOrWhiteSpace(documentHint))
                {
                    return null;
                }

                EnsureDocumentIndex();

                var normalizedHint = NormalizeDocumentPath(documentHint);
                if (_documentIndexByPath!.TryGetValue(normalizedHint, out var byExactPath))
                {
                    return byExactPath.ToSourceDocumentLocation();
                }

                var fileName = Path.GetFileName(normalizedHint);
                if (!string.IsNullOrEmpty(fileName) && _documentIndexByFileName!.TryGetValue(fileName, out var byFileName))
                {
                    return byFileName.ToSourceDocumentLocation();
                }

                CandidateLocation? best = null;
                var bestDelta = int.MaxValue;
                foreach (var kvp in _documentIndexByPath)
                {
                    if (!kvp.Key.EndsWith(normalizedHint, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var delta = kvp.Key.Length - normalizedHint.Length;
                    if (best is null
                        || delta < bestDelta
                        || (delta == bestDelta && kvp.Value.IsBetterThan(best.Value)))
                    {
                        best = kvp.Value;
                        bestDelta = delta;
                    }
                }

                return best?.ToSourceDocumentLocation();
            }

            private void EnsureDocumentIndex()
            {
                if (_documentIndexByPath is not null && _documentIndexByFileName is not null)
                {
                    return;
                }

                lock (_documentIndexSync)
                {
                    if (_documentIndexByPath is not null && _documentIndexByFileName is not null)
                    {
                        return;
                    }

                    var byPath = new Dictionary<string, CandidateLocation>(StringComparer.OrdinalIgnoreCase);
                    var byFileName = new Dictionary<string, CandidateLocation>(StringComparer.OrdinalIgnoreCase);

                    foreach (var methodHandle in _peMetadataReader.MethodDefinitions)
                    {
                        var methodDefinition = _peMetadataReader.GetMethodDefinition(methodHandle);
                        var methodName = _peMetadataReader.GetString(methodDefinition.Name);
                        var methodDebugInfo = _pdbMetadataReader.GetMethodDebugInformation(methodHandle.ToDebugInformationHandle());
                        var fallbackDocument = methodDebugInfo.Document;

                        foreach (var sequencePoint in methodDebugInfo.GetSequencePoints())
                        {
                            if (sequencePoint.IsHidden || sequencePoint.StartLine <= 0)
                            {
                                continue;
                            }

                            var documentHandle = sequencePoint.Document.IsNil ? fallbackDocument : sequencePoint.Document;
                            if (documentHandle.IsNil)
                            {
                                continue;
                            }

                            var documentPath = GetDocumentPath(documentHandle);
                            if (documentPath.Length == 0)
                            {
                                continue;
                            }

                            var candidate = new CandidateLocation(
                                documentPath,
                                sequencePoint.StartLine,
                                sequencePoint.StartColumn,
                                methodName,
                                priority: 0);
                            var normalizedPath = NormalizeDocumentPath(documentPath);
                            UpsertBestCandidate(byPath, normalizedPath, candidate);

                            var fileName = Path.GetFileName(normalizedPath);
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                UpsertBestCandidate(byFileName, fileName, candidate);
                            }
                        }
                    }

                    _documentIndexByPath = byPath;
                    _documentIndexByFileName = byFileName;
                }
            }

            private CandidateLocation? TryResolveTypeLocalLocation(
                MethodDefinitionHandleCollection methodHandles,
                LocationKind kind)
            {
                CandidateLocation? best = null;
                foreach (var methodHandle in methodHandles)
                {
                    var methodDefinition = _peMetadataReader.GetMethodDefinition(methodHandle);
                    var methodName = _peMetadataReader.GetString(methodDefinition.Name);
                    var methodPriority = GetMethodPriority(methodName, kind);

                    var candidate = TryResolveMethodLocation(
                        methodHandle,
                        methodName,
                        methodPriority,
                        kind,
                        fileNamePredicate: null);

                    if (candidate is null)
                    {
                        continue;
                    }

                    if (best is null || candidate.Value.IsBetterThan(best.Value))
                    {
                        best = candidate;
                    }
                }

                return best;
            }

            private CandidateLocation? TryResolveAssemblyWideLocation(string typeName, LocationKind kind)
            {
                var expectedFileNames = kind switch
                {
                    LocationKind.Xaml => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        typeName + ".xaml",
                        typeName + ".axaml"
                    },
                    _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        typeName + ".xaml.cs",
                        typeName + ".axaml.cs",
                        typeName + ".cs"
                    }
                };

                CandidateLocation? best = null;
                foreach (var typeHandle in _peMetadataReader.TypeDefinitions)
                {
                    var typeDefinition = _peMetadataReader.GetTypeDefinition(typeHandle);
                    foreach (var methodHandle in typeDefinition.GetMethods())
                    {
                        var methodDefinition = _peMetadataReader.GetMethodDefinition(methodHandle);
                        var methodName = _peMetadataReader.GetString(methodDefinition.Name);
                        var methodPriority = GetMethodPriority(methodName, kind) + 10;
                        var candidate = TryResolveMethodLocation(
                            methodHandle,
                            methodName,
                            methodPriority,
                            kind,
                            fileNamePredicate: fileName => expectedFileNames.Contains(fileName));

                        if (candidate is null)
                        {
                            continue;
                        }

                        if (best is null || candidate.Value.IsBetterThan(best.Value))
                        {
                            best = candidate;
                        }
                    }
                }

                return best;
            }

            private CandidateLocation? TryResolveMethodLocation(
                MethodDefinitionHandle methodHandle,
                string methodName,
                int methodPriority,
                LocationKind kind,
                Func<string, bool>? fileNamePredicate)
            {
                var methodDebugInfo = _pdbMetadataReader.GetMethodDebugInformation(methodHandle.ToDebugInformationHandle());
                var fallbackDocument = methodDebugInfo.Document;

                foreach (var sequencePoint in methodDebugInfo.GetSequencePoints())
                {
                    if (sequencePoint.IsHidden || sequencePoint.StartLine <= 0)
                    {
                        continue;
                    }

                    var documentHandle = sequencePoint.Document.IsNil ? fallbackDocument : sequencePoint.Document;
                    if (documentHandle.IsNil)
                    {
                        continue;
                    }

                    var documentPath = GetDocumentPath(documentHandle);
                    if (documentPath.Length == 0)
                    {
                        continue;
                    }

                    if (!IsMatchingExtension(documentPath, kind))
                    {
                        continue;
                    }

                    if (fileNamePredicate is not null && !fileNamePredicate(Path.GetFileName(documentPath)))
                    {
                        continue;
                    }

                    return new CandidateLocation(
                        documentPath,
                        sequencePoint.StartLine,
                        sequencePoint.StartColumn,
                        methodName,
                        methodPriority);
                }

                return null;
            }

            private string GetDocumentPath(DocumentHandle handle)
            {
                var token = MetadataTokens.GetToken(handle);
                if (_documentPathCache.TryGetValue(token, out var existingPath))
                {
                    return existingPath;
                }

                var document = _pdbMetadataReader.GetDocument(handle);
                var path = DecodeDocumentName(_pdbMetadataReader, document.Name);
                var normalizedPath = NormalizeDocumentPath(path);
                _documentPathCache[token] = normalizedPath;
                return normalizedPath;
            }

            private static void UpsertBestCandidate(
                IDictionary<string, CandidateLocation> target,
                string key,
                CandidateLocation candidate)
            {
                if (target.TryGetValue(key, out var existing))
                {
                    if (candidate.IsBetterThan(existing))
                    {
                        target[key] = candidate;
                    }
                }
                else
                {
                    target[key] = candidate;
                }
            }

            private static string DecodeDocumentName(MetadataReader reader, BlobHandle nameHandle)
            {
                if (nameHandle.IsNil)
                {
                    return string.Empty;
                }

                var blobReader = reader.GetBlobReader(nameHandle);
                if (blobReader.RemainingBytes == 0)
                {
                    return string.Empty;
                }

                var separator = (char)blobReader.ReadByte();
                var builder = new StringBuilder();
                while (blobReader.RemainingBytes > 0)
                {
                    var partHandle = MetadataTokens.BlobHandle(blobReader.ReadCompressedInteger());
                    var partReader = reader.GetBlobReader(partHandle);
                    var part = partReader.ReadUTF8(partReader.RemainingBytes);
                    if (part.Length == 0)
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append(separator);
                    }

                    builder.Append(part);
                }

                return builder.ToString();
            }

            private static string NormalizeDocumentPath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return string.Empty;
                }

                var trimmed = path.Trim();
                if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri)
                {
                    if (uri.IsFile && uri.LocalPath.Length > 0)
                    {
                        trimmed = Uri.UnescapeDataString(uri.LocalPath);
                    }
                    else if (uri.AbsolutePath.Length > 0)
                    {
                        trimmed = Uri.UnescapeDataString(uri.AbsolutePath);
                    }
                }

                trimmed = trimmed.Replace('\\', '/');

                if (!Path.IsPathRooted(trimmed) && !OperatingSystem.IsWindows())
                {
                    var rootedCandidate = "/" + trimmed.TrimStart('/');
                    if (File.Exists(rootedCandidate))
                    {
                        return rootedCandidate;
                    }
                }

                return trimmed;
            }

            private static bool TryGetTypeHandle(Type type, out TypeDefinitionHandle typeHandle)
            {
                typeHandle = default;

                int token;
                try
                {
                    token = type.MetadataToken;
                }
                catch
                {
                    return false;
                }

                if (token <= 0)
                {
                    return false;
                }

                var entityHandle = MetadataTokens.EntityHandle(token);
                if (entityHandle.Kind != HandleKind.TypeDefinition)
                {
                    return false;
                }

                typeHandle = (TypeDefinitionHandle)entityHandle;
                return true;
            }

            private static int GetMethodPriority(string methodName, LocationKind kind)
            {
                if (kind == LocationKind.Xaml)
                {
                    if (string.Equals(methodName, "InitializeComponent", StringComparison.Ordinal))
                    {
                        return 0;
                    }

                    if (methodName.IndexOf("Build", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return 1;
                    }

                    return 2;
                }

                if (string.Equals(methodName, ".ctor", StringComparison.Ordinal))
                {
                    return 0;
                }

                if (string.Equals(methodName, "InitializeComponent", StringComparison.Ordinal))
                {
                    return 1;
                }

                return 2;
            }

            private static bool IsMatchingExtension(string path, LocationKind kind)
            {
                var extension = Path.GetExtension(path);
                if (kind == LocationKind.Xaml)
                {
                    return string.Equals(extension, ".xaml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".axaml", StringComparison.OrdinalIgnoreCase);
                }

                return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase);
            }

            private static string BuildStatus(CandidateLocation? xaml, CandidateLocation? code)
            {
                if (xaml is not null && code is not null)
                {
                    return "XAML and C# source locations resolved.";
                }

                if (xaml is not null)
                {
                    return "XAML source resolved; C# source location unavailable.";
                }

                if (code is not null)
                {
                    return "C# source resolved; XAML source location unavailable.";
                }

                return "No source sequence points were found for the selected type.";
            }

            private enum LocationKind
            {
                Xaml,
                Code
            }

            private readonly struct CandidateLocation
            {
                public CandidateLocation(string filePath, int line, int column, string methodName, int priority)
                {
                    FilePath = filePath;
                    Line = line;
                    Column = column;
                    MethodName = methodName;
                    Priority = priority;
                }

                public string FilePath { get; }

                public int Line { get; }

                public int Column { get; }

                public string MethodName { get; }

                public int Priority { get; }

                public bool IsBetterThan(CandidateLocation other)
                {
                    if (Priority != other.Priority)
                    {
                        return Priority < other.Priority;
                    }

                    if (Line != other.Line)
                    {
                        return Line < other.Line;
                    }

                    if (Column != other.Column)
                    {
                        return Column < other.Column;
                    }

                    return string.CompareOrdinal(FilePath, other.FilePath) < 0;
                }

                public SourceDocumentLocation ToSourceDocumentLocation()
                {
                    return new SourceDocumentLocation(FilePath, Line, MethodName, Column);
                }
            }
        }
    }
}
