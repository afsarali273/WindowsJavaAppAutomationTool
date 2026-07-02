using WinInspector.Core.Models;

namespace WinInspector.Core.Services.ControlMessages;

public sealed class ControlMessageExtractorRegistry
{
    private readonly Dictionary<string, IControlMessageExtractor> _extractors;

    public ControlMessageExtractorRegistry()
    {
        _extractors = new Dictionary<string, IControlMessageExtractor>(StringComparer.OrdinalIgnoreCase)
        {
            ["combobox"] = new ComboBoxExtractor(),
            ["listbox"] = new ListBoxExtractor(),
            ["tab"] = new TabControlExtractor(),
            ["listview"] = new ListViewExtractor(),
            ["treeview"] = new TreeViewExtractor(),
            ["legacy-container"] = new LegacyContainerExtractor()
        };
    }

    public bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        var family = parent.Metadata.TryGetValue("controlFamily", out var rawFamily)
            ? rawFamily
            : Win32ControlClassCatalog.GetFamily(parent.ClassName);

        if (_extractors.TryGetValue(family, out var extractor) && extractor.TryPopulateVirtualChildren(parent, maxChildren))
        {
            return true;
        }

        if (ShouldTryLegacyContainer(parent))
        {
            return _extractors["legacy-container"].TryPopulateVirtualChildren(parent, maxChildren);
        }

        return false;
    }

    private static bool ShouldTryLegacyContainer(WindowsAutomationNode parent)
    {
        var className = parent.ClassName ?? string.Empty;
        if (Win32ControlClassCatalog.IsLikelyCanvasLike(className) || Win32ControlClassCatalog.IsVb6Class(className))
        {
            return true;
        }

        if (parent.Metadata.TryGetValue("customPanelIndicator", out var indicator) &&
            !string.IsNullOrWhiteSpace(indicator) &&
            !string.Equals(indicator, "Real Control", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(className, "ThunderRT6UserControlDC", StringComparison.OrdinalIgnoreCase);
    }
}
