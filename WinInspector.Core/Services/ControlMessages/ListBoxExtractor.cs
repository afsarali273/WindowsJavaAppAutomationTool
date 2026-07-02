using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal sealed class ListBoxExtractor : ControlMessageExtractorBase
{
    public override string ControlFamily => "listbox";

    public override bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        var count = (int)User32DesktopNative.SendMessage(parent.NativeHandle, User32DesktopNative.LbGetCount, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0)
        {
            return false;
        }

        for (var index = 0; index < Math.Min(count, maxChildren); index++)
        {
            parent.Children.Add(CreateVirtualSelectionNode(parent, "list item", index, ReadListText(parent.NativeHandle, index), count));
        }

        return true;
    }
}
