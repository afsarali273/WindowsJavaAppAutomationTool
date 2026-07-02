using System.Text;
using System.Windows.Automation;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services;

public sealed class WindowsAutomationActionService
{
    private readonly MsaaScanner _msaaScanner = new();

    public bool TryFocus(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        if (node.BackendKind == WindowsAutomationBackendKind.Msaa)
        {
            return _msaaScanner.TryFocus(window, node, out message);
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Win32)
        {
            return TryFocusWin32(window, node, out message);
        }

        if (TryResolveUiaElement(window, node, out var element, out message))
        {
            try
            {
                element.SetFocus();
                message = $"{DescribeNode(node)} | route=UIA SetFocus | result=Focus requested.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"{DescribeNode(node)} | route=UIA SetFocus | result=Failed | reason={ex.Message}";
                return false;
            }
        }

        return false;
    }

    public bool TryInvoke(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        if (node.BackendKind == WindowsAutomationBackendKind.Msaa)
        {
            return _msaaScanner.TryInvoke(window, node, out message);
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Win32)
        {
            if (TryInvokeVirtualWin32Selection(node, out message))
            {
                return true;
            }
            return TryInvokeWin32(window, node, out message);
        }

        if (!TryResolveUiaElement(window, node, out var element, out message))
        {
            return false;
        }

        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
            {
                ((InvokePattern)invoke).Invoke();
                message = $"{DescribeNode(node)} | route=UIA InvokePattern | result=Success";
                return true;
            }

            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection))
            {
                ((SelectionItemPattern)selection).Select();
                message = $"{DescribeNode(node)} | route=UIA SelectionItemPattern.Select | result=Success";
                return true;
            }

            if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandCollapse))
            {
                var pattern = (ExpandCollapsePattern)expandCollapse;
                if (pattern.Current.ExpandCollapseState is ExpandCollapseState.Collapsed or ExpandCollapseState.PartiallyExpanded)
                {
                    pattern.Expand();
                    message = $"{DescribeNode(node)} | route=UIA ExpandCollapsePattern.Expand | result=Success";
                }
                else
                {
                    pattern.Collapse();
                    message = $"{DescribeNode(node)} | route=UIA ExpandCollapsePattern.Collapse | result=Success";
                }

                return true;
            }

            message = $"{DescribeNode(node)} | route=UIA Patterns | result=Unavailable | reason=No supported invoke/select/expand pattern was exposed.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"{DescribeNode(node)} | route=UIA Patterns | result=Failed | reason={ex.Message}";
            return false;
        }
    }

    public bool TrySetText(DesktopWindowInfo window, WindowsAutomationNode node, string text, out string message)
    {
        if (node.BackendKind == WindowsAutomationBackendKind.Msaa)
        {
            message = "MSAA set text is not implemented yet for this element.";
            return false;
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Win32)
        {
            return TrySetTextWin32(window, node, text, out message);
        }

        if (!TryResolveUiaElement(window, node, out var element, out message))
        {
            return false;
        }

        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
            {
                ((ValuePattern)valuePattern).SetValue(text);
                message = $"{DescribeNode(node)} | route=UIA ValuePattern.SetValue | result=Success | chars={text.Length}";
                return true;
            }

            message = $"{DescribeNode(node)} | route=UIA ValuePattern.SetValue | result=Unavailable | reason=No writable ValuePattern was exposed.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"{DescribeNode(node)} | route=UIA ValuePattern.SetValue | result=Failed | reason={ex.Message}";
            return false;
        }
    }

    public string GetText(DesktopWindowInfo window, WindowsAutomationNode node)
    {
        if (node.BackendKind == WindowsAutomationBackendKind.Msaa)
        {
            return _msaaScanner.GetText(window, node);
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Win32)
        {
            return GetTextWin32(window, node);
        }

        if (!TryResolveUiaElement(window, node, out var element, out var message))
        {
            return message;
        }

        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
            {
                var value = ((ValuePattern)valuePattern).Current.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern))
            {
                var value = ((TextPattern)textPattern).DocumentRange.GetText(-1)?.TrimEnd('\r', '\n', '\0');
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            if (!string.IsNullOrWhiteSpace(element.Current.Name))
            {
                return element.Current.Name;
            }

            return $"{DescribeNode(node)} | route=UIA GetText | result=Unavailable | reason=No UIA text or value was exposed.";
        }
        catch (Exception ex)
        {
            return $"{DescribeNode(node)} | route=UIA GetText | result=Failed | reason={ex.Message}";
        }
    }

    private static bool TryFocusWin32(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        var hwnd = ResolveNativeHandle(window, node);
        if (hwnd == IntPtr.Zero)
        {
            message = $"{DescribeNode(node)} | route=Win32 Focus | result=Unavailable | reason=No native handle was available.";
            return false;
        }

        try
        {
            User32DesktopNative.SetForegroundWindow(hwnd);
            User32DesktopNative.SetFocus(hwnd);
            message = $"{DescribeNode(node)} | route=Win32 SetForegroundWindow/SetFocus | result=Success | hwnd=0x{hwnd.ToInt64():X}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"{DescribeNode(node)} | route=Win32 SetForegroundWindow/SetFocus | result=Failed | reason={ex.Message}";
            return false;
        }
    }

    private static bool TryInvokeWin32(DesktopWindowInfo window, WindowsAutomationNode node, out string message)
    {
        var hwnd = ResolveNativeHandle(window, node);
        if (hwnd == IntPtr.Zero)
        {
            message = $"{DescribeNode(node)} | route=Win32 BM_CLICK | result=Unavailable | reason=No native handle was available.";
            return false;
        }

        try
        {
            User32DesktopNative.SetForegroundWindow(window.Hwnd);
            User32DesktopNative.SendMessage(hwnd, User32DesktopNative.BmClick, IntPtr.Zero, IntPtr.Zero);
            message = $"{DescribeNode(node)} | route=Win32 BM_CLICK | result=Success | hwnd=0x{hwnd.ToInt64():X}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"{DescribeNode(node)} | route=Win32 BM_CLICK | result=Failed | reason={ex.Message}";
            return false;
        }
    }

    private static bool TryInvokeVirtualWin32Selection(WindowsAutomationNode node, out string message)
    {
        message = string.Empty;
        if (!node.Metadata.TryGetValue("isVirtual", out var isVirtual) || !bool.TryParse(isVirtual, out var virtualFlag) || !virtualFlag)
        {
            return false;
        }

        if (!node.Metadata.TryGetValue("virtualIndex", out var rawIndex) || !int.TryParse(rawIndex, out var index))
        {
            message = $"{DescribeNode(node)} | route=Win32 Virtual Select | result=Unavailable | reason=Virtual item index was missing.";
            return true;
        }

        var hwnd = node.Parent?.NativeHandle ?? node.NativeHandle;
        if (hwnd == IntPtr.Zero)
        {
            message = $"{DescribeNode(node)} | route=Win32 Virtual Select | result=Unavailable | reason=No parent handle was available.";
            return true;
        }

        var family = node.Metadata.TryGetValue("controlFamily", out var controlFamily)
            ? controlFamily
            : Win32ControlClassCatalog.GetFamily(node.ClassName);

        switch (family)
        {
            case "combobox":
                User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbSetCurSel, (IntPtr)index, IntPtr.Zero);
                message = $"{DescribeNode(node)} | route=Win32 Virtual Combo Select | result=Success | index={index}";
                return true;
            case "listbox":
                User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbSetCurSel, (IntPtr)index, IntPtr.Zero);
                message = $"{DescribeNode(node)} | route=Win32 Virtual List Select | result=Success | index={index}";
                return true;
            case "tab":
                User32DesktopNative.SendMessage(hwnd, User32DesktopNative.TcmSetCurSel, (IntPtr)index, IntPtr.Zero);
                message = $"{DescribeNode(node)} | route=Win32 Virtual Tab Select | result=Success | index={index}";
                return true;
            default:
                message = $"{DescribeNode(node)} | route=Win32 Virtual Select | result=Unavailable | reason=Unsupported parent control family '{family}'.";
                return true;
        }
    }

    private static bool TrySetTextWin32(DesktopWindowInfo window, WindowsAutomationNode node, string text, out string message)
    {
        var hwnd = ResolveNativeHandle(window, node);
        if (hwnd == IntPtr.Zero)
        {
            message = $"{DescribeNode(node)} | route=Win32 WM_SETTEXT | result=Unavailable | reason=No native handle was available.";
            return false;
        }

        try
        {
            var safeText = text ?? string.Empty;
            if (TrySelectWin32(hwnd, node.ClassName, safeText, out var selectRoute, out var selectDetail))
            {
                message = $"{DescribeNode(node)} | route={selectRoute} | result=Success{selectDetail}";
                return true;
            }

            User32DesktopNative.SetForegroundWindow(window.Hwnd);
            User32DesktopNative.SendMessage(hwnd, User32DesktopNative.WmSetText, IntPtr.Zero, safeText);
            message = $"{DescribeNode(node)} | route=Win32 WM_SETTEXT | result=Success | chars={safeText.Length} | hwnd=0x{hwnd.ToInt64():X}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"{DescribeNode(node)} | route=Win32 WM_SETTEXT | result=Failed | reason={ex.Message}";
            return false;
        }
    }

    private static bool TrySelectWin32(IntPtr hwnd, string className, string requestedValue, out string route, out string detail)
    {
        route = string.Empty;
        detail = string.Empty;
        var family = Win32ControlClassCatalog.GetFamily(className);
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return false;
        }

        switch (family)
        {
            case "combobox":
                if (int.TryParse(requestedValue, out var comboIndex))
                {
                    var result = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbSetCurSel, (IntPtr)comboIndex, IntPtr.Zero);
                    if (result >= 0)
                    {
                        route = "Win32 CB_SETCURSEL";
                        detail = $" | index={comboIndex}";
                        return true;
                    }
                }

                {
                    var result = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbSelectString, (IntPtr)(-1), requestedValue);
                    if (result >= 0)
                    {
                        route = "Win32 CB_SELECTSTRING";
                        detail = $" | text={requestedValue}";
                        return true;
                    }
                }

                return false;

            case "listbox":
                if (int.TryParse(requestedValue, out var listIndex))
                {
                    var result = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbSetCurSel, (IntPtr)listIndex, IntPtr.Zero);
                    if (result >= 0)
                    {
                        route = "Win32 LB_SETCURSEL";
                        detail = $" | index={listIndex}";
                        return true;
                    }
                }

                {
                    var result = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbSelectString, (IntPtr)(-1), requestedValue);
                    if (result >= 0)
                    {
                        route = "Win32 LB_SELECTSTRING";
                        detail = $" | text={requestedValue}";
                        return true;
                    }
                }

                return false;

            case "tab":
                if (!int.TryParse(requestedValue, out var tabIndex))
                {
                    return false;
                }

                {
                    var result = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.TcmSetCurSel, (IntPtr)tabIndex, IntPtr.Zero);
                    if (result >= 0)
                    {
                        route = "Win32 TCM_SETCURSEL";
                        detail = $" | index={tabIndex}";
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    private static string GetTextWin32(DesktopWindowInfo window, WindowsAutomationNode node)
    {
        var hwnd = ResolveNativeHandle(window, node);
        if (hwnd == IntPtr.Zero)
        {
            return $"{DescribeNode(node)} | route=Win32 WM_GETTEXT | result=Unavailable | reason=No native handle was available.";
        }

        try
        {
            if (TryReadClassSpecificWin32Text(hwnd, node.ClassName, out var extractedText, out var extractedRoute))
            {
                return string.IsNullOrWhiteSpace(extractedText)
                    ? $"{DescribeNode(node)} | route={extractedRoute} | result=Unavailable | reason=No class-specific text was exposed."
                    : extractedText;
            }

            var length = Math.Max((int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.WmGetTextLength, IntPtr.Zero, IntPtr.Zero), 0);
            var builder = new StringBuilder(Math.Max(length + 1, 256));
            User32DesktopNative.SendMessage(hwnd, User32DesktopNative.WmGetText, (IntPtr)builder.Capacity, builder);
            var value = builder.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (!string.IsNullOrWhiteSpace(node.Name))
            {
                return node.Name;
            }

            if (!string.IsNullOrWhiteSpace(node.Value))
            {
                return node.Value;
            }

            return $"{DescribeNode(node)} | route=Win32 WM_GETTEXT | result=Unavailable | reason=No Win32 text or value was exposed.";
        }
        catch (Exception ex)
        {
            return $"{DescribeNode(node)} | route=Win32 WM_GETTEXT | result=Failed | reason={ex.Message}";
        }
    }

    private static bool TryReadClassSpecificWin32Text(IntPtr hwnd, string className, out string value, out string route)
    {
        value = string.Empty;
        route = string.Empty;
        var family = Win32ControlClassCatalog.GetFamily(className);

        switch (family)
        {
            case "combobox":
                {
                    var index = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbGetCurSel, IntPtr.Zero, IntPtr.Zero);
                    if (index < 0)
                    {
                        route = "Win32 CB_GETCURSEL";
                        return true;
                    }

                    var length = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbGetLbTextLen, (IntPtr)index, IntPtr.Zero);
                    if (length < 0)
                    {
                        route = "Win32 CB_GETLBTEXTLEN";
                        return true;
                    }

                    var builder = new StringBuilder(Math.Max(length + 1, 256));
                    User32DesktopNative.SendMessage(hwnd, User32DesktopNative.CbGetLbText, (IntPtr)index, builder);
                    value = builder.ToString();
                    route = "Win32 CB_GETCURSEL/CB_GETLBTEXT";
                    return true;
                }
            case "listbox":
                {
                    var index = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbGetCurSel, IntPtr.Zero, IntPtr.Zero);
                    if (index < 0)
                    {
                        route = "Win32 LB_GETCURSEL";
                        return true;
                    }

                    var length = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbGetTextLen, (IntPtr)index, IntPtr.Zero);
                    if (length < 0)
                    {
                        route = "Win32 LB_GETTEXTLEN";
                        return true;
                    }

                    var builder = new StringBuilder(Math.Max(length + 1, 256));
                    User32DesktopNative.SendMessage(hwnd, User32DesktopNative.LbGetText, (IntPtr)index, builder);
                    value = builder.ToString();
                    route = "Win32 LB_GETCURSEL/LB_GETTEXT";
                    return true;
                }
            case "tab":
                {
                    var index = (int)User32DesktopNative.SendMessage(hwnd, User32DesktopNative.TcmGetCurSel, IntPtr.Zero, IntPtr.Zero);
                    value = index >= 0 ? $"Selected tab index: {index}" : string.Empty;
                    route = "Win32 TCM_GETCURSEL";
                    return true;
                }
            default:
                return false;
        }
    }

    private bool TryResolveUiaElement(DesktopWindowInfo window, WindowsAutomationNode node, out AutomationElement element, out string message)
    {
        try
        {
            element = AutomationElement.FromHandle(window.Hwnd);
            if (element is null)
            {
                message = "UIA could not resolve the selected window handle.";
                return false;
            }

            var walker = ResolveWalker(node);
            var path = BuildPath(node);
            foreach (var index in path)
            {
                var currentChild = walker.GetFirstChild(element);
                var currentIndex = 0;
                AutomationElement? matchedChild = null;
                while (currentChild is not null)
                {
                    if (currentIndex == index)
                    {
                        matchedChild = currentChild;
                        break;
                    }

                    currentChild = walker.GetNextSibling(currentChild);
                    currentIndex++;
                }

                if (matchedChild is null)
                {
                message = $"UIA path resolution failed at child index {index}.";
                return false;
                }

                element = matchedChild;
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

    private static TreeWalker ResolveWalker(WindowsAutomationNode node)
    {
        if (!node.Metadata.TryGetValue("uia.viewMode", out var viewMode))
        {
            return TreeWalker.RawViewWalker;
        }

        return Enum.TryParse<UiaTreeViewMode>(viewMode, true, out var parsed)
            ? parsed switch
            {
                UiaTreeViewMode.Control => TreeWalker.ControlViewWalker,
                UiaTreeViewMode.Content => TreeWalker.ContentViewWalker,
                _ => TreeWalker.RawViewWalker
            }
            : TreeWalker.RawViewWalker;
    }

    private static IntPtr ResolveNativeHandle(DesktopWindowInfo window, WindowsAutomationNode node) =>
        node.NativeHandle != IntPtr.Zero ? node.NativeHandle : window.Hwnd;

    private static string DescribeNode(WindowsAutomationNode node)
    {
        var name = string.IsNullOrWhiteSpace(node.Name) ? "(no name)" : node.Name;
        var role = string.IsNullOrWhiteSpace(node.Role) ? "unknown" : node.Role;
        var view = node.Metadata.TryGetValue("uia.viewMode", out var rawView) && !string.IsNullOrWhiteSpace(rawView)
            ? rawView
            : node.BackendKind == WindowsAutomationBackendKind.Uia ? "Raw" : "";
        var viewPart = string.IsNullOrWhiteSpace(view) ? "" : $" | view={view}";
        return $"backend={node.BackendKind}{viewPart} | role={role} | name={name}";
    }

    private static IReadOnlyList<int> BuildPath(WindowsAutomationNode node)
    {
        var indices = new Stack<int>();
        for (var cursor = node; cursor is not null; cursor = cursor.Parent)
        {
            if (cursor.Parent is null)
            {
                continue;
            }

            indices.Push(Math.Max(cursor.IndexInParent, 0));
        }

        return indices.ToArray();
    }
}
