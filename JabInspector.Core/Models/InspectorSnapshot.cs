namespace JabInspector.Core.Models;

public sealed record InspectorSnapshot(DateTime ExportedAt, string WindowTitle, string Hwnd, int VmId, AccessibleNode Root);
