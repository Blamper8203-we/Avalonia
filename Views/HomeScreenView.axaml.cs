using Avalonia.Controls;

namespace DINBoard.Views;

public partial class HomeScreenView : UserControl
{
    public HomeScreenView()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
