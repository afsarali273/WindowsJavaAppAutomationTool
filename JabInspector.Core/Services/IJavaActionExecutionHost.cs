using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public interface IJavaActionExecutionHost
{
    bool Focus(AccessibleNode node, out string message);
    bool InvokeDefaultAction(AccessibleNode node, out string message);
    bool SetText(AccessibleNode node, string text, out string message);
    string GetText(AccessibleNode node, out string message);
    bool PhysicalClick(AccessibleNode node, int count, out string message);
    int TypeUnicodeText(AccessibleNode node, string text, out string message);
    void BeforeAction(AccessibleNode node);
    void BetweenVirtualKeyClicks();
}
