namespace JabInspector.Core.Models;

public sealed record JavaTableCellReference(
    AccessibleNode Node,
    int RowIndex,
    int ColumnIndex,
    string ColumnHeader);

public sealed record JavaTableRowReference(
    int RowIndex,
    AccessibleNode AnchorNode,
    IReadOnlyList<JavaTableCellReference> Cells,
    bool IsSynthetic);

public sealed record JavaTableContext(
    AccessibleNode Container,
    IReadOnlyList<JavaTableRowReference> Rows,
    IReadOnlyList<JavaTableCellReference> Cells,
    bool IsPopupLike,
    string PopupKind);
