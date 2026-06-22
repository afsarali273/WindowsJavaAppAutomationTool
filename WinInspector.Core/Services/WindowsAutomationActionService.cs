using System.Windows.Automation;
using WinInspector.Core.Models;

namespace WinInspector.Core.Services;

public sealed class WindowsAutomationActionService
{
    public bool TryFocus(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        if (TryResolveElement(window, node, out var element, out message))
        {
            try
            {
                element.SetFocus();
                message = $"Focus requested through {node.BackendKind}.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Focus failed: {ex.Message}";
                return false;
            }
        }
        return false;
    }

    public bool TryInvoke(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        if (!TryResolveElement(window, node, out var element, out message)) return false;
        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
            {
                ((InvokePattern)invoke).Invoke();
                message = "InvokePattern executed successfully.";
                return true;
            }
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection))
            {
                ((SelectionItemPattern)selection).Select();
                message = "SelectionItemPattern executed successfully.";
                return true;
            }
            if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandCollapse))
            {
                var pattern = (ExpandCollapsePattern)expandCollapse;
                if (pattern.Current.ExpandCollapseState is ExpandCollapseState.Collapsed or ExpandCollapseState.PartiallyExpanded)
                {
                    pattern.Expand();
                    message = "ExpandCollapsePattern expanded the element.";
                }
                else
                {
                    pattern.Collapse();
                    message = "ExpandCollapsePattern collapsed the element.";
                }
                return true;
            }
            message = "No supported UIA invoke pattern was exposed.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Invoke failed: {ex.Message}";
            return false;
        }
    }

    public bool TrySetText(DesktopWindowInfo window, WindowsAutomationNode node, string text, out string message)
    {
        if (!TryResolveElement(window, node, out var element, out message)) return false;
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
            {
                ((ValuePattern)valuePattern).SetValue(text);
                message = $"ValuePattern set {text.Length} character(s).";
                return true;
            }
            message = "No writable ValuePattern was exposed for this element.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Set text failed: {ex.Message}";
            return false;
        }
    }

    public string GetText(DesktopWindowInfo window, WindowsAutomationNode node)
    {
        if (!TryResolveElement(window, node, out var element, out var message)) return message;
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
            {
                var value = ((ValuePattern)valuePattern).Current.Value;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern))
            {
                var value = ((TextPattern)textPattern).DocumentRange.GetText(-1)?.TrimEnd('\r', '\n', '\0');
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            if (!string.IsNullOrWhiteSpace(element.Current.Name)) return element.Current.Name;
            return "No UIA text or value was exposed for the selected Windows element.";
        }
        catch (Exception ex)
        {
            return $"Get text failed: {ex.Message}";
        }
    }

    private bool TryResolveElement(DesktopWindowInfo window, WindowsAutomationNode node, out AutomationElement element, out string message)
    {
        try
        {
            element = AutomationElement.FromHandle(window.Hwnd);
            if (element is null)
            {
                message = "UIA could not resolve the selected window handle.";
                return false;
            }

            var path = BuildPath(node);
            foreach (var index in path)
            {
                var children = element.FindAll(TreeScope.Children, Condition.TrueCondition);
                if (index < 0 || index >= children.Count)
                {
                    message = $"UIA path resolution failed at child index {index}.";
                    return false;
                }
                element = children[index];
            }

            message = "";
            return true;
        }
        catch (Exception ex)
        {
            element = null!;
            message = $"UIA resolve failed: {ex.Message}";
            return false;
        }
    }

    private static IReadOnlyList<int> BuildPath(WindowsAutomationNode node)
    {
        var indices = new Stack<int>();
        for (var cursor = node; cursor is not null; cursor = cursor.Parent)
        {
            if (cursor.Parent is null) continue;
            indices.Push(Math.Max(cursor.IndexInParent, 0));
        }
        return indices.ToArray();
    }
}
