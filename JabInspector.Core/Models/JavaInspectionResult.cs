namespace JabInspector.Core.Models;

public sealed class JavaInspectionResult
{
    public AccessibleNode ResolvedNode { get; }
    public AccessibleNode VisibleAncestor { get; }
    public ElementBounds PhysicalBounds { get; }
    public bool UsedLogicalHitTesting { get; }
    public bool UsedAncestorFallback { get; }

    public JavaInspectionResult(
        AccessibleNode resolvedNode,
        AccessibleNode visibleAncestor,
        ElementBounds physicalBounds,
        bool usedLogicalHitTesting,
        bool usedAncestorFallback)
    {
        ResolvedNode = resolvedNode;
        VisibleAncestor = visibleAncestor;
        PhysicalBounds = physicalBounds;
        UsedLogicalHitTesting = usedLogicalHitTesting;
        UsedAncestorFallback = usedAncestorFallback;
    }
}
