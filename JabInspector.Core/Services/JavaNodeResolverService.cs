using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaNodeResolverService
{
    public AccessibleNode? Resolve(AccessibleNode root, JavaObjectRepositoryEntry entry)
    {
        var nodes = Enumerate(root).ToList();

        var exactIndexPath = TryResolveByAbsoluteIndexPath(root, entry);
        if (exactIndexPath is not null && Score(exactIndexPath, entry) >= 40) return exactIndexPath;

        var exactPath = nodes
            .Where(node => EqualsNormalized(node.Path, entry.Path))
            .OrderByDescending(node => Score(node, entry))
            .FirstOrDefault();
        if (exactPath is not null && Score(exactPath, entry) >= 55) return exactPath;

        var indexedWeak = TryResolveByIndexedPath(root, entry);
        if (indexedWeak is not null && Score(indexedWeak, entry) >= 45) return indexedWeak;

        var bestNode = default(AccessibleNode);
        var bestScore = int.MinValue;

        foreach (var node in nodes)
        {
            var score = Score(node, entry);
            if (score <= bestScore) continue;
            bestScore = score;
            bestNode = node;
        }

        return bestScore >= 42 ? bestNode : null;
    }

    private static IEnumerable<AccessibleNode> Enumerate(AccessibleNode root)
    {
        var stack = new Stack<AccessibleNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            for (var i = current.Children.Count - 1; i >= 0; i--) stack.Push(current.Children[i]);
        }
    }

    private static int Score(AccessibleNode node, JavaObjectRepositoryEntry entry)
    {
        var score = 0;

        if (EqualsNormalized(node.Path, entry.Path)) score += 70;
        if (EqualsNormalized(LocatorGenerator.BuildIndexPath(node), entry.IndexPath)) score += 64;
        if (EqualsNormalized(node.RoleEnUs, entry.RoleEnUs)) score += 18;
        else if (EqualsNormalized(node.Role, entry.Role)) score += 16;
        if (EqualsNormalized(node.Name, entry.Name)) score += 22;
        if (EqualsNormalized(node.VirtualAccessibleName, entry.VirtualAccessibleName)) score += 20;
        if (EqualsNormalized(node.Description, entry.Description)) score += 10;
        if (EqualsNormalized(node.Parent?.Role, entry.ParentRole)) score += 8;
        if (EqualsNormalized(node.Parent?.Name, entry.ParentName)) score += 10;
        if (node.IndexInParent == entry.IndexInParent) score += 5;
        if (entry.ObjectDepth >= 0 && node.ObjectDepth == entry.ObjectDepth) score += 6;
        if (node.ChildrenCount == entry.ChildrenCount) score += 2;
        if (node.AccessibleAction == entry.AccessibleAction) score += 3;
        if (node.AccessibleText == entry.AccessibleText) score += 3;
        if (node.AccessibleValue == entry.AccessibleValue) score += 3;
        if (node.AccessibleSelection == entry.AccessibleSelection) score += 2;
        if (node.AccessibleComponent == entry.AccessibleComponent) score += 2;
        if (node.HasManagedDescendantAncestor == entry.HasManagedDescendantAncestor) score += 2;
        if (entry.ActionNames.Count > 0 && node.ActionNames.Count > 0)
        {
            var overlap = node.ActionNames.Count(action => entry.ActionNames.Any(recorded => EqualsNormalized(action, recorded)));
            score += Math.Min(10, overlap * 3);
        }

        var boundsDistance = BoundsDistance(node, entry);
        if (boundsDistance == 0 && entry.Width > 0 && entry.Height > 0) score += 8;
        else if (boundsDistance <= 12) score += 5;
        else if (boundsDistance <= 40) score += 2;

        return score;
    }

    private static AccessibleNode? TryResolveByIndexedPath(AccessibleNode root, JavaObjectRepositoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Path)) return null;
        var cursor = root;
        var segments = entry.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        var startIndex = SegmentMatches(cursor, segments[0]) ? 1 : 0;
        for (var i = startIndex; i < segments.Length; i++)
        {
            var segment = ParseSegment(segments[i]);
            if (segment is null) return null;

            AccessibleNode? next = null;
            var matchingRoleSiblings = cursor.Children
                .Where(child => RoleMatches(child, segment.Value.Role))
                .ToList();

            if (segment.Value.Index >= 0 && segment.Value.Index < matchingRoleSiblings.Count)
            {
                var indexed = matchingRoleSiblings[segment.Value.Index];
                if (WeakMatches(indexed, entry) || i < segments.Length - 1) next = indexed;
            }

            next ??= matchingRoleSiblings.FirstOrDefault(child => WeakMatches(child, entry));
            next ??= matchingRoleSiblings.FirstOrDefault();
            if (next is null) return null;
            cursor = next;
        }

        return cursor;
    }

    private static AccessibleNode? TryResolveByAbsoluteIndexPath(AccessibleNode root, JavaObjectRepositoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.IndexPath)) return null;
        var parts = entry.IndexPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var cursor = root;
        var startIndex = 0;
        if (int.TryParse(parts[0], out var rootIndex) && rootIndex == Math.Max(root.IndexInParent, 0))
            startIndex = 1;

        for (var i = startIndex; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var childIndex) || childIndex < 0 || childIndex >= cursor.Children.Count)
                return null;
            cursor = cursor.Children[childIndex];
        }

        return cursor;
    }

    private static bool SegmentMatches(AccessibleNode node, string segment)
    {
        var parsed = ParseSegment(segment);
        return parsed is not null && RoleMatches(node, parsed.Value.Role);
    }

    private static (string Role, int Index)? ParseSegment(string segment)
    {
        var open = segment.LastIndexOf("[", StringComparison.Ordinal);
        var close = segment.LastIndexOf("]", StringComparison.Ordinal);
        if (open <= 0 || close <= open) return null;
        var role = segment[..open].Trim();
        return int.TryParse(segment[(open + 1)..close], out var index) ? (role, index) : null;
    }

    private static bool RoleMatches(AccessibleNode node, string role) =>
        EqualsNormalized(node.RoleEnUs, role) || EqualsNormalized(node.Role, role);

    private static bool WeakMatches(AccessibleNode node, JavaObjectRepositoryEntry entry)
    {
        if (!RoleMatches(node, string.IsNullOrWhiteSpace(entry.RoleEnUs) ? entry.Role : entry.RoleEnUs)) return false;
        return EqualsNormalized(node.Name, entry.Name) ||
               EqualsNormalized(node.VirtualAccessibleName, entry.VirtualAccessibleName) ||
               EqualsNormalized(node.Description, entry.Description) ||
               node.IndexInParent == entry.IndexInParent;
    }

    private static int BoundsDistance(AccessibleNode node, JavaObjectRepositoryEntry entry)
    {
        if (entry.Width <= 0 || entry.Height <= 0 || node.Width <= 0 || node.Height <= 0) return int.MaxValue;
        var dx = Math.Abs(node.X - entry.X);
        var dy = Math.Abs(node.Y - entry.Y);
        var dw = Math.Abs(node.Width - entry.Width);
        var dh = Math.Abs(node.Height - entry.Height);
        return dx + dy + dw + dh;
    }

    private static bool EqualsNormalized(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
