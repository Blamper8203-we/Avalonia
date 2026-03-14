using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;

namespace DINBoard.Controls;

public enum ToastType { Success, Warning, Error, Info }

public partial class ToastNotification : UserControl
{
    private DispatcherTimer? _autoCloseTimer;

    public ToastNotification()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public void Show(string title, string message, ToastType type = ToastType.Success, int durationMs = 3500)
    {
        var titleText = this.FindControl<TextBlock>("TitleText");
        var messageText = this.FindControl<TextBlock>("MessageText");
        var iconBorder = this.FindControl<Border>("IconBorder");
        var toastIcon = this.FindControl<MaterialIcon>("ToastIcon");

        if (titleText != null) titleText.Text = title;
        if (messageText != null)
        {
            messageText.Text = message;
            messageText.IsVisible = !string.IsNullOrEmpty(message);
        }

        // Ustaw kolor i ikonę wg typu
        var (color, icon) = type switch
        {
            ToastType.Success => ("#22C55E", MaterialIconKind.Check),
            ToastType.Warning => ("#FACC15", MaterialIconKind.AlertOutline),
            ToastType.Error   => ("#EF4444", MaterialIconKind.AlertCircleOutline),
            ToastType.Info    => ("#3B82F6", MaterialIconKind.InformationOutline),
            _                 => ("#22C55E", MaterialIconKind.Check)
        };

        if (iconBorder != null)
            iconBorder.Background = new SolidColorBrush(Color.Parse(color));
        if (toastIcon != null)
            toastIcon.Kind = icon;

        // Pokaż z animacją
        IsVisible = true;
        Opacity = 1;
        RenderTransform = new TranslateTransform(0, 0);

        // Auto-close timer
        _autoCloseTimer?.Stop();
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            Hide();
        };
        _autoCloseTimer.Start();
    }

    public void Hide()
    {
        Opacity = 0;
        RenderTransform = new TranslateTransform(0, 20);

        // Ukryj po zakończeniu animacji
        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            IsVisible = false;
        };
        hideTimer.Start();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        _autoCloseTimer?.Stop();
        Hide();
    }
}
