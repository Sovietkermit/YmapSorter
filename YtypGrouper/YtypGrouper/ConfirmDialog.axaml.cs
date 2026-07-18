using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YtypGrouper;

public partial class ConfirmDialog : Window
{
    public bool Result { get; private set; }

    public ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        TbTitle.Text   = title;
        TbMessage.Text = message;
    }

    private void BtnYes_Click(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void BtnNo_Click(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
