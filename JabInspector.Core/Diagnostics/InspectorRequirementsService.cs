using System.Diagnostics;
using Microsoft.Win32;
using JabInspector.Native;

namespace JabInspector.Core.Diagnostics;

public static class InspectorRequirementsService
{
    private const string AccessibilityRegistrationKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Accessibility\ATs\Oracle_JavaAccessBridge";

    public static InspectorRequirementsReport Generate()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME") ?? "";
        var bridgePath = NativeEnvironment.FindAccessBridgeDll() ?? "";
        var jabSwitchPath = FindJabSwitch() ?? "";
        var registrationExists = Registry.LocalMachine.OpenSubKey(AccessibilityRegistrationKey) is not null;
        var checks = new List<RequirementCheck>
        {
            new()
            {
                Title = "Inspector architecture",
                Status = Environment.Is64BitProcess ? "Ready" : "Unsupported",
                Details = Environment.Is64BitProcess ? "The inspector is running as a 64-bit process." : "The inspector should run as x64 to match modern Java Access Bridge runtimes.",
                IsOk = Environment.Is64BitProcess
            },
            new()
            {
                Title = "Windows Access Bridge DLL",
                Status = string.IsNullOrWhiteSpace(bridgePath) ? "Missing" : "Found",
                Details = string.IsNullOrWhiteSpace(bridgePath) ? "WindowsAccessBridge-64.dll was not discovered in running Java runtimes, JAVA_HOME, or common install folders." : bridgePath,
                IsOk = !string.IsNullOrWhiteSpace(bridgePath)
            },
            new()
            {
                Title = "jabswitch command",
                Status = string.IsNullOrWhiteSpace(jabSwitchPath) ? "Missing" : "Available",
                Details = string.IsNullOrWhiteSpace(jabSwitchPath) ? "jabswitch.exe was not found. Without it, enable/disable actions must be handled outside the inspector." : jabSwitchPath,
                IsOk = !string.IsNullOrWhiteSpace(jabSwitchPath)
            },
            new()
            {
                Title = "Windows AT registration",
                Status = registrationExists ? "Registered" : "Missing",
                Details = registrationExists ? AccessibilityRegistrationKey : "Windows does not currently show the Oracle Java Access Bridge accessibility registration key.",
                IsOk = registrationExists
            },
            new()
            {
                Title = "JAVA_HOME",
                Status = string.IsNullOrWhiteSpace(javaHome) ? "Optional" : "Configured",
                Details = string.IsNullOrWhiteSpace(javaHome) ? "JAVA_HOME is not set. The inspector can still work if it finds the bridge from a running Java process or installed JDK." : javaHome,
                IsOk = !string.IsNullOrWhiteSpace(javaHome)
            }
        };

        var readyCount = checks.Count(x => x.IsOk);
        var summary = readyCount == checks.Count
            ? "All primary Java Access Bridge requirements look healthy."
            : $"{readyCount} of {checks.Count} primary checks passed. Review the items below for setup gaps.";

        return new InspectorRequirementsReport
        {
            Summary = summary,
            JavaHome = string.IsNullOrWhiteSpace(javaHome) ? "(not set)" : javaHome,
            BridgeDllPath = string.IsNullOrWhiteSpace(bridgePath) ? "(not found)" : bridgePath,
            JabSwitchPath = string.IsNullOrWhiteSpace(jabSwitchPath) ? "(not found)" : jabSwitchPath,
            AccessibilityRegistrationPath = registrationExists ? AccessibilityRegistrationKey : "(not found)",
            Checks = checks
        };
    }

    public static string? FindJabSwitch()
    {
        var candidates = new List<string>();
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome)) candidates.Add(Path.Combine(javaHome, "bin", "jabswitch.exe"));

        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(folder.Trim(), "jabswitch.exe"));

        TryAddRegistrationPath(candidates);

        foreach (var root in new[]
                 {
                     @"C:\Program Files\Java",
                     @"C:\Program Files\Eclipse Adoptium",
                     @"C:\Program Files\OpenLogic",
                     @"C:\Program Files\JetBrains",
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JabRef")
                 })
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                candidates.Add(Path.Combine(root, "bin", "jabswitch.exe"));
                foreach (var installation in Directory.EnumerateDirectories(root))
                    candidates.Add(Path.Combine(installation, "bin", "jabswitch.exe"));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void TryAddRegistrationPath(List<string> candidates)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AccessibilityRegistrationKey);
            var path = key?.GetValue("StartExe") as string;
            if (!string.IsNullOrWhiteSpace(path)) candidates.Add(Environment.ExpandEnvironmentVariables(path));
        }
        catch
        {
            // Best-effort only.
        }
    }
}
