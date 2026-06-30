using System.Windows;

namespace JabInspector.App;

public partial class LocatorDetailsWindow : Window
{
    private Action? _highlightAction;
    private Action? _addToRepositoryAction;

    public LocatorDetailsWindow(string title, string subtitle, string details)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        DetailsText.Text = details;
    }

    public void EnableRepositoryActions(Action highlightAction, Action addToRepositoryAction)
    {
        _highlightAction = highlightAction;
        _addToRepositoryAction = addToRepositoryAction;
        HighlightButton.Visibility = Visibility.Visible;
        AddToRepositoryButton.Visibility = Visibility.Visible;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(DetailsText.Text))
        {
            System.Windows.Clipboard.SetText(DetailsText.Text);
        }
    }

    private void Highlight_Click(object sender, RoutedEventArgs e) => _highlightAction?.Invoke();

    private void AddToRepository_Click(object sender, RoutedEventArgs e) => _addToRepositoryAction?.Invoke();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
