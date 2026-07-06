using System.Text.Json.Serialization;
using System.Collections.ObjectModel;

namespace JabInspector.Core.Models;

public sealed class AccessibleNode
{
    public int VmId { get; set; }
    public long Context { get; set; }
    public string Name { get; set; } = "";
    public string VirtualAccessibleName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Role { get; set; } = "unknown";
    public string RoleEnUs { get; set; } = "";
    public string States { get; set; } = "";
    public string StatesEnUs { get; set; } = "";
    public int IndexInParent { get; set; }
    public int ObjectDepth { get; set; } = -1;
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
    public bool IsTableLikeContainer { get; set; }
    public bool IsTableLikeRow { get; set; }
    public bool IsTableLikeCell { get; set; }
    public string TableLikeKind { get; set; } = "";
    public string TableLikeContainerPath { get; set; } = "";
    public string TableLikeColumnHeader { get; set; } = "";
    public int TableLikeRowIndex { get; set; } = -1;
    public int TableLikeColumnIndex { get; set; } = -1;
    public int TableLikeRowCount { get; set; } = -1;
    public int TableLikeColumnCount { get; set; } = -1;
    public bool IsFormsLikeScope { get; set; }
    public bool IsFormsViewportLikeContainer { get; set; }
    public string FormsScopePath { get; set; } = "";
    public string FormsScopeRole { get; set; } = "";
    public string FormsScopeName { get; set; } = "";
    public string FormsViewportPath { get; set; } = "";
    public string FormsViewportRole { get; set; } = "";
    public string FormsViewportName { get; set; } = "";
    public List<string> ActionNames { get; set; } = [];
    public string TextPreview { get; set; } = "";
    public string TextPreviewSource { get; set; } = "";
    public int TextCharCount { get; set; } = -1;
    public int TextCaretIndex { get; set; } = -1;
    public int TextIndexAtPoint { get; set; } = -1;
    public string TextLetter { get; set; } = "";
    public string TextSelected { get; set; } = "";
    public int TextSelectionStartIndex { get; set; } = -1;
    public int TextSelectionEndIndex { get; set; } = -1;
    public string TextWord { get; set; } = "";
    public string TextSentence { get; set; } = "";
    public string CurrentValue { get; set; } = "";
    public string MinimumValue { get; set; } = "";
    public string MaximumValue { get; set; } = "";
    public ObservableCollection<AccessibleNode> Children { get; set; } = [];
    [JsonIgnore] public AccessibleNode? Parent { get; set; }
    public string Path { get; set; } = "";
    public bool HasManagedDescendantAncestor { get; set; }
    [JsonIgnore] public bool ManagesDescendants => States.Contains("manages descendants", StringComparison.OrdinalIgnoreCase) ||
                                                   StatesEnUs.Contains("manages descendants", StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public string DisplayName => $"{Role}: {(string.IsNullOrWhiteSpace(Name) ? "(no name)" : Name)}";
    [JsonIgnore] public bool HasValidBounds => Width > 0 && Height > 0;
}
