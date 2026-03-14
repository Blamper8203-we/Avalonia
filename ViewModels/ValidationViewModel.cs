using System;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DINBoard.Models;
using DINBoard.Services;

namespace DINBoard.ViewModels;

/// <summary>
/// Odpowiada za zarządzanie komunikatami walidacji, błędami i ostrzeżeniami na schemacie.
/// Posiada listę komunikatów i podsumowanie problemów.
/// </summary>
public partial class ValidationViewModel : ObservableObject
{
    private readonly IElectricalValidationService _validationService;

    [ObservableProperty]
    private ObservableCollection<ValidationMessage> _validationMessages = new();

    [ObservableProperty]
    private ObservableCollection<ValidationMessage> _validationPreviewMessages = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    private int _validationErrorCount;

    public bool HasErrors => ValidationErrorCount > 0;

    [ObservableProperty]
    private int _validationWarningCount;

    [ObservableProperty]
    private string _validationSummary = "Brak problemów walidacji";

    [ObservableProperty]
    private bool _hasValidationMessages;

    public ValidationViewModel(IElectricalValidationService validationService)
    {
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    /// <summary>
    /// Przelicza na nowo walidację z uwzględnieniem obwodów i symboli.
    /// Zwraca true jeśli wykryto nowe ostrzeżenia lub błędy.
    /// </summary>
    public bool RecalculateValidation(Project project, ObservableCollection<SymbolItem> symbols)
    {
        var result = _validationService.ValidateProject(project, symbols);

        ValidationMessages.Clear();
        foreach (var message in result.AllMessages)
            ValidationMessages.Add(message);

        ValidationPreviewMessages.Clear();
        foreach (var message in result.AllMessages.Take(3))
            ValidationPreviewMessages.Add(message);

        ValidationErrorCount = result.Errors.Count;
        ValidationWarningCount = result.Warnings.Count;
        HasValidationMessages = ValidationMessages.Count > 0;

        ValidationSummary = result.Errors.Count > 0
            ? $"Błędy: {ValidationErrorCount}, Ostrzeżenia: {ValidationWarningCount}"
            : result.Warnings.Count > 0
            ? $"Ostrzeżenia: {ValidationWarningCount}"
            : "Brak problemów walidacji";

        return HasValidationMessages;
    }
}
