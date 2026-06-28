using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using JabInspector.App.ViewModels;
using JabInspector.Core.Models;
using Microsoft.Win32;

namespace JabInspector.App;

public partial class ObjectRepositoryWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MainWindow _ownerWindow;

    public ObjectRepositoryWindow(MainViewModel viewModel, MainWindow ownerWindow)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _ownerWindow = ownerWindow;
        DataContext = viewModel;
        Loaded += (_, _) => RefreshEditor();
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.RepositoryEntries.CollectionChanged += RepositoryEntries_CollectionChanged;
        Closed += (_, _) =>
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.RepositoryEntries.CollectionChanged -= RepositoryEntries_CollectionChanged;
        };
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedRepositoryEntry))
        {
            RefreshEditor();
        }
    }

    private void RepositoryEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshEditor();
    }

    private void RefreshEditor()
    {
        var entry = _viewModel.SelectedRepositoryEntry;
        ObjectKeyText.Text = entry?.ObjectKey ?? "";
        FriendlyNameText.Text = entry?.FriendlyName ?? "";
        _viewModel.RefreshSelectedRepositoryPreview();
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        var entry = _viewModel.AddSelectedNodeToRepository();
        if (entry is null)
        {
            _viewModel.Log("Object Repository Manager: select an inspected Java element first.");
            return;
        }

        _viewModel.Log($"Object Repository Manager: added/refreshed {entry.ObjectKey} from current inspector selection.");
        RefreshEditor();
    }

    private void Highlight_Click(object sender, RoutedEventArgs e)
    {
        _ownerWindow.HighlightRepositorySelection();
    }

    private void RefreshFromInspector_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRepositoryEntry is null)
        {
            _viewModel.Log("Object Repository Manager: select an object to refresh.");
            return;
        }

        var currentKey = _viewModel.SelectedRepositoryEntry.ObjectKey;
        var entry = _viewModel.RefreshSelectedRepositoryEntryFromSelectedNode();
        if (entry is null) return;

        _viewModel.Log($"Object Repository Manager: refreshed {currentKey} from selected inspector element.");
        RefreshEditor();
    }

    private void ApplyName_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RenameSelectedRepositoryEntry(ObjectKeyText.Text, FriendlyNameText.Text);
        RefreshEditor();
    }

    private void AddProperty_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddPropertyToSelectedRepositoryEntry();
        RefreshEditor();
    }

    private void DeleteProperty_Click(object sender, RoutedEventArgs e)
    {
        if (PropertiesGrid.SelectedItem is not JavaRepositoryProperty property)
        {
            _viewModel.Log("Object Repository Manager: select a property to delete.");
            return;
        }

        _viewModel.DeletePropertyFromSelectedRepositoryEntry(property);
        RefreshEditor();
    }

    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        PropertiesGrid.CommitEdit();
        PropertiesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        _viewModel.ApplySelectedRepositoryPropertyEdits();
        RefreshEditor();
    }

    private void DeleteObject_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRepositoryEntry is null)
        {
            _viewModel.Log("Object Repository Manager: select an object to delete.");
            return;
        }

        var references = _viewModel.CountRecordedStepsUsingSelectedRepositoryEntry();
        var message = references == 0
            ? $"Delete repository object '{_viewModel.SelectedRepositoryEntry.ObjectKey}'?"
            : $"Repository object '{_viewModel.SelectedRepositoryEntry.ObjectKey}' is used by {references} recorded step(s).\n\nDelete the object and dependent steps?";
        var result = System.Windows.MessageBox.Show(this, message, "Delete repository object", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        _viewModel.DeleteSelectedRepositoryEntry(deleteReferencingSteps: references > 0);
        RefreshEditor();
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open object repository / recording project",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
            CheckFileExists = true,
            InitialDirectory = _viewModel.RepositoryStorageDirectory
        };
        if (dialog.ShowDialog(this) != true) return;
        _viewModel.LoadRecordingProject(dialog.FileName);
        _viewModel.Log($"Object Repository Manager: loaded repository from {dialog.FileName}.");
        RefreshEditor();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var fileName = _viewModel.GetDefaultRecordingProjectFileName();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save object repository / recording project",
            Filter = "Java recording project (*.jrecording.json)|*.jrecording.json|JSON files (*.json)|*.json",
            FileName = fileName,
            DefaultExt = ".jrecording.json",
            InitialDirectory = !string.IsNullOrWhiteSpace(_viewModel.RecordingProjectPath)
                ? (Path.GetDirectoryName(_viewModel.RecordingProjectPath) ?? _viewModel.RepositoryStorageDirectory)
                : _viewModel.RepositoryStorageDirectory
        };
        if (dialog.ShowDialog(this) != true) return;
        if (_viewModel.SaveRecordingProject(dialog.FileName))
        {
            _viewModel.Log($"Object Repository Manager: saved repository to {dialog.FileName}.");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
