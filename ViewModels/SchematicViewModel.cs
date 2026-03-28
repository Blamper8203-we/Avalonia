using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Constants;
using DINBoard.ViewModels.Messages;
using DINBoard.Helpers;

namespace DINBoard.ViewModels;

/// <summary>
/// Zarządza logiką powiązaną bezpośrednio z rysunkiem schematu.
/// - Szyna DIN (skala, osie, widoczność)
/// - Arkusze (przenoszenie, nawigacja)
/// - Auto-Balansowanie (animacja, polecenia)
/// </summary>
public partial class SchematicViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IProjectService? _projectService;

    public SchematicViewModel(MainViewModel mainViewModel, IProjectService? projectService = null)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _projectService = projectService;
    }

    // === DIN RAIL ===

    [ObservableProperty]
    private bool _isDinRailVisible = true;

    /// <summary>SVG content of the DIN rail for PDF export</summary>
    public string? DinRailSvgContent { get; set; }

    /// <summary>Scaled dimensions of the DIN rail (width, height)</summary>
    public (double Width, double Height) DinRailSize { get; set; }

    /// <summary>Y positions of DIN rail horizontal axes (for snap-to-rail)</summary>
    public List<double> DinRailAxes { get; } = new();

    /// <summary>Skala szyny DIN</summary>
    public double DinRailScale { get; set; } = AppDefaults.DinRailScale;

    // === SHEETS ===

    [ObservableProperty]
    private int _currentSheetIndex = 0;

    [ObservableProperty]
    private bool _isSheet1Active = true;

    [ObservableProperty]
    private bool _isSheet2Active;

    [ObservableProperty]
    private bool _isSheet3Active;

    [ObservableProperty]
    private bool _isSheet4Active;

    private bool _suppressSheetSync;

    partial void OnCurrentSheetIndexChanged(int value)
    {
        _suppressSheetSync = true;
        IsSheet1Active = value == 0;
        IsSheet2Active = value == 1;
        IsSheet3Active = value == 2;
        IsSheet4Active = value == 3;
        _suppressSheetSync = false;
        WeakReferenceMessenger.Default.Send(new NavigateToSheetMessage(value));
    }

    partial void OnIsSheet1ActiveChanged(bool value)
    {
        if (_suppressSheetSync || !value) return;
        CurrentSheetIndex = 0;
    }

    partial void OnIsSheet2ActiveChanged(bool value)
    {
        if (_suppressSheetSync || !value) return;
        CurrentSheetIndex = 1;
    }

    partial void OnIsSheet3ActiveChanged(bool value)
    {
        if (_suppressSheetSync || !value) return;
        CurrentSheetIndex = 2;
    }

    partial void OnIsSheet4ActiveChanged(bool value)
    {
        if (_suppressSheetSync || !value) return;
        CurrentSheetIndex = 3;
    }

    [RelayCommand]
    public void SwitchToSheet1() => CurrentSheetIndex = 0;

    [RelayCommand]
    public void SwitchToSheet2() => CurrentSheetIndex = 1;

    [RelayCommand]
    public void SwitchToSheet3() => CurrentSheetIndex = 2;

    [RelayCommand]
    public void SwitchToSheet4() => CurrentSheetIndex = 3;

    // === CIRCUIT REFERENCES ===

    [ObservableProperty]
    private ObservableCollection<CircuitReference> _circuitReferences = new();

    public void NavigateToReference(CircuitReference reference)
    {
        if (reference == null) return;
        int targetSheet = CurrentSheetIndex == 0 ? 1 : 0;
        CurrentSheetIndex = targetSheet;
        _mainViewModel.StatusMessage = $"Przejście do: {reference.Label} (Arkusz {targetSheet + 1})";
        reference.HighlightTemporarily(5000);
    }

    // === AUTOMATIC PHASE BALANCING ===

    /// <summary>Snapshot faz sprzed ostatniego bilansowania (do Undo)</summary>
    private Dictionary<string, string>? _lastBalanceSnapshot;

    [ObservableProperty]
    private BalanceMode _selectedBalanceMode = BalanceMode.Current;

    [ObservableProperty]
    private BalanceScope _selectedBalanceScope = BalanceScope.OnlyUnlocked;

    [ObservableProperty]
    private bool _isBalancing;

    public bool CanUndoLastBalance => _lastBalanceSnapshot != null;

    [RelayCommand]
    public async Task AutoBalancePhases()
    {
        var symbols = _mainViewModel.Symbols;
        var project = _mainViewModel.CurrentProject;

        if (symbols.Count == 0 || project == null) return;

        IsBalancing = true;

        try
        {
            var voltage = project.PowerConfig?.Voltage ?? 400;
            var phaseVoltage = (int)(voltage / Math.Sqrt(3));

            Services.AppLog.Debug($"AutoBalancePhases: {symbols.Count} symboli, napięcie fazowe={phaseVoltage}V, mode={SelectedBalanceMode}, scope={SelectedBalanceScope}");

            _lastBalanceSnapshot = await PhaseDistributionCalculator.BalancePhasesAsync(
                symbols,
                SelectedBalanceMode,
                SelectedBalanceScope,
                phaseVoltage);

            OnPropertyChanged(nameof(CanUndoLastBalance));

            _mainViewModel.RecalculatePhaseBalance();
            _mainViewModel.RecalculateValidation();
            _mainViewModel.MarkProjectAsChanged();

            _mainViewModel.StatusMessage = "Automatycznie zbalansowano fazy";
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("AutoBalancePhases: Błąd bilansowania", ex);
            _mainViewModel.StatusMessage = $"Błąd bilansowania: {ex.Message}";
        }
        finally
        {
            IsBalancing = false;
        }
    }

    [RelayCommand]
    public void UndoLastBalance()
    {
        if (_lastBalanceSnapshot == null) return;

        var symbols = _mainViewModel.Symbols;

        foreach (var s in symbols)
        {
            if (_lastBalanceSnapshot.TryGetValue(s.Id, out var prevPhase))
                s.Phase = prevPhase;
        }

        _lastBalanceSnapshot = null;
        OnPropertyChanged(nameof(CanUndoLastBalance));

        _mainViewModel.RecalculatePhaseBalance();
        _mainViewModel.RecalculateValidation();
        _mainViewModel.MarkProjectAsChanged();

        _mainViewModel.StatusMessage = "Cofnięto bilansowanie faz";
    }

    // === OPCJE WIDOKU ===
    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _showDinRailAxis = true;

    [ObservableProperty]
    private bool _showPageNumbers = true;

    [ObservableProperty]
    private double _iconSize = 32.0;

    [ObservableProperty]
    private bool _isSingleLineLightTheme;

    [RelayCommand]
    public void ToggleSingleLineTheme()
    {
        IsSingleLineLightTheme = !IsSingleLineLightTheme;
    }

    partial void OnShowGridChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
            LocalizationHelper.GetString("ToastTitleViewChanged"), 
            value ? LocalizationHelper.GetString("ToastMsgGridOn") : LocalizationHelper.GetString("ToastMsgGridOff"), 
            Controls.ToastType.Info, 1500)));
    }

    partial void OnShowPageNumbersChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
            LocalizationHelper.GetString("ToastTitleViewChanged"), 
            value ? LocalizationHelper.GetString("ToastMsgPageNumOn") : LocalizationHelper.GetString("ToastMsgPageNumOff"), 
            Controls.ToastType.Info, 1500)));
    }

    partial void OnShowDinRailAxisChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
            LocalizationHelper.GetString("ToastTitleViewChanged"), 
            value ? LocalizationHelper.GetString("ToastMsgAxesOn") : LocalizationHelper.GetString("ToastMsgAxesOff"), 
            Controls.ToastType.Info, 1500)));
    }
}
