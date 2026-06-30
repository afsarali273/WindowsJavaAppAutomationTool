using System.Text;
using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaClientCodeGenerationService
{
    public string GenerateRepositoryBackedMainClass(
        IEnumerable<JavaRecordedStep> recordedSteps,
        string? repositoryPath,
        string? className = null,
        string defaultApiUrl = "http://localhost:5000")
    {
        var steps = recordedSteps
            .Where(step => step is not null)
            .OrderBy(step => step.Sequence)
            .ToList();

        var normalizedClassName = NormalizeClassName(className);
        var normalizedRepositoryPath = string.IsNullOrWhiteSpace(repositoryPath)
            ? "C:\\\\path\\\\to\\\\your-object-repository.jrecording.json"
            : repositoryPath!;

        var builder = new StringBuilder();
        builder.AppendLine("import com.afsarali.jab.client.JavaAutomation;");
        builder.AppendLine("import com.afsarali.jab.client.RetryOptions;");
        builder.AppendLine("import com.afsarali.jab.client.model.JavaWindowSelector;");
        builder.AppendLine("import com.afsarali.jab.client.model.ResolutionPolicy;");
        builder.AppendLine();
        builder.AppendLine("import java.net.URI;");
        builder.AppendLine("import java.time.Duration;");
        builder.AppendLine();
        builder.AppendLine($"public final class {normalizedClassName} {{");
        builder.AppendLine($"    private static final String DEFAULT_API = {JavaString(defaultApiUrl)};");
        builder.AppendLine($"    private static final String DEFAULT_REPOSITORY = {JavaString(normalizedRepositoryPath)};");
        builder.AppendLine();
        builder.AppendLine($"    private {normalizedClassName}() {{");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static void main(String[] args) {");
        builder.AppendLine("        URI api = URI.create(args.length > 0 ? args[0] : DEFAULT_API);");
        builder.AppendLine("        String repository = args.length > 1 ? args[1] : DEFAULT_REPOSITORY;");
        builder.AppendLine();
        builder.AppendLine("        JavaAutomation automation = JavaAutomation.connect(api)");
        builder.AppendLine("                .repository(repository)");
        builder.AppendLine("                .resolutionPolicy(ResolutionPolicy.strict());");
        builder.AppendLine();
        builder.AppendLine("        RetryOptions actionRetry = RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200));");
        builder.AppendLine("        RetryOptions windowWait = RetryOptions.of(Duration.ofSeconds(10), Duration.ofMillis(250));");

        if (steps.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("        // No recorded steps are available yet.");
        }

        foreach (var step in steps)
        {
            AppendStep(builder, step);
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendStep(StringBuilder builder, JavaRecordedStep step)
    {
        builder.AppendLine();
        builder.AppendLine($"        // Step {step.Sequence}: {step.ActionKind} - {DisplayComment(step)}");

        if (step.ActionKind == JavaRecordedActionKind.CloseWindow)
        {
            builder.AppendLine($"        automation.window({WindowSelector(step)}, windowWait).closeWindow(actionRetry);");
            return;
        }

        if (string.IsNullOrWhiteSpace(step.ObjectKey))
        {
            builder.AppendLine("        // Skipped: this recorded step has no object repository key.");
            return;
        }

        var target = $"automation.window({WindowSelector(step)}, windowWait).object({JavaString(step.ObjectKey)})";
        switch (step.ActionKind)
        {
            case JavaRecordedActionKind.Focus:
                builder.AppendLine($"        {target}.focus(actionRetry);");
                break;
            case JavaRecordedActionKind.Click:
                builder.AppendLine($"        {target}.click(actionRetry);");
                break;
            case JavaRecordedActionKind.DoubleClick:
                builder.AppendLine($"        {target}.doubleClick(actionRetry);");
                break;
            case JavaRecordedActionKind.SetText:
                builder.AppendLine($"        {target}.setText({JavaString(step.InputText)}, actionRetry);");
                break;
            case JavaRecordedActionKind.TypeText:
                builder.AppendLine($"        {target}.typeText({JavaString(step.InputText)}, actionRetry);");
                break;
            case JavaRecordedActionKind.GetText:
                builder.AppendLine($"        String step{Math.Max(step.Sequence, 1)}Text = {target}.getText(actionRetry);");
                builder.AppendLine($"        System.out.println(\"Step {step.Sequence} text: \" + step{Math.Max(step.Sequence, 1)}Text);");
                break;
            case JavaRecordedActionKind.AssertVisible:
                builder.AppendLine($"        if (!{target}.isVisible()) {{");
                builder.AppendLine($"            throw new AssertionError(\"Expected visible object: {EscapeJavaString(step.ObjectKey)}\");");
                builder.AppendLine("        }");
                break;
            default:
                builder.AppendLine($"        // TODO: Unsupported recorded action: {step.ActionKind}");
                break;
        }
    }

    private static string WindowSelector(JavaRecordedStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.WindowTitle))
        {
            var selector = $"JavaWindowSelector.title({JavaString(step.WindowTitle)})";
            if (!string.IsNullOrWhiteSpace(step.WindowClassName))
            {
                selector += $".className({JavaString(step.WindowClassName)})";
            }

            return selector;
        }

        if (!string.IsNullOrWhiteSpace(step.WindowHwndDisplay))
        {
            return $"JavaWindowSelector.hwnd({JavaString(step.WindowHwndDisplay)})";
        }

        return "JavaWindowSelector.title(\"\")";
    }

    private static string DisplayComment(JavaRecordedStep step)
    {
        var target = string.IsNullOrWhiteSpace(step.ObjectKey) ? step.WindowTitle : step.ObjectKey;
        if (string.IsNullOrWhiteSpace(target)) target = step.ObjectSummary;
        return target.Replace("\r", " ").Replace("\n", " ");
    }

    private static string NormalizeClassName(string? className)
    {
        if (string.IsNullOrWhiteSpace(className)) return "GeneratedJavaRecording";

        var builder = new StringBuilder();
        foreach (var ch in className)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_') builder.Append(ch);
        }

        if (builder.Length == 0) return "GeneratedJavaRecording";
        if (char.IsDigit(builder[0])) builder.Insert(0, '_');
        return builder.ToString();
    }

    private static string JavaString(string? value) => $"\"{EscapeJavaString(value ?? "")}\"";

    private static string EscapeJavaString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
