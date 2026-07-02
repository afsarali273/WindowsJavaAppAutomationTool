using System.Runtime.InteropServices;
using System.Drawing;
using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal sealed class ListViewExtractor : ControlMessageExtractorBase
{
    private const int MaxTextChars = 260;
    private const uint LvifText = 0x0001;
    private const uint TimeoutMs = 200;

    public override string ControlFamily => "listview";

    public override bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        var count = (int)User32DesktopNative.SendMessage(parent.NativeHandle, User32DesktopNative.LvmGetItemCount, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0)
        {
            return false;
        }

        try
        {
            using var session = new ControlMessageRemoteSession(parent.ProcessId);
            var itemSize = Marshal.SizeOf<ListViewItemText>();
            var remoteText = session.Allocate(MaxTextChars * 2);
            var remoteItem = session.Allocate(itemSize);

            for (var index = 0; index < Math.Min(count, maxChildren); index++)
            {
                var item = new ListViewItemText
                {
                    mask = LvifText,
                    iItem = index,
                    iSubItem = 0,
                    pszText = remoteText,
                    cchTextMax = MaxTextChars
                };

                session.WriteStruct(remoteItem, item);
                var ok = session.TrySendMessage(
                    parent.NativeHandle,
                    User32DesktopNative.LvmGetItemTextW,
                    (IntPtr)index,
                    remoteItem,
                    TimeoutMs,
                    out _);

                var name = ok ? session.ReadUnicodeString(remoteText, MaxTextChars) : string.Empty;
                var node = CreateVirtualSelectionNode(parent, "list view item", index, name, count);
                node.Metadata["virtualExtractor"] = "ListViewExtractor";
                if (TryReadItemBounds(parent.NativeHandle, session, index, out var bounds))
                {
                    node.Bounds = bounds;
                    node.ClientBounds = bounds;
                    node.Metadata["virtualBounds"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
                }
                parent.Children.Add(node);
            }

            return parent.Children.Count > 0;
        }
        catch
        {
            for (var index = 0; index < Math.Min(count, maxChildren); index++)
            {
                var node = CreateVirtualSelectionNode(parent, "list view item", index, $"Item {index + 1}", count);
                node.Metadata["virtualExtractor"] = "ListViewExtractor";
                node.Metadata["virtualTextFallback"] = bool.TrueString;
                parent.Children.Add(node);
            }

            return true;
        }
    }

    private static bool TryReadItemBounds(IntPtr hwnd, ControlMessageRemoteSession session, int index, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        try
        {
            var remoteRect = session.Allocate(Marshal.SizeOf<User32DesktopNative.NativeRect>());
            session.WriteStruct(remoteRect, new User32DesktopNative.NativeRect
            {
                Left = User32DesktopNative.LvirBounds
            });

            var ok = session.TrySendMessage(
                hwnd,
                User32DesktopNative.LvmGetItemRect,
                (IntPtr)index,
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ListViewItemText
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
        public int iIndent;
        public int iGroupId;
        public uint cColumns;
        public IntPtr puColumns;
        public IntPtr piColFmt;
        public int iGroup;
    }
}
