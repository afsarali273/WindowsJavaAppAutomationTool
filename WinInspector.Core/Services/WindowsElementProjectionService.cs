using System.Text.Json;
using WinInspector.Core.Models;

namespace WinInspector.Core.Services;

public sealed class WindowsElementProjectionService
{
    public DesktopElement Project(DesktopWindowInfo window, WindowsAutomationNode node)
    {
        var projected = new Dictionary<WindowsAutomationNode, DesktopElement>(ReferenceEqualityComparer.Instance);
        return ProjectNode(window, node, null, projected);
    }

    private DesktopElement ProjectNode(
        DesktopWindowInfo window,
        WindowsAutomationNode node,
        DesktopElement? parent,
        IDictionary<WindowsAutomationNode, DesktopElement> projected)
    {
        var element = new DesktopElement
        {
            Id = BuildElementId(window, node),
            Name = node.Name,
            Role = node.Role,
            Bounds = WindowsRect.FromRectangle(node.Bounds),
            SourceType = MapSource(node),
            ElementKind = InferElementKind(node),
            Hwnd = node.NativeHandle == IntPtr.Zero ? null : node.NativeHandle,
            ClassName = node.ClassName,
            ControlId = node.ControlId >= 0 ? node.ControlId : null,
            ParentId = parent?.Id ?? "",
            Confidence = BaseConfidence(node),
            Metadata = BuildMetadata(window, node)
        };

        element.Locators = BuildLocators(window, node, element);
        element.SupportedActions = BuildSupportedActions(node);
        projected[node] = element;

        foreach (var child in node.Children)
        {
            var childElement = ProjectNode(window, child, element, projected);
            element.ChildIds.Add(childElement.Id);
        }

        return element;
    }

    private static string BuildElementId(DesktopWindowInfo window, WindowsAutomationNode node)
    {
        var handlePart = node.NativeHandle == IntPtr.Zero ? "nohwnd" : $"0x{node.NativeHandle.ToInt64():X}";
        var pathPart = BuildPath(node);
        return $"{window.HwndDisplay}:{handlePart}:{pathPart}";
    }

    private static string BuildPath(WindowsAutomationNode node)
    {
        var segments = new Stack<string>();
        for (var current = node; current is not null; current = current.Parent)
        {
            segments.Push($"{NormalizeSegment(current.Role)}[{Math.Max(current.IndexInParent, 0)}]");
        }

        return string.Join("/", segments);
    }

