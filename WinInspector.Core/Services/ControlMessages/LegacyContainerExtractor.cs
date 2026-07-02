using System.Drawing;
using Accessibility;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal sealed class LegacyContainerExtractor : ControlMessageExtractorBase
{
    public override string ControlFamily => "legacy-container";

    public override bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        if (!TryResolveAccessibleParent(parent, out var accessible, out var sourceHwnd))
        {
            return false;
        }

        var childCount = SafeReadInt(() => accessible!.accChildCount);
        if (childCount <= 0)
        {
            return false;
        }

        try
        {
            using var session = new ControlMessageRemoteSession(parent.ProcessId);
            var count = Math.Min(childCount, maxChildren);
            for (var index = 1; index <= count; index++)
            {
                var childPath = $"msaa:child:{index}";
                var child = SafeReadObject(() => accessible!.get_accChild(index));
                WindowsAutomationNode childNode;

                if (child is IAccessible childAccessible)
                {
                    childNode = BuildNodeFromAccessible(childAccessible, OleAccNative.ChildidSelf, parent, parent.NativeHandle, index - 1, childPath, childCount);
                    PopulateDescendants(childAccessible, childNode, parent.NativeHandle, 1, 4, maxChildren, childPath);
                }
                else
                {
                    childNode = BuildNodeFromAccessible(accessible!, index, parent, sourceHwnd, index - 1, childPath, childCount);
                }

                parent.Children.Add(childNode);
            }

            return parent.Children.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void PopulateDescendants(
        IAccessible accessible,
        WindowsAutomationNode parent,
        IntPtr hwnd,
        int depth,
        int maxDepth,
        int maxChildren,
        string parentPath)
    {
        if (depth > maxDepth)
        {
            return;
        }

        var childCount = SafeReadInt(() => accessible.accChildCount);
        if (childCount <= 0)
        {
            return;
        }

        var count = Math.Min(childCount, maxChildren);
        for (var index = 1; index <= count; index++)
        {
            var childPath = $"{parentPath}/child:{index}";
            var child = SafeReadObject(() => accessible.get_accChild(index));
            WindowsAutomationNode childNode;

            if (child is IAccessible childAccessible)
            {
                childNode = BuildNodeFromAccessible(childAccessible, OleAccNative.ChildidSelf, parent, hwnd, index - 1, childPath, childCount);
                PopulateDescendants(childAccessible, childNode, hwnd, depth + 1, maxDepth, maxChildren, childPath);
            }
            else
            {
                childNode = BuildNodeFromAccessible(accessible, index, parent, hwnd, index - 1, childPath, childCount);
            }

            parent.Children.Add(childNode);
        }
    }

    private static WindowsAutomationNode BuildNodeFromAccessible(
        IAccessible accessible,
        object childId,
        WindowsAutomationNode parent,
        IntPtr hwnd,
        int indexInParent,
        string path,
        int siblingCount)
    {
        var bounds = ReadBounds(accessible, childId);
        var role = SafeReadObject(() => accessible.get_accRole(childId));
        var state = SafeReadObject(() => accessible.get_accState(childId));
        var name = SafeRead(() => accessible.get_accName(childId));
        var description = SafeRead(() => accessible.get_accDescription(childId));
        var value = SafeRead(() => accessible.get_accValue(childId));
        var defaultAction = SafeRead(() => accessible.get_accDefaultAction(childId));

        var node = new WindowsAutomationNode
        {
            BackendKind = WindowsAutomationBackendKind.Msaa,
            Parent = parent,
            NativeHandle = hwnd,
            Name = name,
            Role = string.IsNullOrWhiteSpace(ConvertRole(role)) ? "legacy child" : ConvertRole(role),
            ClassName = parent.ClassName,
            AutomationId = "",
            Value = value,
            Bounds = bounds,
            ClientBounds = bounds,
            ControlId = parent.ControlId,
            ProcessId = parent.ProcessId,
            ThreadId = parent.ThreadId,
            IsVisible = parent.IsVisible,
            IsEnabled = parent.IsEnabled,
            Style = parent.Style,
            ExtendedStyle = parent.ExtendedStyle,
            IndexInParent = indexInParent
        };

        node.Metadata["isVirtual"] = bool.TrueString;
        node.Metadata["virtualExtractor"] = "LegacyContainerExtractor";
        node.Metadata["virtualItemType"] = node.Role;
        node.Metadata["virtualIndex"] = (indexInParent + 1).ToString();
        node.Metadata["virtualItemCount"] = siblingCount.ToString();
        node.Metadata["virtualParentClass"] = parent.ClassName;
        node.Metadata["msaa.path"] = path;
        node.Metadata["msaa.role"] = ConvertRole(role);
        node.Metadata["msaa.state"] = ConvertState(state);
        node.Metadata["msaa.description"] = description;
        node.Metadata["msaa.value"] = value;
        node.Metadata["msaa.defaultAction"] = defaultAction;
        node.Metadata["msaa.childId"] = NormalizeChildId(childId).ToString();
        node.Metadata["msaa.isChildIdOnly"] = (NormalizeChildId(childId) != OleAccNative.ChildidSelf).ToString();
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            node.Metadata["virtualBounds"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
        }

        return node;
    }

    private static bool TryResolveAccessibleParent(WindowsAutomationNode parent, out IAccessible? accessible, out IntPtr hwnd)
    {
        hwnd = parent.NativeHandle;
        if (hwnd != IntPtr.Zero && OleAccNative.TryAccessibleObjectFromWindow(hwnd, OleAccNative.ObjidClient, out accessible) && accessible is not null)
        {
            return true;
        }

        if (hwnd != IntPtr.Zero && OleAccNative.TryAccessibleObjectFromWindow(hwnd, OleAccNative.ObjidWindow, out accessible) && accessible is not null)
        {
            return true;
        }

        if (parent.Parent is not null)
        {
            hwnd = parent.Parent.NativeHandle;
            if (hwnd != IntPtr.Zero && OleAccNative.TryAccessibleObjectFromWindow(hwnd, OleAccNative.ObjidClient, out accessible) && accessible is not null)
            {
                return true;
            }

            if (hwnd != IntPtr.Zero && OleAccNative.TryAccessibleObjectFromWindow(hwnd, OleAccNative.ObjidWindow, out accessible) && accessible is not null)
            {
                return true;
            }
        }

        accessible = null;
        return false;
    }

    private static Rectangle ReadBounds(IAccessible accessible, object childId)
    {
        try
        {
            accessible.accLocation(out var left, out var top, out var width, out var height, childId);
            return width <= 0 || height <= 0 ? Rectangle.Empty : new Rectangle(left, top, width, height);
        }
        catch
        {
            return Rectangle.Empty;
        }
    }

    private static string ConvertRole(object? role) => role?.ToString() ?? "";

    private static string ConvertState(object? state) => state?.ToString() ?? "";

    private static int NormalizeChildId(object? childId) => childId switch
    {
        int value => value,
        short value => value,
        null => OleAccNative.ChildidSelf,
        _ => OleAccNative.ChildidSelf
    };

    private static int SafeReadInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }

    private static object? SafeReadObject(Func<object?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static string SafeRead(Func<string?> getter)
    {
        try
        {
            return getter() ?? "";
        }
        catch
        {
            return "";
        }
    }
}
