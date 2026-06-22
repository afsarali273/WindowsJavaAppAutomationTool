using System.Text.Json.Serialization;
using System.Collections.ObjectModel;

namespace JabInspector.Core.Models;

public sealed class AccessibleNode
{
    public int VmId { get; set; }
    public long Context { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Role { get; set; } = "unknown";
    public string RoleEnUs { get; set; } = "";
    public string States { get; set; } = "";
    public string StatesEnUs { get; set; } = "";
    public int IndexInParent { get; set; }
    public int ChildrenCount { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool AccessibleComponent { get; set; }
    public bool AccessibleAction { get; set; }
    public bool AccessibleSelection { get; set; }
    public bool AccessibleText { get; set; }
    public bool AccessibleValue { get; set; }
    public bool AccessibleTable { get; set; }
    public bool AccessibleInterfaces { get; set; }
    public ObservableCollection<AccessibleNode> Children { get; set; } = [];
    [JsonIgnore] public AccessibleNode? Parent { get; set; }
    public string Path { get; set; } = "";
    [JsonIgnore] public string DisplayName => $"{Role}: {(string.IsNullOrWhiteSpace(Name) ? "(no name)" : Name)}";
    [JsonIgnore] public bool HasValidBounds => Width > 0 && Height > 0;
}
