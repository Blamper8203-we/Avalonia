using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace DINBoard.Services;

/// <summary>
/// Serwis do monitorowania zużycia pamięci przez aplikację.
/// Używa .NET 10 API do zbierania szczegółowych informacji o pamięci.
/// </summary>
public sealed class MemoryMonitorService : IDisposable
{
    private readonly PeriodicTimer _timer;
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;

    private long _peakMemoryBytes;
    private long _currentMemoryBytes;
    private int _gen0Collections;
    private int _gen1Collections;
    private int _gen2Collections;

    /// <summary>
    /// Interwał pomiędzy pomiarami (domyślnie 5 sekund).
    /// </summary>
    public TimeSpan MonitoringInterval { get; }

    /// <summary>
    /// Próg ostrzeżenia o wysokim zużyciu pamięci (domyślnie 500 MB).
    /// </summary>
    public long WarningThresholdBytes { get; set; } = 500_000_000;

    /// <summary>
    /// Event wywoływany gdy zużycie pamięci przekroczy próg ostrzeżenia.
    /// </summary>
    public event EventHandler<MemoryWarningEventArgs>? MemoryWarningRaised;

    /// <summary>
    /// Event wywoływany po każdym pomiarze pamięci.
    /// </summary>
    public event EventHandler<MemoryUsageReport>? MemoryMeasured;

    public MemoryMonitorService(TimeSpan? monitoringInterval = null)
    {
        MonitoringInterval = monitoringInterval ?? TimeSpan.FromSeconds(5);
        _timer = new PeriodicTimer(MonitoringInterval);
    }

    /// <summary>
    /// Rozpoczyna monitorowanie pamięci w tle.
    /// </summary>
    public void StartMonitoring()
    {
        if (_monitoringTask != null)
            return;

        _cts = new CancellationTokenSource();
        _monitoringTask = MonitorMemoryAsync(_cts.Token);
        MeasureMemory();
    }

    /// <summary>
    /// Zatrzymuje monitorowanie pamięci.
    /// </summary>
    public void StopMonitoring()
    {
        _cts?.Cancel();
        _monitoringTask = null;
    }

    private async Task MonitorMemoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                MeasureMemory();
            }
        }
        catch (OperationCanceledException)
        {
            // Normalne zakończenie
        }
    }

    private void MeasureMemory()
    {
        _currentMemoryBytes = GC.GetTotalMemory(false);
        _peakMemoryBytes = Math.Max(_peakMemoryBytes, _currentMemoryBytes);

        _gen0Collections = GC.CollectionCount(0);
        _gen1Collections = GC.CollectionCount(1);
        _gen2Collections = GC.CollectionCount(2);

        var report = GetMemoryReport();
        MemoryMeasured?.Invoke(this, report);

        if (_currentMemoryBytes > WarningThresholdBytes)
        {
            MemoryWarningRaised?.Invoke(this, new MemoryWarningEventArgs(
                _currentMemoryBytes, 
                WarningThresholdBytes,
                $"Zużycie pamięci ({FormatBytes(_currentMemoryBytes)}) przekroczyło próg ({FormatBytes(WarningThresholdBytes)})"
            ));
        }
    }

    /// <summary>
    /// Wykonuje natychmiastowy pomiar pamięci.
    /// </summary>
    public MemoryUsageReport GetMemoryReport()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        return new MemoryUsageReport
        {
            CurrentMemoryBytes = GC.GetTotalMemory(false),
            PeakMemoryBytes = _peakMemoryBytes,
            HeapSizeBytes = gcInfo.HeapSizeBytes,
            FragmentedBytes = gcInfo.FragmentedBytes,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            TotalAllocatedBytes = GC.GetTotalAllocatedBytes(false),
            MeasuredAt = DateTime.Now
        };
    }

    /// <summary>
    /// Wymusza pełne czyszczenie pamięci (Garbage Collection).
    /// Uwaga: Używać oszczędnie, może spowodować chwilowe spowolnienie.
    /// </summary>
    public void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// Formatuje liczbę bajtów do czytelnej postaci (KB, MB, GB).
    /// Używa InvariantCulture dla spójności.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", bytes / 1_073_741_824.0),
            >= 1_048_576 => string.Format(CultureInfo.InvariantCulture, "{0:F2} MB", bytes / 1_048_576.0),
            >= 1_024 => string.Format(CultureInfo.InvariantCulture, "{0:F2} KB", bytes / 1_024.0),
            _ => $"{bytes} B"
        };
    }

    public void Dispose()
    {
        StopMonitoring();
        _cts?.Dispose();
        _timer.Dispose();
    }
}

/// <summary>
/// Raport o zużyciu pamięci.
/// </summary>
public record MemoryUsageReport
{
    public long CurrentMemoryBytes { get; init; }
    public long PeakMemoryBytes { get; init; }
    public long HeapSizeBytes { get; init; }
    public long FragmentedBytes { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public long TotalAllocatedBytes { get; init; }
    public DateTime MeasuredAt { get; init; }

    /// <summary>
    /// Zwraca sformatowany ciąg znaków z informacjami o pamięci.
    /// </summary>
    public override string ToString()
    {
        return $"Pamięć: {MemoryMonitorService.FormatBytes(CurrentMemoryBytes)} " +
               $"(szczyt: {MemoryMonitorService.FormatBytes(PeakMemoryBytes)}) | " +
               $"GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
    }
}

/// <summary>
/// Argumenty eventu ostrzeżenia o pamięci.
/// </summary>
public record MemoryWarningEventArgs(
    long CurrentMemoryBytes,
    long ThresholdBytes,
    string Message
);
