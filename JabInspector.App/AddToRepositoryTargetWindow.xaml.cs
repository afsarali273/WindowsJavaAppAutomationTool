using System.Windows;

namespace JabInspector.App;

public enum AddToRepositoryTarget
{
    Current,
    ExistingFile,
    NewFile
}

public partial class AddToRepositoryTargetWindow : Window
{
    public AddToRepositoryTarget? SelectedTarget { get; private set; }

    public AddToRepositoryTargetWindow()
    {
        InitializeComponent();
    }

    private void Current_Click(object sender, RoutedEventArgs e) => Complete(AddToRepositoryTarget.Current);

    private void Existing_Click(object sender, RoutedEventArgs e) => Complete(AddToRepositoryTarget.ExistingFile);

    private void New_Click(object sender, RoutedEventArgs e) => Complete(AddToRepositoryTarget.NewFile);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Complete(AddToRepositoryTarget target)
    {
        SelectedTarget = target;
        DialogResult = true;
        Close();
    }
}
