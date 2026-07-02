namespace WinInspector.Core.Services.ActiveX;

internal static class ActiveXModuleCatalog
{
    private static readonly LegacyModuleDefinition[] Definitions =
    [
        new("msflxgrd.ocx", "MSFlexGrid", true),
        new("mshflxgd.ocx", "MSHFlexGrid", true),
        new("mscomctl.ocx", "Microsoft Common Controls", true),
        new("tabctl32.ocx", "TabStrip/Toolbar", true),
        new("richtx32.ocx", "RichTextBox", true),
        new("comdlg32.ocx", "Common Dialog", true),
        new("threed32.ocx", "Sheridan 3D Controls", true),
        new("ssdw3b32.ocx", "Sheridan Data Widgets", true),
        new("vsflex8l.ocx", "ComponentOne VSFlexGrid", true),
        new("vsflex7l.ocx", "ComponentOne VSFlexGrid", true),
        new("c1sizer.ocx", "ComponentOne Sizer", true),
        new("c1trueDBGrid.ocx", "ComponentOne True DBGrid", true),
        new("igmedit.ocx", "Infragistics Edit", true),
        new("igtblx.ocx", "Infragistics Grid", true),
        new("fpSpread*.ocx", "FarPoint Spread", true)
    ];

    public static IReadOnlyList<LegacyModuleMatch> MatchModules(IEnumerable<string> moduleFileNames)
    {
        var matches = new List<LegacyModuleMatch>();
        foreach (var fileName in moduleFileNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var definition in Definitions)
            {
                if (!IsMatch(fileName, definition.Pattern))
                {
                    continue;
                }

                matches.Add(new LegacyModuleMatch(fileName, definition.DisplayName, definition.IsOcx));
                break;
            }
        }

        return matches;
    }

    private static bool IsMatch(string fileName, string pattern)
    {
        if (pattern.Contains('*'))
        {
            var prefix = pattern[..pattern.IndexOf('*')];
            var suffix = pattern[(pattern.IndexOf('*') + 1)..];
            return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                   && fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }

    internal sealed record LegacyModuleDefinition(string Pattern, string DisplayName, bool IsOcx);
    internal sealed record LegacyModuleMatch(string FileName, string DisplayName, bool IsOcx);
}
