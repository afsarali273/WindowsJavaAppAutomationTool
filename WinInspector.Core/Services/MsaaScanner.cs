using System.Drawing;
using Accessibility;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services;

public sealed class MsaaScanner
{
    public WindowsAutomationNode? InspectWindow(DesktopWindowInfo window, int maxDepth = 4, int maxChildren = 200)
    {
        if (!TryGetRootAccessible(window.Hwnd, out var accessible)) return null;

        var root = BuildNode(accessible!, OleAccNative.ChildidSelf, null, window.Hwnd, "root", "root");
        PopulateChildren(accessible!, root, 1, maxDepth, maxChildren, window.Hwnd, "root");
        return root;
    }

    public MsaaEvidence? InspectFromPoint(int x, int y)
    {
        var point = new User32DesktopNative.NativePoint(x, y);
        if (!OleAccNative.TryAccessibleObjectFromPoint(point, out var accessible, out var child) || accessible is null)
        {
            return null;
        }

        var childId = NormalizeChildId(child);
        return BuildEvidence(accessible, childId, "");
    }

    public bool TryFocus(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        if (!TryResolve(window, node, out var accessible, out var childId, out message)) return false;

        try
        {
            accessible.accSelect(OleAccNative.SelFlagTakeFocus, childId);
            message = $"{DescribeNode(node)} | route=MSAA accSelect(TAKEFOCUS) | result=Success";
            return true;
        }
        catch (Exception ex)
        {
            message = $"{DescribeNode(node)} | route=MSAA accSelect(TAKEFOCUS) | result=Failed | reason={ex.Message}";
            return false;
        }
    }

    public bool TryInvoke(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        if (!TryResolve(window, node, out var accessible, out var childId, out message)) return false;

        try
        {
            var defaultAction = SafeRead(() => accessible.get_accDefaultAction(childId));
            accessible.accDoDefaultAction(childId);
            message = string.IsNullOrWhiteSpace(defaultAction)
                ? $"{DescribeNode(node)} | route=MSAA accDoDefaultAction | result=Success"
                : $"{DescribeNode(node)} | route=MSAA accDoDefaultAction | result=Success | defaultAction={defaultAction}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"{DescribeNode(node)} | route=MSAA accDoDefaultAction | result=Failed | reason={ex.Message}";
            return false;
        }
    }

    public string GetText(DesktopWindowInfo window, WindowsAutomationNode node)
    {
        if (!TryResolve(window, node, out var accessible, out var childId, out var message)) return message;

        var value = SafeRead(() => accessible.get_accValue(childId));
        if (!string.IsNullOrWhiteSpace(value)) return value;
        var name = SafeRead(() => accessible.get_accName(childId));
        if (!string.IsNullOrWhiteSpace(name)) return name;
        var description = SafeRead(() => accessible.get_accDescription(childId));
        return string.IsNullOrWhiteSpace(description)
            ? $"{DescribeNode(node)} | route=MSAA GetText | result=Unavailable | reason=No MSAA text or value was exposed."
            : description;
    }

    private bool TryResolve(DesktopWindowInfo window, WindowsAutomationNode node, out IAccessible accessible, out object childId, out string message)
    {
        accessible = null!;
        childId = OleAccNative.ChildidSelf;

        if (!TryGetRootAccessible(window.Hwnd, out var root) || root is null)
        {
            message = "MSAA could not resolve the selected window handle.";
            return false;
        }

        accessible = root;
        var currentAccessible = accessible;
        var path = node.Metadata.TryGetValue("msaa.path", out var rawPath) ? rawPath : "";
        if (string.IsNullOrWhiteSpace(path) || string.Equals(path, "root", StringComparison.OrdinalIgnoreCase))
        {
            message = "";
            return true;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            if (!segment.StartsWith("child:", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(segment["child:".Length..], out var index))
            {
                message = $"MSAA path segment '{segment}' could not be parsed.";
                return false;
            }

            childId = index;
            var child = SafeReadObject(() => currentAccessible.get_accChild(index));
            if (child is IAccessible childAccessible)
            {
                currentAccessible = childAccessible;
                accessible = childAccessible;
                childId = OleAccNative.ChildidSelf;
            }
        }

        message = "";
        return true;
    }

    private void PopulateChildren(IAccessible parentAccessible, WindowsAutomationNode parentNode, int depth, int maxDepth, int maxChildren, IntPtr windowHwnd, string parentPath)
    {
        if (depth > maxDepth) return;

        var childCount = SafeReadInt(() => parentAccessible.accChildCount);
        for (var index = 1; index <= Math.Min(childCount, maxChildren); index++)
        {
            var childPath = $"{parentPath}/child:{index}";
            var child = SafeReadObject(() => parentAccessible.get_accChild(index));
            WindowsAutomationNode childNode;

            if (child is IAccessible childAccessible)
            {
                childNode = BuildNode(childAccessible, OleAccNative.ChildidSelf, parentNode, windowHwnd, "msaa child", childPath);
                PopulateChildren(childAccessible, childNode, depth + 1, maxDepth, maxChildren, windowHwnd, childPath);
            }
            else
            {
                childNode = BuildNode(parentAccessible, index, parentNode, windowHwnd, "msaa child", childPath);
            }

            childNode.IndexInParent = index - 1;
            parentNode.Children.Add(childNode);
        }
    }

    private WindowsAutomationNode BuildNode(IAccessible accessible, object childId, WindowsAutomationNode? parent, IntPtr windowHwnd, string fallbackRole, string path)
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
            NativeHandle = windowHwnd,
            Name = name,
            Role = !string.IsNullOrWhiteSpace(ConvertRole(role)) ? ConvertRole(role) : fallbackRole,
            ClassName = "",
            AutomationId = "",
            Value = value,
            Bounds = bounds,
            ClientBounds = bounds,
            IsVisible = true,
            IsEnabled = true
        };

