using Avalonia.Controls;

namespace PoeRedux.Views;

/// <summary>Asks before an action that cannot be undone. The safe choice has the focus and answers Enter and Esc.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    public ConfirmDialog(string title, string message, string confirm, string cancel) : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirm;
        CancelButton.Content = cancel;
        ConfirmButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
        Opened += (_, _) => CancelButton.Focus();
    }
}
