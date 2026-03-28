using Avalonia.Controls;
using Avalonia.Interactivity;
using DINBoard.Models;
using System;

namespace DINBoard.Dialogs;

public partial class ProjectMetadataDialog : Window
{
    private readonly ProjectMetadata _metadata;

    // Parameterless constructor required by Avalonia XAML loader
    public ProjectMetadataDialog()
    {
        InitializeComponent();
        _metadata = new ProjectMetadata();
    }

    public ProjectMetadataDialog(ProjectMetadata metadata)
    {
        InitializeComponent();
        
        // Klonujemy obiekt, by nie modyfikować oryginału dopóki użytkownik nie kliknie Zapisz
        _metadata = new ProjectMetadata
        {
            ProjectNumber = metadata.ProjectNumber,
            Author = metadata.Author,
            AuthorLicense = metadata.AuthorLicense,
            Company = metadata.Company,
            Address = metadata.Address,
            Investor = metadata.Investor,
            Contractor = metadata.Contractor,
            DesignerId = metadata.DesignerId,
            Revision = metadata.Revision,
            DateCreated = metadata.DateCreated,
            DateModified = metadata.DateModified,
            Notes = metadata.Notes,
            Standards = metadata.Standards != null ? new System.Collections.Generic.List<string>(metadata.Standards) : new()
        };

        PopulateFields();
    }

    private void PopulateFields()
    {
        this.FindControl<TextBox>("ProjectNameTextBox")!.Text = _metadata.Company; // Używamy Company jako opisu obiektu przejściowo 
        this.FindControl<TextBox>("ProjectNumberTextBox")!.Text = _metadata.ProjectNumber;
        this.FindControl<TextBox>("AddressTextBox")!.Text = _metadata.Address;
        this.FindControl<TextBox>("InvestorTextBox")!.Text = _metadata.Investor;
        this.FindControl<TextBox>("ContractorTextBox")!.Text = _metadata.Contractor;
        this.FindControl<TextBox>("AuthorTextBox")!.Text = _metadata.Author;
        this.FindControl<TextBox>("DesignerIdTextBox")!.Text = _metadata.DesignerId;
        this.FindControl<TextBox>("RevisionTextBox")!.Text = _metadata.Revision;
        this.FindControl<TextBox>("NotesTextBox")!.Text = _metadata.Notes;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _metadata.Company = this.FindControl<TextBox>("ProjectNameTextBox")!.Text;
        _metadata.ProjectNumber = this.FindControl<TextBox>("ProjectNumberTextBox")!.Text;
        _metadata.Address = this.FindControl<TextBox>("AddressTextBox")!.Text;
        _metadata.Investor = this.FindControl<TextBox>("InvestorTextBox")!.Text;
        _metadata.Contractor = this.FindControl<TextBox>("ContractorTextBox")!.Text;
        _metadata.Author = this.FindControl<TextBox>("AuthorTextBox")!.Text;
        _metadata.DesignerId = this.FindControl<TextBox>("DesignerIdTextBox")!.Text;
        _metadata.Revision = this.FindControl<TextBox>("RevisionTextBox")!.Text;
        _metadata.Notes = this.FindControl<TextBox>("NotesTextBox")!.Text;
        
        _metadata.DateModified = DateTime.UtcNow;

        Close(_metadata);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
