using JabInspector.Core.Diagnostics;
using JabInspector.Native;
using System.Text;

namespace JabInspector.Core.Services;

public struct WindowRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

public sealed class AccessBridgeService : IDisposable
{
    private readonly InspectorLogger _logger;
    private bool _initialized;
    public AccessBridgeService(InspectorLogger logger) => _logger = logger;

    public bool Initialize()
    {
        if (_initialized) return true;
        try { AccessBridgeNative.WindowsRun(); _initialized = true; return true; }
        catch (DllNotFoundException) { _logger.Log("WindowsAccessBridge-64.dll was not found. Ensure Java Access Bridge is installed, JAVA_HOME is set, and the application is running as x64."); }
        catch (Exception ex) { _logger.Log($"Access Bridge initialization failed: {ex.Message}"); }
        return false;
    }

    public bool IsJavaWindow(IntPtr hwnd)
    {
        try { return _initialized && AccessBridgeNative.isJavaWindow(hwnd); }
        catch (Exception ex) { _logger.Log($"Window probe failed: {ex.Message}"); return false; }
    }

    public bool IsSameObject(int vmId, long first, long second)
    { try { return first != 0 && second != 0 && AccessBridgeNative.isSameObject(vmId, first, second); } catch { return false; } }

    public bool TryGetAccessibleContextFromHwnd(IntPtr hwnd, out int vmId, out long context)
    {
        vmId = 0; context = 0;
        try { return _initialized && AccessBridgeNative.getAccessibleContextFromHWND(hwnd, out vmId, out context); }
        catch (Exception ex) { _logger.Log($"Could not obtain root context: {ex.Message}"); return false; }
    }

    public bool TryGetAccessibleContextInfo(int vmId, long context, out AccessibleContextInfo info)
    {
        info = default;
        try { return context != 0 && AccessBridgeNative.getAccessibleContextInfo(vmId, context, out info); }
        catch (Exception ex) { _logger.Log($"Could not read context {context}: {ex.Message}. If values look corrupted, verify AccessibleContextInfo against AccessBridgePackages.h for the installed JDK."); return false; }
    }

    public bool TryGetChildContext(int vmId, long context, int index, out long child)
    {
        child = 0;
        try { child = AccessBridgeNative.getAccessibleChildFromContext(vmId, context, index); return child != 0; }
        catch (Exception ex) { _logger.Log($"Could not read child {index}: {ex.Message}"); return false; }
    }

    public bool TryGetAccessibleContextAt(int vmId, long parentContext, int x, int y, out long context)
    {
        context = 0;
        try { return AccessBridgeNative.getAccessibleContextAt(vmId, parentContext, x, y, out context) && context != 0; }
        catch (Exception ex) { _logger.Log($"Hover inspection failed at ({x}, {y}): {ex.Message}"); return false; }
    }

    public bool TryGetParentContext(int vmId, long context, out long parent)
    {
        parent = 0;
        try { parent = AccessBridgeNative.getAccessibleParentFromContext(vmId, context); return parent != 0; }
        catch (Exception ex) { _logger.Log($"Could not resolve hover parent: {ex.Message}"); return false; }
    }

    public void ReleaseObject(int vmId, long context)
    { if (context == 0) return; try { AccessBridgeNative.releaseJavaObject(vmId, context); } catch { } }

    public IReadOnlyList<string> GetAccessibleActions(int vmId, long context)
    {
        try
        {
            var actions = new AccessibleActions { ActionInfo = new AccessibleActionInfo[256] };
            if (!AccessBridgeNative.getAccessibleActions(vmId, context, ref actions)) return [];
            return actions.ActionInfo.Take(Math.Clamp(actions.ActionsCount, 0, actions.ActionInfo.Length))
                .Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }
        catch (Exception ex) { _logger.Log($"Could not read accessible actions: {ex.Message}"); return []; }
    }

    public string GetVirtualAccessibleName(int vmId, long context)
    {
        try
        {
            if (context == 0) return "";
            var name = new StringBuilder(1024);
            return AccessBridgeNative.getVirtualAccessibleName(vmId, context, name, name.Capacity) && name.Length > 0
                ? name.ToString()
                : "";
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not read virtual accessible name: {ex.Message}");
            return "";
        }
    }

