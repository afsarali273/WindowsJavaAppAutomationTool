namespace WinInspector.Core.Services;

internal static class Win32ControlClassCatalog
{
    public static string GetFamily(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return "unknown";
        }

        var value = className.Trim();

        if (EqualsAny(value, "Edit", "ThunderRT6TextBox") || value.StartsWith("WindowsForms10.EDIT", StringComparison.OrdinalIgnoreCase))
        {
            return "edit";
        }

        if (EqualsAny(value, "Button", "ThunderRT6CommandButton") || value.StartsWith("WindowsForms10.BUTTON", StringComparison.OrdinalIgnoreCase))
        {
            return "button";
        }

        if (EqualsAny(value, "ComboBox", "ThunderRT6ComboBox") || value.StartsWith("WindowsForms10.COMBOBOX", StringComparison.OrdinalIgnoreCase))
        {
            return "combobox";
        }

        if (EqualsAny(value, "ListBox", "ThunderRT6ListBox") || value.StartsWith("WindowsForms10.LISTBOX", StringComparison.OrdinalIgnoreCase))
        {
            return "listbox";
        }

        if (EqualsAny(value, "SysTabControl32") || value.StartsWith("WindowsForms10.SysTabControl32", StringComparison.OrdinalIgnoreCase))
        {
            return "tab";
        }

        if (EqualsAny(value, "SysListView32") || value.StartsWith("WindowsForms10.SysListView32", StringComparison.OrdinalIgnoreCase))
        {
            return "listview";
        }

        if (EqualsAny(value, "SysTreeView32") || value.StartsWith("WindowsForms10.SysTreeView32", StringComparison.OrdinalIgnoreCase))
        {
            return "treeview";
        }

        if (EqualsAny(
                value,
                "MSFlexGridWndClass",
                "MSHFlexGridWndClass",
                "VSFlexGrid",
                "TrueDBGrid80",
                "TrueOleDBGrid80",
                "TDBGrid",
                "vaSpread",
                "AfxOleControl42"))
        {
            return "grid";
        }

        if (EqualsAny(value, "TabDlg.SSTab", "TabCtlWndClass"))
        {
            return "tab";
        }

        return "generic";
    }

    public static bool IsVb6Class(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        return className.StartsWith("ThunderRT6", StringComparison.OrdinalIgnoreCase)
               || EqualsAny(
                   className,
                   "MSFlexGridWndClass",
                   "MSHFlexGridWndClass",
                   "TabDlg.SSTab",
                   "VSFlexGrid",
                   "TrueDBGrid80",
                   "TrueOleDBGrid80",
                   "TDBGrid");
    }

    public static bool IsLikelyCanvasLike(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        return EqualsAny(className,
                   "ThunderRT6PictureBoxDC",
                   "ThunderRT6UserControlDC",
                   "ThunderRT6Frame",
                   "MSFlexGridWndClass",
                   "MSHFlexGridWndClass",
                   "VSFlexGrid",
                   "TrueDBGrid80",
                   "TrueOleDBGrid80",
                   "TDBGrid",
                   "AfxWnd",
                   "Static",
                   "CustomControl",
                   "Panel")
               || className.Contains("PictureBox", StringComparison.OrdinalIgnoreCase)
               || className.Contains("UserControl", StringComparison.OrdinalIgnoreCase)
               || className.Contains("Canvas", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetTechnology(string className)
    {
        if (IsVb6Class(className))
        {
            return "VB6";
        }

        if (EqualsAny(
                className,
                "MSFlexGridWndClass",
                "MSHFlexGridWndClass",
                "TabDlg.SSTab",
                "VSFlexGrid",
                "TrueDBGrid80",
                "TrueOleDBGrid80",
                "TDBGrid"))
        {
            return "ActiveX/OCX";
        }

        return "Win32";
    }

    public static bool SupportsClick(string className)
    {
        var family = GetFamily(className);
        return family is "button" or "tab" or "combobox" or "generic";
    }

    public static bool SupportsSetText(string className)
    {
        var family = GetFamily(className);
        return family is "edit" or "combobox";
    }

    public static bool SupportsSelectionRead(string className)
    {
        var family = GetFamily(className);
        return family is "combobox" or "listbox" or "tab" or "listview" or "treeview" or "grid";
    }

    public static bool SupportsGetText(string className)
    {
        var family = GetFamily(className);
        return family is "edit" or "button" or "combobox" or "listbox" or "tab" or "listview" or "treeview" or "grid" or "generic";
    }

    private static bool EqualsAny(string value, params string[] matches) =>
        matches.Any(match => string.Equals(value, match, StringComparison.OrdinalIgnoreCase));
}
