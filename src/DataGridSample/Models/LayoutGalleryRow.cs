// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace DataGridSample.Models;

public sealed class LayoutGalleryRow
{
    public int Id { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;
}
