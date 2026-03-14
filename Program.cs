using System;
using Avalonia;
using Avalonia.Diagnostics;
using DINBoard.Services;

namespace DINBoard;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_debug.log");
            System.IO.File.WriteAllText(logPath, ex.ToString());
            Console.WriteLine("CRASH CAUGHT: " + ex.ToString());
            AppLog.Fatal(ex, "Fatal error during application startup");
            Environment.Exit(1);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
