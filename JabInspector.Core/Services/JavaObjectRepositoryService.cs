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
            LocatorJson = JsonExportService.Serialize(locator),
            Role = node.Role,
            RoleEnUs = node.RoleEnUs,
            Name = node.Name,
            VirtualAccessibleName = node.VirtualAccessibleName,
            Description = node.Description,
            States = node.States,
            StatesEnUs = node.StatesEnUs,
            Path = string.IsNullOrWhiteSpace(node.Path) ? LocatorGenerator.BuildPath(node) : node.Path,
            IndexPath = locator.IndexPath,
            XPath = locator.XPath,
            IndexXPath = locator.IndexXPath,
            SemanticXPath = locator.SemanticXPath,
            ParentRole = node.Parent?.Role ?? "",
            ParentName = node.Parent?.Name ?? "",
            IndexInParent = node.IndexInParent,
            ObjectDepth = node.ObjectDepth,
            ChildrenCount = node.ChildrenCount,
            X = node.X,
            Y = node.Y,
            Width = node.Width,
            Height = node.Height,
            AccessibleComponent = node.AccessibleComponent,
            AccessibleAction = node.AccessibleAction,
            AccessibleSelection = node.AccessibleSelection,
            AccessibleText = node.AccessibleText,
            AccessibleValue = node.AccessibleValue,
            AccessibleTable = node.AccessibleTable,
            AccessibleInterfaces = node.AccessibleInterfaces,
            HasManagedDescendantAncestor = node.HasManagedDescendantAncestor,
            ActionNames = node.ActionNames.ToList()
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
            Property("actions", string.Join(",", entry.ActionNames), false)
        ];

        return entry;
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
        if (!string.IsNullOrWhiteSpace(step.ObjectRole)) builder.AppendLine($"object.role={step.ObjectRole}");
        if (!string.IsNullOrWhiteSpace(step.ObjectName)) builder.AppendLine($"object.name={step.ObjectName}");
        if (!string.IsNullOrWhiteSpace(step.ObjectVirtualAccessibleName)) builder.AppendLine($"object.virtualAccessibleName={step.ObjectVirtualAccessibleName}");
        if (!string.IsNullOrWhiteSpace(step.ObjectDescription)) builder.AppendLine($"object.description={step.ObjectDescription}");
        if (!string.IsNullOrWhiteSpace(step.ObjectPath)) builder.AppendLine($"object.path={step.ObjectPath}");
        if (step.ObjectDepth >= 0) builder.AppendLine($"object.depth={step.ObjectDepth}");
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

    private static string Sanitize(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("__", StringComparison.Ordinal)) collapsed = collapsed.Replace("__", "_", StringComparison.Ordinal);
        return collapsed.Trim('_');
    }
}
