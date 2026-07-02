using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaTableInferenceService
{
    public void Annotate(AccessibleNode root)
    {
        foreach (var node in Enumerate(root))
        {
            TryAnnotate(node);
        }
    }

    private void TryAnnotate(AccessibleNode node)
    {
        var candidates = node.Children
            .Where(IsCellCandidate)
            .Where(child => child.HasValidBounds)
            .OrderBy(child => child.Y)
            .ThenBy(child => child.X)
            .ToList();

        if (candidates.Count < 4) return;

        var rowTolerance = CalculateTolerance(candidates.Select(x => x.Height));
        var columnTolerance = CalculateTolerance(candidates.Select(x => x.Width));

        var rows = Cluster(candidates, child => child.Y, rowTolerance);
        var columns = Cluster(candidates, child => child.X, columnTolerance);
        if (rows.Count < 2 || columns.Count < 2) return;

        var assignments = new List<CellAssignment>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var rowIndex = FindNearestIndex(rows, candidate.Y);
            var columnIndex = FindNearestIndex(columns, candidate.X);
            if (rowIndex < 0 || columnIndex < 0) continue;
            assignments.Add(new CellAssignment(candidate, rowIndex, columnIndex));
        }

        if (assignments.Count < 4) return;

        var occupiedCells = assignments
            .Select(x => (x.RowIndex, x.ColumnIndex))
            .Distinct()
            .Count();
        var coverage = occupiedCells / (double)(rows.Count * columns.Count);
        var enoughCoverage = coverage >= 0.35d || assignments.Count >= Math.Max(6, rows.Count + columns.Count);
        if (!enoughCoverage) return;

        node.IsTableLikeContainer = true;
        node.TableLikeKind = node.AccessibleTable || IsTableRole(node.Role, node.RoleEnUs) ? "AccessibleTable" : "InferredGrid";
        node.TableLikeContainerPath = node.Path;
        node.TableLikeRowCount = rows.Count;
        node.TableLikeColumnCount = columns.Count;

        foreach (var assignment in assignments)
        {
            assignment.Node.IsTableLikeCell = true;
            assignment.Node.TableLikeKind = "Cell";
            assignment.Node.TableLikeContainerPath = node.Path;
            assignment.Node.TableLikeRowIndex = assignment.RowIndex;
            assignment.Node.TableLikeColumnIndex = assignment.ColumnIndex;
            assignment.Node.TableLikeRowCount = rows.Count;
            assignment.Node.TableLikeColumnCount = columns.Count;
            assignment.Node.TableLikeColumnHeader = ResolveColumnHeader(columns[assignment.ColumnIndex], assignment.ColumnIndex, candidates);
        }

        foreach (var rowIndex in assignments.Select(x => x.RowIndex).Distinct())
        {
            var rowNodes = assignments.Where(x => x.RowIndex == rowIndex).Select(x => x.Node).ToList();
            var rowContainer = rowNodes
                .SelectMany(GetAncestors)
                .FirstOrDefault(ancestor => ancestor != node
                                            && ancestor.Parent == node
                                            && ancestor.HasValidBounds
                                            && Math.Abs(ancestor.Y - rows[rowIndex]) <= rowTolerance * 2);
            if (rowContainer is null) continue;
            rowContainer.IsTableLikeRow = true;
            rowContainer.TableLikeKind = "Row";
            rowContainer.TableLikeContainerPath = node.Path;
            rowContainer.TableLikeRowIndex = rowIndex;
            rowContainer.TableLikeRowCount = rows.Count;
            rowContainer.TableLikeColumnCount = columns.Count;
        }
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

    private static IEnumerable<AccessibleNode> GetAncestors(AccessibleNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    private static bool IsCellCandidate(AccessibleNode node)
    {
        var role = NormalizeRole(node.RoleEnUs, node.Role);
        if (string.IsNullOrWhiteSpace(role)) return false;
        if (role.Contains("scroll bar", StringComparison.OrdinalIgnoreCase)
            || role.Contains("viewport", StringComparison.OrdinalIgnoreCase)
            || role.Contains("separator", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return node.AccessibleText
               || node.AccessibleValue
               || node.AccessibleSelection
               || node.AccessibleAction
               || role.Contains("text", StringComparison.OrdinalIgnoreCase)
               || role.Contains("label", StringComparison.OrdinalIgnoreCase)
               || role.Contains("check box", StringComparison.OrdinalIgnoreCase)
               || role.Contains("radio button", StringComparison.OrdinalIgnoreCase)
               || role.Contains("combo box", StringComparison.OrdinalIgnoreCase)
               || role.Contains("list", StringComparison.OrdinalIgnoreCase)
               || role.Contains("table", StringComparison.OrdinalIgnoreCase)
               || role.Contains("cell", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTableRole(string role, string roleEnUs)
    {
        var value = NormalizeRole(roleEnUs, role);
        return value.Contains("table", StringComparison.OrdinalIgnoreCase)
               || value.Contains("grid", StringComparison.OrdinalIgnoreCase)
               || value.Contains("row header", StringComparison.OrdinalIgnoreCase)
               || value.Contains("column header", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRole(string? roleEnUs, string? role)
    {
        return string.IsNullOrWhiteSpace(roleEnUs) ? (role ?? "") : roleEnUs.Trim();
    }

    private static int CalculateTolerance(IEnumerable<int> values)
    {
        var filtered = values.Where(x => x > 0).OrderBy(x => x).ToArray();
        if (filtered.Length == 0) return 8;
        var median = filtered[filtered.Length / 2];
        return Math.Clamp(median / 2, 6, 24);
    }

    private static List<int> Cluster(IEnumerable<AccessibleNode> nodes, Func<AccessibleNode, int> selector, int tolerance)
    {
        var clusters = new List<int>();
        foreach (var value in nodes.Select(selector).OrderBy(x => x))
        {
            if (clusters.Count == 0 || Math.Abs(value - clusters[^1]) > tolerance)
            {
                clusters.Add(value);
                continue;
            }

            clusters[^1] = (clusters[^1] + value) / 2;
        }

        return clusters;
    }

    private static int FindNearestIndex(IReadOnlyList<int> anchors, int value)
    {
        if (anchors.Count == 0) return -1;
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var index = 0; index < anchors.Count; index++)
        {
            var distance = Math.Abs(anchors[index] - value);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestIndex = index;
        }

        return bestIndex;
    }

    private static string ResolveColumnHeader(int anchorX, int columnIndex, IReadOnlyList<AccessibleNode> candidates)
    {
        var header = candidates
            .Where(node => !string.IsNullOrWhiteSpace(node.Name) || !string.IsNullOrWhiteSpace(node.TextPreview))
            .Where(node => Math.Abs(node.X - anchorX) <= Math.Max(16, node.Width / 2))
            .OrderBy(node => node.Y)
            .ThenBy(node => node.ObjectDepth)
            .FirstOrDefault();

        var text = header?.Name;
        if (string.IsNullOrWhiteSpace(text)) text = header?.TextPreview;
        return string.IsNullOrWhiteSpace(text) ? $"Column{columnIndex + 1}" : text.Trim();
    }

    private sealed record CellAssignment(AccessibleNode Node, int RowIndex, int ColumnIndex);
}
