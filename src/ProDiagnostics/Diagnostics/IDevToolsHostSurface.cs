namespace Avalonia.Diagnostics;

/// <summary>
/// Marks standalone windows that belong to the diagnostics tool itself.
/// Controls hosted by these surfaces are ignored by local inspection fallback.
/// </summary>
internal interface IDevToolsHostSurface
{
}
