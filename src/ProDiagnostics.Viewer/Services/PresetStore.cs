using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProDiagnostics.Viewer.Models;

namespace ProDiagnostics.Viewer.Services;

public static class PresetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<PresetDefinition> LoadPresets()
    {
        var presets = new List<PresetDefinition>
        {
            new()
            {
                Name = "All",
                Description = "Show every metric and activity",
                IncludeActivities = Array.Empty<string>(),
                IncludeMetrics = Array.Empty<string>()
            }
        };

        foreach (var folder in GetPresetFolders())
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                var preset = TryLoadPreset(file);
                if (preset != null)
                {
                    presets.Add(preset);
                }
            }
        }

        return presets;
    }

    public static string GetUserPresetFolder()
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(baseFolder, "ProDiagnostics.Viewer", "Presets");
    }

    private static IEnumerable<string> GetPresetFolders()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Presets");
        yield return GetUserPresetFolder();
    }

    private static PresetDefinition? TryLoadPreset(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PresetDefinition>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }
}
