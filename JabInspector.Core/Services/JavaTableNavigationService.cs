using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaTableNavigationService
{
    public JavaTableContext? BuildContext(AccessibleNode? seed)
    {
        var container = FindNearestTableContainer(seed);
        if (container is null) return null;

        var cells = Enumerate(container)
            .Where(node => node.IsTableLikeCell && node.TableLikeRowIndex >= 0 && node.TableLikeColumnIndex >= 0)
            .Select(node => new JavaTableCellReference(
                node,
                node.TableLikeRowIndex,
                node.TableLikeColumnIndex,
                string.IsNullOrWhiteSpace(node.TableLikeColumnHeader) ? $"Column{node.TableLikeColumnIndex + 1}" : node.TableLikeColumnHeader))
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)
            .ToList();

        if (cells.Count == 0) return null;

        var rows = BuildRows(container, cells);
        var popupKind = DetectPopupKind(container, cells);
        return new JavaTableContext(container, rows, cells, !string.IsNullOrWhiteSpace(popupKind), popupKind);
    }

    public AccessibleNode? FindNearestTableContainer(AccessibleNode? seed)
    {
        for (var current = seed; current is not null; current = current.Parent)
        {
            if (current.IsTableLikeContainer) return current;
        }

        return null;
    }

    public JavaTableCellReference? FindCellByPosition(AccessibleNode? seed, int rowIndex, int columnIndex)
    {
        var context = BuildContext(seed);
        return context?.Cells.FirstOrDefault(cell => cell.RowIndex == rowIndex && cell.ColumnIndex == columnIndex);
    }

    public JavaTableCellReference? FindCellByHeader(AccessibleNode? seed, int rowIndex, string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        var context = BuildContext(seed);
        return context?.Cells.FirstOrDefault(cell =>
            cell.RowIndex == rowIndex &&
            string.Equals(cell.ColumnHeader, header.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public JavaTableRowReference? FindRowByCellText(AccessibleNode? seed, string text, string? columnHeader = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var context = BuildContext(seed);
        if (context is null) return null;

        foreach (var row in context.Rows)
        {
            var cells = string.IsNullOrWhiteSpace(columnHeader)
                ? row.Cells
                : row.Cells.Where(cell => string.Equals(cell.ColumnHeader, columnHeader.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            if (cells.Any(cell => CellHasText(cell.Node, text)))
            {
                return row;
            }
        }

        return null;
    }

    public string BuildSummary(AccessibleNode? seed)
    {
        var context = BuildContext(seed);
        if (context is null) return "No inferred table or Oracle-style grid context was detected for the selected element.";

        var parts = new List<string>
        {
            $"{context.Container.TableLikeKind} container",
            $"{context.Rows.Count} row(s)",
            $"{context.Cells.Select(cell => cell.ColumnIndex).Distinct().Count()} column(s)"
        };

        if (context.IsPopupLike) parts.Add(context.PopupKind);
        if (seed?.IsTableLikeCell == true)
        {
            parts.Add($"selected cell: row {seed.TableLikeRowIndex}, column {seed.TableLikeColumnIndex}");
            if (!string.IsNullOrWhiteSpace(seed.TableLikeColumnHeader)) parts.Add($"header '{seed.TableLikeColumnHeader}'");
        }

        return string.Join(" | ", parts);
    }

    public string BuildDetails(AccessibleNode? seed)
    {
        var context = BuildContext(seed);
        if (context is null) return "";

        var headers = context.Cells
            .GroupBy(cell => cell.ColumnIndex)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.First().ColumnHeader}")
            .ToList();

        var syntheticRows = context.Rows.Count(row => row.IsSynthetic);
        var seedDetails = seed?.IsTableLikeCell == true
            ? $"Cell locator: row={seed.TableLikeRowIndex}, column={seed.TableLikeColumnIndex}, header={seed.TableLikeColumnHeader}"
            : seed?.IsTableLikeRow == true
                ? $"Row locator: row={seed.TableLikeRowIndex}, synthetic={context.Rows.FirstOrDefault(row => row.RowIndex == seed.TableLikeRowIndex)?.IsSynthetic}"
                : "";

        return string.Join(Environment.NewLine, new[]
        {
            $"Container path: {context.Container.Path}",
            $"Rows: {context.Rows.Count} (synthetic: {syntheticRows})",
            $"Columns: {string.Join(", ", headers)}",
            $"Popup-like container: {context.IsPopupLike} {(context.IsPopupLike ? $"({context.PopupKind})" : string.Empty)}".Trim(),
            seedDetails
        }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static IReadOnlyList<JavaTableRowReference> BuildRows(AccessibleNode container, IReadOnlyList<JavaTableCellReference> cells)
    {
        var actualRows = Enumerate(container)
            .Where(node => node.IsTableLikeRow && node.TableLikeRowIndex >= 0)
            .GroupBy(node => node.TableLikeRowIndex)
            .ToDictionary(group => group.Key, group => group.OrderBy(node => node.ObjectDepth).First());

        return cells
            .GroupBy(cell => cell.RowIndex)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var groupCells = group.OrderBy(cell => cell.ColumnIndex).ToList();
                var hasActualRow = actualRows.TryGetValue(group.Key, out var rowNode);
                return new JavaTableRowReference(
                    group.Key,
                    hasActualRow && rowNode is not null ? rowNode : groupCells[0].Node,
                    groupCells,
                    !hasActualRow);
            })
            .ToList();
    }

    private static bool CellHasText(AccessibleNode node, string text)
    {
        var candidateText = string.Join(" ", new[]
        {
            node.Name,
            node.VirtualAccessibleName,
            node.Description,
            node.TextPreview,
            node.TextSelected,
            node.TextWord,
            node.TextSentence,
            node.CurrentValue
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return candidateText.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectPopupKind(AccessibleNode container, IReadOnlyList<JavaTableCellReference> cells)
    {
        var role = $"{container.Role} {container.RoleEnUs}".Trim();
        var name = $"{container.Name} {container.Description} {container.VirtualAccessibleName}".Trim();
        if (name.Contains("list of values", StringComparison.OrdinalIgnoreCase) || name.Contains("lov", StringComparison.OrdinalIgnoreCase))
            return "LOV popup";
        if (role.Contains("dialog", StringComparison.OrdinalIgnoreCase) || role.Contains("internal frame", StringComparison.OrdinalIgnoreCase))
            return cells.Any(cell => cell.Node.AccessibleSelection) ? "dialog selection grid" : "dialog grid";
        if (role.Contains("list", StringComparison.OrdinalIgnoreCase))
            return "list popup";
        return "";
    }

    private static IEnumerable<AccessibleNode> Enumerate(AccessibleNode root)
    {
        var stack = new Stack<AccessibleNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            for (var index = current.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(current.Children[index]);
            }
        }
    }
}
