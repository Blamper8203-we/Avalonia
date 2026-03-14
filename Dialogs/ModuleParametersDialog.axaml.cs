using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;

namespace DINBoard.Dialogs;

public partial class ModuleParametersDialog : Window
{
    private Dictionary<string, TextBox> _inputs = new();

    public Dictionary<string, string>? Result { get; private set; }

    public ModuleParametersDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public ModuleParametersDialog(Dictionary<string, string> currentParameters) : this()
    {
        ArgumentNullException.ThrowIfNull(currentParameters);
        var panel = this.FindControl<StackPanel>("ParametersPanel");

        if (panel != null)
        {
            // 1. Define Priority Fields with Polish Labels
            var priorityFields = new List<(string Key, string Label)>
            {
                ("LABEL", "Nazwa Obwodu"),
                ("CURRENT", "Zabezpieczenie"),
                ("POWER", "Moc (W)")
            };

            // 2. Render Priority Fields
            foreach (var field in priorityFields)
            {
                string value = "";
                if (currentParameters.TryGetValue(field.Key, out var val))
                {
                    value = val;
                }

                AddInputField(panel, field.Key, field.Label, value);
            }

            // 3. Render Remaining (Dynamic) Fields
            foreach (var kvp in currentParameters)
            {
                // Skip if already rendered
                if (IsPriorityKey(kvp.Key)) continue;

                AddInputField(panel, kvp.Key, kvp.Key, kvp.Value);
            }
        }
    }

    private bool IsPriorityKey(string key)
    {
        return key == "LABEL" || key == "CURRENT" || key == "POWER";
    }

    private void AddInputField(StackPanel panel, string key, string labelText, string value)
    {
        var label = new TextBlock
        {
            Text = labelText,
            Margin = new global::Avalonia.Thickness(0, 0, 0, 5)
        };

        var textBox = new TextBox
        {
            Text = value,
            Watermark = $"Wpisz wartość dla {labelText}"
        };

        _inputs[key] = textBox;

        panel.Children.Add(label);
        panel.Children.Add(textBox);
        panel.Children.Add(new Control { Height = 10 });
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        Result = new Dictionary<string, string>();
        foreach (var kvp in _inputs)
        {
            Result[kvp.Key] = kvp.Value.Text ?? "";
        }
        this.Close(true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        this.Close(false);
    }
}
