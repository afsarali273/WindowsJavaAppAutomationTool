namespace JabInspector.Core.Models;

public sealed class JavaRecordedStep
{
    public int Sequence { get; set; }
    public string StepName { get; set; } = "";
    public JavaRecordedActionKind ActionKind { get; set; }
    public string ObjectKey { get; set; } = "";
    public string InputText { get; set; } = "";
    public DateTime CapturedAtUtc { get; set; }
    public string WindowHwndDisplay { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string WindowClassName { get; set; } = "";
    public int WindowProcessId { get; set; }
    public int WindowVmId { get; set; }
    public int? RecordedScreenX { get; set; }
    public int? RecordedScreenY { get; set; }
    public int? WindowOffsetX { get; set; }
    public int? WindowOffsetY { get; set; }
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
            var label = !string.IsNullOrWhiteSpace(ObjectName)
                ? ObjectName
                : !string.IsNullOrWhiteSpace(ObjectVirtualAccessibleName)
                    ? ObjectVirtualAccessibleName
                    : !string.IsNullOrWhiteSpace(ObjectDescription)
                        ? ObjectDescription
                        : ObjectKey;
            return string.IsNullOrWhiteSpace(ObjectRole) ? label : $"{ObjectRole}: {label}";
        }
    }

    public string DisplayName => $"{Sequence:00}. {ActionKind} -> {ObjectSummary}";
}
