using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DINBoard.Models;

namespace DINBoard.Views;

public partial class ProjectPropertiesView : UserControl
{
    private ProjectMetadata? _currentMetadata;
    private bool _isLoading;

    public event System.EventHandler? PropertiesChanged;

    public ProjectPropertiesView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentMetadata == null) return;
        
        SaveMetadata(_currentMetadata);
        PropertiesChanged?.Invoke(this, System.EventArgs.Empty);
    }

    /// <summary>
    /// Ładuje dane z ProjectMetadata do pól formularza.
    /// </summary>
    public void LoadMetadata(ProjectMetadata? metadata)
    {
        _currentMetadata = metadata;
        
        if (metadata == null) return;

        _isLoading = true;

        var projectName = this.FindControl<TextBox>("ProjectNameTextBox");
        var address = this.FindControl<TextBox>("AddressTextBox");
        var investor = this.FindControl<TextBox>("InvestorTextBox");
        var contractor = this.FindControl<TextBox>("ContractorTextBox");
        var projectNumber = this.FindControl<TextBox>("ProjectNumberTextBox");
        var author = this.FindControl<TextBox>("AuthorTextBox");
        var designerId = this.FindControl<TextBox>("DesignerIdTextBox");
        var revision = this.FindControl<TextBox>("RevisionTextBox");
        var notes = this.FindControl<TextBox>("NotesTextBox");

        if (projectName != null) projectName.Text = metadata.Company ?? "";
        if (address != null) address.Text = metadata.Address ?? "";
        if (investor != null) investor.Text = metadata.Investor ?? "";
        if (contractor != null) contractor.Text = metadata.Contractor ?? "";
        if (projectNumber != null) projectNumber.Text = metadata.ProjectNumber ?? "";
        if (author != null) author.Text = metadata.Author ?? "";
        if (designerId != null) designerId.Text = metadata.DesignerId ?? "";
        if (revision != null) revision.Text = metadata.Revision ?? "1.0";
        if (notes != null) notes.Text = metadata.Notes ?? "";
        
        _isLoading = false;
    }

    /// <summary>
    /// Zapisuje dane z pól formularza do ProjectMetadata.
    /// </summary>
    public void SaveMetadata(ProjectMetadata metadata)
    {
        var projectName = this.FindControl<TextBox>("ProjectNameTextBox");
        var address = this.FindControl<TextBox>("AddressTextBox");
        var investor = this.FindControl<TextBox>("InvestorTextBox");
        var contractor = this.FindControl<TextBox>("ContractorTextBox");
        var projectNumber = this.FindControl<TextBox>("ProjectNumberTextBox");
        var author = this.FindControl<TextBox>("AuthorTextBox");
        var designerId = this.FindControl<TextBox>("DesignerIdTextBox");
        var revision = this.FindControl<TextBox>("RevisionTextBox");
        var notes = this.FindControl<TextBox>("NotesTextBox");

        metadata.Company = projectName?.Text ?? "";
        metadata.Address = address?.Text ?? "";
        metadata.Investor = investor?.Text ?? "";
        metadata.Contractor = contractor?.Text ?? "";
        metadata.ProjectNumber = projectNumber?.Text ?? "";
        metadata.Author = author?.Text ?? "";
        metadata.DesignerId = designerId?.Text ?? "";
        metadata.Revision = revision?.Text ?? "1.0";
        metadata.Notes = notes?.Text ?? "";
        metadata.DateModified = System.DateTime.UtcNow;
    }
}
