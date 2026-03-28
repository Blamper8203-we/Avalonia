using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.Helpers;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using DINBoard.ViewModels.Messages;

namespace DINBoard.ViewModels;

/// <summary>
/// ViewModel odpowiedzialny za logike eksportu projektu (PDF, PNG, BOM, LaTeX).
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private const string PdfLongReasonRendering = "Eksport PDF renderuje kilka stron schematow w wysokiej rozdzielczosci (Skia + PNG), dlatego przy wiekszych projektach moze potrwac dluzej.";
    private const string PdfLongReasonFinalization = "Koncowy etap sklada i kompresuje strony do jednego dokumentu PDF. Czas zalezy od liczby stron i rozmiaru grafik.";
    private const string PngLongReason = "Eksport PNG tworzy obraz o wysokiej jakosci. Przy wielu modulach i duzym obszarze schematu render trwa dluzej.";
    private const string PdfPreviewLongReason = "Podglad renderuje wszystkie strony dokumentu PDF jako obrazy, dlatego przy wiekszych projektach moze to potrwac.";

    private readonly MainViewModel _mainViewModel;
    private readonly IDialogService _dialogService;
    private readonly PdfExportService _pdfExportService;
    private readonly BomExportService _bomExportService;
    private readonly LatexExportService _latexExportService;
    private readonly List<byte[]> _pdfPreviewPages = new();
    private Bitmap? _currentPdfPreviewImage;

    [ObservableProperty]
    private string _exportStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isExportInProgress;

    [ObservableProperty]
    private int _exportProgressPercent;

    [ObservableProperty]
    private string _exportProgressStage = string.Empty;

    [ObservableProperty]
    private string _exportLongOperationReason = string.Empty;

    [ObservableProperty]
    private bool _isPdfPreviewInProgress;

    [ObservableProperty]
    private int _pdfPreviewProgressPercent;

    [ObservableProperty]
    private string _pdfPreviewStatusMessage = "Podglad PDF nie jest jeszcze wygenerowany.";

    [ObservableProperty]
    private string _pdfPreviewLongOperationReason = string.Empty;

    [ObservableProperty]
    private int _pdfPreviewPageIndex;

    [ObservableProperty]
    private int _pdfPreviewPageCount;

    [ObservableProperty]
    private bool _isPdfPreviewDirty = true;

    public Bitmap? CurrentPdfPreviewImage
    {
        get => _currentPdfPreviewImage;
        private set
        {
            if (ReferenceEquals(_currentPdfPreviewImage, value))
            {
                return;
            }

            var previous = _currentPdfPreviewImage;
            _currentPdfPreviewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPdfPreview));
            OnPropertyChanged(nameof(IsPdfPreviewEmpty));
            previous?.Dispose();
        }
    }

    public bool HasPdfPreview => CurrentPdfPreviewImage != null && PdfPreviewPageCount > 0;

    public bool IsPdfPreviewEmpty => !HasPdfPreview;

    public bool CanGoToPreviousPdfPreviewPage => PdfPreviewPageIndex > 0;

    public bool CanGoToNextPdfPreviewPage => PdfPreviewPageIndex < PdfPreviewPageCount - 1;

    public string PdfPreviewPageLabel => PdfPreviewPageCount == 0
        ? "Strona 0/0"
        : $"Strona {PdfPreviewPageIndex + 1}/{PdfPreviewPageCount}";

    public ExportViewModel(
        MainViewModel mainViewModel,
        IDialogService dialogService,
        PdfExportService pdfExportService,
        BomExportService bomExportService,
        LatexExportService latexExportService)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _pdfExportService = pdfExportService ?? throw new ArgumentNullException(nameof(pdfExportService));
        _bomExportService = bomExportService ?? throw new ArgumentNullException(nameof(bomExportService));
        _latexExportService = latexExportService ?? throw new ArgumentNullException(nameof(latexExportService));
    }

    partial void OnPdfPreviewPageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(PdfPreviewPageLabel));
        OnPropertyChanged(nameof(CanGoToPreviousPdfPreviewPage));
        OnPropertyChanged(nameof(CanGoToNextPdfPreviewPage));
    }

    partial void OnPdfPreviewPageCountChanged(int value)
    {
        OnPropertyChanged(nameof(PdfPreviewPageLabel));
        OnPropertyChanged(nameof(CanGoToPreviousPdfPreviewPage));
        OnPropertyChanged(nameof(CanGoToNextPdfPreviewPage));
        OnPropertyChanged(nameof(HasPdfPreview));
        OnPropertyChanged(nameof(IsPdfPreviewEmpty));
    }

    [RelayCommand]
    public async Task RefreshPdfPreviewAsync()
    {
        if (_mainViewModel.CurrentProject == null)
        {
            MarkPdfPreviewDirty(clearCurrentPreview: true);
            PdfPreviewStatusMessage = "Brak aktywnego projektu do podgladu PDF.";
            _mainViewModel.StatusMessage = PdfPreviewStatusMessage;
            return;
        }

        if (IsPdfPreviewInProgress)
        {
            return;
        }

        try
        {
            StartPdfPreviewUi();
            var options = new PdfExportOptions();
            var progress = new Progress<int>(ReportPdfPreviewProgress);
            var previewPages = await _pdfExportService.GeneratePdfPreviewImagesAsync(_mainViewModel, options, progress);
            ReplacePdfPreviewPages(previewPages);

            if (_pdfPreviewPages.Count == 0)
            {
                CurrentPdfPreviewImage = null;
                IsPdfPreviewDirty = false;
                PdfPreviewStatusMessage = "Podglad PDF nie zawiera zadnych stron.";
                _mainViewModel.StatusMessage = PdfPreviewStatusMessage;
                return;
            }

            SetPdfPreviewPage(0);
            PdfPreviewProgressPercent = 100;
            PdfPreviewStatusMessage = $"Podglad PDF gotowy ({PdfPreviewPageCount} stron).";
            IsPdfPreviewDirty = false;
            _mainViewModel.StatusMessage = PdfPreviewStatusMessage;
        }
        catch (Exception ex)
        {
            MarkPdfPreviewDirty(clearCurrentPreview: true);
            PdfPreviewStatusMessage = $"Blad generowania podgladu PDF: {ex.Message}";
            _mainViewModel.StatusMessage = PdfPreviewStatusMessage;
        }
        finally
        {
            IsPdfPreviewInProgress = false;
            PdfPreviewLongOperationReason = string.Empty;
        }
    }

    public Task EnsurePdfPreviewAsync()
    {
        if (IsPdfPreviewInProgress)
        {
            return Task.CompletedTask;
        }

        if (HasPdfPreview && !IsPdfPreviewDirty)
        {
            return Task.CompletedTask;
        }

        return RefreshPdfPreviewAsync();
    }

    public void MarkPdfPreviewDirty(bool clearCurrentPreview = false)
    {
        IsPdfPreviewDirty = true;

        if (clearCurrentPreview)
        {
            _pdfPreviewPages.Clear();
            PdfPreviewPageCount = 0;
            PdfPreviewPageIndex = 0;
            CurrentPdfPreviewImage = null;
            PdfPreviewProgressPercent = 0;
            return;
        }

        if (HasPdfPreview && !IsPdfPreviewInProgress)
        {
            PdfPreviewStatusMessage = "Podglad PDF moze byc nieaktualny. Kliknij Odswiez.";
        }
    }

    [RelayCommand]
    public void ShowPreviousPdfPreviewPage()
    {
        if (!CanGoToPreviousPdfPreviewPage)
        {
            return;
        }

        SetPdfPreviewPage(PdfPreviewPageIndex - 1);
    }

    [RelayCommand]
    public void ShowNextPdfPreviewPage()
    {
        if (!CanGoToNextPdfPreviewPage)
        {
            return;
        }

        SetPdfPreviewPage(PdfPreviewPageIndex + 1);
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (_mainViewModel.CurrentProject == null)
        {
            return;
        }

        _mainViewModel.CurrentProject.Metadata ??= new ProjectMetadata();
        var updatedMetadata = await _dialogService.ShowProjectMetadataDialogAsync(_mainViewModel.CurrentProject.Metadata);
        if (updatedMetadata == null)
        {
            return;
        }

        _mainViewModel.CurrentProject.Metadata = updatedMetadata;
        _mainViewModel.MarkProjectAsChanged();
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
                "Bledy walidacji",
                "Projekt zawiera bledy (brak urzadzen, asymetria). Czy na pewno chcesz wygenerowac plik PDF?");

            if (!confirm)
            {
                return;
            }
        }

        try
        {
            StartProgressUi("Generowanie PDF...", GetPdfStageLabel(0), PdfLongReasonRendering);
            var progress = new Progress<int>(ReportPdfProgress);
            await _pdfExportService.ExportToPdfAsync(_mainViewModel, filePath, options, progress);
            CompleteProgressUi($"Wyeksportowano do: {filePath}");

            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"),
                LocalizationHelper.GetString("ToastMsgExportPdf"),
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            FailProgressUi($"Blad eksportu PDF: {ex.Message}");
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"),
                ex.Message,
                Controls.ToastType.Error)));
        }
    }

    [RelayCommand]
    public async Task ExportPdfQuickAsync()
    {
        if (_mainViewModel.CurrentProject == null)
        {
            return;
        }

        var options = new PdfExportOptions();
        var filePath = await _dialogService.PickSaveFileAsync("Szybki eksport PDF", ".pdf", "Dokument PDF");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            StartProgressUi("Szybki eksport PDF...", GetPdfStageLabel(0), PdfLongReasonRendering);
            var progress = new Progress<int>(ReportPdfProgress);
            await _pdfExportService.ExportToPdfAsync(_mainViewModel, filePath, options, progress);
            CompleteProgressUi($"Wyeksportowano do: {filePath}");

            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"),
                LocalizationHelper.GetString("ToastMsgExportPdfQuick"),
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            FailProgressUi($"Blad eksportu PDF: {ex.Message}");
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
        if (_mainViewModel.CurrentProject == null)
        {
            return;
        }

        var filePath = await _dialogService.PickSaveFileAsync(
            "Eksportuj zestawienie materialowe (BOM)",
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
            _mainViewModel.StatusMessage = $"Blad eksportu BOM: {ex.Message}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"),
                ex.Message,
                Controls.ToastType.Error)));
        }
    }

    [RelayCommand]
    public async Task ExportLatexAsync()
    {
        if (_mainViewModel.CurrentProject == null)
        {
            return;
        }

        var filePath = await _dialogService.PickSaveFileAsync("Eksportuj do LaTeX", ".tex", "Dokument LaTeX");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            _mainViewModel.StatusMessage = "Generowanie LaTeX...";
            ExportStatusMessage = "Eksport LaTeX w toku...";
            var options = new PdfExportOptions();
            await Task.Run(() => _latexExportService.ExportToLatex(_mainViewModel, filePath, options));
            _mainViewModel.StatusMessage = $"Wyeksportowano do: {filePath}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"),
                LocalizationHelper.GetString("ToastMsgExportLatex"),
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Blad eksportu LaTeX: {ex.Message}";
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"),
                ex.Message,
                Controls.ToastType.Error)));
        }
    }

    private async Task ExportPngAsync(bool isAnnotated)
    {
        if (_mainViewModel.CurrentProject == null)
        {
            return;
        }

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
            StartProgressUi("Generowanie PNG...", GetPngStageLabel(0), PngLongReason);
            var progress = new Progress<int>(ReportPngProgress);
            var options = new PdfExportOptions
            {
                IncludeCleanSchematic = false
            };

            await _pdfExportService.ExportToPngAsync(_mainViewModel, filePath, options, progress);
            CompleteProgressUi($"Wyeksportowano do: {filePath}");
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportDone"),
                LocalizationHelper.GetString("ToastMsgExportPng"),
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            FailProgressUi($"Blad eksportu PNG: {ex.Message}");
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleExportError"),
                ex.Message,
                Controls.ToastType.Error)));
        }
    }

    private void StartProgressUi(string statusMessage, string stageMessage, string reasonMessage)
    {
        IsExportInProgress = true;
        ExportProgressPercent = 0;
        ExportProgressStage = stageMessage;
        ExportLongOperationReason = reasonMessage;
        ExportStatusMessage = stageMessage;
        _mainViewModel.StatusMessage = statusMessage;
    }

    private void CompleteProgressUi(string statusMessage)
    {
        ExportProgressPercent = 100;
        ExportProgressStage = "Zakonczono eksport.";
        ExportLongOperationReason = string.Empty;
        IsExportInProgress = false;
        ExportStatusMessage = statusMessage;
        _mainViewModel.StatusMessage = statusMessage;
    }

    private void FailProgressUi(string statusMessage)
    {
        IsExportInProgress = false;
        ExportLongOperationReason = string.Empty;
        ExportStatusMessage = statusMessage;
        _mainViewModel.StatusMessage = statusMessage;
    }

    private void ReportPdfProgress(int progressValue)
    {
        var progress = Math.Clamp(progressValue, 0, 100);
        ExportProgressPercent = progress;
        ExportProgressStage = GetPdfStageLabel(progress);
        ExportLongOperationReason = progress >= 90 ? PdfLongReasonFinalization : PdfLongReasonRendering;
        var statusMessage = $"Eksport PDF {progress}% - {ExportProgressStage}";
        ExportStatusMessage = statusMessage;
        _mainViewModel.StatusMessage = statusMessage;
    }

    private void ReportPngProgress(int progressValue)
    {
        var progress = Math.Clamp(progressValue, 0, 100);
        ExportProgressPercent = progress;
        ExportProgressStage = GetPngStageLabel(progress);
        ExportLongOperationReason = PngLongReason;
        var statusMessage = $"Eksport PNG {progress}% - {ExportProgressStage}";
        ExportStatusMessage = statusMessage;
        _mainViewModel.StatusMessage = statusMessage;
    }

    private void StartPdfPreviewUi()
    {
        IsPdfPreviewInProgress = true;
        PdfPreviewProgressPercent = 0;
        PdfPreviewLongOperationReason = PdfPreviewLongReason;
        PdfPreviewStatusMessage = "Generowanie podgladu PDF...";
        _mainViewModel.StatusMessage = PdfPreviewStatusMessage;
    }

    private void ReportPdfPreviewProgress(int progressValue)
    {
        var progress = Math.Clamp(progressValue, 0, 100);
        PdfPreviewProgressPercent = progress;
        PdfPreviewStatusMessage = $"Podglad PDF {progress}%";
        _mainViewModel.StatusMessage = PdfPreviewStatusMessage;
    }

    private void ReplacePdfPreviewPages(IReadOnlyList<byte[]> previewPages)
    {
        _pdfPreviewPages.Clear();

        foreach (var pageData in previewPages)
        {
            if (pageData.Length > 0)
            {
                _pdfPreviewPages.Add(pageData);
            }
        }

        PdfPreviewPageCount = _pdfPreviewPages.Count;
        PdfPreviewPageIndex = 0;
    }

    private void SetPdfPreviewPage(int pageIndex)
    {
        if (_pdfPreviewPages.Count == 0)
        {
            CurrentPdfPreviewImage = null;
            PdfPreviewPageIndex = 0;
            return;
        }

        var boundedIndex = Math.Clamp(pageIndex, 0, _pdfPreviewPages.Count - 1);
        using var stream = new MemoryStream(_pdfPreviewPages[boundedIndex], writable: false);
        CurrentPdfPreviewImage = new Bitmap(stream);
        PdfPreviewPageIndex = boundedIndex;
    }

    private static string GetPdfStageLabel(int progressValue)
    {
        if (progressValue < 20)
        {
            return "Przygotowanie danych projektu";
        }

        if (progressValue < 40)
        {
            return "Render schematu jednokreskowego";
        }

        if (progressValue < 60)
        {
            return "Render schematow szyny DIN";
        }

        if (progressValue < 90)
        {
            return "Budowa tabel i sekcji dokumentacji";
        }

        if (progressValue < 100)
        {
            return "Finalizacja i zapis pliku PDF";
        }

        return "Zakonczono eksport PDF";
    }

    private static string GetPngStageLabel(int progressValue)
    {
        if (progressValue < 30)
        {
            return "Przygotowanie renderu";
        }

        if (progressValue < 70)
        {
            return "Render obrazu PNG";
        }

        if (progressValue < 100)
        {
            return "Zapis pliku PNG";
        }

        return "Zakonczono eksport PNG";
    }
}
