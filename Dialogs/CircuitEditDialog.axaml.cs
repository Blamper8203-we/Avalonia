using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Constants;
using Material.Icons;
using Material.Icons.Avalonia;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace DINBoard.Dialogs;

public partial class CircuitEditDialog : Window
{
    private readonly SymbolItem? _symbol;
    private readonly Dictionary<string, Control> _inputs = new();
    private readonly IModuleTypeService _moduleTypeService;

    // Predefiniowane typy RCD
    private static readonly string[] RcdPresets = DialogConstants.RcdPresets;

    // Predefiniowane typy SPD
    private static readonly string[] SpdPresets = DialogConstants.SpdPresets;

    // Predefiniowane typy FR
    private static readonly string[] FrPresets = DialogConstants.FrPresets;

    // Predefiniowane modele kontrolek faz
    private static readonly string[] PhaseIndicatorModelPresets = DialogConstants.PhaseIndicatorModelPresets;

    // Predefiniowane bezpieczniki kontrolek faz
    private static readonly string[] PhaseIndicatorFusePresets = DialogConstants.PhaseIndicatorFusePresets;

    // Predefiniowane zabezpieczenia MCB
    private static readonly string[] ProtectionPresets = DialogConstants.ProtectionPresets;

    // Typy obwodów (oświetlenie, gniazdo, siła, inne)
    private static readonly string[] CircuitTypePresets = DialogConstants.CircuitTypePresets;

    public bool WasModified { get; private set; }

    public CircuitEditDialog()
    {
        _moduleTypeService = ((App)Application.Current!).Services.GetRequiredService<IModuleTypeService>();
        InitializeComponent();
    }



    public CircuitEditDialog(SymbolItem symbol) : this()
    {
        ArgumentNullException.ThrowIfNull(symbol);
        _symbol = symbol;

        var moduleType = _moduleTypeService.GetModuleTypeName(symbol);
        var isRcd = moduleType == "RCD";
        var isSpd = moduleType == "SPD";
        var isFr = moduleType == "FR";
        var isKontrolkiFaz = moduleType == "KontrolkiFaz";

        // Update header
        var circuitNameText = this.FindControl<TextBlock>("CircuitNameText");
        var circuitTypeText = this.FindControl<TextBlock>("CircuitTypeText");
        var iconBorder = this.FindControl<Border>("IconBorder");
        var headerIcon = this.FindControl<MaterialIcon>("HeaderIcon");

        // Ustaw nagłówek w zależności od typu
        if (isFr)
        {
            if (circuitNameText != null)
                circuitNameText.Text = symbol.Label ?? "Rozłącznik główny";
            if (circuitTypeText != null)
                circuitTypeText.Text = "Rozłącznik główny (FR)";
            if (iconBorder != null)
                iconBorder.Background = ResolveBrush("AccentRed", "#EF4444");
            if (headerIcon != null)
                headerIcon.Kind = MaterialIconKind.PowerPlug;
        }
        else if (isKontrolkiFaz)
        {
            if (circuitNameText != null)
                circuitNameText.Text = symbol.Label ?? "Kontrolki faz";
            if (circuitTypeText != null)
                circuitTypeText.Text = "Kontrolki faz (lampki sygnalizacyjne)";
            if (iconBorder != null)
                iconBorder.Background = ResolveBrush("AccentOrange", "#F59E0B");
            if (headerIcon != null)
                headerIcon.Kind = MaterialIconKind.LightbulbOn;
        }
        else if (isRcd)
        {
            if (circuitNameText != null)
                circuitNameText.Text = symbol.CircuitName ?? "RCD";
            if (circuitTypeText != null)
                circuitTypeText.Text = "Wyłącznik różnicowoprądowy";
            if (iconBorder != null)
                iconBorder.Background = ResolveBrush("AccentGreen", "#10B981");
            if (headerIcon != null)
                headerIcon.Kind = MaterialIconKind.ShieldCheck;
        }
        else
        {
            if (circuitNameText != null)
                circuitNameText.Text = symbol.CircuitName ?? $"Obwód #{symbol.ModuleNumber}";
            if (circuitTypeText != null)
                circuitTypeText.Text = isSpd ? "Ogranicznik przepięć (SPD)" : "Wyłącznik nadprądowy (MCB)";
            if (iconBorder != null)
                iconBorder.Background = isSpd
                    ? ResolveBrush("AccentOrange", "#F59E0B")
                    : ResolveBrush("AccentBlue", "#3B82F6");
            if (headerIcon != null)
                headerIcon.Kind = isSpd ? MaterialIconKind.Flash : MaterialIconKind.LightningBolt;
        }

        // Build form
        var panel = this.FindControl<StackPanel>("FieldsPanel");
        if (panel == null) return;

        if (isFr)
        {
            AddTextField(panel, "Label", "Etykieta", symbol.Label ?? "");
            AddComboField(panel, "FrType", "Typ FR", symbol.FrType ?? "63", FrPresets);
            AddTextField(panel, "FrRatedCurrent", "Prąd znamionowy", symbol.FrRatedCurrent ?? "63A");
        }
        else if (isKontrolkiFaz)
        {
            AddTextField(panel, "Label", "Etykieta", symbol.Label ?? "");
            AddComboField(panel, "PhaseIndicatorModel", "Model", symbol.PhaseIndicatorModel ?? "3 lampki z bezpiecznikiem", PhaseIndicatorModelPresets);
            AddComboField(panel, "PhaseIndicatorFuseRating", "Bezpiecznik", symbol.PhaseIndicatorFuseRating ?? "2A gG", PhaseIndicatorFusePresets);
        }
        else if (isRcd)
        {
            var currentRcdValue = $"{symbol.RcdRatedCurrent}A/{symbol.RcdResidualCurrent}mA Typ {symbol.RcdType}";
            AddComboField(panel, "RcdPreset", "Typ RCD", currentRcdValue, RcdPresets);
        }
        else if (isSpd)
        {
            var currentSpdValue = $"{symbol.SpdType} {symbol.SpdVoltage}V {symbol.SpdDischargeCurrent}kA";
            AddComboField(panel, "SpdPreset", "Typ SPD", currentSpdValue, SpdPresets);
        }
        else
        {
            AddTextField(panel, "CircuitName", "Nazwa obwodu", symbol.CircuitName ?? "");
            AddTextField(panel, "Location", "Lokalizacja", symbol.Location ?? "");
            AddComboField(panel, "CircuitType", "Typ obwodu", symbol.CircuitType ?? "Gniazdo", CircuitTypePresets);
            AddComboField(panel, "ProtectionType", "Zabezpieczenie", symbol.ProtectionType ?? "B16", ProtectionPresets);
            AddNumberField(panel, "PowerW", "Moc (W)", symbol.PowerW);
            
            // Dynamiczne opcje faz w zależności od typu modułu
            var poleCount = _moduleTypeService.GetPoleCount(symbol);
            var isThreePhase = poleCount == ModulePoleCount.P3 || poleCount == ModulePoleCount.P4;
            
            string[] phaseOptions = isThreePhase 
                ? new[] { "L1+L2+L3", "L1", "L2", "L3", "L1+L2", "L1+L3", "L2+L3" }
                : new[] { "L1", "L2", "L3", "L1+L2", "L1+L3", "L2+L3", "L1+L2+L3" };
            
            AddComboField(panel, "Phase", "Faza", symbol.Phase, phaseOptions);
            AddCheckboxField(panel, "IsPhaseLocked", "Zablokuj fazę (pomiń przy autom. bilansowaniu)", symbol.IsPhaseLocked);
            AddNumberField(panel, "CableLength", "Długość kabla (m)", symbol.CableLength);
            AddNumberField(panel, "CableCrossSection", "Przekrój (mm²)", symbol.CableCrossSection);
        }
    }

