using JabInspector.Core.Diagnostics;

namespace JabInspector.App.ViewModels;

public sealed class RequirementCheckViewModel
{
    public RequirementCheckViewModel(RequirementCheck model)
    {
        Title = model.Title;
        Status = model.Status;
        Details = model.Details;
        StatusBrush = model.IsOk ? "#E8F5EE" : model.IsWarning ? "#FFF4E8" : "#FCEAEA";
        StatusForeground = model.IsOk ? "#1F7A50" : model.IsWarning ? "#9A5B13" : "#B42318";
    }

    public string Title { get; }
    public string Status { get; }
    public string Details { get; }
    public string StatusBrush { get; }
    public string StatusForeground { get; }
}
