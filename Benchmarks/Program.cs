using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Globalization;

namespace DINBoard.Benchmarks;

/// <summary>
/// Standalone benchmarki dla formatowania bajtów.
/// Uruchom: dotnet run -c Release
/// </summary>
[MemoryDiagnoser]
public class FormatBytesBenchmarks
{
    [Benchmark]
    public string FormatBytes_Bytes()
    {
        return FormatBytes(500);
    }

    [Benchmark]
    public string FormatBytes_KB()
    {
        return FormatBytes(1_024);
    }

    [Benchmark]
    public string FormatBytes_MB()
    {
        return FormatBytes(1_048_576);
    }

    [Benchmark]
    public string FormatBytes_GB()
    {
        return FormatBytes(1_073_741_824);
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", bytes / 1_073_741_824.0),
            >= 1_048_576 => string.Format(CultureInfo.InvariantCulture, "{0:F2} MB", bytes / 1_048_576.0),
            >= 1_024 => string.Format(CultureInfo.InvariantCulture, "{0:F2} KB", bytes / 1_024.0),
            _ => $"{bytes} B"
        };
    }
}

/// <summary>
/// Benchmark dla GC API (standalone - nie wymaga serwisów).
/// </summary>
[MemoryDiagnoser]
public class GcApiBenchmarks
{
    [Benchmark]
    public long GetTotalMemory()
    {
        return GC.GetTotalMemory(false);
    }

    [Benchmark]
    public GCMemoryInfo GetGCMemoryInfo()
    {
        return GC.GetGCMemoryInfo();
    }

    [Benchmark]
    public int GetCollectionCount_Gen0()
    {
        return GC.CollectionCount(0);
    }

    [Benchmark]
    public long GetTotalAllocatedBytes()
    {
        return GC.GetTotalAllocatedBytes(false);
    }
}

/// <summary>
/// Benchmark dla alokacji i operacji stringowych.
/// </summary>
[MemoryDiagnoser]
public class AllocationBenchmarks
{
    [Benchmark(Baseline = true)]
    public object CreateSmallObject()
    {
        return new object();
    }

    [Benchmark]
    public string StringConcat_Small()
    {
        return "MCB" + 1.ToString() + "P";
    }

    [Benchmark]
    public string StringInterpolation_Small()
    {
        return $"MCB{1}P";
    }

    [Benchmark]
    public string StringFormat_Medium()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F2} MB", 100.5);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║   DINBoard Performance Benchmarks        ║");
        Console.WriteLine("║   .NET 10 + BenchmarkDotNet              ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();
        
        // Uruchom wszystkie benchmarki
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}
