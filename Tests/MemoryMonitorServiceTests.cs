using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DINBoard.Services;

namespace Avalonia.Tests;

public class MemoryMonitorServiceTests : IDisposable
{
    private readonly MemoryMonitorService _service;

    public MemoryMonitorServiceTests()
    {
        _service = new MemoryMonitorService(TimeSpan.FromMilliseconds(100));
    }

    public void Dispose()
    {
        _service.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetMemoryReport_ShouldReturnValidData()
    {
        // Act
        var report = _service.GetMemoryReport();

        // Assert
        Assert.True(report.CurrentMemoryBytes > 0);
        Assert.True(report.HeapSizeBytes > 0);
        Assert.True(report.Gen0Collections >= 0);
        Assert.NotEqual(default, report.MeasuredAt);
    }

    [Fact]
    public void FormatBytes_ShouldFormatCorrectly()
    {
        // Assert
        Assert.Equal("500 B", MemoryMonitorService.FormatBytes(500));
        Assert.Equal("1.00 KB", MemoryMonitorService.FormatBytes(1024));
        Assert.Equal("1.00 MB", MemoryMonitorService.FormatBytes(1_048_576));
        Assert.Equal("1.00 GB", MemoryMonitorService.FormatBytes(1_073_741_824));
    }

    [Fact]
    public void MemoryReport_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var report = _service.GetMemoryReport();

        // Act
        var str = report.ToString();

        // Assert
        Assert.Contains("Pamięć:", str);
        Assert.Contains("GC:", str);
    }

    [Fact]
    public async Task StartMonitoring_ShouldRaiseMemoryMeasuredEvent()
    {
        // Arrange
        var eventRaised = false;
        _service.MemoryMeasured += (_, _) => eventRaised = true;

        // Act
        _service.StartMonitoring();
        await Task.Delay(200); // Wait for at least one tick
        _service.StopMonitoring();

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void WarningThresholdBytes_ShouldBeConfigurable()
    {
        // Arrange
        var newThreshold = 100_000_000L;

        // Act
        _service.WarningThresholdBytes = newThreshold;

        // Assert
        Assert.Equal(newThreshold, _service.WarningThresholdBytes);
    }

    [Fact]
    public void ForceGarbageCollection_ShouldNotThrow()
    {
        // Act & Assert - just verify it doesn't throw
        var exception = Record.Exception(() => _service.ForceGarbageCollection());
        Assert.Null(exception);
    }
}
