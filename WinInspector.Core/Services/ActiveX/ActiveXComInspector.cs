using System.Reflection;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ActiveX;

public sealed class ActiveXComInspector
{
    private static readonly string[] CandidateProperties =
    [
        "Text",
        "Value",
        "Caption",
        "Rows",
        "Cols",
        "Row",
        "Col",
        "FixedRows",
        "FixedCols",
        "RowSel",
        "ColSel",
        "ListCount",
        "SelectedIndex",
        "Enabled",
        "Visible"
    ];

    public bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("WININSPECTOR_ENABLE_ACTIVEX_COM"), "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("WININSPECTOR_ENABLE_ACTIVEX_COM"), "true", StringComparison.OrdinalIgnoreCase);

    public ActiveXEvidence? TryInspect(WindowsAutomationNode node)
    {
        if (!IsEnabled || !ShouldInspect(node))
        {
            return null;
        }

        var comObject = TryResolveDispatch(node.NativeHandle)
                        ?? TryResolveDispatch(node.Parent?.NativeHandle ?? IntPtr.Zero);
        if (comObject is null)
        {
            return null;
        }

        try
        {
            var type = comObject.GetType();
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in CandidateProperties)
            {
                TryReadScalarMember(type, comObject, name, properties);
            }

            TryReadSelectionPreview(type, comObject, properties);
            TryReadGridPreview(type, comObject, properties);
            TryReadListPreview(type, comObject, properties);

            if (properties.Count == 0)
            {
                return null;
            }

            return new ActiveXEvidence
            {
                ProgId = "",
                TypeName = type.FullName ?? type.Name,
                Properties = properties
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool ShouldInspect(WindowsAutomationNode node)
    {
        var family = Win32ControlClassCatalog.GetFamily(node.ClassName);
        if (family == "grid" || Win32ControlClassCatalog.GetTechnology(node.ClassName) == "ActiveX/OCX")
        {
            return true;
        }

        return node.Metadata.TryGetValue("windowHasOcxModules", out var hasOcx)
               && bool.TryParse(hasOcx, out var enabled)
               && enabled;
    }

    private static object? TryResolveDispatch(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        return OleAccNative.TryDispatchObjectFromWindow(hwnd, OleAccNative.ObjidNativeOm, out var nativeOm) ? nativeOm
             : OleAccNative.TryDispatchObjectFromWindow(hwnd, OleAccNative.ObjidClient, out var client) ? client
             : OleAccNative.TryDispatchObjectFromWindow(hwnd, OleAccNative.ObjidWindow, out var window) ? window
             : null;
    }

    private static void TryReadScalarMember(Type type, object instance, string name, IDictionary<string, string> properties)
    {
        try
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is not null && property.GetIndexParameters().Length == 0 && property.CanRead)
            {
                var propertyValue = property.GetValue(instance);
                if (TryConvertValue(propertyValue, out var propertyText))
                {
                    properties[property.Name] = propertyText;
                    return;
                }
            }

            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase, null, Type.EmptyTypes, null);
            if (method is null)
            {
                return;
            }

            var methodValue = method.Invoke(instance, null);
            if (TryConvertValue(methodValue, out var methodText))
            {
                properties[method.Name] = methodText;
            }
        }
        catch
        {
        }
    }

    private static void TryReadSelectionPreview(Type type, object instance, IDictionary<string, string> properties)
    {
        try
        {
            var row = TryGetInt(properties, "Row");
            var col = TryGetInt(properties, "Col");
            if (row is null || col is null)
            {
                return;
            }

            if (TryInvokeIndexedMember(type, instance, ["TextMatrix", "get_TextMatrix"], [row.Value, col.Value], out var value))
            {
                var text = SanitizeCell(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    properties["CurrentCell"] = $"({row},{col}) {text}";
                }
            }
        }
        catch
        {
        }
    }

    private static void TryReadGridPreview(Type type, object instance, IDictionary<string, string> properties)
    {
        try
        {
            var rows = TryGetInt(properties, "Rows");
            var cols = TryGetInt(properties, "Cols");
            if (rows is null || cols is null || rows <= 0 || cols <= 0)
            {
                return;
            }

            var maxRows = Math.Min(rows.Value, 3);
            var maxCols = Math.Min(cols.Value, 3);
            var previewRows = new List<string>();
            for (var row = 0; row < maxRows; row++)
            {
                var cells = new List<string>();
                for (var col = 0; col < maxCols; col++)
                {
                    try
                    {
                        if (TryInvokeIndexedMember(type, instance, ["TextMatrix", "get_TextMatrix"], [row, col], out var value))
                        {
                            cells.Add(SanitizeCell(value));
                        }
                        else
                        {
                            cells.Add("");
                        }
                    }
                    catch
                    {
                        cells.Add("");
                    }
                }

                previewRows.Add($"r{row}: {string.Join(" | ", cells)}");
            }

            if (previewRows.Count > 0)
            {
                properties["GridDimensions"] = $"{rows} x {cols}";
                properties["GridPreview"] = string.Join(Environment.NewLine, previewRows);
            }
        }
        catch
        {
        }
    }

    private static void TryReadListPreview(Type type, object instance, IDictionary<string, string> properties)
    {
        try
        {
            var listCount = TryGetInt(properties, "ListCount");
            if (listCount is null || listCount <= 0)
            {
                return;
            }

            var maxItems = Math.Min(listCount.Value, 5);
            var items = new List<string>();
            for (var index = 0; index < maxItems; index++)
            {
                if (TryInvokeIndexedMember(type, instance, ["Item", "List"], [index], out var value)
                    && TryConvertValue(value, out var text))
                {
                    items.Add($"{index}: {SanitizeCell(text)}");
                }
            }

            if (items.Count > 0)
            {
                properties["ListPreview"] = string.Join(Environment.NewLine, items);
            }
        }
        catch
        {
        }
    }

    private static int? TryGetInt(IDictionary<string, string> properties, string name) =>
        properties.TryGetValue(name, out var text) && int.TryParse(text, out var value) ? value : null;

    private static bool TryInvokeIndexedMember(Type type, object instance, IEnumerable<string> memberNames, object[] args, out object? value)
    {
        foreach (var memberName in memberNames)
        {
            try
            {
                var method = type.GetMethod(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (method is not null && method.GetParameters().Length == args.Length)
                {
                    value = method.Invoke(instance, args);
                    return true;
                }

                var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property is not null && property.GetIndexParameters().Length == args.Length && property.CanRead)
                {
                    value = property.GetValue(instance, args);
                    return true;
                }
            }
            catch
            {
            }
        }

        value = null;
        return false;
    }

    private static bool TryConvertValue(object? value, out string text)
    {
        text = "";
        if (value is null)
        {
            return false;
        }

        text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        text = text.Trim();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static string SanitizeCell(object? value)
    {
        var text = value is string str ? str : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > 40 ? text[..40] : text;
    }
}
