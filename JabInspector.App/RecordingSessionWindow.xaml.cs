using System.Windows;

namespace JabInspector.App;

public partial class RecordingSessionWindow : Window
{
    public RecordingSessionWindow(string? suggestedAlias)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(suggestedAlias))
        {
            ApplicationAliasBox.Text = suggestedAlias;
            SessionNameBox.Text = $"{suggestedAlias}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
    }

    public string SessionName => SessionNameBox.Text.Trim();
    public string ApplicationAlias => ApplicationAliasBox.Text.Trim();

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SessionNameBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Please enter a session name.", "Recording session", MessageBoxButton.OK, MessageBoxImage.Information);
            SessionNameBox.Focus();
            return;
        }

        DialogResult = true;
    }
}
