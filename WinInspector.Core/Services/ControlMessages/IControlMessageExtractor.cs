using WinInspector.Core.Models;

namespace WinInspector.Core.Services.ControlMessages;

public interface IControlMessageExtractor
{
    string ControlFamily { get; }

    bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren);
}
