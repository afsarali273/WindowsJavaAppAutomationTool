using System.Windows;

namespace JabInspector.App;

public partial class LocatorDetailsWindow : Window
{
    public LocatorDetailsWindow(string title, string subtitle, string details)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        DetailsText.Text = details;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(DetailsText.Text))
        {
            System.Windows.Clipboard.SetText(DetailsText.Text);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
