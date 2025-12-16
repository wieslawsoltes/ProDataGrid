// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;

namespace Avalonia.Controls
{
    /// <summary>
    /// Defines formats that can be exported to the clipboard.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    enum DataGridClipboardExportFormat
    {
        /// <summary>
        /// No clipboard export format specified.
        /// </summary>
        None,
        /// <summary>
        /// Tab-separated plain text (default clipboard payload).
        /// </summary>
        Text,
        /// <summary>
        /// Comma-separated values (text/csv).
        /// </summary>
        Csv,
        /// <summary>
        /// HTML table, including CF_HTML metadata and text/html.
        /// </summary>
        Html,
        /// <summary>
        /// Markdown table (text/markdown).
        /// </summary>
        Markdown,
        /// <summary>
        /// XML representation of the grid data (application/xml).
        /// </summary>
        Xml,
        /// <summary>
        /// YAML representation of the grid data (application/x-yaml).
        /// </summary>
        Yaml,
        /// <summary>
        /// JSON array of rows (application/json).
        /// </summary>
        Json
    }
}