    private static string GetPlaceholder(string key) => DialogConstants.GetPlaceholder(key);

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private void AddTextField(StackPanel parent, string key, string label, string value)
    {
        var container = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("120,*"),
            Margin = new Thickness(0, 4)
        };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        });

        var textBox = new TextBox
        {
            Text = value,
            Watermark = GetPlaceholder(key),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(10, 6),
            FontSize = 12
        };
        Grid.SetColumn(textBox, 1);
        container.Children.Add(textBox);

        _inputs[key] = textBox;
        parent.Children.Add(container);
    }

    private void AddNumberField(StackPanel parent, string key, string label, double value)
    {
        var container = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("120,*"),
            Margin = new Thickness(0, 4)
        };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        });

        var numBox = new NumericUpDown
        {
            Value = (decimal)value,
            Minimum = 0,
            Maximum = 100000,
            Watermark = GetPlaceholder(key),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(6, 2),
            FontSize = 12
        };
        Grid.SetColumn(numBox, 1);
        container.Children.Add(numBox);

        _inputs[key] = numBox;
        parent.Children.Add(container);
    }

    private void AddComboField(StackPanel parent, string key, string label, string value, string[] options)
    {
        var container = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("120,*"),
            Margin = new Thickness(0, 4)
        };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        });

        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedItem = options.Contains(value) ? value : options.FirstOrDefault(),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(10, 6),
            FontSize = 12
        };
        Grid.SetColumn(combo, 1);
        container.Children.Add(combo);

        _inputs[key] = combo;
        parent.Children.Add(container);
    }

    private void AddCheckboxField(StackPanel parent, string key, string label, bool value)
    {
        var container = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("120,*"),
            Margin = new Thickness(0, 4)
        };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        });

        var checkBox = new CheckBox
        {
            IsChecked = value,
            Margin = new Thickness(0, 0, 0, 0)
        };
        Grid.SetColumn(checkBox, 1);
        container.Children.Add(checkBox);

        _inputs[key] = checkBox;
        parent.Children.Add(container);
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        if (_symbol == null) return;

        foreach (var kvp in _inputs)
        {
            var value = GetInputValue(kvp.Value);
            ApplyValueToSymbol(_symbol, kvp.Key, value);
        }

        WasModified = true;
        Close(true);
    }

    private string GetInputValue(Control control)
    {
        return control switch
        {
            TextBox tb => tb.Text ?? "",
            NumericUpDown nud => nud.Value?.ToString() ?? "0",
            ComboBox cb => cb.SelectedItem?.ToString() ?? "",
            CheckBox chk => chk.IsChecked?.ToString() ?? "False",
            _ => ""
        };
    }

    private void ApplyValueToSymbol(SymbolItem symbol, string key, string value)
    {
        switch (key)
        {
            case "CircuitName":
                symbol.CircuitName = value;
                break;
            case "Location":
                symbol.Location = value;
                break;
            case "ProtectionType":
                symbol.ProtectionType = value;
                break;
            case "PowerW":
                if (double.TryParse(value, out var power))
                    symbol.PowerW = power;
                break;
            case "Phase":
                symbol.Phase = value;
                break;
            case "CableLength":
                if (double.TryParse(value, out var length))
                    symbol.CableLength = length;
                break;
            case "CableCrossSection":
                if (double.TryParse(value, out var cross))
                    symbol.CableCrossSection = cross;
                break;
            case "RcdRatedCurrent":
                if (int.TryParse(value, out var rated))
                    symbol.RcdRatedCurrent = rated;
                break;
            case "RcdResidualCurrent":
                if (int.TryParse(value, out var residual))
                    symbol.RcdResidualCurrent = residual;
                break;
            case "RcdType":
                symbol.RcdType = value;
                break;
            case "RcdPreset":
                ParseAndApplyRcdPreset(symbol, value);
                break;
            case "SpdPreset":
                ParseAndApplySpdPreset(symbol, value);
                break;
            case "Label":
                symbol.Label = value;
                break;
            case "FrType":
                symbol.FrType = value;
                break;
            case "FrRatedCurrent":
                symbol.FrRatedCurrent = value;
                break;
            case "PhaseIndicatorModel":
                symbol.PhaseIndicatorModel = value;
                break;
            case "PhaseIndicatorFuseRating":
                symbol.PhaseIndicatorFuseRating = value;
                break;
            case "IsPhaseLocked":
                if (bool.TryParse(value, out var isLocked))
                    symbol.IsPhaseLocked = isLocked;
                break;
            case "CircuitType":
                symbol.CircuitType = value;
                break;
        }
    }

    /// <summary>
    /// Parsuje preset RCD (np. "40A/30mA Typ A") i ustawia wartości na symbolu
    /// </summary>
    private static void ParseAndApplyRcdPreset(SymbolItem symbol, string preset)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (string.IsNullOrWhiteSpace(preset))
        {
            symbol.RcdRatedCurrent = 40;
            symbol.RcdResidualCurrent = 30;
            symbol.RcdType = "A";
            return;
        }

        var parts = preset.Split(' ');
        if (parts.Length >= 3)
        {
            var ampParts = parts[0].Split('/');
            if (ampParts.Length == 2)
            {
                if (int.TryParse(ampParts[0].Replace("A", ""), out var rated))
                    symbol.RcdRatedCurrent = rated;

                if (int.TryParse(ampParts[1].Replace("mA", ""), out var residual))
                    symbol.RcdResidualCurrent = residual;
            }

            symbol.RcdType = parts[2];
        }
    }

    /// <summary>
    /// Parsuje preset SPD (np. "T1+T2 275V 25kA") i ustawia wartoĹ›ci na symbolu
    /// </summary>
    private static void ParseAndApplySpdPreset(SymbolItem symbol, string preset)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (string.IsNullOrWhiteSpace(preset))
        {
            symbol.SpdType = "T1+T2";
            symbol.SpdVoltage = 275;
            symbol.SpdDischargeCurrent = 25;
            return;
        }

        var parts = preset.Split(' ');
        if (parts.Length >= 3)
        {
            symbol.SpdType = parts[0];

            if (int.TryParse(parts[1].Replace("V", ""), out var voltage))
                symbol.SpdVoltage = voltage;

            if (int.TryParse(parts[2].Replace("kA", "").Replace(".5", ""), out var discharge))
                symbol.SpdDischargeCurrent = discharge;
        }
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
