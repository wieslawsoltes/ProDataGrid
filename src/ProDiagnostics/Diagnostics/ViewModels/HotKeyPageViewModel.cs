using System.Collections.ObjectModel;
using Avalonia.Input;

namespace Avalonia.Diagnostics.ViewModels
{
    internal record HotKeyDescription(string Gesture, string BriefDescription, string? DetailedDescription = null);

    internal class HotKeyPageViewModel : ViewModelBase
    {
        private ObservableCollection<HotKeyDescription>? _hotKeyDescriptions;
        public ObservableCollection<HotKeyDescription>? HotKeyDescriptions
        {
            get => _hotKeyDescriptions;
            private set => RaiseAndSetIfChanged(ref _hotKeyDescriptions, value);
        }

        public void SetOptions(DevToolsOptions options)
        {
            var hotKeys = options.HotKeys;

            HotKeyDescriptions = new()
            {
                new(CreateDescription(options.Gesture), "Launch DevTools", "Launches DevTools to inspect the TopLevel that received the hotkey input"),
                new(CreateDescription(hotKeys.ValueFramesFreeze), "Freeze Value Frames", "Pauses refreshing the Value Frames inspector for the selected Control"),
                new(CreateDescription(hotKeys.ValueFramesUnfreeze), "Unfreeze Value Frames", "Resumes refreshing the Value Frames inspector for the selected Control"),
                new(CreateDescription(hotKeys.InspectHoveredControl), "Inspect Control Under Pointer", "Inspects the hovered Control in the Logical or Visual Tree Page"),
                new(CreateDescription(hotKeys.ToggleElementHighlight), "Toggle Element Highlight", "Enables or disables hovered element highlight overlays"),
                new(CreateDescription(hotKeys.ToggleFocusTracking), "Toggle Focus Tracking", "Enables or disables focused-control tracking"),
                new(CreateDescription(hotKeys.ToggleOverlayRulers), "Toggle Overlay Rulers", "Shows or hides overlay ruler lines"),
                new(CreateDescription(hotKeys.ToggleOverlayInfo), "Toggle Overlay Info", "Shows or hides overlay information tooltip"),
                new(CreateDescription(hotKeys.TogglePopupFreeze), "Toggle Popup Freeze", "Prevents visible Popups from closing so they can be inspected"),
                new(CreateDescription(hotKeys.ScreenshotSelectedControl), "Screenshot Selected Control", "Saves a Screenshot of the Selected Control in the Logical or Visual Tree Page"),
                new(CreateDescription(hotKeys.ToggleTopMost), "Toggle TopMost", "Toggles DevTools TopMost mode"),
                new(CreateDescription(hotKeys.NextToolTab), "Next Tool Tab", "Navigates to the next diagnostics tab"),
                new(CreateDescription(hotKeys.PreviousToolTab), "Previous Tool Tab", "Navigates to the previous diagnostics tab"),
                new(CreateDescription(hotKeys.RefreshCurrentTool), "Refresh Current Tool", "Refreshes the active diagnostics page"),
                new(CreateDescription(hotKeys.ClearCurrentTool), "Clear Current Tool", "Clears records in the active diagnostics page"),
                new(CreateDescription(hotKeys.OpenSettings), "Open Settings", "Opens the DevTools settings page"),
                new(CreateDescription(hotKeys.SetBreakpoint), "Set Breakpoint", "Creates a breakpoint for the selected property or event"),
                new(CreateDescription(hotKeys.FocusCurrentFilter), "Focus Filter", "Focuses the filter box in the active diagnostics page"),
                new(CreateDescription(hotKeys.NextSearchMatch), "Next Match", "Selects the next visible match in filtered records"),
                new(CreateDescription(hotKeys.PreviousSearchMatch), "Previous Match", "Selects the previous visible match in filtered records"),
                new(CreateDescription(hotKeys.ClearSelectionOrFilter), "Clear Selection/Filter", "Clears selection first, then clears the active filter"),
                new(CreateDescription(hotKeys.RemoveSelectedRecord), "Remove Selected Record", "Removes selected item from the active record list")
            };
        }

        private string CreateDescription(KeyGesture gesture)
        {
            if (gesture.Key == Key.None && gesture.KeyModifiers != KeyModifiers.None)
                return gesture.ToString().Replace("+None", "");
            else
                return gesture.ToString();
        }
    }
}
