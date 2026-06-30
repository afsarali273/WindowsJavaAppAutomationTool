using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JabInspector.App.ViewModels;
using JabInspector.Core.Models;
using Microsoft.Win32;

namespace JabInspector.App;

public partial class RecordingStudioWindow : Window
{
    private System.Windows.Point _recordedStepDragStartPoint;
    private JavaRecordedStep? _draggedRecordedStep;

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
            OwnerWindow.BringCurrentJavaWindowToForeground("recording studio start");
        }
    }

    private void AppendSession_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentWindow is null || ViewModel.Root is null)
        {
            ViewModel.Log("Attach to a Java window in the main inspector before appending recording.");
            return;
        }

        var alias = string.IsNullOrWhiteSpace(ViewModel.RecordingApplicationAlias)
            ? ViewModel.CurrentWindow?.Title
            : ViewModel.RecordingApplicationAlias;
        var sessionName = !string.IsNullOrWhiteSpace(ViewModel.RecordingSessionName)
                          && !string.Equals(ViewModel.RecordingSessionName, "No active recording session", StringComparison.OrdinalIgnoreCase)
            ? ViewModel.RecordingSessionName
            : null;
        var dialog = new RecordingSessionWindow(alias, sessionName) { Owner = this };

        if (dialog.ShowDialog() != true) return;
        if (ViewModel.StartJavaRecordingSession(dialog.SessionName, dialog.ApplicationAlias, appendExisting: true))
        {
            OwnerWindow.UpdateRecordingBadge();
            OwnerWindow.BringCurrentJavaWindowToForeground("recording studio append");
        }
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

    private void DeleteRepositoryObject_Click(object sender, RoutedEventArgs e)
    {
        var entry = ViewModel.SelectedRepositoryEntry;
        if (entry is null)
        {
            ViewModel.DeleteSelectedRepositoryEntry();
            return;
        }

        var referencingStepCount = ViewModel.CountRecordedStepsUsingSelectedRepositoryEntry();
        var deleteDependents = false;
        if (referencingStepCount > 0)
        {
            var answer = System.Windows.MessageBox.Show(
                this,
                $"'{entry.ObjectKey}' is used by {referencingStepCount} recorded step(s).\n\nDelete the object and those dependent timeline steps?",
                "Delete repository object",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            deleteDependents = true;
        }

        ViewModel.DeleteSelectedRepositoryEntry(deleteDependents);
        OwnerWindow.UpdateRecordingBadge();
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var initialDirectory = !string.IsNullOrWhiteSpace(ViewModel.RecordingProjectPath)
            ? Path.GetDirectoryName(ViewModel.RecordingProjectPath)
            : ViewModel.RepositoryStorageDirectory;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Java recording project / object repository",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
            FileName = ViewModel.GetDefaultRecordingProjectFileName(),
            DefaultExt = ".jrecording.json",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != true) return;
        ViewModel.SaveRecordingProject(dialog.FileName);
    }

    private void LoadProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load Java recording project",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
            InitialDirectory = ViewModel.RepositoryStorageDirectory
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
        var window = CreateLocatorDetailsWindow(
            $"Repository Object: {entry.DisplayName}",
            "Object repository locator and accessibility metadata",
            ViewModel.RecordingRepositoryPreview);
        window.EnableRepositoryActions(
            () => OwnerWindow.HighlightRepositoryEntry(entry),
            () => OwnerWindow.ExportRepositoryEntry(entry));
        window.ShowDialog();
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

    private void HighlightRecordedStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: JavaRecordedStep step }) return;
        ViewModel.SelectedRecordedStep = step;
        OwnerWindow.HighlightRecordedStep(step);
        e.Handled = true;
    }

    private void ShowLocatorDetails(string title, string subtitle, string details)
    {
        var window = CreateLocatorDetailsWindow(title, subtitle, details);
        window.ShowDialog();
    }

    private LocatorDetailsWindow CreateLocatorDetailsWindow(string title, string subtitle, string details) => new(title, subtitle, details)
    {
        Owner = this
    };

    private void RecordedStepsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _recordedStepDragStartPoint = e.GetPosition(RecordedStepsList);
        _draggedRecordedStep = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as JavaRecordedStep;
    }

    private void RecordedStepsList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedRecordedStep is null) return;

        var currentPosition = e.GetPosition(RecordedStepsList);
        if (Math.Abs(currentPosition.X - _recordedStepDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _recordedStepDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        System.Windows.DragDrop.DoDragDrop(RecordedStepsList, new System.Windows.DataObject(typeof(JavaRecordedStep), _draggedRecordedStep), System.Windows.DragDropEffects.Move);
    }

    private void RecordedStepsList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(JavaRecordedStep)) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void RecordedStepsList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(JavaRecordedStep))) return;
        if (e.Data.GetData(typeof(JavaRecordedStep)) is not JavaRecordedStep draggedStep) return;

        var listBoxItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        var targetStep = listBoxItem?.DataContext as JavaRecordedStep;
        var targetIndex = targetStep is null
            ? ViewModel.RecordedSteps.Count - 1
            : ViewModel.RecordedSteps.IndexOf(targetStep);

        if (ViewModel.MoveRecordedStep(draggedStep, targetIndex))
        {
            OwnerWindow.UpdateRecordingBadge();
        }

        _draggedRecordedStep = null;
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = current switch
            {
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(current),
                FrameworkContentElement content => content.Parent,
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }

}
