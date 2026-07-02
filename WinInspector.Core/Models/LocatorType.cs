namespace WinInspector.Core.Models;

public enum LocatorType
{
    Unknown,
    Win32Handle,
    Win32Class,
    Win32ControlId,
    Win32Path,
    Msaa,
    UiaAutomationId,
    UiaName,
    UiaPath,
    LegacyAccessible,
    ControlMessage,
    ActiveX,
    GdiText,
    Backend,
    OcrText,
    ImageTemplate,
    RelativeCoordinate,
    AbsoluteCoordinate
}
