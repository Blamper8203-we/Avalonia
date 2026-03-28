using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using DINBoard.Services;
using DINBoard.ViewModels;

namespace DINBoard;

public partial class HomeWindow : Window
{
    private readonly MainViewModel? _viewModel;
    private readonly MainWindow? _mainWindow;
    private readonly IDialogService? _dialogService;
    private bool _isTransitioning;

    /// <summary>Design-time constructor — wymagany przez Avalonia XAML loader (eliminuje AVLN3001).</summary>
    public HomeWindow()
    {
        InitializeComponent();
    }

    public HomeWindow(MainViewModel viewModel, MainWindow mainWindow, IDialogService dialogService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnClosed(e);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsHomeScreenVisible) &&
            !_viewModel!.IsHomeScreenVisible &&
            !_isTransitioning)
        {
            TransitionToMainWindow();
        }
    }

    private void TransitionToMainWindow()
    {
        _isTransitioning = true;
        IsEnabled = false;

        _dialogService!.Initialize(_mainWindow!);

        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _mainWindow;
        }

        _mainWindow!.Show();
        Close();
    }
}
