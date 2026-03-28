using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DINBoard.Dialogs;

public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
    }

    private void NavigateToTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is null)
            return;

        if (!int.TryParse(button.Tag.ToString(), out var tabIndex))
            return;

        var tabs = this.FindControl<TabControl>("HelpTabs");
        if (tabs != null)
            tabs.SelectedIndex = tabIndex;
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
