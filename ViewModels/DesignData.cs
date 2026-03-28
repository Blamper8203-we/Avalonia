namespace DINBoard.ViewModels;

/// <summary>
/// Statyczne instancje design-time ViewModeli dla XAML previewer.
/// Używane przez d:DataContext="{x:Static vm:DesignData.MainViewModel}".
/// </summary>
public static class DesignData
{
    public static DesignMainViewModel MainViewModel { get; } = new();
}
