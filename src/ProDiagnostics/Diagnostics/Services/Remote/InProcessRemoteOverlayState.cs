using System;
using Avalonia.Diagnostics.Remote;

namespace Avalonia.Diagnostics.Services;

internal sealed class InProcessRemoteOverlayState
{
    private const int SnapshotVersion = 1;

    private readonly object _gate = new();
    private OverlayState _state = OverlayState.Default;
    private long _generation;

    public bool IsLiveHoverEnabled
    {
        get
        {
            lock (_gate)
            {
                return _state.LiveHoverEnabled;
            }
        }
    }

    public RemoteOverlayOptionsSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _state.ToSnapshot(_generation);
        }
    }

    public int ApplyOptions(RemoteSetOverlayOptionsRequest request, out RemoteOverlayOptionsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var changed = 0;
            if (request.VisualizeMarginPadding is { } visualizeMarginPadding &&
                _state.VisualizeMarginPadding != visualizeMarginPadding)
            {
                _state = _state with { VisualizeMarginPadding = visualizeMarginPadding };
                changed++;
            }

            if (request.ShowInfo is { } showInfo && _state.ShowInfo != showInfo)
            {
                _state = _state with { ShowInfo = showInfo };
                changed++;
            }

            if (request.ShowRulers is { } showRulers && _state.ShowRulers != showRulers)
            {
                _state = _state with { ShowRulers = showRulers };
                changed++;
            }

            if (request.ShowExtensionLines is { } showExtensionLines &&
                _state.ShowExtensionLines != showExtensionLines)
            {
                _state = _state with { ShowExtensionLines = showExtensionLines };
                changed++;
            }

            if (request.HighlightElements is { } highlightElements &&
                _state.HighlightElements != highlightElements)
            {
                _state = _state with { HighlightElements = highlightElements };
                changed++;
            }

            if (request.ClipToTargetBounds is { } clipToTargetBounds &&
                _state.ClipToTargetBounds != clipToTargetBounds)
            {
                _state = _state with { ClipToTargetBounds = clipToTargetBounds };
                changed++;
            }

            if (changed > 0)
            {
                _generation++;
            }

            snapshot = _state.ToSnapshot(_generation);
            return changed;
        }
    }

    public bool SetLiveHoverEnabled(bool isEnabled, out RemoteOverlayOptionsSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_state.LiveHoverEnabled == isEnabled)
            {
                snapshot = _state.ToSnapshot(_generation);
                return false;
            }

            _state = _state with { LiveHoverEnabled = isEnabled };
            _generation++;
            snapshot = _state.ToSnapshot(_generation);
            return true;
        }
    }

    private readonly record struct OverlayState(
        bool VisualizeMarginPadding,
        bool ShowInfo,
        bool ShowRulers,
        bool ShowExtensionLines,
        bool HighlightElements,
        bool LiveHoverEnabled,
        bool ClipToTargetBounds)
    {
        public static OverlayState Default => new(
            VisualizeMarginPadding: true,
            ShowInfo: false,
            ShowRulers: false,
            ShowExtensionLines: false,
            HighlightElements: true,
            LiveHoverEnabled: true,
            ClipToTargetBounds: false);

        public RemoteOverlayOptionsSnapshot ToSnapshot(long generation)
        {
            return new RemoteOverlayOptionsSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: generation,
                Status: "ok",
                VisualizeMarginPadding: VisualizeMarginPadding,
                ShowInfo: ShowInfo,
                ShowRulers: ShowRulers,
                ShowExtensionLines: ShowExtensionLines,
                HighlightElements: HighlightElements,
                LiveHoverEnabled: LiveHoverEnabled,
                ClipToTargetBounds: ClipToTargetBounds);
        }
    }
}
