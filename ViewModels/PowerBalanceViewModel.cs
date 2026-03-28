using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DINBoard.Models;

namespace DINBoard.ViewModels;

/// <summary>
/// ViewModel obsługujący bilans i obliczenia mocy na fazach.
/// Odpowiada za:
/// - Moc zainstalowaną (L1PowerW, L2PowerW, L3PowerW)
/// - Prądy fazowe (L1CurrentA, L2CurrentA, L3CurrentA)
/// - Asymetrię obciążenia (PhaseImbalancePercent)
/// - Moc całkowitą i obliczeniową
/// - Sprawdzenie balansu (IsPhaseBalanceOk)
/// </summary>
public partial class PowerBalanceViewModel : ObservableObject
{
    /// <summary>Moc na fazie L1 [W]</summary>
    [ObservableProperty]
    private double _l1PowerW;

    /// <summary>Moc na fazie L2 [W]</summary>
    [ObservableProperty]
    private double _l2PowerW;

    /// <summary>Moc na fazie L3 [W]</summary>
    [ObservableProperty]
    private double _l3PowerW;

    /// <summary>Prąd na fazie L1 [A]</summary>
    [ObservableProperty]
    private double _l1CurrentA;

    /// <summary>Prąd na fazie L2 [A]</summary>
    [ObservableProperty]
    private double _l2CurrentA;

    /// <summary>Prąd na fazie L3 [A]</summary>
    [ObservableProperty]
    private double _l3CurrentA;

    /// <summary>Asymetria obciążenia faz [%]</summary>
    [ObservableProperty]
    private double _phaseImbalancePercent;

    /// <summary>Całkowita moc zainstalowana [W]</summary>
    [ObservableProperty]
    private double _totalInstalledPowerW;

    /// <summary>Moc obliczeniowa (z wsp. jednoczesności) [W]</summary>
    [ObservableProperty]
    private double _calculatedPowerW;

    /// <summary>Współczynnik jednoczesności</summary>
    [ObservableProperty]
    private double _simultaneityFactor = 0.8;

    /// <summary>Moc przyłączeniowa z konfiguracji projektu [W]</summary>
    [ObservableProperty]
    private double _connectionPowerW;

    /// <summary>Wykorzystanie mocy przyłączeniowej [%]</summary>
    [ObservableProperty]
    private double _connectionPowerUsagePercent;

    /// <summary>Rezerwa mocy względem mocy obliczeniowej [W]</summary>
    [ObservableProperty]
    private double _connectionPowerReserveW;

    /// <summary>Czy moc obliczeniowa przekracza moc przyłączeniową</summary>
    [ObservableProperty]
    private bool _isConnectionPowerExceeded;

    /// <summary>Czy bilans faz jest OK (asymetria <= 10%)</summary>
    public bool IsPhaseBalanceOk => PhaseImbalancePercent <= 10.0;

    /// <summary>Maksymalny prąd spośród wszystkich faz [A]</summary>
    public double MaxPhaseCurrentA => Math.Max(L1CurrentA, Math.Max(L2CurrentA, L3CurrentA));

    /// <summary>Czy w projekcie ustawiono moc przyłączeniową większą od zera</summary>
    public bool HasConnectionPowerLimit => ConnectionPowerW > 0;

    /// <summary>Status porównania mocy obliczeniowej do mocy przyłączeniowej</summary>
    public string ConnectionPowerStatusText =>
        !HasConnectionPowerLimit
            ? "Brak limitu mocy (ustaw w zakladce Konfiguracja)"
            : IsConnectionPowerExceeded
                ? "Przekroczono moc przylaczeniowa"
                : "OK";

    /// <summary>Czy status mocy przyłączeniowej jest poprawny</summary>
    public bool IsConnectionPowerStatusOk => HasConnectionPowerLimit && !IsConnectionPowerExceeded;

    /// <summary>Czy status mocy przyłączeniowej wskazuje przekroczenie</summary>
    public bool IsConnectionPowerStatusError => HasConnectionPowerLimit && IsConnectionPowerExceeded;

    /// <summary>Czy status mocy przyłączeniowej jest neutralny (brak limitu)</summary>
    public bool IsConnectionPowerStatusNeutral => !HasConnectionPowerLimit;

