namespace WinInspector.Core.Models;

public sealed class ElementEvidence
{
    public Win32Evidence? Win32 { get; set; }
    public MsaaEvidence? Msaa { get; set; }
    public UiaEvidence? Uia { get; set; }
    public LegacyAccessibleEvidence? LegacyAccessible { get; set; }
    public ControlMessageEvidence? ControlMessages { get; set; }
    public ActiveXEvidence? ActiveX { get; set; }
    public GdiEvidence? Gdi { get; set; }
    public BackendEvidence? Backend { get; set; }
    public OcrEvidence? Ocr { get; set; }
    public ImageEvidence? Image { get; set; }
    public CoordinateEvidence? Coordinate { get; set; }
}

public sealed class Win32Evidence
{
    public IntPtr Hwnd { get; set; }
    public string ClassName { get; set; } = "";
    public string WindowText { get; set; } = "";
    public int? ControlId { get; set; }
    public WindowsRect Bounds { get; set; }
    public int ChildCount { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MsaaEvidence
{
    public MsaaElementRef? ElementRef { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public string DefaultAction { get; set; } = "";
    public int ChildCount { get; set; }
    public int? ChildId { get; set; }
    public WindowsRect Bounds { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MsaaElementRef
{
    public IntPtr Hwnd { get; set; }
    public int ChildId { get; set; }
    public string Path { get; set; } = "";
    public string Role { get; set; } = "";
    public string State { get; set; } = "";
    public WindowsRect Bounds { get; set; }
}

public sealed class UiaEvidence
{
    public string Name { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ControlType { get; set; } = "";
    public string LocalizedControlType { get; set; } = "";
    public string FrameworkId { get; set; } = "";
    public WindowsRect Bounds { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsOffscreen { get; set; }
    public bool IsControlElement { get; set; }
    public bool IsContentElement { get; set; }
    public List<string> Patterns { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LegacyAccessibleEvidence
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string State { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public string DefaultAction { get; set; } = "";
    public int? ChildId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ControlMessageEvidence
{
    public string ControlType { get; set; } = "";
    public List<string> Items { get; set; } = [];
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ActiveXEvidence
{
    public string ProgId { get; set; } = "";
    public string TypeName { get; set; } = "";
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GdiEvidence
{
    public List<TextEvidenceBlock> TextBlocks { get; set; } = [];
}

public sealed class BackendEvidence
{
    public string MatchType { get; set; } = "";
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class OcrEvidence
{
    public List<TextEvidenceBlock> TextBlocks { get; set; } = [];
}

public sealed class ImageEvidence
{
    public List<ImageMatchEvidence> Matches { get; set; } = [];
}

public sealed class CoordinateEvidence
{
    public string RelativeTo { get; set; } = "";
    public double XRatio { get; set; }
    public double YRatio { get; set; }
    public WindowsRect Bounds { get; set; }
}

public sealed class TextEvidenceBlock
{
    public string Text { get; set; } = "";
    public WindowsRect Bounds { get; set; }
    public double Confidence { get; set; }
    public string Source { get; set; } = "";
}

public sealed class ImageMatchEvidence
{
    public string TemplateId { get; set; } = "";
    public WindowsRect Bounds { get; set; }
    public double Confidence { get; set; }
}
