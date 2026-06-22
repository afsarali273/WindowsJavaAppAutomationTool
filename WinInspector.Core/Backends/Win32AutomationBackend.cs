using System.Drawing;
using System.Text;
using WinInspector.Core.Abstractions;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Backends;

public sealed class Win32AutomationBackend : IWindowsAutomationBackend
{
    public WindowsAutomationBackendKind Kind => WindowsAutomationBackendKind.Win32;
    public string DisplayName => "Raw Win32";

    public bool IsAvailable() => true;

    public bool CanInspect(DesktopWindowInfo window) => true;

    public WindowsAutomationResult InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200)
    {
        try
        {
            var root = CreateNode(window.Hwnd, null, "window");
            PopulateChildren(root, 1, maxDepth, maxChildren);
            return WindowsAutomationResult.Success(Kind, root);
        }
        catch (Exception ex)
        {
            return WindowsAutomationResult.Failure(Kind, ex.Message);
        }
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
            return true;
        }, IntPtr.Zero);
    }

    private WindowsAutomationNode CreateNode(IntPtr hwnd, WindowsAutomationNode? parent, string fallbackRole)
    {
        User32DesktopNative.GetWindowRect(hwnd, out var rect);
        return new WindowsAutomationNode
        {
            BackendKind = Kind,
            Parent = parent,
            NativeHandle = hwnd,
            Name = ReadWindowText(hwnd),
            Role = fallbackRole,
            ClassName = ReadClassName(hwnd),
            Bounds = rect.ToRectangle(),
            AutomationId = "",
            Value = ""
        };
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
}
