using JabInspector.Core.Models;

namespace JabInspector.App;

/// <summary>
/// App-layer coordinator for native highlight overlays.
/// Keeps highlight ownership out of feature code and prevents stale overlays.
/// </summary>
public sealed class HighlightManager
{
    private HighlightMode _currentMode = HighlightMode.None;
    private ElementBounds? _currentBounds;

    public HighlightMode CurrentMode => _currentMode;

    public void Flash(ElementBounds bounds, HighlightMode mode, TimeSpan? duration = null)
    {
        if (!IsDrawable(bounds)) return;
        ClearTransient();
        _currentMode = mode;
        _currentBounds = bounds;
        HighlightOverlay.Show(bounds, duration ?? DefaultDuration(mode));
    }

    public void ShowPersistent(ElementBounds bounds, HighlightMode mode = HighlightMode.Persistent)
    {
        if (!IsDrawable(bounds)) return;
        if (_currentMode == mode && _currentBounds == bounds) return;
        HighlightOverlay.HideTransient();
        HighlightOverlay.ShowPersistent(bounds);
        _currentMode = mode;
        _currentBounds = bounds;
    }

    public void ClearTransient()
    {
        HighlightOverlay.HideTransient();
        if (_currentMode is HighlightMode.TransientHover or HighlightMode.HierarchySelectionFlash or HighlightMode.RecorderActionFlash or HighlightMode.PlaybackStep)
        {
            _currentMode = HighlightMode.None;
            _currentBounds = null;
        }
    }

    public void ClearPersistent()
    {
        HighlightOverlay.HidePersistent();
        _currentMode = HighlightMode.None;
        _currentBounds = null;
    }

    public void ClearAll()
    {
        HighlightOverlay.ClearAll();
        _currentMode = HighlightMode.None;
        _currentBounds = null;
    }

    private static TimeSpan DefaultDuration(HighlightMode mode) => mode switch
    {
        HighlightMode.RecorderActionFlash => TimeSpan.FromSeconds(1.1),
        HighlightMode.PlaybackStep => TimeSpan.FromSeconds(1.4),
        HighlightMode.HierarchySelectionFlash => TimeSpan.FromSeconds(1.8),
        _ => TimeSpan.FromSeconds(2)
    };

    private static bool IsDrawable(ElementBounds bounds) => bounds.Width > 0 && bounds.Height > 0;
}
