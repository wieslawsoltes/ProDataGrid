using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProDiagnostics.Viewer.Models;
using ProDiagnostics.Viewer.Services;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Viewer;

public class PresetStoreTests
{
    [Fact]
    public void LoadPresets_Includes_Default_All()
    {
        var presets = PresetStore.LoadPresets();
        Assert.Contains(presets, preset => preset.Name == "All");
    }

    [Fact]
    public void LoadPresets_Reads_Asset_Preset_And_Skips_Invalid()
    {
        var presetFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Presets");
        Directory.CreateDirectory(presetFolder);

        var validPath = Path.Combine(presetFolder, $"preset-{Guid.NewGuid():N}.json");
        var invalidPath = Path.Combine(presetFolder, $"preset-invalid-{Guid.NewGuid():N}.json");

        try
        {
            var preset = new PresetDefinition
            {
                Name = "UnitTestPreset",
                Description = "Test",
                IncludeActivities = new[] { "http*" },
                IncludeMetrics = new[] { "cpu*" }
            };
            File.WriteAllText(validPath, JsonSerializer.Serialize(preset));
            File.WriteAllText(invalidPath, "{not-json");

            var presets = PresetStore.LoadPresets();
            Assert.Contains(presets, p => p.Name == "UnitTestPreset");
            Assert.DoesNotContain(presets, p => p.Name == "InvalidPreset");
        }
        finally
        {
            if (File.Exists(validPath))
            {
                File.Delete(validPath);
            }

            if (File.Exists(invalidPath))
            {
                File.Delete(invalidPath);
            }
        }
    }
}
