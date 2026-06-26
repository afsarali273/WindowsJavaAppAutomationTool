using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaActionExecutionService(JavaVirtualKeypadService? virtualKeypad = null)
{
    private readonly JavaVirtualKeypadService _virtualKeypad = virtualKeypad ?? new JavaVirtualKeypadService();

    public JavaActionExecutionResult Execute(
        JavaRecordedActionKind actionKind,
        AccessibleNode node,
        string inputText,
        IJavaActionExecutionHost host)
    {
        host.BeforeAction(node);

        return actionKind switch
        {
            JavaRecordedActionKind.Focus => ExecuteFocus(node, host),
            JavaRecordedActionKind.Click => ExecuteClick(node, host),
            JavaRecordedActionKind.DoubleClick => ExecutePhysicalClick(node, 2, host),
            JavaRecordedActionKind.SetText => ExecuteSetText(node, inputText, host),
            JavaRecordedActionKind.TypeText => ExecuteTypeText(node, inputText, host),
            JavaRecordedActionKind.GetText => ExecuteGetText(node, host),
            _ => new JavaActionExecutionResult(false, $"Unsupported Java action '{actionKind}'.")
        };
    }

    private static JavaActionExecutionResult ExecuteFocus(AccessibleNode node, IJavaActionExecutionHost host)
    {
        var success = host.Focus(node, out var message);
        return new JavaActionExecutionResult(success, message);
    }

    private static JavaActionExecutionResult ExecuteClick(AccessibleNode node, IJavaActionExecutionHost host)
    {
        if (host.PhysicalClick(node, 1, out var physicalMessage))
            return new JavaActionExecutionResult(true, physicalMessage);

        if (host.InvokeDefaultAction(node, out var semanticMessage))
            return new JavaActionExecutionResult(true, semanticMessage);

        return new JavaActionExecutionResult(false, $"{physicalMessage} Semantic fallback also failed: {semanticMessage}");
    }

    private static JavaActionExecutionResult ExecutePhysicalClick(AccessibleNode node, int count, IJavaActionExecutionHost host)
    {
        var success = host.PhysicalClick(node, count, out var message);
        return new JavaActionExecutionResult(success, message);
    }

    private static JavaActionExecutionResult ExecuteSetText(AccessibleNode node, string text, IJavaActionExecutionHost host)
    {
        var success = host.SetText(node, text, out var message);
        return new JavaActionExecutionResult(success, message);
    }

    private JavaActionExecutionResult ExecuteTypeText(AccessibleNode node, string text, IJavaActionExecutionHost host)
    {
        if (_virtualKeypad.ShouldUseVirtualKeypad(node, text))
            return ExecuteVirtualKeypadTyping(node, text, host);

        var typed = host.TypeUnicodeText(node, text, out var typedMessage);
        if (typed > 0 || text.Length == 0)
            return new JavaActionExecutionResult(true, typedMessage);

        if (host.SetText(node, text, out var setMessage))
            return new JavaActionExecutionResult(true, $"{typedMessage} Unicode typing inserted no characters; direct text fallback succeeded. {setMessage}");

        return new JavaActionExecutionResult(false, $"{typedMessage} Direct text fallback also failed: {setMessage}");
    }

    private JavaActionExecutionResult ExecuteVirtualKeypadTyping(AccessibleNode keyboardRoot, string text, IJavaActionExecutionHost host)
    {
        if (!_virtualKeypad.TryBuildPlan(keyboardRoot, text, out var plan, out var message))
            return new JavaActionExecutionResult(false, message);

        var clicked = 0;
        foreach (var step in plan.Steps)
        {
            if (!host.PhysicalClick(step.KeyNode, 1, out var clickMessage))
                return new JavaActionExecutionResult(false, $"Virtual keypad key '{step.Label}' failed: {clickMessage}");

            clicked++;
            host.BetweenVirtualKeyClicks();
        }

        return new JavaActionExecutionResult(clicked > 0 || text.Length == 0, $"Typed {clicked} key(s) using virtual keypad container {keyboardRoot.DisplayName}.");
    }

    private static JavaActionExecutionResult ExecuteGetText(AccessibleNode node, IJavaActionExecutionHost host)
    {
        var text = host.GetText(node, out var message);
        return new JavaActionExecutionResult(true, message, text);
    }
}
