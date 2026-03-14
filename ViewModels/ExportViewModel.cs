using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.ViewModels.Messages;
using DINBoard.Helpers;

namespace DINBoard.ViewModels;

/// <summary>
/// Specjalistyczny ViewModel odpowiedzialny za logikę eksportu projektu (PDF, PNG, BOM).
/// Rozbija wielkiego MainViewModel zgodnie z zasadą Single Responsibility Principle (SRP).
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IDialogService _dialogService;
    private readonly PdfExportService _pdfExportService;
    private readonly BomExportService _bomExportService;

    [ObservableProperty]
    private string _exportStatusMessage = string.Empty;

    public ExportViewModel(
        MainViewModel mainViewModel,
        IDialogService dialogService,
        PdfExportService pdfExportService,
        BomExportService bomExportService)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _pdfExportService = pdfExportService ?? throw new ArgumentNullException(nameof(pdfExportService));
        _bomExportService = bomExportService ?? throw new ArgumentNullException(nameof(bomExportService));
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (_mainViewModel.CurrentProject == null) return;

        _mainViewModel.CurrentProject.Metadata ??= new ProjectMetadata();
        var updatedMetadata = await _dialogService.ShowProjectMetadataDialogAsync(_mainViewModel.CurrentProject.Metadata);
        if (updatedMetadata == null)
        {
            return;
        }

        _mainViewModel.CurrentProject.Metadata = updatedMetadata;
        _mainViewModel.HasUnsavedChanges = true;
        _mainViewModel.ForceCurrentProjectUpdate();

        var options = await _dialogService.ShowPdfExportOptionsDialogAsync();
        if (options == null)
        {
            return;
        }

        var filePath = await _dialogService.PickSaveFileAsync("Eksportuj do PDF", ".pdf", "Dokument PDF");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (_mainViewModel.Validation.HasErrors)
        {
            var confirm = await _dialogService.ShowConfirmationDialogAsync(
                "Błędy Walidacji", 
                "Projekt zawiera błędy (brak urządzeń, asymetria). Czy na pewno chcesz wygenerować plik PDF?");
            
            if (!confirm) return;
        }

        try
        {
            _mainViewModel.StatusMessage = "Generowanie PDF...";
            ExportStatusMessage = "Eksport PDF w toku...";
            await Task.Run(() => _pdfExportService.ExportToPdf(_mainViewModel, filePath, options));
            _mainViewModel.StatusMessage = $"Wyeksportowano do: {filePath}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"), 
                LocalizationHelper.GetString("ToastMsgExportPdf"), 
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd eksportu PDF: {ex.Message}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"), 
                ex.Message, 
                Controls.ToastType.Error)));
        }
    }

    [RelayCommand]
    public async Task ExportPdfQuickAsync()
    {
        if (_mainViewModel.CurrentProject == null) return;

        var options = new PdfExportOptions();
        var filePath = await _dialogService.PickSaveFileAsync("Szybki eksport PDF", ".pdf", "Dokument PDF");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            _mainViewModel.StatusMessage = "Szybki eksport PDF...";
            ExportStatusMessage = "Szybki eksport PDF w toku...";
            await Task.Run(() => _pdfExportService.ExportToPdf(_mainViewModel, filePath, options));
            _mainViewModel.StatusMessage = $"Wyeksportowano do: {filePath}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"), 
                LocalizationHelper.GetString("ToastMsgExportPdfQuick"), 
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd eksportu PDF: {ex.Message}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"), 
                ex.Message, 
                Controls.ToastType.Error)));
        }
    }

    [RelayCommand]
    public Task ExportPngCleanAsync()
    {
        return ExportPngAsync(false);
    }

    [RelayCommand]
    public Task ExportPngAnnotatedAsync()
    {
        return ExportPngAsync(true);
    }

    [RelayCommand]
    public async Task ExportBomAsync()
    {
        if (_mainViewModel.CurrentProject == null) return;

        var filePath = await _dialogService.PickSaveFileAsync(
            "Eksportuj Zestawienie Materiałowe (BOM)",
            ".csv",
            "Arkusz CSV");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            _mainViewModel.StatusMessage = "Generowanie zestawienia BOM...";
            ExportStatusMessage = "Eksport BOM w toku...";
            await Task.Run(() => _bomExportService.ExportToCsv(_mainViewModel.CurrentProject, _mainViewModel.Symbols, filePath));
            _mainViewModel.StatusMessage = $"Zapisano zestawienie: {filePath}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"), 
                LocalizationHelper.GetString("ToastMsgExportBom"), 
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd eksportu BOM: {ex.Message}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"), 
                ex.Message, 
                Controls.ToastType.Error)));
        }
    }

    private async Task ExportPngAsync(bool isAnnotated)
    {
        if (_mainViewModel.CurrentProject == null) return;

        var filePath = await _dialogService.PickSaveFileAsync(
            isAnnotated ? "Eksportuj do PNG (Z oznaczeniami)" : "Eksportuj do PNG (Czysty)",
            ".png",
            "Obraz PNG");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            _mainViewModel.StatusMessage = "Generowanie PNG...";
            ExportStatusMessage = "Eksport PNG w toku...";
            var options = new PdfExportOptions
            {
                IncludeCleanSchematic = false
            };

            await Task.Run(() => _pdfExportService.ExportToPng(_mainViewModel, filePath, options));
            _mainViewModel.StatusMessage = $"Wyeksportowano do: {filePath}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"), 
                LocalizationHelper.GetString("ToastMsgExportPng"), 
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd eksportu PNG: {ex.Message}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"), 
                ex.Message, 
                Controls.ToastType.Error)));
        }
    }
}
