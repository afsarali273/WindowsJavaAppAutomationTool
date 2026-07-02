using System.Drawing;
using System.Windows.Automation;
using WinInspector.Core.Abstractions;
using WinInspector.Core.Models;

namespace WinInspector.Core.Backends;

public sealed class UiaAutomationBackend : IWindowsAutomationBackend
{
    private readonly UiaTreeViewMode _viewMode;

    public UiaAutomationBackend()
        : this(UiaTreeViewMode.Raw)
    {
    }

    public UiaAutomationBackend(UiaTreeViewMode viewMode)
    {
        _viewMode = viewMode;
    }

    public WindowsAutomationBackendKind Kind => WindowsAutomationBackendKind.Uia;
    public string DisplayName => _viewMode switch
    {
        UiaTreeViewMode.Control => "Windows UI Automation (Control View)",
        UiaTreeViewMode.Content => "Windows UI Automation (Content View)",
        _ => "Windows UI Automation (Raw View)"
    };

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
            ClientBounds = ToRectangle(element.Current.BoundingRectangle),
            NativeHandle = new IntPtr(element.Current.NativeWindowHandle),
            ProcessId = (uint)Math.Max(element.Current.ProcessId, 0),
            IsVisible = !element.Current.IsOffscreen,
            IsEnabled = element.Current.IsEnabled
        };

        node.Metadata["frameworkId"] = element.Current.FrameworkId ?? "";
        node.Metadata["localizedControlType"] = element.Current.LocalizedControlType ?? "";
        node.Metadata["isControlElement"] = element.Current.IsControlElement.ToString();
        node.Metadata["isContentElement"] = element.Current.IsContentElement.ToString();
        node.Metadata["uia.viewMode"] = _viewMode.ToString();

        if (depth >= maxDepth) return node;

        var walker = GetWalker();
        var currentChild = walker.GetFirstChild(element);
        var i = 0;
        while (currentChild is not null && i < maxChildren)
        {
            var child = BuildNode(currentChild, node, depth + 1, maxDepth, maxChildren);
            child.IndexInParent = i;
            node.Children.Add(child);
            currentChild = walker.GetNextSibling(currentChild);
            i++;
        }
        return node;
    }

    private TreeWalker GetWalker() =>
        _viewMode switch
        {
            UiaTreeViewMode.Control => TreeWalker.ControlViewWalker,
            UiaTreeViewMode.Content => TreeWalker.ContentViewWalker,
            _ => TreeWalker.RawViewWalker
        };

    private static Rectangle ToRectangle(System.Windows.Rect rect) =>
        rect.IsEmpty ? Rectangle.Empty : Rectangle.FromLTRB((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);
}
