namespace DataGridSample.Models;

public sealed record VirtualSurfaceModeOption(
    string Key,
    string Name,
    string Description,
    bool ExpectsRowlessSurface)
{
    public string ExpectedBackend => ExpectsRowlessSurface
        ? "Expected backend: one virtual cell surface, zero retained rows/cells"
        : "Expected backend: automatic flat retained fallback";
}
