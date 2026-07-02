using System.Collections.ObjectModel;
using System.Drawing;

namespace WinInspector.Core.Models;

public sealed class WindowsAutomationNode
{
    public WindowsAutomationBackendKind BackendKind { get; init; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public string Value { get; set; } = "";
    public Rectangle Bounds { get; set; }
    public Rectangle ClientBounds { get; set; }
    public IntPtr NativeHandle { get; set; }
    public int ControlId { get; set; } = -1;
    public uint ProcessId { get; set; }
    public uint ThreadId { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public long Style { get; set; }
    public long ExtendedStyle { get; set; }
    public int IndexInParent { get; set; } = -1;
    public WindowsAutomationNode? Parent { get; set; }
    public ObservableCollection<WindowsAutomationNode> Children { get; } = [];
    public Dictionary<string, string> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"({Role})" : $"{Name} ({Role})";
}
