namespace JabInspector.Native;

public static class NativeEnvironment
{
    public static string? FindAccessBridgeDll()
    {
        var candidates = new List<string>();
        candidates.AddRange(FindBridgesForRunningJavaProcesses());
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
        return candidates.FirstOrDefault(File.Exists);
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

    public static IReadOnlyList<string> GetDiagnostics() => new[]
    {
        $"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
        $"64-bit process: {Environment.Is64BitProcess}",
        $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
        $"Expected bridge: {AccessBridgeNative.DllName}",
        $"JAVA_HOME: {Environment.GetEnvironmentVariable("JAVA_HOME") ?? "Not set"}",
        $"Bridge location: {FindAccessBridgeDll() ?? "Not found"}"
    };
}
