using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

/// <summary>
/// Shared Java element resolution pipeline for hover, picker, passive recorder capture,
/// and future playback diagnostics.
///
/// The service intentionally does not create or own Java Access Bridge contexts.
/// Callers provide the actual JAB hit-test delegate so native handle lifetime remains
/// centralized in the existing tree/view-model code.
/// </summary>
public sealed class JavaElementInspectionService
{
    private readonly AccessBridgeService _bridge;
    private readonly InspectorLogger _logger;

    public JavaElementInspectionService(AccessBridgeService bridge, InspectorLogger logger)
    {
        _bridge = bridge;
        _logger = logger;
    }

    public JavaInspectionResult? InspectAtScreenPoint(
        AccessibleNode root,
        IntPtr windowHwnd,
        NativePoint screenPoint,
        Func<int, int, AccessibleNode?> inspectAtJabPoint,
        Func<AccessibleNode, ElementBounds> getPhysicalBounds,
        Func<ElementBounds, NativePoint, bool> containsPoint,
        Action<AccessibleNode> refreshBounds,
        string logPrefix = "[INSPECT]")
    {
        if (!_bridge.TryGetWindowRect(windowHwnd, out var nativeRect))
        {
            _logger.Debug($"{logPrefix} Resolve skipped because the native window rect could not be read. Hwnd=0x{windowHwnd.ToInt64():X}.");
            return null;
        }

        if (screenPoint.X < nativeRect.Left || screenPoint.X >= nativeRect.Right ||
            screenPoint.Y < nativeRect.Top || screenPoint.Y >= nativeRect.Bottom)
        {
            _logger.Debug($"{logPrefix} Resolve skipped because point is outside native window rect. Point=({screenPoint.X},{screenPoint.Y}), Rect=({nativeRect.Left},{nativeRect.Top},{nativeRect.Right},{nativeRect.Bottom}).");
            return null;
        }

        refreshBounds(root);
        if (!root.HasValidBounds)
        {
            _logger.Debug($"{logPrefix} Resolve skipped because root has no valid bounds.");
            return null;
        }

        var nativeWidth = Math.Max(1, nativeRect.Right - nativeRect.Left);
        var nativeHeight = Math.Max(1, nativeRect.Bottom - nativeRect.Top);
        var scaleX = (double)nativeWidth / root.Width;
        var scaleY = (double)nativeHeight / root.Height;
        var jabX = root.X + (int)Math.Round((screenPoint.X - nativeRect.Left) / scaleX);
        var jabY = root.Y + (int)Math.Round((screenPoint.Y - nativeRect.Top) / scaleY);

        _logger.Debug($"{logPrefix} Resolving point. Physical=({screenPoint.X},{screenPoint.Y}), NativeRect=({nativeRect.Left},{nativeRect.Top},{nativeRect.Right},{nativeRect.Bottom}), RootBounds=({root.X},{root.Y},{root.Width},{root.Height}), Scale=({scaleX:0.###},{scaleY:0.###}), LogicalProbe=({jabX},{jabY}).");

        var node = inspectAtJabPoint(screenPoint.X, screenPoint.Y);
        var bounds = node is null ? new ElementBounds(0, 0, 0, 0) : getPhysicalBounds(node);
        var usedLogical = false;

        _logger.Debug(node is null
            ? $"{logPrefix} Physical JAB hit-test returned no node."
            : $"{logPrefix} Physical JAB hit-test returned '{node.DisplayName}' with physical bounds ({bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}). ContainsPoint={containsPoint(bounds, screenPoint)}.");

        if (node is null || !containsPoint(bounds, screenPoint))
        {
            var logicalNode = inspectAtJabPoint(jabX, jabY);
            if (logicalNode is not null)
            {
                node = logicalNode;
                bounds = getPhysicalBounds(logicalNode);
                usedLogical = true;
                _logger.Debug($"{logPrefix} Logical JAB hit-test returned '{node.DisplayName}' with physical bounds ({bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}). ContainsPoint={containsPoint(bounds, screenPoint)}.");
            }
            else
            {
                _logger.Debug($"{logPrefix} Logical JAB hit-test returned no node.");
            }
        }

        if (node is null) return null;

        var visualNode = node;
        var usedAncestor = false;
        while (!HasUsableBounds(bounds) && visualNode.Parent is not null)
        {
            visualNode = visualNode.Parent;
            refreshBounds(visualNode);
            bounds = getPhysicalBounds(visualNode);
            usedAncestor = true;
        }

        return HasUsableBounds(bounds)
            ? new JavaInspectionResult(node, visualNode, bounds, usedLogical, usedAncestor)
            : null;
    }

    public JavaInspectionResult? ResolveVisibleBounds(
        AccessibleNode node,
        Func<AccessibleNode, ElementBounds> getPhysicalBounds,
        Action<AccessibleNode> refreshBounds,
        string logPrefix = "[BOUNDS]")
    {
        var visualNode = node;
        var bounds = new ElementBounds(0, 0, 0, 0);
        var usedAncestor = false;

        while (visualNode is not null)
        {
            refreshBounds(visualNode);
            bounds = getPhysicalBounds(visualNode);
            _logger.Debug($"{logPrefix} Candidate '{visualNode.DisplayName}' bounds=({bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}).");
            if (HasUsableBounds(bounds)) break;
            if (visualNode.Parent is null) break;
            visualNode = visualNode.Parent;
            usedAncestor = true;
        }

        return visualNode is not null && HasUsableBounds(bounds)
            ? new JavaInspectionResult(node, visualNode, bounds, usedLogicalHitTesting: false, usedAncestorFallback: usedAncestor)
            : null;
    }

    private static bool HasUsableBounds(ElementBounds bounds) => bounds.Width > 0 && bounds.Height > 0;
}