        node.Metadata["msaa.path"] = path;
        node.Metadata["msaa.childId"] = NormalizeChildId(childId).ToString();
        node.Metadata["msaa.role"] = ConvertRole(role);
        node.Metadata["msaa.state"] = ConvertState(state);
        node.Metadata["msaa.description"] = description;
        node.Metadata["msaa.defaultAction"] = defaultAction;
        node.Metadata["msaa.childCount"] = SafeReadInt(() => accessible.accChildCount).ToString();
        node.Metadata["msaa.value"] = value;
        node.Metadata["msaa.name"] = name;
        node.Metadata["msaa.isChildIdOnly"] = (NormalizeChildId(childId) != OleAccNative.ChildidSelf).ToString();

        return node;
    }

    private MsaaEvidence BuildEvidence(IAccessible accessible, object childId, string path)
    {
        var role = SafeReadObject(() => accessible.get_accRole(childId));
        var state = SafeReadObject(() => accessible.get_accState(childId));
        var normalizedChildId = NormalizeChildId(childId);
        var bounds = WindowsRect.FromRectangle(ReadBounds(accessible, childId));
        return new MsaaEvidence
        {
            ElementRef = new MsaaElementRef
            {
                Hwnd = IntPtr.Zero,
                ChildId = normalizedChildId,
                Path = path,
                Role = ConvertRole(role),
                State = ConvertState(state),
                Bounds = bounds
            },
            Name = SafeRead(() => accessible.get_accName(childId)),
            Role = ConvertRole(role),
            Value = SafeRead(() => accessible.get_accValue(childId)),
            Description = SafeRead(() => accessible.get_accDescription(childId)),
            DefaultAction = SafeRead(() => accessible.get_accDefaultAction(childId)),
            ChildCount = SafeReadInt(() => accessible.accChildCount),
            ChildId = normalizedChildId,
            Bounds = bounds,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["state"] = ConvertState(state),
                ["path"] = path,
                ["isChildIdOnly"] = (normalizedChildId != OleAccNative.ChildidSelf).ToString()
            }
        };
    }

    private static bool TryGetRootAccessible(IntPtr hwnd, out IAccessible? accessible)
    {
        if (OleAccNative.TryAccessibleObjectFromWindow(hwnd, OleAccNative.ObjidClient, out accessible) && accessible is not null)
        {
            return true;
        }

        return OleAccNative.TryAccessibleObjectFromWindow(hwnd, OleAccNative.ObjidWindow, out accessible);
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

    private static string ConvertRole(object? role)
    {
        if (role is null) return "";
        return role.ToString() ?? "";
    }

    private static string ConvertState(object? state)
    {
        if (state is null) return "";
        return state.ToString() ?? "";
    }

    private static int NormalizeChildId(object? childId) => childId switch
    {
        int value => value,
        short value => value,
        null => OleAccNative.ChildidSelf,
        _ => OleAccNative.ChildidSelf
    };

    private static string SafeRead(Func<string> getter)
    {
        try { return getter() ?? ""; }
        catch { return ""; }
    }

    private static object? SafeReadObject(Func<object> getter)
    {
        try { return getter(); }
        catch { return null; }
    }

    private static int SafeReadInt(Func<int> getter)
    {
        try { return getter(); }
        catch { return 0; }
    }

    private static string DescribeNode(WindowsAutomationNode node)
    {
        var name = string.IsNullOrWhiteSpace(node.Name) ? "(no name)" : node.Name;
        var role = string.IsNullOrWhiteSpace(node.Role) ? "unknown" : node.Role;
        var path = node.Metadata.TryGetValue("msaa.path", out var rawPath) && !string.IsNullOrWhiteSpace(rawPath)
            ? rawPath
            : "root";
        return $"backend=Msaa | role={role} | name={name} | path={path}";
    }
}
