using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaNodeResolverService
{
    public AccessibleNode? Resolve(AccessibleNode root, JavaObjectRepositoryEntry entry, JavaRecordedStep? step = null)
    {
        var nodes = Enumerate(root).ToList();

        var exactIndexPath = TryResolveByAbsoluteIndexPath(root, entry);
        if (exactIndexPath is not null && Score(exactIndexPath, entry, step) >= 40) return exactIndexPath;

        var exactPath = nodes
            .Where(node => EqualsNormalized(node.Path, entry.Path))
            .OrderByDescending(node => Score(node, entry, step))
            .FirstOrDefault();
        if (exactPath is not null && Score(exactPath, entry, step) >= 55) return exactPath;

        var indexedWeak = TryResolveByIndexedPath(root, entry);
        if (indexedWeak is not null && Score(indexedWeak, entry, step) >= 52) return indexedWeak;

        var ranked = nodes
            .Select(node => (Node: node, Score: Score(node, entry, step)))
            .OrderByDescending(candidate => candidate.Score)
            .Take(2)
            .ToList();

        if (ranked.Count == 0) return null;

        var best = ranked[0];
        if (best.Score < 52) return null;

        if (ranked.Count > 1)
        {
            var second = ranked[1];
            var ambiguous = best.Score - second.Score < 10;
            if (ambiguous && !HasStrongDiscriminatorMatch(best.Node, entry, step)) return null;
        }

        return best.Node;
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

    private static int Score(AccessibleNode node, JavaObjectRepositoryEntry entry, JavaRecordedStep? step)
    {
        var score = 0;

        if (EqualsNormalized(node.Path, entry.Path)) score += 70;
        else if (!string.IsNullOrWhiteSpace(entry.Path)) score -= 14;
        if (EqualsNormalized(LocatorGenerator.BuildIndexPath(node), entry.IndexPath)) score += 64;
        else if (!string.IsNullOrWhiteSpace(entry.IndexPath)) score -= 12;
        if (EqualsNormalized(node.RoleEnUs, entry.RoleEnUs)) score += 18;
        else if (EqualsNormalized(node.Role, entry.Role)) score += 16;
        else if (!string.IsNullOrWhiteSpace(entry.RoleEnUs) || !string.IsNullOrWhiteSpace(entry.Role)) score -= 20;
        if (EqualsNormalized(node.Name, entry.Name)) score += 22;
        else if (!string.IsNullOrWhiteSpace(entry.Name)) score -= 16;
        if (EqualsNormalized(node.VirtualAccessibleName, entry.VirtualAccessibleName)) score += 20;
        else if (!string.IsNullOrWhiteSpace(entry.VirtualAccessibleName)) score -= 14;
        if (EqualsNormalized(node.Description, entry.Description)) score += 10;
        else if (!string.IsNullOrWhiteSpace(entry.Description)) score -= 5;
        if (EqualsNormalized(node.Parent?.Role, entry.ParentRole)) score += 8;
        if (EqualsNormalized(node.Parent?.Name, entry.ParentName)) score += 10;
        else if (!string.IsNullOrWhiteSpace(entry.ParentName)) score -= 6;
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

        score += ScoreStepContext(node, step);

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
            if (next is null && i < segments.Length - 1)
            {
                next = matchingRoleSiblings.FirstOrDefault();
            }
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

    private static int ScoreStepContext(AccessibleNode node, JavaRecordedStep? step)
    {
        if (step is null) return 0;

        var score = 0;
        var locator = step.ObjectLocator;
        var stepPath = locator?.Path ?? step.ObjectPath;
        if (EqualsNormalized(node.Path, stepPath)) score += 34;
        else if (!string.IsNullOrWhiteSpace(stepPath)) score -= 8;

        if (locator is not null)
        {
            if (EqualsNormalized(LocatorGenerator.BuildIndexPath(node), locator.IndexPath)) score += 26;
            else if (!string.IsNullOrWhiteSpace(locator.IndexPath)) score -= 6;

            if (EqualsNormalized(LocatorGenerator.BuildXPath(node), locator.XPath)) score += 20;
            if (EqualsNormalized(LocatorGenerator.BuildIndexXPath(node), locator.IndexXPath)) score += 18;
            if (EqualsNormalized(node.RoleEnUs, locator.RoleEnUs)) score += 8;
            else if (EqualsNormalized(node.Role, locator.Role)) score += 6;
            if (EqualsNormalized(node.Parent?.Role, locator.ParentRole)) score += 8;
            if (EqualsNormalized(node.Parent?.Name, locator.ParentName)) score += 10;
            if (node.IndexInParent == locator.IndexInParent) score += 8;
            if (node.ChildrenCount == locator.ChildrenCount) score += 3;
            if (EqualsNormalized(node.StatesEnUs, locator.StatesEnUs)) score += 4;
            else if (EqualsNormalized(node.States, locator.States)) score += 3;

            var locatorBoundsDistance = BoundsDistance(node, locator.Bounds);
            if (locatorBoundsDistance == 0 && locator.Bounds.Width > 0 && locator.Bounds.Height > 0) score += 8;
            else if (locatorBoundsDistance <= 12) score += 5;
            else if (locatorBoundsDistance <= 40) score += 2;
        }

        if (EqualsNormalized(node.Name, locator?.Name ?? step.ObjectName)) score += 18;
        if (EqualsNormalized(node.VirtualAccessibleName, locator?.VirtualAccessibleName ?? step.ObjectVirtualAccessibleName)) score += 14;
        if (EqualsNormalized(node.Description, locator?.Description ?? step.ObjectDescription)) score += 6;

        var depth = locator?.ObjectDepth ?? step.ObjectDepth;
        if (depth >= 0 && node.ObjectDepth == depth) score += 6;

        if (step.RecordedScreenX.HasValue && step.RecordedScreenY.HasValue && node.Width > 0 && node.Height > 0)
        {
            var px = step.RecordedScreenX.Value;
            var py = step.RecordedScreenY.Value;
            var containsPoint = px >= node.X && px < node.X + node.Width && py >= node.Y && py < node.Y + node.Height;
            if (containsPoint) score += 22;
            else
            {
                var cx = node.X + node.Width / 2;
                var cy = node.Y + node.Height / 2;
                var distance = Math.Abs(cx - px) + Math.Abs(cy - py);
                if (distance <= 40) score += 8;
                else if (distance <= 120) score += 2;
                else score -= 10;
            }
        }

        return score;
    }

    private static bool HasStrongDiscriminatorMatch(AccessibleNode node, JavaObjectRepositoryEntry entry, JavaRecordedStep? step)
    {
        if (EqualsNormalized(node.Path, entry.Path)) return true;
        if (!string.IsNullOrWhiteSpace(entry.IndexPath) && EqualsNormalized(LocatorGenerator.BuildIndexPath(node), entry.IndexPath)) return true;
        if (step is not null && !string.IsNullOrWhiteSpace(step.ObjectPath) && EqualsNormalized(node.Path, step.ObjectPath)) return true;
        if (step?.ObjectLocator is { } locator)
        {
            if (!string.IsNullOrWhiteSpace(locator.Path) && EqualsNormalized(node.Path, locator.Path)) return true;
            if (!string.IsNullOrWhiteSpace(locator.IndexPath) && EqualsNormalized(LocatorGenerator.BuildIndexPath(node), locator.IndexPath)) return true;
            if (!string.IsNullOrWhiteSpace(locator.XPath) && EqualsNormalized(LocatorGenerator.BuildXPath(node), locator.XPath)) return true;
            if (!string.IsNullOrWhiteSpace(locator.IndexXPath) && EqualsNormalized(LocatorGenerator.BuildIndexXPath(node), locator.IndexXPath)) return true;
        }

        var hasIdentity = EqualsNormalized(node.Name, entry.Name) || EqualsNormalized(node.VirtualAccessibleName, entry.VirtualAccessibleName);
        var hasParent = EqualsNormalized(node.Parent?.Name, entry.ParentName) || EqualsNormalized(node.Parent?.Role, entry.ParentRole);
        return hasIdentity && hasParent;
    }

    private static int BoundsDistance(AccessibleNode node, ElementBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || node.Width <= 0 || node.Height <= 0) return int.MaxValue;
        var dx = Math.Abs(node.X - bounds.X);
        var dy = Math.Abs(node.Y - bounds.Y);
        var dw = Math.Abs(node.Width - bounds.Width);
        var dh = Math.Abs(node.Height - bounds.Height);
        return dx + dy + dw + dh;
    }

    private static bool EqualsNormalized(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
