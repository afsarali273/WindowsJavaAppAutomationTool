using System.Diagnostics;
using JabInspector.Core.Diagnostics;
using JabInspector.Core.Models;
using JabInspector.Native;

namespace JabInspector.Core.Services;

public sealed class AccessibleTreeCrawler(AccessBridgeService bridge, InspectorLogger logger)
{
    public int MaxDepth { get; init; } = 25;
    public int MaxChildrenPerNode { get; init; } = 500;
    public int NodeCount { get; private set; }

    public AccessibleNode? BuildTree(JavaWindowInfo window)
    {
        var sw = Stopwatch.StartNew(); NodeCount = 0;
        var rootContext = window.RootContext;
        if (rootContext == 0 && !bridge.TryGetAccessibleContextFromHwnd(window.Hwnd, out _, out rootContext))
        { logger.Log("Failed to obtain AccessibleContext from selected window."); return null; }
        var root = ReadNode(window.VmId, rootContext, null, 0);
        if (root is not null) LocatorGenerator.AssignPaths(root);
        logger.Log($"Tree crawl completed: {NodeCount:N0} nodes in {sw.ElapsedMilliseconds:N0} ms.");
        if (root is not null && root.Children.Count == 0) logger.Log("The root has no exposed children. The app may use custom controls or a separately bundled JRE.");
        return root;
    }

    private AccessibleNode ReadNode(int vmId, long context, AccessibleNode? parent, int depth)
    {
        NodeCount++;
        if (!bridge.TryGetAccessibleContextInfo(vmId, context, out var info))
            return new AccessibleNode { VmId = vmId, Context = context, Parent = parent, Role = "unknown", Name = "(failed to read)" };
        var node = Map(vmId, context, info); node.Parent = parent;
        node.VirtualAccessibleName = bridge.GetVirtualAccessibleName(vmId, context);
        node.ObjectDepth = bridge.GetObjectDepth(vmId, context);
        node.HasManagedDescendantAncestor = parent?.HasManagedDescendantAncestor == true || parent?.ManagesDescendants == true;
        if (node.AccessibleAction) node.ActionNames = bridge.GetAccessibleActions(vmId, context).ToList();
        if (depth >= MaxDepth) return node;
        var limit = Math.Min(Math.Max(info.ChildrenCount, 0), MaxChildrenPerNode);
        for (var index = 0; index < limit; index++)
            if (bridge.TryGetChildContext(vmId, context, index, out var child)) node.Children.Add(ReadNode(vmId, child, node, depth + 1));
        return node;
    }

    private static AccessibleNode Map(int vmId, long context, AccessibleContextInfo x) => new()
    {
        VmId = vmId, Context = context, Name = x.Name ?? "", Description = x.Description ?? "", Role = string.IsNullOrWhiteSpace(x.Role) ? "unknown" : x.Role,
        RoleEnUs = x.RoleEnUs ?? "", States = x.States ?? "", StatesEnUs = x.StatesEnUs ?? "", IndexInParent = x.IndexInParent, ChildrenCount = x.ChildrenCount,
        X = x.X, Y = x.Y, Width = x.Width, Height = x.Height, AccessibleComponent = x.AccessibleComponent, AccessibleAction = x.AccessibleAction,
        AccessibleSelection = x.AccessibleSelection, AccessibleText = x.AccessibleText, AccessibleValue = x.AccessibleValue, AccessibleTable = x.AccessibleTable,
        AccessibleInterfaces = x.AccessibleInterfaces
    };
}
