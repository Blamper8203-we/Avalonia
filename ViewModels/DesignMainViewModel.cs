using DINBoard.Models;

namespace DINBoard.ViewModels;

/// <summary>
/// Design-time ViewModel z przykładowymi danymi do podglądu XAML.
/// Używany wyłącznie przez XAML previewer i parameterless constructor MainWindow.
/// Dziedziczy po MainViewModel (parameterless ctor), dodaje dane wizualne.
/// </summary>
public class DesignMainViewModel : MainViewModel
{
    public DesignMainViewModel()
    {
        CurrentProject = new Project
        {
            Name = "Projekt przykładowy",
            Description = "Dane podglądowe dla XAML designer",
            PowerConfig = new PowerSupplyConfig
            {
                Voltage = 400,
                MainProtection = 32,
                Phases = 3
            },
            Metadata = new ProjectMetadata
            {
                ProjectNumber = "2026/001",
                Author = "Jan Kowalski",
                Company = "Przykładowa firma"
            }
        };

        Symbols.Add(new SymbolItem
        {
            Type = "MCB",
            Label = "Q1",
            Phase = "L1",
            PowerW = 2300,
            ProtectionType = "B16",
            CircuitType = "Gniazdo",
            Location = "Pokój dzienny"
        });

        Symbols.Add(new SymbolItem
        {
            Type = "MCB",
            Label = "Q2",
            Phase = "L2",
            PowerW = 1500,
            ProtectionType = "B10",
            CircuitType = "Oświetlenie",
            Location = "Kuchnia"
        });

        Symbols.Add(new SymbolItem
        {
            Type = "MCB",
            Label = "Q3",
            Phase = "L3",
            PowerW = 3500,
            ProtectionType = "C20",
            CircuitType = "Siła",
            Location = "Garaż"
        });

        IsHomeScreenVisible = false;
        StatusMessage = "Podgląd projektanta";
        RecalculatePhaseBalance();
    }
}