    partial void OnPhaseImbalancePercentChanged(double value)
    {
        OnPropertyChanged(nameof(IsPhaseBalanceOk));
    }

    partial void OnL1CurrentAChanged(double value) => OnPropertyChanged(nameof(MaxPhaseCurrentA));
    partial void OnL2CurrentAChanged(double value) => OnPropertyChanged(nameof(MaxPhaseCurrentA));
    partial void OnL3CurrentAChanged(double value) => OnPropertyChanged(nameof(MaxPhaseCurrentA));
    partial void OnCalculatedPowerWChanged(double value) => UpdateConnectionPowerStatus();

    partial void OnSimultaneityFactorChanged(double value)
    {
        CalculatedPowerW = TotalInstalledPowerW * SimultaneityFactor;
    }

    partial void OnConnectionPowerWChanged(double value)
    {
        OnPropertyChanged(nameof(HasConnectionPowerLimit));
        RaiseConnectionStatusProperties();
        UpdateConnectionPowerStatus();
    }

    partial void OnIsConnectionPowerExceededChanged(bool value) => RaiseConnectionStatusProperties();

    private void RaiseConnectionStatusProperties()
    {
        OnPropertyChanged(nameof(ConnectionPowerStatusText));
        OnPropertyChanged(nameof(IsConnectionPowerStatusOk));
        OnPropertyChanged(nameof(IsConnectionPowerStatusError));
        OnPropertyChanged(nameof(IsConnectionPowerStatusNeutral));
    }

    private void UpdateConnectionPowerStatus()
    {
        if (ConnectionPowerW <= 0)
        {
            ConnectionPowerUsagePercent = 0;
            ConnectionPowerReserveW = 0;
            IsConnectionPowerExceeded = false;
            return;
        }

        ConnectionPowerUsagePercent = CalculatedPowerW / ConnectionPowerW * 100.0;
        ConnectionPowerReserveW = ConnectionPowerW - CalculatedPowerW;
        IsConnectionPowerExceeded = CalculatedPowerW > ConnectionPowerW;
    }

    /// <summary>
    /// Przelicza moc i prądy na podstawie symboli
    /// Oblicza: moc na każdej fazie, prądy, asymetrię, moc całkowitą i obliczeniową
    /// </summary>
    /// <param name="symbols">Kolekcja symboli modułów</param>
    /// <param name="project">Bieżący projekt (zawiera napięcie zasilania)</param>
    public void RecalculatePhaseBalance(ObservableCollection<SymbolItem> symbols, Project? project)
    {
        var dist = Services.PhaseDistributionCalculator.CalculateTotalDistribution(symbols);
        L1PowerW = dist.L1PowerW;
        L2PowerW = dist.L2PowerW;
        L3PowerW = dist.L3PowerW;

        // Napięcie fazowe (linia-neutral) = napięcie międzyfazowe / √3
        // Np. 400V międzyfazowe → 230V fazowe
        var lineVoltage = project?.PowerConfig?.Voltage ?? 400;
        var phaseVoltage = lineVoltage / Math.Sqrt(3);

        // cosφ = 0.9 — spójne z ElectricalValidationService i PhaseDistributionCalculator
        const double cosPhi = 0.9;
        L1CurrentA = phaseVoltage > 0 ? L1PowerW / (phaseVoltage * cosPhi) : 0;
        L2CurrentA = phaseVoltage > 0 ? L2PowerW / (phaseVoltage * cosPhi) : 0;
        L3CurrentA = phaseVoltage > 0 ? L3PowerW / (phaseVoltage * cosPhi) : 0;

        TotalInstalledPowerW = L1PowerW + L2PowerW + L3PowerW;
        ConnectionPowerW = Math.Max(0, (project?.PowerConfig?.PowerKw ?? 0) * 1000.0);
        CalculatedPowerW = TotalInstalledPowerW * SimultaneityFactor;

        // Asymetria liczona po prądzie — spójna z walidacją
        PhaseImbalancePercent = Services.PhaseDistributionCalculator.CalculateImbalancePercent(L1CurrentA, L2CurrentA, L3CurrentA);
    }
}
