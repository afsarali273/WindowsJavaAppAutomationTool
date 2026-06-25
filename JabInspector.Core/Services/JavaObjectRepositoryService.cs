using System.Text;
using System.Text.Json;
using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaObjectRepositoryService
{
    public JavaRecordingProject CreateProject(string sessionName, string applicationAlias, JavaWindowInfo window) => new()
    {
        SessionName = sessionName.Trim(),
        ApplicationAlias = applicationAlias.Trim(),
        CreatedAtUtc = DateTime.UtcNow,
        WindowTitle = window.Title,
        WindowClassName = window.ClassName
    };

    public JavaObjectRepositoryEntry CreateEntry(JavaWindowInfo window, AccessibleNode node, string objectKey, string friendlyName)
    {
        var locator = LocatorGenerator.GenerateLocator(node);
        var entry = new JavaObjectRepositoryEntry
        {
            ObjectKey = objectKey,
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? node.DisplayName : friendlyName.Trim(),
            CapturedAtUtc = DateTime.UtcNow,
            WindowHwndDisplay = window.HwndDisplay,
            WindowTitle = window.Title,
            WindowClassName = window.ClassName,
            WindowProcessId = window.ProcessId,
            WindowVmId = window.VmId,
            Engine = locator.Engine,
            Locator = locator,
            LocatorJson = JsonExportService.Serialize(locator),
            Role = locator.Role,
            RoleEnUs = locator.RoleEnUs,
            Name = locator.Name,
            VirtualAccessibleName = locator.VirtualAccessibleName,
            Description = locator.Description,
            States = locator.States,
            StatesEnUs = locator.StatesEnUs,
            Path = locator.Path,
            IndexPath = locator.IndexPath,
            XPath = locator.XPath,
            IndexXPath = locator.IndexXPath,
            SemanticXPath = locator.SemanticXPath,
            ParentRole = locator.ParentRole,
            ParentName = locator.ParentName,
            IndexInParent = locator.IndexInParent,
            ObjectDepth = locator.ObjectDepth,
            ChildrenCount = locator.ChildrenCount,
            X = locator.Bounds.X,
            Y = locator.Bounds.Y,
            Width = locator.Bounds.Width,
            Height = locator.Bounds.Height,
            AccessibleComponent = node.AccessibleComponent,
            AccessibleAction = node.AccessibleAction,
            AccessibleSelection = node.AccessibleSelection,
            AccessibleText = node.AccessibleText,
            AccessibleValue = node.AccessibleValue,
            AccessibleTable = node.AccessibleTable,
            AccessibleInterfaces = node.AccessibleInterfaces,
            HasManagedDescendantAncestor = locator.HasManagedDescendantAncestor,
            ActionNames = locator.ActionNames.ToList()
        };

        entry.Properties =
        [
            Property("window.title", entry.WindowTitle, true),
            Property("window.className", entry.WindowClassName, true),
            Property("window.hwnd", entry.WindowHwndDisplay, true),
            Property("window.processId", entry.WindowProcessId.ToString(), false),
            Property("window.vmId", entry.WindowVmId.ToString(), false),
            Property("engine", entry.Engine, true),
            Property("role", entry.Role, true),
            Property("role.enUs", entry.RoleEnUs, true),
            Property("name", entry.Name, true),
            Property("virtualAccessibleName", entry.VirtualAccessibleName, true),
            Property("description", entry.Description, false),
            Property("states", entry.States, false),
            Property("states.enUs", entry.StatesEnUs, false),
            Property("path", entry.Path, true),
            Property("indexPath", entry.IndexPath, true),
            Property("xpath", entry.XPath, true),
            Property("indexXPath", entry.IndexXPath, false),
            Property("semanticXPath", entry.SemanticXPath, true),
            Property("parent.role", entry.ParentRole, true),
            Property("parent.name", entry.ParentName, false),
            Property("indexInParent", entry.IndexInParent.ToString(), false),
            Property("objectDepth", entry.ObjectDepth.ToString(), false),
            Property("childrenCount", entry.ChildrenCount.ToString(), false),
            Property("bounds", $"{entry.X},{entry.Y},{entry.Width},{entry.Height}", false),
            Property("accessible.component", entry.AccessibleComponent.ToString(), false),
            Property("accessible.action", entry.AccessibleAction.ToString(), false),
            Property("accessible.selection", entry.AccessibleSelection.ToString(), false),
            Property("accessible.text", entry.AccessibleText.ToString(), false),
            Property("accessible.value", entry.AccessibleValue.ToString(), false),
            Property("accessible.table", entry.AccessibleTable.ToString(), false),
            Property("accessible.interfaces", entry.AccessibleInterfaces.ToString(), false),
            Property("volatile.managedDescendantAncestor", entry.HasManagedDescendantAncestor.ToString(), false),
            Property("actions", string.Join(",", entry.ActionNames), false),
            Property("text.preview", locator.TextPreview, false),
            Property("text.previewSource", locator.TextPreviewSource, false),
            Property("text.charCount", locator.TextCharCount.ToString(), false),
            Property("text.caretIndex", locator.TextCaretIndex.ToString(), false),
            Property("text.indexAtPoint", locator.TextIndexAtPoint.ToString(), false),
            Property("text.selected", locator.TextSelected, false),
            Property("text.word", locator.TextWord, false),
            Property("text.sentence", locator.TextSentence, false),
            Property("value.current", locator.CurrentValue, false),
            Property("value.minimum", locator.MinimumValue, false),
            Property("value.maximum", locator.MaximumValue, false)
        ];

        return entry;
    }

    public JavaRecordedStep CreateRecordedStep(
        JavaObjectRepositoryEntry entry,
        JavaRecordedActionKind actionKind,
        int sequence,
        string? inputText,
        JavaWindowInfo? window,
        int? recordedScreenX,
        int? recordedScreenY,
        int? windowOffsetX,
        int? windowOffsetY)
    {
        var locator = entry.Locator ?? TryDeserializeLocator(entry.LocatorJson);
        var locatorJson = locator is null
            ? entry.LocatorJson
            : JsonExportService.Serialize(locator);

        return new JavaRecordedStep
        {
            Sequence = sequence,
            StepName = $"{actionKind} {entry.ObjectKey}",
            ActionKind = actionKind,
            ObjectKey = entry.ObjectKey,
            InputText = inputText ?? "",
            CapturedAtUtc = DateTime.UtcNow,
            WindowHwndDisplay = window?.HwndDisplay ?? entry.WindowHwndDisplay,
            WindowTitle = window?.Title ?? entry.WindowTitle,
            WindowClassName = window?.ClassName ?? entry.WindowClassName,
            WindowProcessId = window?.ProcessId ?? entry.WindowProcessId,
            WindowVmId = window?.VmId ?? entry.WindowVmId,
            RecordedScreenX = recordedScreenX,
            RecordedScreenY = recordedScreenY,
            WindowOffsetX = windowOffsetX,
            WindowOffsetY = windowOffsetY,
            ObjectLocator = locator,
            ObjectLocatorJson = locatorJson,
            ObjectRole = locator?.Role ?? entry.Role,
            ObjectName = locator?.Name ?? entry.Name,
            ObjectVirtualAccessibleName = locator?.VirtualAccessibleName ?? entry.VirtualAccessibleName,
            ObjectDescription = locator?.Description ?? entry.Description,
            ObjectPath = locator?.Path ?? entry.Path,
            ObjectDepth = locator?.ObjectDepth ?? entry.ObjectDepth
        };
    }

    public JavaRecordedStep PromoteClickToDoubleClick(JavaRecordedStep step)
    {
        return new JavaRecordedStep
        {
            Sequence = step.Sequence,
            StepName = $"{JavaRecordedActionKind.DoubleClick} {step.ObjectKey}",
            ActionKind = JavaRecordedActionKind.DoubleClick,
            ObjectKey = step.ObjectKey,
            InputText = step.InputText,
            CapturedAtUtc = DateTime.UtcNow,
            WindowHwndDisplay = step.WindowHwndDisplay,
            WindowTitle = step.WindowTitle,
            WindowClassName = step.WindowClassName,
            WindowProcessId = step.WindowProcessId,
            WindowVmId = step.WindowVmId,
            RecordedScreenX = step.RecordedScreenX,
            RecordedScreenY = step.RecordedScreenY,
            WindowOffsetX = step.WindowOffsetX,
            WindowOffsetY = step.WindowOffsetY,
            ObjectLocator = step.ObjectLocator,
            ObjectLocatorJson = step.ObjectLocatorJson,
            ObjectRole = step.ObjectRole,
            ObjectName = step.ObjectName,
            ObjectVirtualAccessibleName = step.ObjectVirtualAccessibleName,
            ObjectDescription = step.ObjectDescription,
            ObjectPath = step.ObjectPath,
            ObjectDepth = step.ObjectDepth
        };
    }

    public string BuildPropertiesPreview(JavaObjectRepositoryEntry entry)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(entry.LocatorJson))
        {
            builder.AppendLine("[locator.json]");
            builder.AppendLine(entry.LocatorJson);
            builder.AppendLine();
        }

        builder.AppendLine($"[{entry.ObjectKey}]");
        foreach (var property in entry.Properties)
        {
            if (string.IsNullOrWhiteSpace(property.Value)) continue;
            builder.Append(property.Name);
            builder.Append('=');
            builder.AppendLine(property.Value.Replace(Environment.NewLine, " "));
        }
        return builder.ToString().TrimEnd();
    }

    public string BuildStepPreview(JavaRecordedStep step)
    {
        var builder = new StringBuilder();
        var locator = step.ObjectLocator ?? TryDeserializeLocator(step.ObjectLocatorJson);
        if (locator is not null)
        {
            builder.AppendLine("[object.locator.json]");
            builder.AppendLine(JsonExportService.Serialize(locator));
            builder.AppendLine();
        }

        builder.AppendLine($"step.sequence={step.Sequence}");
        builder.AppendLine($"step.name={step.StepName}");
        builder.AppendLine($"step.action={step.ActionKind}");
        builder.AppendLine($"step.objectKey={step.ObjectKey}");
        builder.AppendLine($"step.windowHwnd={step.WindowHwndDisplay}");
        builder.AppendLine($"step.windowTitle={step.WindowTitle}");
        builder.AppendLine($"step.windowClassName={step.WindowClassName}");
        builder.AppendLine($"step.windowProcessId={step.WindowProcessId}");
        builder.AppendLine($"step.windowVmId={step.WindowVmId}");
        if (step.RecordedScreenX.HasValue && step.RecordedScreenY.HasValue) builder.AppendLine($"step.recordedScreenPoint={step.RecordedScreenX.Value},{step.RecordedScreenY.Value}");
        if (step.WindowOffsetX.HasValue && step.WindowOffsetY.HasValue) builder.AppendLine($"step.windowOffset={step.WindowOffsetX.Value},{step.WindowOffsetY.Value}");
        if (!string.IsNullOrWhiteSpace(locator?.Role ?? step.ObjectRole)) builder.AppendLine($"object.role={locator?.Role ?? step.ObjectRole}");
        if (!string.IsNullOrWhiteSpace(locator?.RoleEnUs)) builder.AppendLine($"object.roleEnUs={locator.RoleEnUs}");
        if (!string.IsNullOrWhiteSpace(locator?.Name ?? step.ObjectName)) builder.AppendLine($"object.name={locator?.Name ?? step.ObjectName}");
        if (!string.IsNullOrWhiteSpace(locator?.VirtualAccessibleName ?? step.ObjectVirtualAccessibleName)) builder.AppendLine($"object.virtualAccessibleName={locator?.VirtualAccessibleName ?? step.ObjectVirtualAccessibleName}");
        if (!string.IsNullOrWhiteSpace(locator?.Description ?? step.ObjectDescription)) builder.AppendLine($"object.description={locator?.Description ?? step.ObjectDescription}");
        if (!string.IsNullOrWhiteSpace(locator?.States)) builder.AppendLine($"object.states={locator.States}");
        if (!string.IsNullOrWhiteSpace(locator?.StatesEnUs)) builder.AppendLine($"object.statesEnUs={locator.StatesEnUs}");
        if (!string.IsNullOrWhiteSpace(locator?.Path ?? step.ObjectPath)) builder.AppendLine($"object.path={locator?.Path ?? step.ObjectPath}");
        if (!string.IsNullOrWhiteSpace(locator?.IndexPath)) builder.AppendLine($"object.indexPath={locator.IndexPath}");
        if (!string.IsNullOrWhiteSpace(locator?.XPath)) builder.AppendLine($"object.xPath={locator.XPath}");
        if (!string.IsNullOrWhiteSpace(locator?.IndexXPath)) builder.AppendLine($"object.indexXPath={locator.IndexXPath}");
        if (!string.IsNullOrWhiteSpace(locator?.SemanticXPath)) builder.AppendLine($"object.semanticXPath={locator.SemanticXPath}");
        if (!string.IsNullOrWhiteSpace(locator?.ParentRole)) builder.AppendLine($"object.parentRole={locator.ParentRole}");
        if (!string.IsNullOrWhiteSpace(locator?.ParentName)) builder.AppendLine($"object.parentName={locator.ParentName}");
        if (locator is not null)
        {
            builder.AppendLine($"object.indexInParent={locator.IndexInParent}");
            builder.AppendLine($"object.depth={locator.ObjectDepth}");
            builder.AppendLine($"object.childrenCount={locator.ChildrenCount}");
            builder.AppendLine($"object.bounds={locator.Bounds.X},{locator.Bounds.Y},{locator.Bounds.Width},{locator.Bounds.Height}");
            if (locator.ActionNames.Count > 0) builder.AppendLine($"object.actionNames={string.Join(",", locator.ActionNames)}");
            if (!string.IsNullOrWhiteSpace(locator.TextPreview)) builder.AppendLine($"object.textPreview={locator.TextPreview.Replace(Environment.NewLine, " ")}");
            if (!string.IsNullOrWhiteSpace(locator.CurrentValue)) builder.AppendLine($"object.currentValue={locator.CurrentValue}");
        }
        else if (step.ObjectDepth >= 0)
        {
            builder.AppendLine($"object.depth={step.ObjectDepth}");
        }
        if (!string.IsNullOrWhiteSpace(step.InputText)) builder.AppendLine($"step.inputText={step.InputText}");
        builder.AppendLine($"step.capturedAtUtc={step.CapturedAtUtc:O}");
        return builder.ToString().TrimEnd();
    }

    public void SaveProject(string path, JavaRecordingProject project)
    {
        var json = JsonSerializer.Serialize(project, JsonExportService.Options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    public JavaRecordingProject LoadProject(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<JavaRecordingProject>(json) ?? new JavaRecordingProject();
    }

    public string CreateUniqueObjectKey(string preferredName, IEnumerable<JavaObjectRepositoryEntry> existingEntries)
    {
        var seed = Sanitize(preferredName);
        if (string.IsNullOrWhiteSpace(seed)) seed = "java_object";
        var existing = new HashSet<string>(existingEntries.Select(x => x.ObjectKey), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(seed)) return seed;
        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{seed}_{i}";
            if (!existing.Contains(candidate)) return candidate;
        }
        return $"{seed}_{Guid.NewGuid():N}";
    }

    private static JavaRepositoryProperty Property(string name, string value, bool primary) => new()
    {
        Name = name,
        Value = value,
        IsPrimary = primary
    };

    private static LocatorSuggestion? TryDeserializeLocator(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<LocatorSuggestion>(json, JsonExportService.Options);
        }
        catch
        {
            return null;
        }
    }

    private static string Sanitize(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("__", StringComparison.Ordinal)) collapsed = collapsed.Replace("__", "_", StringComparison.Ordinal);
        return collapsed.Trim('_');
    }
}
