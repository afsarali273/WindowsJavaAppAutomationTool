using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaNodeResolverService
{
    public AccessibleNode? Resolve(AccessibleNode root, JavaObjectRepositoryEntry entry, JavaRecordedStep? step = null)
    {
        return ResolveDetailed(root, entry, step).Node;
    }

    public ResolutionResult ResolveDetailed(
        AccessibleNode root,
        JavaObjectRepositoryEntry entry,
        JavaRecordedStep? step = null,
        ResolutionPolicy? policy = null)
    {
        var nodes = Enumerate(root).ToList();
        var locator = step?.ObjectLocator ?? entry.Locator;
        policy ??= new ResolutionPolicy();

        var uniqueLocatorMatch = TryResolveByUniqueLocator(nodes, entry, locator, out var uniqueStrategy, out var uniqueMatches);
        if (uniqueLocatorMatch is not null)
            return ResolutionResult.Found(uniqueLocatorMatch, uniqueStrategy, BuildCandidates(uniqueMatches, entry, step, locator, policy));

        var exactIndexPath = TryResolveByAbsoluteIndexPath(root, entry);
        if (exactIndexPath is not null &&
            Score(exactIndexPath, entry, step) >= 70 &&
            HasStableIdentityMatch(exactIndexPath, entry, locator))
        {
            return ResolutionResult.Found(
                exactIndexPath,
                "absolute-index-path",
                BuildCandidates([exactIndexPath], entry, step, locator, policy));
        }

        var exactPath = nodes
            .Where(node => EqualsNormalized(node.Path, entry.Path))
            .OrderByDescending(node => Score(node, entry, step))
            .FirstOrDefault();
        if (exactPath is not null &&
            Score(exactPath, entry, step) >= 70 &&
            HasStableIdentityMatch(exactPath, entry, locator))
        {
            return ResolutionResult.Found(
                exactPath,
                "exact-path",
                BuildCandidates([exactPath], entry, step, locator, policy));
        }

        var indexedWeak = TryResolveByIndexedPath(root, entry);
        if (indexedWeak is not null &&
            Score(indexedWeak, entry, step) >= 72 &&
            HasStableIdentityMatch(indexedWeak, entry, locator))
        {
            return ResolutionResult.Found(
                indexedWeak,
                "indexed-role-path",
                BuildCandidates([indexedWeak], entry, step, locator, policy));
        }

        var ranked = nodes
            .Select(node => (Node: node, Score: Score(node, entry, step)))
            .OrderByDescending(candidate => candidate.Score)
            .Take(Math.Max(2, policy.MaxCandidates))
            .ToList();

        if (ranked.Count == 0)
            return ResolutionResult.NotFound("No Java accessibility nodes were available to resolve against.", []);

        var best = ranked[0];
        var candidates = ranked.Select(x => ToCandidate(x.Node, x.Score, entry, locator)).ToList();
        if (best.Score < policy.MinimumScore)
            return ResolutionResult.NotFound(
                $"Could not resolve '{entry.ObjectKey}'. Best score {best.Score} is below required score {policy.MinimumScore}.",
                candidates);

        if (ranked.Count > 1)
        {
            var second = ranked[1];
            var ambiguous = best.Score - second.Score < policy.AmbiguityScoreDelta;
            if (ambiguous && policy.RequireUnique && !HasStrongDiscriminatorMatch(best.Node, entry, step))
            {
                return ResolutionResult.Ambiguous(
                    $"Resolution for '{entry.ObjectKey}' is ambiguous. Best score {best.Score}, second score {second.Score}.",
                    "ranked-fallback",
                    candidates);
            }
        }

        return ResolutionResult.Found(best.Node, "ranked-fallback", candidates);
    }

    private static AccessibleNode? TryResolveByUniqueLocator(
        IReadOnlyList<AccessibleNode> nodes,
        JavaObjectRepositoryEntry entry,
        LocatorSuggestion? locator,
        out string strategyName,
        out IReadOnlyList<AccessibleNode> strategyMatches)
    {
        var attempts = new (string Name, Func<AccessibleNode, bool> Match)[]
        {
            ("strict-index-path", node => MatchesExactIndexPath(node, entry, locator) && HasFullIdentityMatch(node, entry, locator)),
            ("strict-xpath", node => MatchesExactXPath(node, locator) && HasFullIdentityMatch(node, entry, locator)),
            ("strict-role-path", node => MatchesExactPath(node, entry, locator) && HasFullIdentityMatch(node, entry, locator)),
            ("stable-index-path", node => MatchesExactIndexPath(node, entry, locator) && HasStableIdentityMatch(node, entry, locator)),
            ("stable-xpath", node => MatchesExactXPath(node, locator) && HasStableIdentityMatch(node, entry, locator)),
            ("stable-role-path", node => MatchesExactPath(node, entry, locator) && HasStableIdentityMatch(node, entry, locator)),
            ("semantic-bounds", node => HasSemanticIdentityMatch(node, entry, locator) && BoundsAreCompatible(node, entry, locator))
        };

        foreach (var attempt in attempts)
        {
            var matches = nodes
                .Where(attempt.Match)
                .OrderByDescending(node => IdentityScore(node, entry, locator))
                .Take(2)
                .ToList();

            if (matches.Count == 1)
            {
                strategyName = attempt.Name;
                strategyMatches = matches;
                return matches[0];
            }
        }

        strategyName = "";
        strategyMatches = [];
        return null;
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

    private static bool MatchesExactPath(AccessibleNode node, JavaObjectRepositoryEntry entry, LocatorSuggestion? locator)
    {
        var path = locator?.Path ?? entry.Path;
        return !string.IsNullOrWhiteSpace(path) && EqualsNormalized(node.Path, path);
    }

    private static bool MatchesExactIndexPath(AccessibleNode node, JavaObjectRepositoryEntry entry, LocatorSuggestion? locator)
    {
        var indexPath = locator?.IndexPath ?? entry.IndexPath;
        return !string.IsNullOrWhiteSpace(indexPath) && EqualsNormalized(LocatorGenerator.BuildIndexPath(node), indexPath);
    }

    private static bool MatchesExactXPath(AccessibleNode node, LocatorSuggestion? locator)
    {
        if (locator is null) return false;
        return (!string.IsNullOrWhiteSpace(locator.XPath) && EqualsNormalized(LocatorGenerator.BuildXPath(node), locator.XPath)) ||
               (!string.IsNullOrWhiteSpace(locator.IndexXPath) && EqualsNormalized(LocatorGenerator.BuildIndexXPath(node), locator.IndexXPath));
    }

    private static bool HasFullIdentityMatch(AccessibleNode node, JavaObjectRepositoryEntry entry, LocatorSuggestion? locator)
    {
        if (!HasStableIdentityMatch(node, entry, locator)) return false;

        var required = 0;
        var matched = 0;
        CountMatch(EqualsNormalized(node.StatesEnUs, locator?.StatesEnUs ?? entry.StatesEnUs) ||
                   EqualsNormalized(node.States, locator?.States ?? entry.States), HasAny(locator?.StatesEnUs, locator?.States, entry.StatesEnUs, entry.States), ref required, ref matched);
        CountMatch(node.ChildrenCount == (locator?.ChildrenCount ?? entry.ChildrenCount), locator?.ChildrenCount >= 0 || entry.ChildrenCount >= 0, ref required, ref matched);
        CountMatch(node.HasManagedDescendantAncestor == (locator?.HasManagedDescendantAncestor ?? entry.HasManagedDescendantAncestor), true, ref required, ref matched);

        var actions = locator?.ActionNames ?? entry.ActionNames;
        if (actions.Count > 0)
        {
            required++;
            if (node.ActionNames.Count > 0 && actions.All(action => node.ActionNames.Any(recorded => EqualsNormalized(action, recorded)))) matched++;
        }

        return required == 0 || matched >= Math.Max(1, required - 1);
    }

    private static bool HasStableIdentityMatch(AccessibleNode node, JavaObjectRepositoryEntry entry, LocatorSuggestion? locator)
    {
        if (!RoleMatches(node, locator?.RoleEnUs ?? locator?.Role ?? entry.RoleEnUs) &&
            !RoleMatches(node, entry.Role))
        {
            return false;
        }

        var required = 0;
        var matched = 0;

        CountMatch(EqualsNormalized(node.Name, locator?.Name ?? entry.Name), HasAny(locator?.Name, entry.Name), ref required, ref matched);
        CountMatch(EqualsNormalized(node.VirtualAccessibleName, locator?.VirtualAccessibleName ?? entry.VirtualAccessibleName), HasAny(locator?.VirtualAccessibleName, entry.VirtualAccessibleName), ref required, ref matched);
        CountMatch(EqualsNormalized(node.Description, locator?.Description ?? entry.Description), HasAny(locator?.Description, entry.Description), ref required, ref matched);
        CountMatch(EqualsNormalized(node.Parent?.Role, locator?.ParentRole ?? entry.ParentRole), HasAny(locator?.ParentRole, entry.ParentRole), ref required, ref matched);
        CountMatch(EqualsNormalized(node.Parent?.Name, locator?.ParentName ?? entry.ParentName), HasAny(locator?.ParentName, entry.ParentName), ref required, ref matched);
        CountMatch(node.IndexInParent == (locator?.IndexInParent ?? entry.IndexInParent), (locator?.IndexInParent ?? entry.IndexInParent) >= 0, ref required, ref matched);
        CountMatch(node.ObjectDepth == (locator?.ObjectDepth ?? entry.ObjectDepth), (locator?.ObjectDepth ?? entry.ObjectDepth) >= 0, ref required, ref matched);

        if (HasAny(locator?.TextPreview))
        {
            required++;
            if (EqualsNormalized(node.TextPreview, locator!.TextPreview)) matched++;
        }

        if (HasAny(locator?.CurrentValue))
        {
            required++;
            if (EqualsNormalized(node.CurrentValue, locator!.CurrentValue)) matched++;
        }

        return required == 0
            ? MatchesExactPath(node, entry, locator) || MatchesExactIndexPath(node, entry, locator) || MatchesExactXPath(node, locator)
            : matched >= Math.Min(required, 3);
    }

    private static bool HasSemanticIdentityMatch(AccessibleNode node, JavaObjectRepositoryEntry entry, LocatorSuggestion? locator)
    {
        if (!HasStableIdentityMatch(node, entry, locator)) return false;

        var hasStrongName = EqualsNormalized(node.Name, locator?.Name ?? entry.Name) ||
                            EqualsNormalized(node.VirtualAccessibleName, locator?.VirtualAccessibleName ?? entry.VirtualAccessibleName) ||
                            EqualsNormalized(node.Description, locator?.Description ?? entry.Description) ||
                            EqualsNormalized(node.TextPreview, locator?.TextPreview);
        var hasStructure = node.IndexInParent == (locator?.IndexInParent ?? entry.IndexInParent) &&
                           node.ObjectDepth == (locator?.ObjectDepth ?? entry.ObjectDepth);
        var hasParent = EqualsNormalized(node.Parent?.Role, locator?.ParentRole ?? entry.ParentRole) ||
                        EqualsNormalized(node.Parent?.Name, locator?.ParentName ?? entry.ParentName);

        return hasStrongName && hasStructure && hasParent;
    }

    private static int IdentityScore(AccessibleNode node, JavaObjectRepositoryEntry entry, LocatorSuggestion? locator)
    {
        var score = 0;
        if (MatchesExactPath(node, entry, locator)) score += 100;
        if (MatchesExactIndexPath(node, entry, locator)) score += 100;
        if (MatchesExactXPath(node, locator)) score += 90;
        if (RoleMatches(node, locator?.RoleEnUs ?? locator?.Role ?? entry.RoleEnUs)) score += 30;
        if (EqualsNormalized(node.Name, locator?.Name ?? entry.Name)) score += 30;
        if (EqualsNormalized(node.VirtualAccessibleName, locator?.VirtualAccessibleName ?? entry.VirtualAccessibleName)) score += 28;
        if (EqualsNormalized(node.Description, locator?.Description ?? entry.Description)) score += 18;
        if (EqualsNormalized(node.Parent?.Role, locator?.ParentRole ?? entry.ParentRole)) score += 18;
        if (EqualsNormalized(node.Parent?.Name, locator?.ParentName ?? entry.ParentName)) score += 18;
        if (node.IndexInParent == (locator?.IndexInParent ?? entry.IndexInParent)) score += 18;
        if (node.ObjectDepth == (locator?.ObjectDepth ?? entry.ObjectDepth)) score += 14;
        if (BoundsAreCompatible(node, entry, locator)) score += 8;
        return score;
    }

    private static bool BoundsAreCompatible(AccessibleNode node, JavaObjectRepositoryEntry entry, LocatorSuggestion? locator)
    {
        if (locator is not null && locator.Bounds.Width > 0 && locator.Bounds.Height > 0)
            return BoundsDistance(node, locator.Bounds) <= 40;
        return BoundsDistance(node, entry) <= 40;
    }

    private static void CountMatch(bool matches, bool hasRecordedValue, ref int required, ref int matched)
    {
        if (!hasRecordedValue) return;
        required++;
        if (matches) matched++;
    }

    private static bool HasAny(params string?[] values) => values.Any(value => !string.IsNullOrWhiteSpace(value));

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

    private static IReadOnlyList<ResolutionCandidate> BuildCandidates(
        IReadOnlyList<AccessibleNode> nodes,
        JavaObjectRepositoryEntry entry,
        JavaRecordedStep? step,
        LocatorSuggestion? locator,
        ResolutionPolicy policy)
    {
        return nodes
            .Select(node => ToCandidate(node, Score(node, entry, step), entry, locator))
            .OrderByDescending(candidate => candidate.Score)
            .Take(policy.MaxCandidates)
            .ToList();
    }

    private static ResolutionCandidate ToCandidate(
        AccessibleNode node,
        int score,
        JavaObjectRepositoryEntry entry,
        LocatorSuggestion? locator)
    {
        return new ResolutionCandidate(
            node.DisplayName,
            score,
            node.Role,
            node.RoleEnUs,
            node.Name,
            node.VirtualAccessibleName,
            node.Description,
            node.Path,
            LocatorGenerator.BuildIndexPath(node),
            LocatorGenerator.BuildXPath(node),
            node.Parent?.Role ?? "",
            node.Parent?.Name ?? "",
            new ElementBounds(node.X, node.Y, node.Width, node.Height),
            BuildMismatches(node, entry, locator));
    }

    private static IReadOnlyList<string> BuildMismatches(
        AccessibleNode node,
        JavaObjectRepositoryEntry entry,
        LocatorSuggestion? locator)
    {
        var result = new List<string>();
        AddMismatch(result, "roleEnUs", locator?.RoleEnUs ?? entry.RoleEnUs, node.RoleEnUs);
        AddMismatch(result, "role", locator?.Role ?? entry.Role, node.Role);
        AddMismatch(result, "name", locator?.Name ?? entry.Name, node.Name);
        AddMismatch(result, "virtualAccessibleName", locator?.VirtualAccessibleName ?? entry.VirtualAccessibleName, node.VirtualAccessibleName);
        AddMismatch(result, "description", locator?.Description ?? entry.Description, node.Description);
        AddMismatch(result, "parentRole", locator?.ParentRole ?? entry.ParentRole, node.Parent?.Role ?? "");
        AddMismatch(result, "parentName", locator?.ParentName ?? entry.ParentName, node.Parent?.Name ?? "");
        AddMismatch(result, "path", locator?.Path ?? entry.Path, node.Path);
        AddMismatch(result, "indexPath", locator?.IndexPath ?? entry.IndexPath, LocatorGenerator.BuildIndexPath(node));

        var expectedDepth = locator?.ObjectDepth ?? entry.ObjectDepth;
        if (expectedDepth >= 0 && node.ObjectDepth != expectedDepth)
            result.Add($"objectDepth expected '{expectedDepth}' but was '{node.ObjectDepth}'");

        var expectedIndex = locator?.IndexInParent ?? entry.IndexInParent;
        if (expectedIndex >= 0 && node.IndexInParent != expectedIndex)
            result.Add($"indexInParent expected '{expectedIndex}' but was '{node.IndexInParent}'");

        return result;
    }

    private static void AddMismatch(List<string> mismatches, string property, string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected)) return;
        if (EqualsNormalized(expected, actual)) return;
        mismatches.Add($"{property} expected '{expected}' but was '{actual ?? ""}'");
    }

    private static bool EqualsNormalized(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
