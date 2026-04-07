using Avalonia.Input;

namespace Avalonia.Diagnostics
{
    public class HotKeyConfiguration
    {
        /// <summary>
        /// Freezes refreshing the Value Frames inspector for the selected Control
        /// </summary>
        public KeyGesture ValueFramesFreeze { get; init; } = new(Key.S, KeyModifiers.Alt);

        /// <summary>
        /// Resumes refreshing the Value Frames inspector for the selected Control
        /// </summary>
        public KeyGesture ValueFramesUnfreeze { get; init; } = new(Key.D, KeyModifiers.Alt);

        /// <summary>
        /// Inspects the hovered Control in the Logical, Visual, or Combined Tree Page.
        /// </summary>
        public KeyGesture InspectHoveredControl { get; init; } = new(Key.None, KeyModifiers.Shift | KeyModifiers.Control);

        /// <summary>
        /// Toggles hovered element highlighting.
        /// </summary>
        public KeyGesture ToggleElementHighlight { get; init; } = new(Key.H, KeyModifiers.Shift | KeyModifiers.Control);

        /// <summary>
        /// Toggles focus tracking overlay.
        /// </summary>
        public KeyGesture ToggleFocusTracking { get; init; } = new(Key.K, KeyModifiers.Shift | KeyModifiers.Control);

        /// <summary>
        /// Toggles overlay rulers.
        /// </summary>
        public KeyGesture ToggleOverlayRulers { get; init; } = new(Key.R, KeyModifiers.Shift | KeyModifiers.Control);

        /// <summary>
        /// Toggles overlay information tooltip.
        /// </summary>
        public KeyGesture ToggleOverlayInfo { get; init; } = new(Key.D, KeyModifiers.Shift | KeyModifiers.Control);

        /// <summary>
        /// Toggles the freezing of Popups which prevents visible Popups from closing so they can be inspected
        /// </summary>
        public KeyGesture TogglePopupFreeze { get; init; } = new(Key.F, KeyModifiers.Alt | KeyModifiers.Control);

        /// <summary>
        /// Saves a Screenshot of the Selected Control in the Logical, Visual, or Combined Tree Page
        /// </summary>
        public KeyGesture ScreenshotSelectedControl { get; init; } = new(Key.F8);

        /// <summary>
        /// Toggles DevTools top-most mode.
        /// </summary>
        public KeyGesture ToggleTopMost { get; init; } = new(Key.T, KeyModifiers.Shift | KeyModifiers.Control);

        /// <summary>
        /// Selects the next tool tab.
        /// </summary>
        public KeyGesture NextToolTab { get; init; } = new(Key.OemCloseBrackets, KeyModifiers.Control);

        /// <summary>
        /// Selects the previous tool tab.
        /// </summary>
        public KeyGesture PreviousToolTab { get; init; } = new(Key.OemOpenBrackets, KeyModifiers.Control);

        /// <summary>
        /// Refreshes the current tool content.
        /// </summary>
        public KeyGesture RefreshCurrentTool { get; init; } = new(Key.F5);

        /// <summary>
        /// Clears records from the current tool when supported.
        /// </summary>
        public KeyGesture ClearCurrentTool { get; init; } = new(Key.L, KeyModifiers.Control);

        /// <summary>
        /// Opens the settings page.
        /// </summary>
        public KeyGesture OpenSettings { get; init; } = new(Key.OemPeriod, KeyModifiers.Control);

        /// <summary>
        /// Sets a breakpoint from current context (selected property or event).
        /// </summary>
        public KeyGesture SetBreakpoint { get; init; } = new(Key.F9);

        /// <summary>
        /// Focuses the current tool filter input.
        /// </summary>
        public KeyGesture FocusCurrentFilter { get; init; } = new(Key.F, KeyModifiers.Control);

        /// <summary>
        /// Selects the next match in current filtered records.
        /// </summary>
        public KeyGesture NextSearchMatch { get; init; } = new(Key.F3);

        /// <summary>
        /// Selects the previous match in current filtered records.
        /// </summary>
        public KeyGesture PreviousSearchMatch { get; init; } = new(Key.F3, KeyModifiers.Shift);

        /// <summary>
        /// Clears selection, then clears current filter when pressed again.
        /// </summary>
        public KeyGesture ClearSelectionOrFilter { get; init; } = new(Key.Escape);

        /// <summary>
        /// Removes selected record from current tool list when supported.
        /// </summary>
        public KeyGesture RemoveSelectedRecord { get; init; } = new(Key.Delete);
    }
}