    private static string NormalizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "node";
        return value.Trim().Replace(' ', '-').ToLowerInvariant();
    }

    private static DesktopElementSource MapSource(WindowsAutomationBackendKind kind) =>
        kind switch
        {
            WindowsAutomationBackendKind.Win32 => DesktopElementSource.Win32,
            WindowsAutomationBackendKind.Msaa => DesktopElementSource.Msaa,
            WindowsAutomationBackendKind.Uia => DesktopElementSource.Uia,
            WindowsAutomationBackendKind.FlaUi => DesktopElementSource.Uia,
            _ => DesktopElementSource.Unknown
        };

    private static DesktopElementSource MapSource(WindowsAutomationNode node)
    {
        if (node.Metadata.TryGetValue("isVirtual", out var isVirtual) && bool.TryParse(isVirtual, out var virtualFlag) && virtualFlag)
        {
            return DesktopElementSource.ControlMessage;
        }

        return MapSource(node.BackendKind);
    }

    private static ElementKind InferElementKind(WindowsAutomationNode node)
    {
        if (node.Metadata.TryGetValue("isVirtual", out var isVirtual) && bool.TryParse(isVirtual, out var virtualFlag) && virtualFlag)
        {
            return ElementKind.VirtualControl;
        }

        if (node.Parent is null) return ElementKind.Window;
        if (node.Children.Count > 0) return ElementKind.Container;
        return ElementKind.RealControl;
    }

    private static double BaseConfidence(WindowsAutomationNode node) =>
        node.BackendKind switch
        {
            WindowsAutomationBackendKind.Win32 => 0.95,
            WindowsAutomationBackendKind.Uia => 0.90,
            WindowsAutomationBackendKind.FlaUi => 0.88,
            _ => 0.50
        };

    private static Dictionary<string, string> BuildMetadata(DesktopWindowInfo window, WindowsAutomationNode node) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["window.hwnd"] = window.HwndDisplay,
            ["window.title"] = window.Title,
            ["window.className"] = window.ClassName,
            ["window.processId"] = window.ProcessId.ToString(),
            ["window.processName"] = window.ProcessName,
            ["backend"] = node.BackendKind.ToString(),
            ["path"] = BuildPath(node),
            ["indexInParent"] = node.IndexInParent.ToString(),
            ["automationId"] = node.AutomationId,
            ["value"] = node.Value,
            ["controlId"] = node.ControlId >= 0 ? node.ControlId.ToString() : "",
            ["processId"] = node.ProcessId.ToString(),
            ["threadId"] = node.ThreadId.ToString(),
            ["isVisible"] = node.IsVisible.ToString(),
            ["isEnabled"] = node.IsEnabled.ToString(),
            ["style"] = $"0x{node.Style:X}",
            ["extendedStyle"] = $"0x{node.ExtendedStyle:X}",
            ["clientBounds"] = $"{node.ClientBounds.X},{node.ClientBounds.Y},{node.ClientBounds.Width},{node.ClientBounds.Height}",
            ["controlFamily"] = node.Metadata.TryGetValue("controlFamily", out var family) ? family : Win32ControlClassCatalog.GetFamily(node.ClassName),
            ["legacyTechnology"] = node.Metadata.TryGetValue("legacyTechnology", out var technology) ? technology : Win32ControlClassCatalog.GetTechnology(node.ClassName),
            ["customPanelIndicator"] = node.Metadata.TryGetValue("customPanelIndicator", out var panelIndicator) ? panelIndicator : "",
            ["customPanelScore"] = node.Metadata.TryGetValue("customPanelScore", out var panelScore) ? panelScore : "",
            ["customPanelReasons"] = node.Metadata.TryGetValue("customPanelReasons", out var panelReasons) ? panelReasons : "",
            ["isVb6"] = node.Metadata.TryGetValue("isVb6", out var isVb6) ? isVb6 : Win32ControlClassCatalog.IsVb6Class(node.ClassName).ToString()
        };

    private static List<LocatorCandidate> BuildLocators(DesktopWindowInfo window, WindowsAutomationNode node, DesktopElement element)
    {
        var locators = new List<LocatorCandidate>();
        var priority = 1;
        var path = BuildPath(node);

        if (node.NativeHandle != IntPtr.Zero)
        {
            locators.Add(new LocatorCandidate
            {
                Id = $"{element.Id}:hwnd",
                Type = LocatorType.Win32Handle,
                Value = $"{{\"hwnd\":\"0x{node.NativeHandle.ToInt64():X}\"}}",
                Confidence = 0.95,
                Priority = priority++,
                Score = 95,
                Properties = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["backend"] = node.BackendKind.ToString(),
                    ["windowHwnd"] = window.HwndDisplay
                }
            });
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Win32 && node.ControlId >= 0)
        {
            locators.Add(new LocatorCandidate
            {
                Id = $"{element.Id}:control-id",
                Type = LocatorType.Win32ControlId,
                Value = JsonSerializer.Serialize(new
                {
                    window = new { hwnd = window.HwndDisplay, title = window.Title, className = window.ClassName },
                    className = node.ClassName,
                    controlId = node.ControlId,
                    path
                }),
                Confidence = 0.90,
                Priority = priority++,
                Score = 90,
                Properties = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["controlId"] = node.ControlId.ToString(),
                    ["className"] = node.ClassName
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(node.ClassName))
        {
            locators.Add(new LocatorCandidate
            {
                Id = $"{element.Id}:class-path",
                Type = node.BackendKind == WindowsAutomationBackendKind.Uia ? LocatorType.UiaPath : LocatorType.Win32Path,
                Value = JsonSerializer.Serialize(new
                {
                    window = new { hwnd = window.HwndDisplay, title = window.Title, className = window.ClassName },
                    backend = node.BackendKind.ToString(),
                    className = node.ClassName,
                    automationId = node.AutomationId,
                    name = node.Name,
                    role = node.Role,
                    path
                }),
                Confidence = node.BackendKind == WindowsAutomationBackendKind.Uia ? 0.90 : 0.88,
                Priority = priority++,
                Score = node.BackendKind == WindowsAutomationBackendKind.Uia ? 90 : 88,
                Properties = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["path"] = path,
                    ["className"] = node.ClassName
                }
            });
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Uia && !string.IsNullOrWhiteSpace(node.AutomationId))
        {
            locators.Add(new LocatorCandidate
            {
                Id = $"{element.Id}:uia-id",
                Type = LocatorType.UiaAutomationId,
                Value = node.AutomationId,
                Confidence = 0.92,
                Priority = priority++,
                Score = 92,
                Properties = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["className"] = node.ClassName,
                    ["role"] = node.Role
                }
            });
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Uia && !string.IsNullOrWhiteSpace(node.Name))
        {
            locators.Add(new LocatorCandidate
            {
                Id = $"{element.Id}:uia-name",
                Type = LocatorType.UiaName,
                Value = node.Name,
                Confidence = 0.84,
                Priority = priority++,
                Score = 84,
                Properties = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["className"] = node.ClassName,
                    ["role"] = node.Role
                }
            });
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Msaa)
        {
            var msaaChildId = node.Metadata.TryGetValue("msaa.childId", out var rawMsaaChildId) ? rawMsaaChildId ?? "0" : "0";
            locators.Add(new LocatorCandidate
            {
                Id = $"{element.Id}:msaa",
                Type = LocatorType.Msaa,
                Value = JsonSerializer.Serialize(new
                {
                    window = new { hwnd = window.HwndDisplay, title = window.Title, className = window.ClassName },
                    name = node.Name,
                    role = node.Role,
                    path,
                    childId = msaaChildId
                }),
                Confidence = 0.88,
                Priority = priority++,
                Score = 88,
                Properties = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["path"] = path,
                    ["childId"] = msaaChildId
                }
            });
        }

        return locators;
    }

    private static List<SupportedAction> BuildSupportedActions(WindowsAutomationNode node)
    {
        var actions = new List<SupportedAction> { SupportedAction.Focus, SupportedAction.GetText, SupportedAction.Screenshot };

        if (node.Metadata.TryGetValue("isVirtual", out var isVirtual) && bool.TryParse(isVirtual, out var virtualFlag) && virtualFlag)
        {
            if (!node.Metadata.TryGetValue("virtualSelectable", out var rawSelectable) || !bool.TryParse(rawSelectable, out var selectable) || selectable)
            {
                actions.Add(SupportedAction.Select);
                actions.Add(SupportedAction.Click);
                actions.Add(SupportedAction.Invoke);
            }
            return actions.Distinct().ToList();
        }

        if (node.BackendKind == WindowsAutomationBackendKind.Uia)
        {
            actions.Add(SupportedAction.Invoke);
            actions.Add(SupportedAction.Click);
            actions.Add(SupportedAction.SetText);
        }
        else if (node.BackendKind == WindowsAutomationBackendKind.Win32)
        {
            if (Win32ControlClassCatalog.SupportsClick(node.ClassName))
            {
                actions.Add(SupportedAction.Click);
                actions.Add(SupportedAction.Invoke);
            }

            if (Win32ControlClassCatalog.SupportsSetText(node.ClassName))
            {
                actions.Add(SupportedAction.SetText);
            }

            if (Win32ControlClassCatalog.SupportsSelectionRead(node.ClassName))
            {
                actions.Add(SupportedAction.Select);
            }
        }

        return actions.Distinct().ToList();
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<WindowsAutomationNode>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public bool Equals(WindowsAutomationNode? x, WindowsAutomationNode? y) => ReferenceEquals(x, y);

        public int GetHashCode(WindowsAutomationNode obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
