using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using DINBoard.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DINBoard;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    /// <summary>Główne okno aplikacji dostępne dla dialogów.</summary>
    public static MainWindow? MainWindowInstance { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        AppLog.Initialize();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainViewModel);
            var homeWindow = new HomeWindow(mainViewModel, mainWindow, Services.GetRequiredService<IDialogService>());
            var fileAssociationService = Services.GetService<FileAssociationService>();
            fileAssociationService?.EnsureDinboardAssociation();

            // Initialize DialogService with Window handle
            var dialogService = Services.GetRequiredService<IDialogService>();
            dialogService.Initialize(homeWindow);

            MainWindowInstance = mainWindow;
            desktop.MainWindow = homeWindow;

            var startupProjectPath = TryGetStartupProjectPath(Program.StartupArgs);
            if (!string.IsNullOrWhiteSpace(startupProjectPath))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await mainViewModel.Workspace.OpenRecentProjectAsync(startupProjectPath).ConfigureAwait(true);
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Fatal(e.Exception, "UI thread exception");
        ShowFatalDialog(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLog.Fatal(ex, "Unhandled exception");
            ShowFatalDialog(ex);
        }
        else
        {
            AppLog.Error("Unhandled exception (unknown type)");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Fatal(e.Exception, "Unobserved task exception");
        ShowFatalDialog(e.Exception);
        e.SetObserved();
    }

    private void ShowFatalDialog(Exception ex)
    {
        var dialogService = Services.GetService<IDialogService>();
        if (dialogService == null) return;

        Dispatcher.UIThread.Post(async () =>
        {
            await dialogService.ShowMessageAsync("Błąd aplikacji", ex.Message);
        });
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddSingleton<MainViewModel>(sp => new MainViewModel(new MainViewModelDeps(
            sp.GetRequiredService<IProjectService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<UndoRedoService>(),
            sp.GetRequiredService<IModuleTypeService>(),
            sp.GetRequiredService<SymbolImportService>(),
            sp.GetRequiredService<ProjectPersistenceService>(),
            sp.GetRequiredService<IElectricalValidationService>(),
            sp.GetRequiredService<PdfExportService>(),
            sp.GetRequiredService<BomExportService>(),
            sp.GetRequiredService<LatexExportService>(),
            sp.GetRequiredService<BusbarPlacementService>(),
            sp.GetRequiredService<LicenseService>(),
            sp.GetRequiredService<RecentProjectsService>())));

        // Core Services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<FileAssociationService>();
        services.AddSingleton<UndoRedoService>();
        services.AddSingleton<IModuleTypeService, ModuleTypeService>();
        services.AddValidationServices();
        services.AddSingleton<LicenseService>();
        services.AddSingleton<RecentProjectsService>();
        services.AddSingleton<SvgProcessor>();
        services.AddSingleton<PowerBusbarGenerator>();
        services.AddSingleton<SymbolParameterService>();
        services.AddTransient<SymbolImportService>();
        services.AddTransient<PdfExportService>();
        services.AddTransient<BomExportService>();
        services.AddTransient<LatexExportService>();
        services.AddTransient<BusbarPlacementService>();
        services.AddTransient<ProjectPersistenceService>();

        // Performance Monitoring
        services.AddSingleton<MemoryMonitorService>();
    }

    private static string? TryGetStartupProjectPath(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return null;
        }

        foreach (var rawArg in args)
        {
            if (string.IsNullOrWhiteSpace(rawArg))
            {
                continue;
            }

            if (rawArg.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            string candidate = rawArg.Trim().Trim('"');
            if (!File.Exists(candidate))
            {
                continue;
            }

            var extension = Path.GetExtension(candidate);
            if (!extension.Equals(".dinboard", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Path.GetFullPath(candidate);
        }

        return null;
    }

    public void UpdateTheme(string themeName)
    {
        if (Application.Current == null) return;

        bool isDark = !string.Equals(themeName, "Jasny", StringComparison.OrdinalIgnoreCase);
        Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (isDark &&
            Application.Current.Resources.ThemeDictionaries.TryGetValue(ThemeVariant.Dark, out var darkDict))
        {
            // Only the neutral dark theme remains available in the UI.
            SetColor(darkDict, "AppBackground", Color.Parse("#18191A"));
            SetColor(darkDict, "PanelBackground", Color.Parse("#242526"));
            SetColor(darkDict, "PanelBackgroundAlt", Color.Parse("#3A3B3C"));
            SetColor(darkDict, "PanelBorder", Color.Parse("#4E4F50"));
            SetColor(darkDict, "ToolbarBackground", Color.Parse("#242526"));
        }
    }

    private void SetColor(object? dictionary, string key, Color color)
    {
        if (dictionary is ResourceDictionary dict)
        {
            if (dict.TryGetValue(key, out var resource) && resource is SolidColorBrush brush)
            {
                brush.Color = color;
            }
            else
            {
                dict[key] = new SolidColorBrush(color);
            }
        }
    }
}

public static class DragDropFormats
{
    public static readonly DataFormat<string> ModuleType = DataFormat.CreateStringApplicationFormat("DINBoard.ModuleType");
    public static readonly DataFormat<string> ModuleName = DataFormat.CreateStringApplicationFormat("DINBoard.ModuleName");
    public static readonly DataFormat<string> ModuleFilePath = DataFormat.CreateStringApplicationFormat("DINBoard.ModuleFilePath");
}
