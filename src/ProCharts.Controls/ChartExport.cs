// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ProCharts.Controls;

/// <summary>Supported chart export encodings. Image exports contain the plot, not its surrounding toolbar.</summary>
public enum ChartExportFormat { Png, Svg, Csv }
/// <summary>An encoded document ready for a platform-specific save service.</summary>
public sealed record ChartExportPayload(string FileName, string MimeType, byte[] Data);
/// <summary>Optional host-owned export destination, useful for browser downloads and application document services.</summary>
public interface IChartExportService
{
    /// <summary>Saves the payload. Cancellation is not reported as an export error.</summary>
    Task SaveAsync(ChartExportPayload payload, CancellationToken cancellationToken);
}

internal sealed class StorageChartExportService(Control owner) : IChartExportService
{
    public async Task SaveAsync(ChartExportPayload payload, CancellationToken cancellationToken)
    {
        TopLevel top = TopLevel.GetTopLevel(owner) ?? throw new InvalidOperationException("The chart is not attached to a window.");
        if (!top.StorageProvider.CanSave) throw new NotSupportedException("This host does not expose a save picker. Supply IChartExportService.");
        string extension = Path.GetExtension(payload.FileName);
        IStorageFile? file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export chart", SuggestedFileName = payload.FileName,
            DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices = new[] { new FilePickerFileType(extension.ToUpperInvariant())
                { Patterns = new[] { "*" + extension }, MimeTypes = new[] { payload.MimeType } } }
        });
        if (file == null) return;
        using (file)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using Stream stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            await stream.WriteAsync(payload.Data, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
}
