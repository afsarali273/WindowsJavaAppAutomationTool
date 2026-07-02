using System.Text;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal abstract class ControlMessageExtractorBase : IControlMessageExtractor
{
    public abstract string ControlFamily { get; }

    public abstract bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren);

    protected static WindowsAutomationNode CreateVirtualSelectionNode(WindowsAutomationNode parent, string role, int index, string name, int totalCount)
    {
        var node = new WindowsAutomationNode
        {
            BackendKind = WindowsAutomationBackendKind.Win32,
            Parent = parent,
            NativeHandle = parent.NativeHandle,
            Name = string.IsNullOrWhiteSpace(name) ? $"Item {index}" : name,
            Role = role,
            ClassName = parent.ClassName,
            AutomationId = "",
            Value = "",
            Bounds = parent.Bounds,
            ClientBounds = parent.ClientBounds,
            ControlId = parent.ControlId,
            ProcessId = parent.ProcessId,
            ThreadId = parent.ThreadId,
            IsVisible = parent.IsVisible,
            IsEnabled = parent.IsEnabled,
            Style = parent.Style,
            ExtendedStyle = parent.ExtendedStyle,
            IndexInParent = parent.Children.Count
        };

        node.Metadata["isVirtual"] = bool.TrueString;
        node.Metadata["virtualItemType"] = role;
        node.Metadata["virtualIndex"] = index.ToString();
        node.Metadata["virtualItemCount"] = totalCount.ToString();
        node.Metadata["virtualParentClass"] = parent.ClassName;
        node.Metadata["controlFamily"] = parent.Metadata.TryGetValue("controlFamily", out var family) ? family : Win32ControlClassCatalog.GetFamily(parent.ClassName);
        node.Metadata["legacyTechnology"] = parent.Metadata.TryGetValue("legacyTechnology", out var tech) ? tech : Win32ControlClassCatalog.GetTechnology(parent.ClassName);
        node.Metadata["customPanelIndicator"] = "Real Control";
        return node;
    }

    protected static string ReadComboText(IntPtr hwnd, int index)
    {
        var length = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbGetLbTextLen, (IntPtr)index, IntPtr.Zero);
        var builder = new StringBuilder(Math.Max(length + 1, 64));
        User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbGetLbText, (IntPtr)index, builder);
        return builder.ToString();
    }

    protected static string ReadListText(IntPtr hwnd, int index)
    {
        var length = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbGetTextLen, (IntPtr)index, IntPtr.Zero);
        var builder = new StringBuilder(Math.Max(length + 1, 64));
        User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbGetText, (IntPtr)index, builder);
        return builder.ToString();
    }
}
