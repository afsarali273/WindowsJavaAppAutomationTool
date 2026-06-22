using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class AutomationService(AccessBridgeService bridge, InspectorLogger logger)
{
    public IReadOnlyList<string> GetActions(AccessibleNode node) => bridge.GetAccessibleActions(node.VmId, node.Context);

    public bool InvokeDefaultAction(AccessibleNode node, out string action)
    {
        var actions = GetActions(node);
        action = actions.FirstOrDefault(x => x.Contains("click", StringComparison.OrdinalIgnoreCase))
            ?? actions.FirstOrDefault(x => x.Contains("press", StringComparison.OrdinalIgnoreCase))
            ?? actions.FirstOrDefault() ?? "";
        if (action.Length == 0) return false;
        var success = bridge.DoAccessibleAction(node.VmId, node.Context, action, out var failure);
        logger.Log(success ? $"Executed JAB action '{action}' on {node.DisplayName}." : $"JAB action '{action}' failed at index {failure}.");
        return success;
    }

    public bool Focus(AccessibleNode node)
    { var result = bridge.RequestFocus(node.VmId, node.Context); logger.Log(result ? $"Focused {node.DisplayName}." : $"Could not focus {node.DisplayName}."); return result; }

    public bool SetText(AccessibleNode node, string text)
    { var result = bridge.SetText(node.VmId, node.Context, text); logger.Log(result ? $"Set text on {node.DisplayName}." : $"Could not set text on {node.DisplayName}; it may not be editable."); return result; }

    public string GetText(AccessibleNode node)
    {
        var text = bridge.GetText(node.VmId, node.Context, node.X, node.Y);
        if (!string.IsNullOrEmpty(text)) { logger.Log($"Read {text.Length} character(s) from {node.DisplayName}."); return text; }
        text = bridge.GetSelectedOrVirtualText(node.VmId, node.Context, out var source);
        if (!string.IsNullOrWhiteSpace(text)) { logger.Log($"Read selected text '{text}' from {node.DisplayName} via {source}."); return text; }
        var fallback = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Description;
        logger.Log($"No AccessibleText value was exposed; returned the accessible name/description for {node.DisplayName}.");
        return fallback;
    }
}
