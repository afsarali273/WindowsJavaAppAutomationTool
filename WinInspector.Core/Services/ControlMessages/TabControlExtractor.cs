using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal sealed class TabControlExtractor : ControlMessageExtractorBase
{
    public override string ControlFamily => "tab";

    public override bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        var count = (int)User32DesktopNative.SendMessage(parent.NativeHandle, User32DesktopNative.TcmGetItemCount, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0)
        {
            return false;
        }

        for (var index = 0; index < Math.Min(count, maxChildren); index++)
        {
            parent.Children.Add(CreateVirtualSelectionNode(parent, "tab item", index, $"Tab {index + 1}", count));
        }

        return true;
    }
}