    public int GetObjectDepth(int vmId, long context)
    {
        try
        {
            return context == 0 ? -1 : AccessBridgeNative.getObjectDepth(vmId, context);
        }
        catch (EntryPointNotFoundException)
        {
            _logger.Log("Could not read object depth: getObjectDepth export was not found in the active WindowsAccessBridge DLL.");
            return -1;
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not read object depth: {ex.Message}");
            return -1;
        }
    }

    public bool DoAccessibleAction(int vmId, long context, string actionName, out int failure)
    {
        failure = -1;
        try
        {
            var actions = new AccessibleActionsToDo { ActionsCount = 1, Actions = new AccessibleActionInfo[32] };
            actions.Actions[0].Name = actionName;
            return AccessBridgeNative.doAccessibleActions(vmId, context, ref actions, out failure);
        }
        catch (Exception ex) { _logger.Log($"Accessible action failed: {ex.Message}"); return false; }
    }

    public bool RequestFocus(int vmId, long context)
    { try { return AccessBridgeNative.requestFocus(vmId, context); } catch (Exception ex) { _logger.Log($"Focus request failed: {ex.Message}"); return false; } }

    public bool SetText(int vmId, long context, string text)
    { try { return AccessBridgeNative.setTextContents(vmId, context, text); } catch (Exception ex) { _logger.Log($"Set text failed: {ex.Message}"); return false; } }

    public string? GetText(int vmId, long context, int x, int y)
    {
        try
        {
            if (AccessBridgeNative.getAccessibleTextInfo(vmId, context, out var info, x, y) && info.CharCount > 0)
            {
                var length = Math.Min(info.CharCount + 1, short.MaxValue);
                var text = new StringBuilder(length);
                if (AccessBridgeNative.getAccessibleTextRange(vmId, context, 0, Math.Min(info.CharCount - 1, short.MaxValue - 2), text, (short)length)) return text.ToString();
            }
            var value = new StringBuilder(1024);
            if (AccessBridgeNative.getCurrentAccessibleValueFromContext(vmId, context, value, 1024) && value.Length > 0) return value.ToString();
            return null;
        }
        catch (Exception ex) { _logger.Log($"Get text failed: {ex.Message}"); return null; }
    }

    public void EnrichTextAndValue(Models.AccessibleNode node, int x = 0, int y = 0)
    {
        if (node.Context == 0) return;
        EnrichAccessibleText(node, x == 0 ? node.X : x, y == 0 ? node.Y : y);
        EnrichAccessibleValue(node);

        if (string.IsNullOrWhiteSpace(node.TextPreview))
        {
            var fallback = GetSelectedOrVirtualText(node.VmId, node.Context, out var source);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                node.TextPreview = TrimPreview(fallback);
                node.TextPreviewSource = source;
            }
        }

