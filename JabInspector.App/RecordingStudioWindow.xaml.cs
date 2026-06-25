using System.IO;
using System.Windows;
using System.Windows.Controls;
using JabInspector.App.ViewModels;
using JabInspector.Core.Models;
using Microsoft.Win32;

namespace JabInspector.App;

public partial class RecordingStudioWindow : Window
{
    private MainWindow OwnerWindow => (MainWindow)Owner;
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public RecordingStudioWindow(MainViewModel viewModel, MainWindow owner)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = owner;
    }

    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentWindow is null || ViewModel.Root is null)
        {
            ViewModel.Log("Attach to a Java window in the main inspector before starting recording.");
            return;
        }

        var alias = ViewModel.CurrentWindow?.Title ?? ViewModel.RecordingApplicationAlias;
        var dialog = new RecordingSessionWindow(alias) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (ViewModel.StartJavaRecordingSession(dialog.SessionName, dialog.ApplicationAlias))
        {
            OwnerWindow.UpdateRecordingBadge();
            Activate();
        }
    }

    private void StopSession_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StopJavaRecordingSession();
        OwnerWindow.UpdateRecordingBadge();
        OwnerWindow.Show();
        OwnerWindow.Activate();
        Activate();
    }

    private void TogglePause_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleJavaRecordingPause();
        OwnerWindow.UpdateRecordingBadge();
    }

    private void CaptureObject_Click(object sender, RoutedEventArgs e)
    {
        var entry = ViewModel.AddSelectedNodeToRepository();
        if (entry is null) ViewModel.Log("Select a Java element in the main inspector before capturing an object.");
        else OwnerWindow.HighlightCurrentJavaSelection();
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedRecordedStep();
        OwnerWindow.UpdateRecordingBadge();
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Java recording project",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
            FileName = string.IsNullOrWhiteSpace(ViewModel.RecordingProjectPath) ? $"{ViewModel.RecordingSessionName}.jrecording.json" : Path.GetFileName(ViewModel.RecordingProjectPath),
            DefaultExt = ".jrecording.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        ViewModel.SaveRecordingProject(dialog.FileName);
    }

    private void LoadProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load Java recording project",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        ViewModel.LoadRecordingProject(dialog.FileName);
        OwnerWindow.UpdateRecordingBadge();
    }

    private async void Playback_Click(object sender, RoutedEventArgs e)
    {
        await OwnerWindow.PlayRecordingAsync();
    }

    private void MoreActions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } owner) return;
        menu.PlacementTarget = owner;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void RepositoryDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: JavaObjectRepositoryEntry entry }) return;
        ViewModel.SelectedRepositoryEntry = entry;
        ShowLocatorDetails(
            $"Repository Object: {entry.DisplayName}",
            "Object repository locator and accessibility metadata",
            ViewModel.RecordingRepositoryPreview);
        e.Handled = true;
    }

    private void StepDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: JavaRecordedStep step }) return;
        ViewModel.SelectedRecordedStep = step;
        ShowLocatorDetails(
            $"Step {step.Sequence}: {step.ActionKind}",
            step.ObjectSummary,
            ViewModel.RecordingStepPreview);
        e.Handled = true;
    }

    private void ShowLocatorDetails(string title, string subtitle, string details)
    {
        var window = new LocatorDetailsWindow(title, subtitle, details)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void Focus_Click(object sender, RoutedEventArgs e) => OwnerWindow.ExecuteJavaRecordedActionFromStudio(JavaRecordedActionKind.Focus, "");
    private void Click_Click(object sender, RoutedEventArgs e) => OwnerWindow.ExecuteJavaRecordedActionFromStudio(JavaRecordedActionKind.Click, "");
    private void DoubleClick_Click(object sender, RoutedEventArgs e) => OwnerWindow.ExecuteJavaRecordedActionFromStudio(JavaRecordedActionKind.DoubleClick, "");
    private void SetText_Click(object sender, RoutedEventArgs e) => OwnerWindow.ExecuteJavaRecordedActionFromStudio(JavaRecordedActionKind.SetText, RecorderTextInput.Text);
    private void TypeText_Click(object sender, RoutedEventArgs e) => OwnerWindow.ExecuteJavaRecordedActionFromStudio(JavaRecordedActionKind.TypeText, RecorderTextInput.Text);
    private void GetText_Click(object sender, RoutedEventArgs e) => OwnerWindow.ExecuteJavaRecordedActionFromStudio(JavaRecordedActionKind.GetText, "");
}
