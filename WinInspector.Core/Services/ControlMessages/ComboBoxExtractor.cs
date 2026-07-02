using WinInspector.Core.Models;
using WinInspector.Core.Native;

namespace WinInspector.Core.Services.ControlMessages;

internal sealed class ComboBoxExtractor : ControlMessageExtractorBase
{
    public override string ControlFamily => "combobox";

    public override bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        var count = (int)User32DesktopNative.SendMessage(parent.NativeHandle, User32DesktopNative.CbGetCount, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0)
        {
            return false;
        }

        for (var index = 0; index < Math.Min(count, maxChildren); index++)
        {
            parent.Children.Add(CreateVirtualSelectionNode(parent, "combo item", index, ReadComboText(parent.NativeHandle, index), count));
        }

        return true;
    }
}
