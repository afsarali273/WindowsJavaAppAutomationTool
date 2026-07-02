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
            ["treeview"] = new TreeViewExtractor()
        };
    }

    public bool TryPopulateVirtualChildren(WindowsAutomationNode parent, int maxChildren)
    {
        var family = parent.Metadata.TryGetValue("controlFamily", out var rawFamily)
            ? rawFamily
            : Win32ControlClassCatalog.GetFamily(parent.ClassName);

        return _extractors.TryGetValue(family, out var extractor) && extractor.TryPopulateVirtualChildren(parent, maxChildren);
    }
}