        if (string.IsNullOrWhiteSpace(node.TextPreview))
        {
            var ownLabel = FirstNonEmpty(node.Name, node.VirtualAccessibleName, node.Description, node.CurrentValue);
            if (!string.IsNullOrWhiteSpace(ownLabel))
            {
                node.TextPreview = TrimPreview(ownLabel);
                node.TextPreviewSource = "accessible label/value fallback";
            }
        }
    }

    public void EnrichContainerTextFromChildren(Models.AccessibleNode node, int maxDescendants = 80)
    {
        if (!ShouldAggregateChildLabels(node)) return;
        if (!string.IsNullOrWhiteSpace(node.TextPreview) && node.TextPreviewSource != "accessible label/value fallback") return;

        var labels = new List<string>();
        foreach (var descendant in EnumerateDescendants(node).Skip(1).Take(maxDescendants))
        {
            var label = FirstNonEmpty(descendant.Name, descendant.VirtualAccessibleName, descendant.Description, descendant.TextPreview, descendant.CurrentValue);
            if (string.IsNullOrWhiteSpace(label)) continue;
            if (labels.Any(existing => string.Equals(existing, label, StringComparison.OrdinalIgnoreCase))) continue;
            labels.Add(label.Trim());
            if (labels.Count >= 20) break;
        }

        if (labels.Count == 0) return;
        node.TextPreview = TrimPreview(string.Join(" ", labels));
        node.TextPreviewSource = "descendant accessible labels";
    }

    public string? GetSelectedOrVirtualText(int vmId, long context, out string source)
    {
        source = "";
        try
        {
            var selectionCount = Math.Clamp(AccessBridgeNative.getAccessibleSelectionCountFromContext(vmId, context), 0, 100);
            for (var i = 0; i < selectionCount; i++)
            {
                var selected = AccessBridgeNative.getAccessibleSelectionFromContext(vmId, context, i);
                if (selected == 0) continue;
                try
                {
                    var label = ReadContextLabel(vmId, selected) ?? FindNamedDescendant(vmId, selected, 2, true);
                    if (!string.IsNullOrWhiteSpace(label)) { source = "AccessibleSelection"; return label; }
                }
                finally { ReleaseObject(vmId, selected); }
            }

            var active = AccessBridgeNative.getActiveDescendent(vmId, context);
            if (active != 0)
            {
                try
                {
                    var label = ReadContextLabel(vmId, active) ?? FindNamedDescendant(vmId, active, 2, false);
                    if (!string.IsNullOrWhiteSpace(label)) { source = "active descendant"; return label; }
                }
                finally { ReleaseObject(vmId, active); }
            }

            var selectedDescendant = FindNamedDescendant(vmId, context, 3, true);
            if (!string.IsNullOrWhiteSpace(selectedDescendant)) { source = "selected descendant"; return selectedDescendant; }

            var virtualName = new StringBuilder(1024);
            if (AccessBridgeNative.getVirtualAccessibleName(vmId, context, virtualName, virtualName.Capacity) && virtualName.Length > 0)
            { source = "virtual accessible name"; return virtualName.ToString(); }
        }
        catch (Exception ex) { _logger.Log($"Combo/selection text lookup failed: {ex.Message}"); }
        return null;
    }

    private void EnrichAccessibleText(Models.AccessibleNode node, int x, int y)
    {
        try
        {
            if (!AccessBridgeNative.getAccessibleTextInfo(node.VmId, node.Context, out var info, x, y)) return;

            node.TextCharCount = info.CharCount;
            node.TextCaretIndex = info.CaretIndex;
            node.TextIndexAtPoint = info.IndexAtPoint;

            if (info.CharCount > 0)
            {
                var length = Math.Min(info.CharCount + 1, short.MaxValue);
                var end = Math.Min(info.CharCount - 1, short.MaxValue - 2);
                var text = new StringBuilder(length);
                if (AccessBridgeNative.getAccessibleTextRange(node.VmId, node.Context, 0, end, text, (short)length) && text.Length > 0)
                {
                    node.TextPreview = TrimPreview(text.ToString());
                    node.TextPreviewSource = "AccessibleText range";
                }
            }

            var itemIndex = Math.Clamp(info.CaretIndex >= 0 ? info.CaretIndex : info.IndexAtPoint, 0, Math.Max(info.CharCount - 1, 0));
            if (AccessBridgeNative.getAccessibleTextItems(node.VmId, node.Context, out var items, itemIndex))
            {
                node.TextWord = items.Word ?? "";
                node.TextSentence = items.Sentence ?? "";
                if (string.IsNullOrWhiteSpace(node.TextPreview))
                {
                    node.TextPreview = TrimPreview(FirstNonEmpty(items.Word, items.Sentence, items.Letter == '\0' ? "" : items.Letter.ToString()));
                    node.TextPreviewSource = "AccessibleText items";
                }
            }

            if (AccessBridgeNative.getAccessibleTextSelectionInfo(node.VmId, node.Context, out var selection)
                && !string.IsNullOrWhiteSpace(selection.SelectedText))
            {
                node.TextSelected = selection.SelectedText;
                if (string.IsNullOrWhiteSpace(node.TextPreview))
                {
                    node.TextPreview = TrimPreview(selection.SelectedText);
                    node.TextPreviewSource = "AccessibleText selection";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Text enrichment failed for {node.DisplayName}: {ex.Message}");
        }
    }

    private void EnrichAccessibleValue(Models.AccessibleNode node)
    {
        try
        {
            var current = new StringBuilder(2048);
            if (AccessBridgeNative.getCurrentAccessibleValueFromContext(node.VmId, node.Context, current, (short)current.Capacity) && current.Length > 0)
                node.CurrentValue = current.ToString();

            var minimum = new StringBuilder(1024);
            if (AccessBridgeNative.getMinimumAccessibleValueFromContext(node.VmId, node.Context, minimum, (short)minimum.Capacity) && minimum.Length > 0)
                node.MinimumValue = minimum.ToString();

            var maximum = new StringBuilder(1024);
            if (AccessBridgeNative.getMaximumAccessibleValueFromContext(node.VmId, node.Context, maximum, (short)maximum.Capacity) && maximum.Length > 0)
                node.MaximumValue = maximum.ToString();

            if (string.IsNullOrWhiteSpace(node.TextPreview) && !string.IsNullOrWhiteSpace(node.CurrentValue))
            {
                node.TextPreview = TrimPreview(node.CurrentValue);
                node.TextPreviewSource = "AccessibleValue current";
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Value enrichment failed for {node.DisplayName}: {ex.Message}");
        }
    }

    private static bool ShouldAggregateChildLabels(Models.AccessibleNode node)
    {
        if (node.Children.Count == 0) return false;
        var role = $"{node.Role} {node.RoleEnUs}";
        return string.IsNullOrWhiteSpace(node.Name + node.VirtualAccessibleName + node.Description + node.TextPreview + node.CurrentValue)
               || role.Contains("layered pane", StringComparison.OrdinalIgnoreCase)
               || role.Contains("root pane", StringComparison.OrdinalIgnoreCase)
               || role.Contains("panel", StringComparison.OrdinalIgnoreCase)
               || role.Contains("pane", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Models.AccessibleNode> EnumerateDescendants(Models.AccessibleNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var nested in EnumerateDescendants(child))
                yield return nested;
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";

    private static string TrimPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 240 ? normalized : normalized[..240];
    }

    private string? ReadContextLabel(int vmId, long context)
    {
        if (!TryGetAccessibleContextInfo(vmId, context, out var info)) return null;
        if (!string.IsNullOrWhiteSpace(info.Name)) return info.Name;
        if (!string.IsNullOrWhiteSpace(info.Description)) return info.Description;
        var virtualName = new StringBuilder(1024);
        return AccessBridgeNative.getVirtualAccessibleName(vmId, context, virtualName, virtualName.Capacity) && virtualName.Length > 0
            ? virtualName.ToString() : null;
    }

    private string? FindNamedDescendant(int vmId, long context, int depth, bool requireSelected)
    {
        if (depth <= 0 || !TryGetAccessibleContextInfo(vmId, context, out var parentInfo)) return null;
        var childCount = Math.Min(Math.Max(parentInfo.ChildrenCount, 0), 250);
        for (var i = 0; i < childCount; i++)
        {
            if (!TryGetChildContext(vmId, context, i, out var child)) continue;
            try
            {
                if (!TryGetAccessibleContextInfo(vmId, child, out var childInfo)) continue;
                var states = $"{childInfo.States} {childInfo.StatesEnUs}";
                var selected = states.Contains("selected", StringComparison.OrdinalIgnoreCase) || states.Contains("focused", StringComparison.OrdinalIgnoreCase) || states.Contains("active", StringComparison.OrdinalIgnoreCase);
                if ((!requireSelected || selected) && !string.IsNullOrWhiteSpace(childInfo.Name)) return childInfo.Name;
                var nested = FindNamedDescendant(vmId, child, depth - 1, requireSelected);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
            finally { ReleaseObject(vmId, child); }
        }
        return null;
    }

    public bool TryGetWindowRect(IntPtr hwnd, out WindowRect rect)
    {
        rect = default;
        try
        {
            if (User32Native.GetWindowRect(hwnd, out var nativeRect))
            {
                rect = new WindowRect
                {
                    Left = nativeRect.Left,
                    Top = nativeRect.Top,
                    Right = nativeRect.Right,
                    Bottom = nativeRect.Bottom
                };
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to get window rect: {ex.Message}");
            return false;
        }
    }

    public void Shutdown()
    {
        // shutdownAccessBridge is part of Oracle's AccessBridgeCalls.c helper,
        // not an export of WindowsAccessBridge-64.dll. The native bridge is
        // released when this process exits.
        _initialized = false;
    }
    public void Dispose() => Shutdown();
}
