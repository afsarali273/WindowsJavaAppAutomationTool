using System.Runtime.InteropServices;

namespace JabInspector.Native;

public static class AccessBridgePump
{
    private static readonly object Sync = new();
    private static ManualResetEventSlim? _started;
    private static Exception? _startupException;
    private static bool _running;

    public static void EnsureStarted(Action<string>? log = null)
    {
        lock (Sync)
        {
            if (_running) return;

            _startupException = null;
            _started = new ManualResetEventSlim(false);
            var thread = new Thread(() => RunMessagePump(log))
            {
                IsBackground = true,
                Name = "Java Access Bridge message pump"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        if (!_started!.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Java Access Bridge message pump did not start within 5 seconds.");

        if (_startupException is not null)
            throw new InvalidOperationException("Java Access Bridge message pump failed to start.", _startupException);
    }

    private static void RunMessagePump(Action<string>? log)
    {
        try
        {
            AccessBridgeNative.WindowsRun();
            _running = true;
            log?.Invoke("Access Bridge Windows_run initialized on dedicated STA message-pump thread.");
            _started?.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        catch (Exception ex)
        {
            _startupException = ex;
            _started?.Set();
            log?.Invoke($"Access Bridge message pump failed: {ex.Message}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out NativeMessage message, IntPtr hwnd, uint messageFilterMin, uint messageFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);
}
