using System.Drawing;
using System.Windows.Automation;
using WinInspector.Core.Abstractions;
using WinInspector.Core.Models;

namespace WinInspector.Core.Backends;

public sealed class UiaAutomationBackend : IWindowsAutomationBackend
{
    public WindowsAutomationBackendKind Kind => WindowsAutomationBackendKind.Uia;
    public string DisplayName => "Windows UI Automation";

    public bool IsAvailable() => true;

    public bool CanInspect(DesktopWindowInfo window) =>
        window.ApplicationKind is WindowsApplicationKind.NativeWin32 or WindowsApplicationKind.MixedDesktop or WindowsApplicationKind.PosShell;

    public WindowsAutomationResult InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200)
    {
        try
        {
            var element = AutomationElement.FromHandle(window.Hwnd);
            if (element is null) return WindowsAutomationResult.Failure(Kind, "UIA could not resolve the window handle.");
            var root = BuildNode(element, null, 0, maxDepth, maxChildren);
            return WindowsAutomationResult.Success(Kind, root);
        }
        catch (Exception ex)
        {
            return WindowsAutomationResult.Failure(Kind, ex.Message);
        }
    }

    private WindowsAutomationNode BuildNode(AutomationElement element, WindowsAutomationNode? parent, int depth, int maxDepth, int maxChildren)
    {
        var node = new WindowsAutomationNode
        {
            BackendKind = Kind,
            Parent = parent,
            Name = element.Current.Name ?? "",
            Role = element.Current.ControlType?.ProgrammaticName?.Replace("ControlType.", "", StringComparison.Ordinal) ?? "unknown",
            ClassName = element.Current.ClassName ?? "",
            AutomationId = element.Current.AutomationId ?? "",
            Value = element.Current.ItemType ?? "",
            Bounds = ToRectangle(element.Current.BoundingRectangle),
            NativeHandle = new IntPtr(element.Current.NativeWindowHandle)
        };

        if (depth >= maxDepth) return node;

        var children = element.FindAll(TreeScope.Children, Condition.TrueCondition);
        for (var i = 0; i < Math.Min(children.Count, maxChildren); i++)
        {
            var child = BuildNode(children[i], node, depth + 1, maxDepth, maxChildren);
            child.IndexInParent = i;
            node.Children.Add(child);
        }
        return node;
    }

    private static Rectangle ToRectangle(System.Windows.Rect rect) =>
        rect.IsEmpty ? Rectangle.Empty : Rectangle.FromLTRB((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);
}
