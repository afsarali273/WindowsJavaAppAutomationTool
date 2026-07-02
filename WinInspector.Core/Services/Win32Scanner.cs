using System.Drawing;
using System.Text;
using WinInspector.Core.Models;
using WinInspector.Core.Native;
using WinInspector.Core.Services.ControlMessages;

namespace WinInspector.Core.Services;

public sealed class Win32Scanner
{
    private readonly Win32LegacyPanelHeuristics _legacyHeuristics = new();
    private readonly ControlMessageExtractorRegistry _controlMessageExtractors = new();

    public WindowsAutomationNode InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200)
    {
        var root = CreateNode(window.Hwnd, null, "window");
        PopulateChildren(root, 1, maxDepth, maxChildren);
        return root;
    }

    public IReadOnlyList<WindowsAutomationNode> FindChildrenInRegion(
        DesktopWindowInfo window,
        WindowsRect region,
        bool includeDescendants = true,
        int maxDepth = 4,
        int maxResults = 200)
    {
        var results = new List<WindowsAutomationNode>();
        if (region.IsEmpty || maxResults <= 0)
        {
            return results;
        }

        var regionRectangle = region.ToRectangle();
        var root = CreateNode(window.Hwnd, null, "window");
        if (Intersects(root.Bounds, regionRectangle))
        {
            root.Metadata["regionMatch"] = "intersects";
            results.Add(root);
            if (results.Count >= maxResults)
            {
                return results;
            }
        }

        CollectChildrenInRegion(root, regionRectangle, 1, maxDepth, maxResults, includeDescendants, results);
        return results;
    }

    public Win32Evidence InspectFromPoint(int x, int y)
    {
        var screenPoint = new User32DesktopNative.NativePoint(x, y);
        var hwnd = User32DesktopNative.WindowFromPoint(screenPoint);
        if (hwnd == IntPtr.Zero) return new Win32Evidence();

        var root = User32DesktopNative.GetAncestor(hwnd, User32DesktopNative.GaRoot);
        if (root != IntPtr.Zero)
        {
            var clientPoint = screenPoint;
            if (User32DesktopNative.ScreenToClient(root, ref clientPoint))
            {
                var realChild = User32DesktopNative.RealChildWindowFromPoint(root, clientPoint);
                if (realChild != IntPtr.Zero) hwnd = realChild;
            }
        }

        return BuildEvidence(hwnd);
    }

    private void PopulateChildren(WindowsAutomationNode parent, int depth, int maxDepth, int maxChildren)
    {
        if (depth > maxDepth) return;
        var index = 0;
        User32DesktopNative.EnumChildWindows(parent.NativeHandle, (hwnd, _) =>
        {
            if (index >= maxChildren) return false;
            var child = CreateNode(hwnd, parent, "child window");
            child.IndexInParent = index++;
            parent.Children.Add(child);
            PopulateChildren(child, depth + 1, maxDepth, maxChildren);
            PopulateVirtualChildren(child, maxChildren);
            return true;
        }, IntPtr.Zero);
    }

    private void PopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        _controlMessageExtractors.TryPopulateVirtualChildren(parent, maxChildren);
    }

    private void CollectChildrenInRegion(
        WindowsAutomationNode parent,
        Rectangle region,
        int depth,
        int maxDepth,
        int maxResults,
        bool includeDescendants,
        ICollection<WindowsAutomationNode> results)
    {
        if (depth > maxDepth || results.Count >= maxResults)
        {
            return;
        }

        var index = 0;
        User32DesktopNative.EnumChildWindows(parent.NativeHandle, (hwnd, _) =>
        {
            if (results.Count >= maxResults)
            {
                return false;
            }

            var child = CreateNode(hwnd, parent, "child window");
            child.IndexInParent = index++;
            var intersects = Intersects(child.Bounds, region) || Intersects(child.ClientBounds, region);
            if (intersects)
            {
                child.Metadata["regionMatch"] = region.Contains(child.Bounds) ? "contained" : "intersects";
                results.Add(child);
            }

            if (includeDescendants)
            {
                CollectChildrenInRegion(child, region, depth + 1, maxDepth, maxResults, true, results);
            }

            return results.Count < maxResults;
        }, IntPtr.Zero);
    }

    private Win32Evidence BuildEvidence(IntPtr hwnd)
    {
        User32DesktopNative.GetWindowRect(hwnd, out var rect);
        var threadId = User32DesktopNative.GetWindowThreadProcessId(hwnd, out var processId);
        var controlId = ReadControlId(hwnd);
        return new Win32Evidence
        {
            Hwnd = hwnd,
            ClassName = ReadClassName(hwnd),
            WindowText = ReadWindowText(hwnd),
            ControlId = controlId >= 0 ? controlId : null,
            Bounds = WindowsRect.FromRectangle(rect.ToRectangle()),
            ChildCount = CountChildren(hwnd),
            IsVisible = User32DesktopNative.IsWindowVisible(hwnd),
            IsEnabled = User32DesktopNative.IsWindowEnabled(hwnd),
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["threadId"] = threadId.ToString(),
                ["processId"] = processId.ToString(),
                ["style"] = $"0x{User32DesktopNative.GetWindowLongPtr(hwnd, User32DesktopNative.GwlStyle):X}",
                ["extendedStyle"] = $"0x{User32DesktopNative.GetWindowLongPtr(hwnd, User32DesktopNative.GwlExStyle):X}"
            }
        };
    }

    private WindowsAutomationNode CreateNode(IntPtr hwnd, WindowsAutomationNode? parent, string fallbackRole)
    {
        User32DesktopNative.GetWindowRect(hwnd, out var rect);
        var clientBounds = ReadClientBounds(hwnd);
        var threadId = User32DesktopNative.GetWindowThreadProcessId(hwnd, out var processId);
        var controlId = ReadControlId(hwnd);
        var style = User32DesktopNative.GetWindowLongPtr(hwnd, User32DesktopNative.GwlStyle);
        var extendedStyle = User32DesktopNative.GetWindowLongPtr(hwnd, User32DesktopNative.GwlExStyle);

        var node = new WindowsAutomationNode
        {
            BackendKind = WindowsAutomationBackendKind.Win32,
            Parent = parent,
            NativeHandle = hwnd,
            Name = ReadWindowText(hwnd),
            Role = fallbackRole,
            ClassName = ReadClassName(hwnd),
            Bounds = rect.ToRectangle(),
            ClientBounds = clientBounds,
            AutomationId = "",
            Value = "",
            ControlId = controlId,
            ProcessId = processId,
            ThreadId = threadId,
            IsVisible = User32DesktopNative.IsWindowVisible(hwnd),
            IsEnabled = User32DesktopNative.IsWindowEnabled(hwnd),
            Style = style,
            ExtendedStyle = extendedStyle
        };

        node.Metadata["hwnd"] = $"0x{hwnd.ToInt64():X}";
        node.Metadata["parentHwnd"] = parent is null ? "" : $"0x{parent.NativeHandle.ToInt64():X}";
        node.Metadata["threadId"] = threadId.ToString();
        node.Metadata["processId"] = processId.ToString();
        node.Metadata["style"] = $"0x{style:X}";
        node.Metadata["extendedStyle"] = $"0x{extendedStyle:X}";
        node.Metadata["clientBounds"] = $"{clientBounds.X},{clientBounds.Y},{clientBounds.Width},{clientBounds.Height}";
        node.Metadata["controlFamily"] = Win32ControlClassCatalog.GetFamily(node.ClassName);
        node.Metadata["legacyTechnology"] = Win32ControlClassCatalog.GetTechnology(node.ClassName);
        if (controlId >= 0) node.Metadata["controlId"] = controlId.ToString();

        var assessment = _legacyHeuristics.Evaluate(node);
        node.Metadata["isVb6"] = assessment.IsVb6.ToString();
        node.Metadata["isCanvasLike"] = assessment.IsCanvasLike.ToString();
        node.Metadata["customPanelScore"] = assessment.Score.ToString();
        node.Metadata["customPanelIndicator"] = assessment.Indicator;
        node.Metadata["customPanelReasons"] = string.Join(", ", assessment.Reasons);

        return node;
    }

    private static Rectangle ReadClientBounds(IntPtr hwnd)
    {
        if (!User32DesktopNative.GetClientRect(hwnd, out var rect)) return Rectangle.Empty;
        var topLeft = new User32DesktopNative.NativePoint(rect.Left, rect.Top);
        var bottomRight = new User32DesktopNative.NativePoint(rect.Right, rect.Bottom);
        if (!User32DesktopNative.ClientToScreen(hwnd, ref topLeft)) return Rectangle.Empty;
        if (!User32DesktopNative.ClientToScreen(hwnd, ref bottomRight)) return Rectangle.Empty;
        return Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    private static int ReadControlId(IntPtr hwnd)
    {
        try
        {
            var value = User32DesktopNative.GetDlgCtrlID(hwnd);
            return value <= 0 ? -1 : value;
        }
        catch
        {
            return -1;
        }
    }

    private static int CountChildren(IntPtr hwnd)
    {
        var count = 0;
        User32DesktopNative.EnumChildWindows(hwnd, (_, _) =>
        {
            count++;
            return true;
        }, IntPtr.Zero);
        return count;
    }

    private static string ReadWindowText(IntPtr hwnd)
    {
        var builder = new StringBuilder(512);
        User32DesktopNative.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        User32DesktopNative.GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static bool Intersects(Rectangle bounds, Rectangle region) =>
        !bounds.IsEmpty && !region.IsEmpty && bounds.IntersectsWith(region);
}
