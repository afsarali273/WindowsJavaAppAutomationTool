using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public static class LocatorGenerator
{
    public static LocatorSuggestion GenerateLocator(AccessibleNode node) => new("java-access-bridge", node.Role, node.Name, node.Description,
        node.States, node.IndexInParent, BuildPath(node), new(node.X, node.Y, node.Width, node.Height));

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
}
