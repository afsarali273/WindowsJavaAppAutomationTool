using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaFormsScopeInferenceService
{
    public void Annotate(AccessibleNode root)
    {
        Annotate(root, null, null);
    }

    private static void Annotate(AccessibleNode node, AccessibleNode? currentScope, AccessibleNode? currentViewport)
    {
        var nextScope = IsFormsScopeCandidate(node) ? node : currentScope;
        var nextViewport = IsViewportCandidate(node) ? node : currentViewport;

        node.IsFormsLikeScope = ReferenceEquals(nextScope, node);
        node.IsFormsViewportLikeContainer = ReferenceEquals(nextViewport, node);

        if (nextScope is not null)
        {
            node.FormsScopePath = nextScope.Path;
            node.FormsScopeRole = PreferredRole(nextScope);
            node.FormsScopeName = PreferredName(nextScope);
        }

        if (nextViewport is not null)
        {
            node.FormsViewportPath = nextViewport.Path;
            node.FormsViewportRole = PreferredRole(nextViewport);
            node.FormsViewportName = PreferredName(nextViewport);
        }

        foreach (var child in node.Children)
        {
            Annotate(child, nextScope, nextViewport);
        }
    }

    private static bool IsFormsScopeCandidate(AccessibleNode node)
    {
        var role = PreferredRole(node);
        if (string.IsNullOrWhiteSpace(role)) return false;

        if (ContainsAny(role, "internal frame", "desktop pane", "dialog", "frame", "page tab", "tab page"))
            return true;

        var name = PreferredName(node);
        var hasContainerShape = node.ChildrenCount > 0 && node.HasValidBounds;
        return hasContainerShape
               && ContainsAny(role, "panel", "root pane", "layered pane")
               && ContainsAny(name, "oracle", "opera", "forms", "lov", "canvas", "block", "manager", "setup");
    }

    private static bool IsViewportCandidate(AccessibleNode node)
    {
        var role = PreferredRole(node);
        if (string.IsNullOrWhiteSpace(role)) return false;

        if (ContainsAny(role, "viewport", "scroll pane", "scrollpane"))
            return true;

        if (ContainsAny(role, "table", "grid", "list"))
            return node.HasValidBounds && node.ChildrenCount > 0;

        return false;
    }

    private static string PreferredRole(AccessibleNode node)
    {
        return string.IsNullOrWhiteSpace(node.RoleEnUs) ? node.Role.Trim() : node.RoleEnUs.Trim();
    }

    private static string PreferredName(AccessibleNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.VirtualAccessibleName)) return node.VirtualAccessibleName.Trim();
        if (!string.IsNullOrWhiteSpace(node.Name)) return node.Name.Trim();
        if (!string.IsNullOrWhiteSpace(node.Description)) return node.Description.Trim();
        if (!string.IsNullOrWhiteSpace(node.TextPreview)) return node.TextPreview.Trim();
        return "";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
