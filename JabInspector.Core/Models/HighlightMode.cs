namespace JabInspector.Core.Models;

/// <summary>
/// Defines highlight modes that can be used by the app-layer highlight coordinator.
/// </summary>
public enum HighlightMode
{
    /// <summary>
    /// No highlight active.
    /// </summary>
    None,

    /// <summary>
    /// Transient highlight that appears briefly when hovering over elements.
    /// </summary>
    TransientHover,

    /// <summary>
    /// Brief flash highlight when selecting an element from the hierarchy.
    /// </summary>
    HierarchySelectionFlash,

    /// <summary>
    /// Persistent highlight that stays visible until explicitly cleared.
    /// </summary>
    Persistent,

    /// <summary>
    /// Brief flash highlight when the recorder captures an action.
    /// </summary>
    RecorderActionFlash,

    /// <summary>
    /// Highlight that shows the target element during playback.
    /// </summary>
    PlaybackStep
}
