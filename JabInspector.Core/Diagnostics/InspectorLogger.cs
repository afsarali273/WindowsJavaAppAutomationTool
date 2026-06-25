using System.Text;
using System.Runtime.InteropServices;

namespace JabInspector.Core.Diagnostics;

public sealed class InspectorLogger
{
    private static readonly object FileLock = new();
    private static readonly object ConsoleLock = new();
    private static bool _consoleAttachAttempted;
    private readonly string _logFilePath;

    public InspectorLogger()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JabInspector");
        Directory.CreateDirectory(directory);
        _logFilePath = Path.Combine(directory, "jaccessinspector.log");
        WriteLine($"---- Logger started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ----");
    }

    public event Action<string>? MessageLogged;

    public string LogFilePath => _logFilePath;
    public bool VerboseEnabled { get; set; }

    public void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        WriteLine(line);
        MessageLogged?.Invoke(line);
    }

    public void Debug(string message)
    {
        if (!VerboseEnabled) return;
        Log($"[DEBUG] {message}");
    }

    private void WriteLine(string line)
    {
        lock (FileLock)
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
        }

        WriteConsoleLine(line);
    }

    private static void WriteConsoleLine(string line)
    {
        lock (ConsoleLock)
        {
            TryAttachParentConsole();
            try
            {
                Console.WriteLine(line);
                Console.Out.Flush();
            }
            catch
            {
                // Console output is best-effort because the app can run as a WinExe without a parent console.
            }
        }
    }

    private static void TryAttachParentConsole()
    {
        if (_consoleAttachAttempted) return;
        _consoleAttachAttempted = true;
        try { AttachConsole(AttachParentProcess); }
        catch { }
    }

    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);
}
