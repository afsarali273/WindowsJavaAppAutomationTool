using System.Windows;

namespace JabInspector.App;

public partial class CustomRepositoryTemplateWindow : Window
{
    public string RepositoryName => RepositoryNameText.Text.Trim();
    public string ApplicationAlias => ApplicationAliasText.Text.Trim();

    public CustomRepositoryTemplateWindow(string suggestedName, string suggestedAlias)
    {
        InitializeComponent();
        RepositoryNameText.Text = suggestedName;
        ApplicationAliasText.Text = suggestedAlias;
        Loaded += (_, _) => RepositoryNameText.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RepositoryName))
        {
            System.Windows.MessageBox.Show(this, "Enter a repository name before continuing.", "Repository name required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
