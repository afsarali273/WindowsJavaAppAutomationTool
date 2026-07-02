using WinInspector.Core.Models;

namespace WinInspector.Core.Services;

public sealed class Win32LegacyPanelHeuristics
{
    public Win32LegacyPanelAssessment Evaluate(WindowsAutomationNode node)
    {
        var reasons = new List<string>();
        var score = 0;
        var className = node.ClassName ?? string.Empty;
        var family = Win32ControlClassCatalog.GetFamily(className);
        var isVb6 = Win32ControlClassCatalog.IsVb6Class(className);
        var isCanvasLike = Win32ControlClassCatalog.IsLikelyCanvasLike(className);
        var hasLegacyModules = node.Metadata.TryGetValue("windowHasLegacyModules", out var hasLegacyModulesValue)
                               && bool.TryParse(hasLegacyModulesValue, out var parsedLegacyModules)
                               && parsedLegacyModules;
        var hasOcxModules = node.Metadata.TryGetValue("windowHasOcxModules", out var hasOcxModulesValue)
                            && bool.TryParse(hasOcxModulesValue, out var parsedOcxModules)
                            && parsedOcxModules;

        if (isVb6)
        {
            score++;
            reasons.Add("VB6 class");
        }

        if (isCanvasLike)
        {
            score += 2;
            reasons.Add("canvas-like class");
        }

        if (node.Children.Count == 0)
        {
            score++;
            reasons.Add("no child hwnds");
        }
        else if (node.Children.Count <= 2)
        {
            score++;
            reasons.Add("very shallow hwnd tree");
        }

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            score++;
            reasons.Add("no accessible/window text");
        }

        if (hasLegacyModules)
        {
            score++;
            reasons.Add("legacy modules loaded");
        }

        if (hasOcxModules)
        {
            score++;
            reasons.Add("OCX modules loaded");
        }

        if (node.Bounds.Width >= 160 && node.Bounds.Height >= 80)
        {
            score++;
            reasons.Add("large interactive region");
        }

        if (family is "edit" or "button" or "combobox" or "listbox")
        {
            score = Math.Max(0, score - 2);
            reasons.Add("standard classic control");
        }

        var indicator = score switch
        {
            >= 5 when hasOcxModules => "OCX-backed Custom Panel",
            >= 4 => "Custom Drawn Panel",
            >= 2 => "Likely Container",
            _ => "Real Control"
        };

        return new Win32LegacyPanelAssessment
        {
            LegacyTechnology = Win32ControlClassCatalog.GetTechnology(className),
            ControlFamily = family,
            IsVb6 = isVb6,
            IsCanvasLike = isCanvasLike,
            Score = score,
            Indicator = indicator,
            Reasons = reasons
        };
    }
}

public sealed class Win32LegacyPanelAssessment
{
    public string LegacyTechnology { get; init; } = "";
    public string ControlFamily { get; init; } = "";
    public bool IsVb6 { get; init; }
    public bool IsCanvasLike { get; init; }
    public int Score { get; init; }
    public string Indicator { get; init; } = "";
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
