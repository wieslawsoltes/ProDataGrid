using System;

namespace DataGridSample.Models
{
    public sealed class LegacyFilterItem
    {
        public string Description { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public uint Token { get; init; }

        public string? Notes { get; init; }
    }
}
