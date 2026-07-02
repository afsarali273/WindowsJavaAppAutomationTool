using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal sealed class ControlMessageRemoteSession : IDisposable
{
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;
    private readonly List<IntPtr> _allocations = [];
    private readonly IntPtr _processHandle;

    public ControlMessageRemoteSession(uint processId)
    {
        _processHandle = OpenProcess(
            ProcessQueryInformation | ProcessVmOperation | ProcessVmRead | ProcessVmWrite,
            false,
            processId);

        if (_processHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open target process {processId}.");
        }
    }

    public IntPtr Allocate(int bytes)
    {
        var pointer = VirtualAllocEx(_processHandle, IntPtr.Zero, (nuint)bytes, MemCommit | MemReserve, PageReadWrite);
        if (pointer == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not allocate {bytes} remote bytes.");
        }

        _allocations.Add(pointer);
        return pointer;
    }

    public void WriteStruct<T>(IntPtr remoteAddress, T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, buffer, false);
            WriteBytes(remoteAddress, ToArray(buffer, size));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void WriteBytes(IntPtr remoteAddress, byte[] bytes)
    {
        if (!WriteProcessMemory(_processHandle, remoteAddress, bytes, bytes.Length, out var written) || written != bytes.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not write remote process memory.");
        }
    }

    public T ReadStruct<T>(IntPtr remoteAddress) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        if (!ReadProcessMemory(_processHandle, remoteAddress, bytes, size, out var read) || read < size)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not read remote struct {typeof(T).Name}.");
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(bytes, 0, buffer, size);
            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public string ReadUnicodeString(IntPtr remoteAddress, int maxChars)
    {
        if (maxChars <= 0)
        {
            return string.Empty;
        }

        var bytes = new byte[maxChars * 2];
        if (!ReadProcessMemory(_processHandle, remoteAddress, bytes, bytes.Length, out var read) || read <= 0)
        {
            return string.Empty;
        }

        var charCount = (int)Math.Min(read / 2, maxChars);
        var text = Encoding.Unicode.GetString(bytes, 0, charCount * 2);
        var terminator = text.IndexOf('\0');
        return terminator >= 0 ? text[..terminator] : text;
    }

    public bool TrySendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint timeoutMs, out IntPtr result)
    {
        var sendResult = User32DesktopNative.SendMessageTimeout(
            hwnd,
            message,
            wParam,
            lParam,
            User32DesktopNative.SmtoAbortIfHung,
            timeoutMs,
            out result);

        return sendResult != IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var allocation in _allocations)
        {
            if (allocation != IntPtr.Zero)
            {
                VirtualFreeEx(_processHandle, allocation, nuint.Zero, MemRelease);
            }
        }

        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
        }
    }

    private static byte[] ToArray(IntPtr source, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(source, bytes, 0, length);
        return bytes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr processHandle, IntPtr address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr processHandle, IntPtr address, nuint size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr processHandle, IntPtr baseAddress, [Out] byte[] buffer, int size, out int bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, int size, out int bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
