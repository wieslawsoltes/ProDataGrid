namespace Avalonia.Diagnostics
{
    /// <summary>
    /// Selects which tree diagnostics segment a standalone diagnostics view should host.
    /// </summary>
    public enum DevToolsTreeSegmentKind
    {
        /// <summary>
        /// The logical, visual, or combined tree segment.
        /// </summary>
        Tree,

        /// <summary>
        /// The selected control property grid segment.
        /// </summary>
        Properties,

        /// <summary>
        /// The selected control layout visualizer and style analyzer segment.
        /// </summary>
        LayoutStyles
    }
}
