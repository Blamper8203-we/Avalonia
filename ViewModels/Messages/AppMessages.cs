using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DINBoard.ViewModels.Messages;

public sealed class SymbolsRefreshMessage : ValueChangedMessage<bool>
{
    public SymbolsRefreshMessage() : base(true) { }
}

public sealed class ExportPdfMessage : ValueChangedMessage<ExportSettings>
{
    public ExportPdfMessage(ExportSettings value) : base(value) { }
}

public sealed class ExportPdfQuickMessage : ValueChangedMessage<DINBoard.Services.PdfExportOptions>
{
    public ExportPdfQuickMessage(DINBoard.Services.PdfExportOptions value) : base(value) { }
}

public sealed class ExportPngMessage : ValueChangedMessage<bool>
{
    public ExportPngMessage(bool isAnnotated) : base(isAnnotated) { }
}

public sealed class ExportBomMessage : ValueChangedMessage<bool>
{
    public ExportBomMessage() : base(true) { }
}

public sealed class DinRailRefreshMessage : ValueChangedMessage<bool>
{
    public DinRailRefreshMessage() : base(true) { }
}

public sealed class ThemeChangedMessage : ValueChangedMessage<string>
{
    public ThemeChangedMessage(string themeName) : base(themeName) { }
}

public sealed class NavigateToSheetMessage : ValueChangedMessage<int>
{
    public NavigateToSheetMessage(int sheetIndex) : base(sheetIndex) { }
}

public record ToastData(string Title, string Message, DINBoard.Controls.ToastType Type = DINBoard.Controls.ToastType.Success, int DurationMs = 3500);

public sealed class ShowToastMessage : ValueChangedMessage<ToastData>
{
    public ShowToastMessage(ToastData data) : base(data) { }
}

public sealed class ProjectGroupsChangedMessage : ValueChangedMessage<bool>
{
    public ProjectGroupsChangedMessage() : base(true) { }
}

public sealed class StartPlacementMessage : ValueChangedMessage<System.Collections.Generic.List<DINBoard.Models.SymbolItem>>
{
    public bool IsCloningMode { get; }
    public StartPlacementMessage(System.Collections.Generic.List<DINBoard.Models.SymbolItem> clones, bool isCloningMode = false) 
        : base(clones) 
    { 
        IsCloningMode = isCloningMode;
    }
}
