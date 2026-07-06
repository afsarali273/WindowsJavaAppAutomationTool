namespace JabInspector.Core.Models;

public sealed class JavaObjectRepositoryEntry
{
    public string ObjectKey { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public DateTime CapturedAtUtc { get; set; }
    public string WindowKey { get; set; } = "";
    public string WindowHwndDisplay { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string WindowClassName { get; set; } = "";
    public int WindowProcessId { get; set; }
    public int WindowVmId { get; set; }
    public string Engine { get; set; } = "java-access-bridge";
    public LocatorSuggestion? Locator { get; set; }
    public string LocatorJson { get; set; } = "";
    public string Role { get; set; } = "";
    public string RoleEnUs { get; set; } = "";
    public string Name { get; set; } = "";
    public string VirtualAccessibleName { get; set; } = "";
    public string Description { get; set; } = "";
    public string States { get; set; } = "";
    public string StatesEnUs { get; set; } = "";
    public string Path { get; set; } = "";
    public string IndexPath { get; set; } = "";
    public string XPath { get; set; } = "";
    public string IndexXPath { get; set; } = "";
    public string SemanticXPath { get; set; } = "";
    public string ParentRole { get; set; } = "";
    public string ParentName { get; set; } = "";
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
    public bool HasManagedDescendantAncestor { get; set; }
    public List<string> ActionNames { get; set; } = [];
    public List<JavaRepositoryProperty> Properties { get; set; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(FriendlyName) ? ObjectKey : $"{FriendlyName} ({ObjectKey})";
}
