using WinInspector.Core.Models;
using System.Windows;

namespace JabInspector.App.ViewModels;

public sealed record WindowsWindowViewModel(DesktopWindowInfo Model)
{
    public string ElevationBadge => Model.IsElevated ? "Admin" : "";
    public bool HasElevationBadge => Model.IsElevated;
    public Visibility ElevationBadgeVisibility => HasElevationBadge ? Visibility.Visible : Visibility.Collapsed;
}
