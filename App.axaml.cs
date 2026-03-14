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
using DINBoard.ViewModels;
using System;
using System.Threading.Tasks;

namespace DINBoard;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    /// <summary>Główne okno aplikacji - dostępne dla dialogów</summary>
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
            var mainWindow = Services.GetRequiredService<MainWindow>();

            // Initialize DialogService with Window handle
            var dialogService = Services.GetRequiredService<IDialogService>();
            dialogService.Initialize(mainWindow);

            MainWindowInstance = mainWindow;
            desktop.MainWindow = mainWindow;
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
        services.AddTransient<MainViewModel>();

        // Views (Window)
        services.AddTransient<MainWindow>();

        // Core Services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<UndoRedoService>();
        services.AddSingleton<IModuleTypeService, ModuleTypeService>();
        services.AddSingleton<IElectricalValidationService, ElectricalValidationService>();
        services.AddSingleton<SvgProcessor>();
        services.AddSingleton<PowerBusbarGenerator>();
        services.AddSingleton<SymbolParameterService>();
        services.AddTransient<SymbolImportService>();
        services.AddTransient<PdfExportService>();
        services.AddTransient<BomExportService>();
        services.AddTransient<BusbarPlacementService>();
        services.AddTransient<ProjectPersistenceService>();
        
        // Performance Monitoring
        services.AddSingleton<MemoryMonitorService>();
    }
    public void UpdateTheme(string themeName)
    {
        if (Application.Current == null) return;

        // Base theme variant
        bool isDark = !string.Equals(themeName, "Jasny", StringComparison.OrdinalIgnoreCase);
        Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (isDark)
        {
            // Update resources for specific dark variant
            if (Application.Current.Resources.ThemeDictionaries.TryGetValue(ThemeVariant.Dark, out var darkDict))
            {
                switch (themeName)
                {
                    case "Ciemny (Granat)": // Navy (Old style)
                        SetColor(darkDict, "AppBackground", Color.Parse("#111827"));
                        SetColor(darkDict, "PanelBackground", Color.Parse("#1F2937"));
                        SetColor(darkDict, "PanelBackgroundAlt", Color.Parse("#2D3748"));
                        SetColor(darkDict, "PanelBorder", Color.Parse("#374151"));
                        SetColor(darkDict, "ToolbarBackground", Color.Parse("#1F2937"));
                        break;

                    case "Ciemny (Czerń)": // OLED Black
                        SetColor(darkDict, "AppBackground", Color.Parse("#000000"));
                        SetColor(darkDict, "PanelBackground", Color.Parse("#121212"));
                        SetColor(darkDict, "PanelBackgroundAlt", Color.Parse("#1E1E1E"));
                        SetColor(darkDict, "PanelBorder", Color.Parse("#333333"));
                        SetColor(darkDict, "ToolbarBackground", Color.Parse("#121212"));
                        break;

                    default: // Ciemny (Antracyt) - Default neutral
                        SetColor(darkDict, "AppBackground", Color.Parse("#18191A"));
                        SetColor(darkDict, "PanelBackground", Color.Parse("#242526"));
                        SetColor(darkDict, "PanelBackgroundAlt", Color.Parse("#3A3B3C"));
                        SetColor(darkDict, "PanelBorder", Color.Parse("#4E4F50"));
                        SetColor(darkDict, "ToolbarBackground", Color.Parse("#242526"));
                        break;
                }
            }
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

