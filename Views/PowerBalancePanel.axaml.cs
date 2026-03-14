using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DINBoard.ViewModels;
using Material.Icons;
using Material.Icons.Avalonia;

namespace DINBoard.Views;

public partial class PowerBalancePanel : UserControl
{
    // Converters as static properties for XAML binding
    public static IValueConverter WattsToKwConverter { get; } = new WattsToKwConverterImpl();
    public static IValueConverter CurrentToWidthConverter { get; } = new CurrentToWidthConverterImpl();

    private Border? _statusBorder;
    private MaterialIcon? _statusIcon;
    private TextBlock? _statusText;
    private MainViewModel? _viewModel;
    private PropertyChangedEventHandler? _viewModelPropertyChanged;

    public PowerBalancePanel()
    {
        InitializeComponent();

        _statusBorder = this.FindControl<Border>("StatusBorder");
        _statusIcon = this.FindControl<MaterialIcon>("StatusIcon");
        _statusText = this.FindControl<TextBlock>("StatusText");

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            AttachViewModel(vm);
        }
        else
        {
            DetachViewModel();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainViewModel vm)
        {
            AttachViewModel(vm);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachViewModel();
    }

    private void AttachViewModel(MainViewModel vm)
    {
        if (_viewModel == vm) return;
        DetachViewModel();

        _viewModel = vm;
        _viewModelPropertyChanged = OnViewModelPropertyChanged;
        _viewModel.PowerBalance.PropertyChanged += _viewModelPropertyChanged;
        UpdateStatusIndicator(_viewModel);
    }

    private void DetachViewModel()
    {
        if (_viewModel != null && _viewModelPropertyChanged != null)
        {
            _viewModel.PowerBalance.PropertyChanged -= _viewModelPropertyChanged;
        }
        _viewModel = null;
        _viewModelPropertyChanged = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_viewModel == null) return;

        if (args.PropertyName == nameof(PowerBalanceViewModel.PhaseImbalancePercent) ||
            args.PropertyName == nameof(PowerBalanceViewModel.IsPhaseBalanceOk) ||
            args.PropertyName == nameof(PowerBalanceViewModel.TotalInstalledPowerW))
        {
            UpdateStatusIndicator(_viewModel);
        }
    }

    private void UpdateStatusIndicator(MainViewModel vm)
    {
        if (_statusBorder == null || _statusIcon == null || _statusText == null)
            return;

        bool isOk = vm.PowerBalance.IsPhaseBalanceOk;
        double imbalance = vm.PowerBalance.PhaseImbalancePercent;
        var neutralBrush = ResolveBrush("PanelBorder", Brushes.Gray);
        var okBrush = ResolveBrush("AccentGreen", Brushes.Green);
        var warningBrush = ResolveBrush("AccentOrange", Brushes.Orange);
        var errorBrush = ResolveBrush("AccentRed", Brushes.Red);

        if (vm.PowerBalance.TotalInstalledPowerW == 0)
        {
            _statusBorder.Background = neutralBrush;
            _statusIcon.Kind = MaterialIconKind.InformationOutline;
            _statusIcon.Foreground = Brushes.White;
            _statusText.Text = "Brak danych";
            _statusText.Foreground = Brushes.White;
        }
        else if (imbalance <= 10)
        {
            _statusBorder.Background = okBrush;
            _statusIcon.Kind = MaterialIconKind.CheckCircle;
            _statusIcon.Foreground = Brushes.White;
            _statusText.Text = $"Bilans OK ({imbalance:N1}%)";
            _statusText.Foreground = Brushes.White;
        }
        else if (imbalance <= 15)
        {
            _statusBorder.Background = warningBrush;
            _statusIcon.Kind = MaterialIconKind.AlertCircleOutline;
            _statusIcon.Foreground = Brushes.White;
            _statusText.Text = $"Asymetria: {imbalance:N1}%";
            _statusText.Foreground = Brushes.White;
        }
        else
        {
            _statusBorder.Background = errorBrush;
            _statusIcon.Kind = MaterialIconKind.AlertOctagon;
            _statusIcon.Foreground = Brushes.White;
            _statusText.Text = $"Asymetria: {imbalance:N1}%!";
            _statusText.Foreground = Brushes.White;
        }
    }

    private IBrush ResolveBrush(string key, IBrush fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is IBrush brush)
        {
            return brush;
        }
        return fallback;
    }

    /// <summary>
    /// Converts Watts to kW for display
    /// </summary>
    private class WattsToKwConverterImpl : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double watts)
            {
                return watts / 1000.0;
            }
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts current (A) to progress bar width (px)
    /// Max width ~200px for ~40A, scales proportionally
    /// </summary>
    private class CurrentToWidthConverterImpl : IValueConverter
    {
        private const double MaxWidth = 200.0;
        private const double MaxCurrent = 40.0; // Reference max current for full bar

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double current)
            {
                if (current <= 0) return 0.0;
                
                // Scale: 40A = full width (200px)
                double width = (current / MaxCurrent) * MaxWidth;
                
                // Clamp to max width
                return Math.Min(width, MaxWidth);
            }
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
