using System.Runtime.InteropServices;
using System.Drawing;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal sealed class TreeViewExtractor : ControlMessageExtractorBase
{
    private const int MaxTextChars = 260;
    private const uint TvifText = 0x0001;
    private const uint TimeoutMs = 200;

    public override string ControlFamily => "treeview";

    public override bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        var count = (int)User32DesktopNative.SendMessage(parent.NativeHandle, User32DesktopNative.TvmGetCount, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0)
        {
            return false;
        }

        try
        {
            using var session = new ControlMessageRemoteSession(parent.ProcessId);
            var itemSize = Marshal.SizeOf<TreeViewItemText>();
            var remoteText = session.Allocate(MaxTextChars * 2);
            var remoteItem = session.Allocate(itemSize);
            var root = GetNextItem(parent.NativeHandle, User32DesktopNative.TvgnRoot, IntPtr.Zero);
            if (root == IntPtr.Zero)
            {
                return false;
            }

            var queue = new Queue<(IntPtr Handle, int Depth)>();
            queue.Enqueue((root, 0));
            var index = 0;

            while (queue.Count > 0 && index < Math.Min(count, maxChildren))
            {
                var current = queue.Dequeue();
                var name = ReadTreeItemText(parent.NativeHandle, session, remoteItem, remoteText, current.Handle);
                var node = CreateVirtualSelectionNode(parent, "tree view item", index, name, count);
                node.Metadata["virtualExtractor"] = "TreeViewExtractor";
                node.Metadata["virtualDepth"] = current.Depth.ToString();
                node.Metadata["virtualTreeHandle"] = $"0x{current.Handle.ToInt64():X}";
                if (TryReadItemBounds(parent.NativeHandle, session, current.Handle, out var bounds))
                {
                    node.Bounds = bounds;
                    node.ClientBounds = bounds;
                    node.Metadata["virtualBounds"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
                }
                parent.Children.Add(node);
                index++;

                var child = GetNextItem(parent.NativeHandle, User32DesktopNative.TvgnChild, current.Handle);
                if (child != IntPtr.Zero)
                {
                    queue.Enqueue((child, current.Depth + 1));
                }

                var sibling = GetNextItem(parent.NativeHandle, User32DesktopNative.TvgnNext, current.Handle);
                if (sibling != IntPtr.Zero)
                {
                    queue.Enqueue((sibling, current.Depth));
                }
            }

            return parent.Children.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadItemBounds(
        IntPtr hwnd,
        ControlMessageRemoteSession session,
        IntPtr itemHandle,
        out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        try
        {
            var remoteRect = session.Allocate(Marshal.SizeOf<User32DesktopNative.NativeRect>());
            var seed = new byte[Marshal.SizeOf<User32DesktopNative.NativeRect>()];
            var pointerBytes = BitConverter.GetBytes(itemHandle.ToInt64());
            Array.Copy(pointerBytes, seed, Math.Min(pointerBytes.Length, seed.Length));
            session.WriteBytes(remoteRect, seed);

            var ok = session.TrySendMessage(
                hwnd,
                User32DesktopNative.TvmGetItemRect,
                (IntPtr)1,
                remoteRect,
                TimeoutMs,
                out _);

            if (!ok)
            {
                return false;
            }

            var rect = session.ReadStruct<User32DesktopNative.NativeRect>(remoteRect);
            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
            {
                return false;
            }

            var topLeft = new User32DesktopNative.NativePoint(rect.Left, rect.Top);
            var bottomRight = new User32DesktopNative.NativePoint(rect.Right, rect.Bottom);
            if (!User32DesktopNative.ClientToScreen(hwnd, ref topLeft) || !User32DesktopNative.ClientToScreen(hwnd, ref bottomRight))
            {
                return false;
            }

            bounds = Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr GetNextItem(IntPtr hwnd, int relation, IntPtr itemHandle) =>
        User32DesktopNative.SendMessage(hwnd, User32DesktopNative.TvmGetNextItem, (IntPtr)relation, itemHandle);

    private static string ReadTreeItemText(
        IntPtr hwnd,
        ControlMessageRemoteSession session,
        IntPtr remoteItem,
        IntPtr remoteText,
        IntPtr itemHandle)
    {
        var item = new TreeViewItemText
        {
            mask = TvifText,
            hItem = itemHandle,
            pszText = remoteText,
            cchTextMax = MaxTextChars
        };

        session.WriteStruct(remoteItem, item);
        var ok = session.TrySendMessage(hwnd, User32DesktopNative.TvmGetItemW, IntPtr.Zero, remoteItem, TimeoutMs, out _);
        return ok ? session.ReadUnicodeString(remoteText, MaxTextChars) : string.Empty;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TreeViewItemText
    {
        public uint mask;
        public IntPtr hItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public int iSelectedImage;
        public int cChildren;
        public IntPtr lParam;
        public int iIntegral;
        public uint uStateEx;
        public IntPtr hwnd;
        public int iExpandedImage;
        public int iReserved;
    }
}
