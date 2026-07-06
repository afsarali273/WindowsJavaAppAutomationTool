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

    public string GenerateElementSnippet(
        AccessibleNode? node,
        JavaWindowInfo? window = null,
        string? className = null,
        string defaultApiUrl = "http://localhost:5000")
    {
        if (node is null)
        {
            return "// Select a Java element to generate a code snippet.";
        }

        var locator = LocatorGenerator.GenerateLocator(node);
        var normalizedClassName = NormalizeClassName(string.IsNullOrWhiteSpace(className)
            ? $"{SanitizeIdentifier(node.DisplayName)}ElementSnippet"
            : className);

        var builder = new StringBuilder();
        builder.AppendLine("import com.afsarali.jab.client.JavaAutomation;");
        builder.AppendLine("import com.afsarali.jab.client.RetryOptions;");
        builder.AppendLine("import com.afsarali.jab.client.model.JavaWindowSelector;");
        builder.AppendLine("import com.afsarali.jab.client.model.LocatorSuggestion;");
        builder.AppendLine("import com.afsarali.jab.client.model.ResolutionPolicy;");
        builder.AppendLine();
        builder.AppendLine("import java.net.URI;");
        builder.AppendLine("import java.time.Duration;");
        builder.AppendLine("import java.util.List;");
        builder.AppendLine();
        builder.AppendLine($"public final class {normalizedClassName} {{");
        builder.AppendLine($"    private static final String DEFAULT_API = {JavaString(defaultApiUrl)};");
        builder.AppendLine();
        builder.AppendLine($"    private {normalizedClassName}() {{");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static void main(String[] args) {");
        builder.AppendLine("        URI api = URI.create(args.length > 0 ? args[0] : DEFAULT_API);");
        builder.AppendLine("        JavaAutomation automation = JavaAutomation.connect(api)");
        builder.AppendLine("                .resolutionPolicy(ResolutionPolicy.inline());");
        builder.AppendLine();
        builder.AppendLine($"        JavaWindowSelector window = {WindowSelectorLiteral(window)};");
        builder.AppendLine("        LocatorSuggestion locator =");
        builder.AppendLine(IndentBlock(LocatorLiteral(locator), 8));
        builder.AppendLine("        var element = automation.window(window).element(locator);");
        builder.AppendLine();
        builder.AppendLine("        RetryOptions retry = RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200));");
        builder.AppendLine("        element.waitUntilExists(retry);");
        builder.AppendLine("        element.click(retry);");
        builder.AppendLine("        String text = element.getText(retry);");
        builder.AppendLine("        boolean visible = element.isVisible();");
        builder.AppendLine("        boolean enabled = element.isEnabled();");
        builder.AppendLine("        List<?> children = element.findChildElements();");
        builder.AppendLine("        System.out.println(text);");
        builder.AppendLine("        System.out.println(\"visible=\" + visible + \", enabled=\" + enabled + \", childCount=\" + children.size());");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    public string GenerateInlineLocatorMainClass(
        IEnumerable<JavaRecordedStep> recordedSteps,
        string? className = null,
        string defaultApiUrl = "http://localhost:5000")
    {
        var steps = recordedSteps
            .Where(step => step is not null)
            .OrderBy(step => step.Sequence)
            .ToList();

        var normalizedClassName = NormalizeClassName(string.IsNullOrWhiteSpace(className)
            ? "GeneratedJavaInlineRecording"
            : $"{className}Inline");

        var builder = new StringBuilder();
        builder.AppendLine("import com.afsarali.jab.client.JavaAutomation;");
        builder.AppendLine("import com.afsarali.jab.client.RetryOptions;");
        builder.AppendLine("import com.afsarali.jab.client.model.ElementBounds;");
        builder.AppendLine("import com.afsarali.jab.client.model.JavaWindowSelector;");
        builder.AppendLine("import com.afsarali.jab.client.model.LocatorSuggestion;");
        builder.AppendLine("import com.afsarali.jab.client.model.ResolutionPolicy;");
        builder.AppendLine();
        builder.AppendLine("import java.net.URI;");
        builder.AppendLine("import java.time.Duration;");
        builder.AppendLine("import java.util.List;");
        builder.AppendLine();
        builder.AppendLine($"public final class {normalizedClassName} {{");
        builder.AppendLine($"    private static final String DEFAULT_API = {JavaString(defaultApiUrl)};");
        builder.AppendLine();
        builder.AppendLine($"    private {normalizedClassName}() {{");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static void main(String[] args) {");
        builder.AppendLine("        URI api = URI.create(args.length > 0 ? args[0] : DEFAULT_API);");
        builder.AppendLine();
        builder.AppendLine("        JavaAutomation automation = JavaAutomation.connect(api)");
        builder.AppendLine("                .resolutionPolicy(ResolutionPolicy.inline());");
        builder.AppendLine();
        builder.AppendLine("        RetryOptions actionRetry = RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200));");
        builder.AppendLine("        RetryOptions windowWait = RetryOptions.of(Duration.ofSeconds(10), Duration.ofMillis(250));");

        if (steps.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("        // No recorded steps are available yet.");
        }

        var emittedLocators = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedVariableNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (step.ActionKind == JavaRecordedActionKind.CloseWindow) continue;
            var locator = GetLocator(step);
            if (locator is null) continue;
            var variableName = CreateLocatorVariableName(step, emittedLocators.Count + 1, usedVariableNames);
            emittedLocators[StepKey(step)] = variableName;
            AppendLocatorVariable(builder, variableName, locator);
        }

        foreach (var step in steps)
        {
            AppendStep(builder, step, inlineLocatorVariable: emittedLocators.GetValueOrDefault(StepKey(step)));
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendStep(StringBuilder builder, JavaRecordedStep step, string? inlineLocatorVariable = null)
    {
        builder.AppendLine();
        builder.AppendLine($"        // Step {step.Sequence}: {step.ActionKind} - {DisplayComment(step)}");

        if (step.ActionKind == JavaRecordedActionKind.CloseWindow)
        {
            builder.AppendLine($"        automation.window({WindowSelector(step)}, windowWait).closeWindow(actionRetry);");
            return;
        }

        var targetLocator = !string.IsNullOrWhiteSpace(inlineLocatorVariable)
            ? inlineLocatorVariable
            : !string.IsNullOrWhiteSpace(step.ObjectKey)
                ? JavaString(step.ObjectKey)
                : "";

        if (string.IsNullOrWhiteSpace(targetLocator))
        {
            builder.AppendLine("        // Skipped: this recorded step has no object repository key or inline locator metadata.");
            return;
        }

        var target = $"automation.window({WindowSelector(step)}, windowWait).object({targetLocator})";
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

    private static void AppendLocatorVariable(StringBuilder builder, string variableName, LocatorSuggestion locator)
    {
        builder.AppendLine();
        builder.AppendLine($"        LocatorSuggestion {variableName} = LocatorSuggestion.builder()");
        AppendBuilderString(builder, "engine", locator.Engine);
        AppendBuilderString(builder, "role", locator.Role);
        AppendBuilderString(builder, "roleEnUs", locator.RoleEnUs);
        AppendBuilderString(builder, "name", locator.Name);
        AppendBuilderString(builder, "virtualAccessibleName", locator.VirtualAccessibleName);
        AppendBuilderString(builder, "description", locator.Description);
        AppendBuilderString(builder, "states", locator.States);
        AppendBuilderString(builder, "statesEnUs", locator.StatesEnUs);
        AppendBuilderInt(builder, "indexInParent", locator.IndexInParent);
        AppendBuilderInt(builder, "objectDepth", locator.ObjectDepth);
        AppendBuilderInt(builder, "childrenCount", locator.ChildrenCount);
        AppendBuilderString(builder, "path", locator.Path);
        AppendBuilderString(builder, "indexPath", locator.IndexPath);
        AppendBuilderString(builder, "xPath", locator.XPath);
        AppendBuilderString(builder, "indexXPath", locator.IndexXPath);
        AppendBuilderString(builder, "semanticXPath", locator.SemanticXPath);
        AppendBuilderString(builder, "parentRole", locator.ParentRole);
        AppendBuilderString(builder, "parentName", locator.ParentName);
        AppendBuilderBool(builder, "isTableLikeContainer", locator.IsTableLikeContainer);
        AppendBuilderBool(builder, "isTableLikeRow", locator.IsTableLikeRow);
        AppendBuilderBool(builder, "isTableLikeCell", locator.IsTableLikeCell);
        AppendBuilderString(builder, "tableLikeKind", locator.TableLikeKind);
        AppendBuilderString(builder, "tableLikeContainerPath", locator.TableLikeContainerPath);
        AppendBuilderString(builder, "tableLikeColumnHeader", locator.TableLikeColumnHeader);
        AppendBuilderInt(builder, "tableLikeRowIndex", locator.TableLikeRowIndex);
        AppendBuilderInt(builder, "tableLikeColumnIndex", locator.TableLikeColumnIndex);
        AppendBuilderInt(builder, "tableLikeRowCount", locator.TableLikeRowCount);
        AppendBuilderInt(builder, "tableLikeColumnCount", locator.TableLikeColumnCount);
        AppendBuilderBool(builder, "isFormsLikeScope", locator.IsFormsLikeScope);
        AppendBuilderBool(builder, "isFormsViewportLikeContainer", locator.IsFormsViewportLikeContainer);
        AppendBuilderString(builder, "formsScopePath", locator.FormsScopePath);
        AppendBuilderString(builder, "formsScopeRole", locator.FormsScopeRole);
        AppendBuilderString(builder, "formsScopeName", locator.FormsScopeName);
        AppendBuilderString(builder, "formsViewportPath", locator.FormsViewportPath);
        AppendBuilderString(builder, "formsViewportRole", locator.FormsViewportRole);
        AppendBuilderString(builder, "formsViewportName", locator.FormsViewportName);
        builder.AppendLine($"                .hasManagedDescendantAncestor({locator.HasManagedDescendantAncestor.ToString().ToLowerInvariant()})");
        if (locator.ActionNames.Count > 0)
        {
            builder.AppendLine($"                .actionNames(List.of({string.Join(", ", locator.ActionNames.Select(JavaString))}))");
        }
        AppendBuilderString(builder, "textPreview", locator.TextPreview);
        AppendBuilderString(builder, "textPreviewSource", locator.TextPreviewSource);
        AppendBuilderInt(builder, "textCharCount", locator.TextCharCount);
        AppendBuilderInt(builder, "textCaretIndex", locator.TextCaretIndex);
        AppendBuilderInt(builder, "textIndexAtPoint", locator.TextIndexAtPoint);
        AppendBuilderString(builder, "textSelected", locator.TextSelected);
        AppendBuilderString(builder, "textLetter", locator.TextLetter);
        AppendBuilderInt(builder, "textSelectionStartIndex", locator.TextSelectionStartIndex);
        AppendBuilderInt(builder, "textSelectionEndIndex", locator.TextSelectionEndIndex);
        AppendBuilderString(builder, "textWord", locator.TextWord);
        AppendBuilderString(builder, "textSentence", locator.TextSentence);
        AppendBuilderString(builder, "currentValue", locator.CurrentValue);
        AppendBuilderString(builder, "minimumValue", locator.MinimumValue);
        AppendBuilderString(builder, "maximumValue", locator.MaximumValue);
        if (locator.Bounds.Width != 0 || locator.Bounds.Height != 0)
        {
            builder.AppendLine($"                .bounds(new ElementBounds({locator.Bounds.X}, {locator.Bounds.Y}, {locator.Bounds.Width}, {locator.Bounds.Height}))");
        }
        builder.AppendLine("                .build();");
    }

    private static void AppendBuilderString(StringBuilder builder, string methodName, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"                .{methodName}({JavaString(value)})");
        }
    }

    private static void AppendBuilderInt(StringBuilder builder, string methodName, int value)
    {
        if (value >= 0)
        {
            builder.AppendLine($"                .{methodName}({value})");
        }
    }

    private static void AppendBuilderBool(StringBuilder builder, string methodName, bool value)
    {
        if (value)
        {
            builder.AppendLine($"                .{methodName}(true)");
        }
    }

    private static string LocatorLiteral(LocatorSuggestion locator)
    {
        var builder = new StringBuilder();
        builder.AppendLine("LocatorSuggestion.builder()");
        AppendBuilderString(builder, "engine", locator.Engine);
        AppendBuilderString(builder, "role", locator.Role);
        AppendBuilderString(builder, "roleEnUs", locator.RoleEnUs);
        AppendBuilderString(builder, "name", locator.Name);
        AppendBuilderString(builder, "virtualAccessibleName", locator.VirtualAccessibleName);
        AppendBuilderString(builder, "description", locator.Description);
        AppendBuilderString(builder, "states", locator.States);
        AppendBuilderString(builder, "statesEnUs", locator.StatesEnUs);
        AppendBuilderInt(builder, "indexInParent", locator.IndexInParent);
        AppendBuilderInt(builder, "objectDepth", locator.ObjectDepth);
        AppendBuilderInt(builder, "childrenCount", locator.ChildrenCount);
        AppendBuilderString(builder, "path", locator.Path);
        AppendBuilderString(builder, "indexPath", locator.IndexPath);
        AppendBuilderString(builder, "xPath", locator.XPath);
        AppendBuilderString(builder, "indexXPath", locator.IndexXPath);
        AppendBuilderString(builder, "semanticXPath", locator.SemanticXPath);
        AppendBuilderString(builder, "parentRole", locator.ParentRole);
        AppendBuilderString(builder, "parentName", locator.ParentName);
        AppendBuilderBool(builder, "isTableLikeContainer", locator.IsTableLikeContainer);
        AppendBuilderBool(builder, "isTableLikeRow", locator.IsTableLikeRow);
        AppendBuilderBool(builder, "isTableLikeCell", locator.IsTableLikeCell);
        AppendBuilderString(builder, "tableLikeKind", locator.TableLikeKind);
        AppendBuilderString(builder, "tableLikeContainerPath", locator.TableLikeContainerPath);
        AppendBuilderString(builder, "tableLikeColumnHeader", locator.TableLikeColumnHeader);
        AppendBuilderInt(builder, "tableLikeRowIndex", locator.TableLikeRowIndex);
        AppendBuilderInt(builder, "tableLikeColumnIndex", locator.TableLikeColumnIndex);
        AppendBuilderInt(builder, "tableLikeRowCount", locator.TableLikeRowCount);
        AppendBuilderInt(builder, "tableLikeColumnCount", locator.TableLikeColumnCount);
        AppendBuilderBool(builder, "isFormsLikeScope", locator.IsFormsLikeScope);
        AppendBuilderBool(builder, "isFormsViewportLikeContainer", locator.IsFormsViewportLikeContainer);
        AppendBuilderString(builder, "formsScopePath", locator.FormsScopePath);
        AppendBuilderString(builder, "formsScopeRole", locator.FormsScopeRole);
        AppendBuilderString(builder, "formsScopeName", locator.FormsScopeName);
        AppendBuilderString(builder, "formsViewportPath", locator.FormsViewportPath);
        AppendBuilderString(builder, "formsViewportRole", locator.FormsViewportRole);
        AppendBuilderString(builder, "formsViewportName", locator.FormsViewportName);
        builder.AppendLine($"                .hasManagedDescendantAncestor({locator.HasManagedDescendantAncestor.ToString().ToLowerInvariant()})");
        if (locator.ActionNames.Count > 0)
        {
            builder.AppendLine($"                .actionNames(List.of({string.Join(", ", locator.ActionNames.Select(JavaString))}))");
        }
        AppendBuilderString(builder, "textPreview", locator.TextPreview);
        AppendBuilderString(builder, "textPreviewSource", locator.TextPreviewSource);
        AppendBuilderInt(builder, "textCharCount", locator.TextCharCount);
        AppendBuilderInt(builder, "textCaretIndex", locator.TextCaretIndex);
        AppendBuilderInt(builder, "textIndexAtPoint", locator.TextIndexAtPoint);
        AppendBuilderString(builder, "textSelected", locator.TextSelected);
        AppendBuilderString(builder, "textLetter", locator.TextLetter);
        AppendBuilderInt(builder, "textSelectionStartIndex", locator.TextSelectionStartIndex);
        AppendBuilderInt(builder, "textSelectionEndIndex", locator.TextSelectionEndIndex);
        AppendBuilderString(builder, "textWord", locator.TextWord);
        AppendBuilderString(builder, "textSentence", locator.TextSentence);
        AppendBuilderString(builder, "currentValue", locator.CurrentValue);
        AppendBuilderString(builder, "minimumValue", locator.MinimumValue);
        AppendBuilderString(builder, "maximumValue", locator.MaximumValue);
        if (locator.Bounds.Width != 0 || locator.Bounds.Height != 0)
        {
            builder.AppendLine($"                .bounds(new com.afsarali.jab.client.model.ElementBounds({locator.Bounds.X}, {locator.Bounds.Y}, {locator.Bounds.Width}, {locator.Bounds.Height}))");
        }
        builder.Append("                .build();");
        return builder.ToString();
    }

    private static string WindowSelectorLiteral(JavaWindowInfo? window)
    {
        if (window is null)
        {
            return "JavaWindowSelector.title(\"<window title>\")";
        }

        var selector = $"JavaWindowSelector.title({JavaString(window.Title)})";
        if (!string.IsNullOrWhiteSpace(window.ClassName))
        {
            selector += $".className({JavaString(window.ClassName)})";
        }

        return selector;
    }

    private static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder();
        var capitalizeNext = true;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        var sanitized = builder.Length == 0 ? "GeneratedJavaElement" : builder.ToString();
        if (char.IsDigit(sanitized[0])) sanitized = "_" + sanitized;
        return sanitized;
    }

    private static string IndentBlock(string value, int spaces)
    {
        var indent = new string(' ', spaces);
        return string.Join(Environment.NewLine, value.Split(Environment.NewLine, StringSplitOptions.None).Select(line => indent + line));
    }

    private static LocatorSuggestion? GetLocator(JavaRecordedStep step)
    {
        if (step.ObjectLocator is not null) return step.ObjectLocator;
        if (!string.IsNullOrWhiteSpace(step.ObjectRole)
            || !string.IsNullOrWhiteSpace(step.ObjectName)
            || !string.IsNullOrWhiteSpace(step.ObjectVirtualAccessibleName)
            || !string.IsNullOrWhiteSpace(step.ObjectPath))
        {
            return new LocatorSuggestion(
                "java-access-bridge",
                step.ObjectRole ?? "",
                "",
                step.ObjectName ?? "",
                step.ObjectVirtualAccessibleName ?? "",
                step.ObjectDescription ?? "",
                "",
                "",
                -1,
                step.ObjectDepth,
                -1,
                step.ObjectPath ?? "",
                "",
                "",
                "",
                "",
                "",
                "",
                false,
                false,
                false,
                "",
                "",
                "",
                -1,
                -1,
                -1,
                -1,
                false,
                false,
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                -1,
                -1,
                false,
                Array.Empty<string>(),
                "",
                "",
                -1,
                -1,
                -1,
                "",
                "",
                "",
                "",
                "",
                "",
                new ElementBounds(0, 0, 0, 0));
        }

        return null;
    }

    private static string CreateLocatorVariableName(JavaRecordedStep step, int fallbackIndex, ISet<string> usedVariableNames)
    {
        var basis = !string.IsNullOrWhiteSpace(step.ObjectKey)
            ? step.ObjectKey
            : !string.IsNullOrWhiteSpace(step.ObjectName)
                ? step.ObjectName
                : $"step{fallbackIndex}";
        var builder = new StringBuilder("locator");
        var capitalizeNext = true;
        foreach (var ch in basis)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        var baseName = builder.ToString();
        var candidate = baseName;
        var suffix = 2;
        while (!usedVariableNames.Add(candidate))
        {
            candidate = $"{baseName}{suffix++}";
        }

        return candidate;
    }

    private static string StepKey(JavaRecordedStep step) => $"{step.Sequence}:{step.ObjectKey}:{step.ObjectPath}";

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
