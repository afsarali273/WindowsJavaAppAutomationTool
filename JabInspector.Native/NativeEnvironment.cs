namespace JabInspector.Native;

public static class NativeEnvironment
{
    public static string? FindAccessBridgeDll()
    {
        return GetAccessBridgeDllCandidates().FirstOrDefault(File.Exists);
    }

    public static IReadOnlyList<string> GetAccessBridgeDllCandidates()
    {
        var candidates = new List<string>();
        AddExplicitBridgeOverride(candidates);
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome)) candidates.Add(Path.Combine(javaHome, "bin", AccessBridgeNative.DllName));
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(folder.Trim(), AccessBridgeNative.DllName));
        var localPrograms = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var root in new[] { @"C:\Program Files\Java", @"C:\Program Files\Eclipse Adoptium", @"C:\Program Files\Microsoft", Path.Combine(localPrograms, "Programs") })
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                candidates.Add(Path.Combine(root, "bin", AccessBridgeNative.DllName));
                foreach (var installation in Directory.EnumerateDirectories(root))
                    candidates.Add(Path.Combine(installation, "bin", AccessBridgeNative.DllName));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        candidates.AddRange(FindBridgesForRunningJavaProcesses());
        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddExplicitBridgeOverride(List<string> candidates)
    {
        foreach (var variable in new[] { "JAB_BRIDGE_DLL", "WINDOWS_ACCESS_BRIDGE_DLL" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value)) continue;
            candidates.Add(value.Trim());
        }
    }

    private static IEnumerable<string> FindBridgesForRunningJavaProcesses()
    {
        var matches = new List<string>();
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                var className = new System.Text.StringBuilder(128);
                User32Native.GetClassName(process.MainWindowHandle, className, className.Capacity);
                if (!className.ToString().StartsWith("SunAwt", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (System.Diagnostics.ProcessModule module in process.Modules)
                {
                    if (!string.Equals(module.ModuleName, "javaaccessbridge.dll", StringComparison.OrdinalIgnoreCase)) continue;
                    var runtimeBin = Path.GetDirectoryName(module.FileName);
                    if (runtimeBin is not null) matches.Add(Path.Combine(runtimeBin, AccessBridgeNative.DllName));
                }
            }
            catch { /* Elevated and protected processes may not expose modules. */ }
            finally { process.Dispose(); }
        }
        return matches;
    }

    public static IReadOnlyList<string> GetDiagnostics()
    {
        var diagnostics = new List<string>
        {
            $"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
            $"64-bit process: {Environment.Is64BitProcess}",
            $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
            $"Expected bridge: {AccessBridgeNative.DllName}",
            $"JAB_BRIDGE_DLL: {Environment.GetEnvironmentVariable("JAB_BRIDGE_DLL") ?? "Not set"}",
            $"WINDOWS_ACCESS_BRIDGE_DLL: {Environment.GetEnvironmentVariable("WINDOWS_ACCESS_BRIDGE_DLL") ?? "Not set"}",
            $"JAVA_HOME: {Environment.GetEnvironmentVariable("JAVA_HOME") ?? "Not set"}",
            $"Bridge location: {FindAccessBridgeDll() ?? "Not found"}"
        };

        var existingCandidates = GetAccessBridgeDllCandidates().Where(File.Exists).Take(8).ToList();
        diagnostics.Add(existingCandidates.Count == 0
            ? "Existing bridge candidates: none"
            : $"Existing bridge candidates: {string.Join(" | ", existingCandidates)}");
        return diagnostics;
    }
}
