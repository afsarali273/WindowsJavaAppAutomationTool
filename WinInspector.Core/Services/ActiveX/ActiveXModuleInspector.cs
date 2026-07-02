using System.Diagnostics;
using System.IO;

namespace WinInspector.Core.Services.ActiveX;

public sealed class ActiveXModuleInspector
{
    public ActiveXModuleInspection Inspect(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var moduleNames = process.Modules
                .Cast<ProcessModule>()
                .Select(module =>
                {
                    try { return Path.GetFileName(module.FileName); }
                    catch { return module.ModuleName; }
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var matches = ActiveXModuleCatalog.MatchModules(moduleNames);
            var known = matches
                .Select(match => $"{match.DisplayName} ({match.FileName})")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new ActiveXModuleInspection
            {
                IsAvailable = true,
                HasLegacyModules = known.Length > 0,
                HasOcxModules = matches.Any(match => match.IsOcx),
                KnownLegacyModules = known,
                Summary = known.Length == 0 ? "" : string.Join(" | ", known)
            };
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or NotSupportedException)
        {
            return new ActiveXModuleInspection
            {
                IsAvailable = false,
                HasLegacyModules = false,
                HasOcxModules = false,
                KnownLegacyModules = [],
                Summary = $"Module inspection unavailable: {ex.Message}"
            };
        }
    }
}

public sealed class ActiveXModuleInspection
{
    public bool IsAvailable { get; init; }
    public bool HasLegacyModules { get; init; }
    public bool HasOcxModules { get; init; }
    public IReadOnlyList<string> KnownLegacyModules { get; init; } = [];
    public string Summary { get; init; } = "";
}
