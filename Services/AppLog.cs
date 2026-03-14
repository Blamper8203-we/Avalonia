using System;
using System.Diagnostics;
using System.IO;

namespace DINBoard.Services;

public static class AppLog
{
    private static readonly object _initLock = new();
    private static bool _initialized;
    private static readonly List<TraceListener> _listeners = new();

    public static void Initialize(string? logDirectory = null)
    {
        lock (_initLock)
        {
            if (_initialized) return;

            var baseDir = logDirectory ??
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DINBoard", "Logs");
            Directory.CreateDirectory(baseDir);

            var filePath = Path.Combine(baseDir, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
            var fileListener = new TextWriterTraceListener(filePath);
            Trace.Listeners.Add(fileListener);
            _listeners.Add(fileListener);

            var localPath = Path.Combine(AppContext.BaseDirectory, "app-debug.log");
            var localListener = new TextWriterTraceListener(localPath);
            Trace.Listeners.Add(localListener);
            _listeners.Add(localListener);

            Trace.AutoFlush = true;
            _initialized = true;
        }
    }

    /// <summary>
    /// Zamyka listenery logów — wywołaj przy zamykaniu aplikacji.
    /// </summary>
    public static void Shutdown()
    {
        Trace.Flush();
        foreach (var listener in _listeners)
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
        _listeners.Clear();
    }

    private static void Write(string level, string message)
    {
        Trace.WriteLine($"[{DateTime.UtcNow:HH:mm:ss} {level}] {message}");
    }

    [Conditional("DEBUG")]
    public static void Debug(string message) => Write("DEBUG", message);

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Warn(string message, Exception? ex)
    {
        Write("WARN", message);
        if (ex != null)
        {
            Trace.WriteLine(ex.ToString());
        }
    }

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message);
        if (ex != null)
        {
            Trace.WriteLine(ex.ToString());
        }
    }

    public static void Fatal(Exception ex, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(ex);
        Write("FATAL", message ?? $"Unhandled exception: {ex.GetType().Name}");
        Trace.WriteLine(ex.ToString());
        if (ex.InnerException != null)
        {
            Trace.WriteLine("Inner exception:");
            Trace.WriteLine(ex.InnerException.ToString());
        }
        Trace.Flush();
    }
}
