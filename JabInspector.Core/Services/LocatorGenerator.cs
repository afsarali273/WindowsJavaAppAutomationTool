using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public static class LocatorGenerator
{
    public static LocatorSuggestion GenerateLocator(AccessibleNode node) => new(
        "java-access-bridge",
        node.Role,
        node.RoleEnUs,
        node.Name,
        node.VirtualAccessibleName,
        node.Description,
        node.States,
        node.StatesEnUs,
        node.IndexInParent,
        node.ObjectDepth,
        node.ChildrenCount,
        BuildPath(node),
        BuildIndexPath(node),
        BuildXPath(node),
        BuildIndexXPath(node),
        BuildSemanticXPath(node),
        node.Parent?.Role ?? "",
        node.Parent?.Name ?? "",
        node.HasManagedDescendantAncestor,
        node.ActionNames,
        node.TextPreview,
        node.TextPreviewSource,
        node.TextCharCount,
        node.TextCaretIndex,
        node.TextIndexAtPoint,
        node.TextSelected,
        node.TextWord,
        node.TextSentence,
        node.CurrentValue,
        node.MinimumValue,
        node.MaximumValue,
        new(node.X, node.Y, node.Width, node.Height));

    public static string BuildPath(AccessibleNode node)
    {
        var segments = new Stack<string>();
        for (var current = node; current is not null; current = current.Parent)
        {
            var role = string.IsNullOrWhiteSpace(current.RoleEnUs) ? current.Role : current.RoleEnUs;
            role = role.Trim().ToLowerInvariant();
            var siblings = current.Parent?.Children.Where(x => string.Equals(x.RoleEnUs, current.RoleEnUs, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Role, current.Role, StringComparison.OrdinalIgnoreCase)).ToList();
            var index = siblings is null ? Math.Max(current.IndexInParent, 0) : Math.Max(siblings.IndexOf(current), 0);
            segments.Push($"{role}[{index}]");
        }
        return string.Join('/', segments);
    }

    public static void AssignPaths(AccessibleNode node)
    { node.Path = BuildPath(node); foreach (var child in node.Children) AssignPaths(child); }

    public static string BuildIndexPath(AccessibleNode node)
    {
        var segments = new Stack<int>();
        for (var current = node; current is not null; current = current.Parent)
            segments.Push(Math.Max(current.IndexInParent, 0));
        return string.Join('/', segments);
    }

    public static string BuildXPath(AccessibleNode node)
    {
        var segments = new Stack<string>();
        for (var current = node; current is not null; current = current.Parent)
        {
            var role = NormalizeRoleForXPath(current);
            var siblings = current.Parent?.Children
                .Where(child => string.Equals(NormalizeRoleForXPath(child), role, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var zeroBasedIndex = siblings is null ? Math.Max(current.IndexInParent, 0) : Math.Max(siblings.IndexOf(current), 0);
            segments.Push($"{role}[{zeroBasedIndex + 1}]");
        }

        return "/" + string.Join('/', segments);
    }

    public static string BuildIndexXPath(AccessibleNode node)
    {
        var segments = new Stack<string>();
        for (var current = node; current is not null; current = current.Parent)
            segments.Push($"*[{Math.Max(current.IndexInParent, 0) + 1}]");
        return "/" + string.Join('/', segments);
    }

    public static string BuildSemanticXPath(AccessibleNode node)
    {
        var role = NormalizeRoleForXPath(node);
        var predicates = new List<string>();

        AddPredicate(predicates, "role", node.Role);
        AddPredicate(predicates, "roleEnUs", node.RoleEnUs);
        AddPredicate(predicates, "name", node.Name);
        AddPredicate(predicates, "virtualAccessibleName", node.VirtualAccessibleName);
        AddPredicate(predicates, "description", node.Description);
        AddPredicate(predicates, "textPreview", node.TextPreview);
        AddPredicate(predicates, "currentValue", node.CurrentValue);
        AddPredicate(predicates, "parentRole", node.Parent?.Role);
        AddPredicate(predicates, "parentName", node.Parent?.Name);
        if (node.ObjectDepth >= 0) predicates.Add($"@objectDepth='{node.ObjectDepth}'");
        if (node.IndexInParent >= 0) predicates.Add($"@indexInParent='{node.IndexInParent}'");

        return predicates.Count == 0
            ? $"//{role}"
            : $"//{role}[{string.Join(" and ", predicates)}]";
    }

    private static void AddPredicate(List<string> predicates, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        predicates.Add($"@{name}={ToXPathLiteral(value.Trim())}");
    }

    private static string NormalizeRoleForXPath(AccessibleNode node) => NormalizeRoleForXPath(
        string.IsNullOrWhiteSpace(node.RoleEnUs) ? node.Role : node.RoleEnUs);

    private static string NormalizeRoleForXPath(string role)
    {
        var normalized = new string(role.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal)) normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private static string ToXPathLiteral(string value)
    {
        if (!value.Contains('\'', StringComparison.Ordinal)) return $"'{value}'";
        if (!value.Contains('"', StringComparison.Ordinal)) return $"\"{value}\"";
        var parts = value.Split('\'').Select(part => $"'{part}'");
        return $"concat({string.Join(", \"'\", ", parts)})";
    }
}
