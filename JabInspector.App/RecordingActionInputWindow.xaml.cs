using System.Windows;

namespace JabInspector.App;

public partial class RecordingActionInputWindow : Window
{
    public string EnteredText => ValueTextBox.Text;

    public RecordingActionInputWindow(string title, string description, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        TitleBlock.Text = title;
        DescriptionBlock.Text = description;
        ValueTextBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        };
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
