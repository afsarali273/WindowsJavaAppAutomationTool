using System.Windows;

namespace JabInspector.App;

public partial class JavaCodePreviewWindow : Window
{
    public JavaCodePreviewWindow(string repositoryCode, string inlineLocatorCode)
    {
        InitializeComponent();
        RepositoryCodeText.Text = repositoryCode;
        InlineCodeText.Text = inlineLocatorCode;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var code = CodeTabs.SelectedIndex == 1 ? InlineCodeText.Text : RepositoryCodeText.Text;
        if (!string.IsNullOrWhiteSpace(code))
        {
            System.Windows.Clipboard.SetText(code);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
