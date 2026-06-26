namespace JabInspector.Core.Models;

public sealed class JavaRecordedStep
{
    public int Sequence { get; set; }
    public string StepName { get; set; } = "";
    public JavaRecordedActionKind ActionKind { get; set; }
    public string ObjectKey { get; set; } = "";
    public string InputText { get; set; } = "";
    public DateTime CapturedAtUtc { get; set; }
    public string WindowKey { get; set; } = "";
    public string WindowHwndDisplay { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string WindowClassName { get; set; } = "";
    public int WindowProcessId { get; set; }
    public int WindowVmId { get; set; }
    public int? RecordedScreenX { get; set; }
    public int? RecordedScreenY { get; set; }
    public int? WindowOffsetX { get; set; }
    public int? WindowOffsetY { get; set; }
    public LocatorSuggestion? ObjectLocator { get; set; }
    public string ObjectLocatorJson { get; set; } = "";
    public string ObjectRole { get; set; } = "";
    public string ObjectName { get; set; } = "";
    public string ObjectVirtualAccessibleName { get; set; } = "";
    public string ObjectDescription { get; set; } = "";
    public string ObjectPath { get; set; } = "";
    public int ObjectDepth { get; set; } = -1;
    public string ObjectSummary
    {
        get
        {
            var role = !string.IsNullOrWhiteSpace(ObjectLocator?.Role)
                ? ObjectLocator.Role
                : ObjectRole;
            var label = !string.IsNullOrWhiteSpace(ObjectLocator?.Name)
                ? ObjectLocator.Name
                : !string.IsNullOrWhiteSpace(ObjectName)
                ? ObjectName
                : !string.IsNullOrWhiteSpace(ObjectLocator?.VirtualAccessibleName)
                    ? ObjectLocator.VirtualAccessibleName
                    : !string.IsNullOrWhiteSpace(ObjectVirtualAccessibleName)
                    ? ObjectVirtualAccessibleName
                    : !string.IsNullOrWhiteSpace(ObjectLocator?.Description)
                        ? ObjectLocator.Description
                        : !string.IsNullOrWhiteSpace(ObjectDescription)
                        ? ObjectDescription
                        : ObjectKey;
            return string.IsNullOrWhiteSpace(role) ? label : $"{role}: {label}";
        }
    }

    public string DisplayName => $"{Sequence:00}. {ActionKind} -> {ObjectSummary}";
}
