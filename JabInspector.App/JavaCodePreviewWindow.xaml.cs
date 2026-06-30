using System.Windows;

namespace JabInspector.App;

public partial class JavaCodePreviewWindow : Window
{
    public JavaCodePreviewWindow(string code)
    {
        InitializeComponent();
        CodeText.Text = code;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CodeText.Text))
        {
            System.Windows.Clipboard.SetText(CodeText.Text);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
