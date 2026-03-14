using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels;
using DINBoard.Constants;
using Material.Icons;
using Material.Icons.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;

namespace DINBoard.Views;

public partial class CircuitEditPanelView : UserControl
{
    private SymbolItem? _symbol;
    private readonly Dictionary<string, Control> _inputs = new();
    private Action? _onSaved;
    private Action? _onClosed;
    private readonly IModuleTypeService _moduleTypeService;

    public CircuitEditPanelView()
    {
        _moduleTypeService = ((App)Application.Current!).Services.GetRequiredService<IModuleTypeService>();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Ładuje symbol do edycji w panelu.
    /// </summary>
    public void LoadSymbol(SymbolItem symbol, Action? onSaved = null, Action? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        _symbol = symbol;
        _onSaved = onSaved;
        _onClosed = onClosed;
        _inputs.Clear();

        var moduleType = GroupViewModel.GetSymbolType(symbol, _moduleTypeService);
        var isRcd = moduleType == "RCD";
        var isSpd = moduleType == "SPD";
        var isFr = moduleType == "FR";
        var isKontrolkiFaz = moduleType == "KontrolkiFaz";

        // Header
        var circuitNameText = this.FindControl<TextBlock>("CircuitNameText");
        var circuitTypeText = this.FindControl<TextBlock>("CircuitTypeText");
        var iconBorder = this.FindControl<Border>("IconBorder");
        var headerIcon = this.FindControl<MaterialIcon>("HeaderIcon");

        if (isFr)
        {
            if (circuitNameText != null) circuitNameText.Text = symbol.Label ?? "Rozłącznik główny";
            if (circuitTypeText != null) circuitTypeText.Text = "Rozłącznik główny (FR)";
            if (iconBorder != null) iconBorder.Background = ResolveBrush("AccentRed", "#EF4444");
            if (headerIcon != null) headerIcon.Kind = MaterialIconKind.PowerPlug;
        }
        else if (isKontrolkiFaz)
        {
            if (circuitNameText != null) circuitNameText.Text = symbol.Label ?? "Kontrolki faz";
            if (circuitTypeText != null) circuitTypeText.Text = "Kontrolki faz";
            if (iconBorder != null) iconBorder.Background = ResolveBrush("AccentOrange", "#F59E0B");
            if (headerIcon != null) headerIcon.Kind = MaterialIconKind.LightbulbOn;
        }
        else if (isRcd)
        {
            if (circuitNameText != null) circuitNameText.Text = symbol.CircuitName ?? "RCD";
            if (circuitTypeText != null) circuitTypeText.Text = "Wyłącznik różnicowoprądowy";
            if (iconBorder != null) iconBorder.Background = ResolveBrush("AccentGreen", "#10B981");
            if (headerIcon != null) headerIcon.Kind = MaterialIconKind.ShieldCheck;
        }
        else
        {
            if (circuitNameText != null)
                circuitNameText.Text = !string.IsNullOrEmpty(symbol.ReferenceDesignation)
                    ? $"{symbol.ReferenceDesignation} — {symbol.CircuitName ?? "Obwód"}"
                    : symbol.CircuitName ?? "Obwód";
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
        panel.Children.Clear();
        
        var stringType = _moduleTypeService.GetModuleTypeName(symbol);
        int poles = (int)_moduleTypeService.GetPoleCount(symbol);
        if (poles == 0) poles = 1;

        if (isFr)
        {
            AddTextField(panel, "ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? "");
            AddTextField(panel, "Label", "Etykieta", symbol.Label ?? "");
            AddComboField(panel, "FrType", "Typ FR", symbol.FrType ?? "63", DialogConstants.FrPresets);
            AddTextField(panel, "FrRatedCurrent", "Prąd znamionowy", symbol.FrRatedCurrent ?? "63A");
        }
        else if (isKontrolkiFaz)
        {
            AddTextField(panel, "ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? "");
            AddTextField(panel, "Label", "Etykieta", symbol.Label ?? "");
            AddComboField(panel, "PhaseIndicatorModel", "Model", symbol.PhaseIndicatorModel ?? "3 lampki z bezpiecznikiem", DialogConstants.PhaseIndicatorModelPresets);
            AddComboField(panel, "PhaseIndicatorFuseRating", "Bezpiecznik", symbol.PhaseIndicatorFuseRating ?? "2A gG", DialogConstants.PhaseIndicatorFusePresets);
        }
        else if (isRcd)
        {
            AddTextField(panel, "ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? "");
            var currentRcdValue = $"{symbol.RcdRatedCurrent}A/{symbol.RcdResidualCurrent}mA Typ {symbol.RcdType}";
            AddComboField(panel, "RcdPreset", "Typ RCD", currentRcdValue, DialogConstants.RcdPresets);
        }
        else if (isSpd)
        {
            AddTextField(panel, "ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? "");
            var currentSpdValue = $"{symbol.SpdType} {symbol.SpdVoltage}V {symbol.SpdDischargeCurrent}kA";
            AddComboField(panel, "SpdPreset", "Typ SPD", currentSpdValue, DialogConstants.SpdPresets);
        }
        else
        {
            AddTextField(panel, "ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? "");
            AddTextField(panel, "CircuitName", "Nazwa obwodu", symbol.CircuitName ?? "");
            AddTextField(panel, "Location", "Lokalizacja", symbol.Location ?? "");
            AddComboField(panel, "ProtectionType", "Zabezpieczenie", symbol.ProtectionType ?? "B16", DialogConstants.ProtectionPresets);
            AddNumberField(panel, "PowerW", "Moc (W)", symbol.PowerW);

            var poleCount = _moduleTypeService.GetPoleCount(symbol);
            string[] phaseOptions = poleCount switch
            {
                ModulePoleCount.P3 or ModulePoleCount.P4 => new[] { "L1+L2+L3", "L1", "L2", "L3", "L1+L2", "L2+L3", "L3+L1" },
                ModulePoleCount.P2 => new[] { "L1+L2", "L2+L3", "L3+L1", "L1", "L2", "L3", "L1+L2+L3" },
                _ => new[] { "L1", "L2", "L3", "L1+L2", "L2+L3", "L3+L1", "L1+L2+L3" }
            };

            // Zamień 'pending'/'PENDING' na 'L1' przy wyświetlaniu
            var displayPhase = (symbol.Phase is null or "" or "pending" or "PENDING") ? "L1" : symbol.Phase;

            AddComboField(panel, "Phase", "Faza", displayPhase, phaseOptions);
            AddTextField(panel, "CableDesig", "Oznaczenie kabla", symbol.Parameters?.GetValueOrDefault("CableDesig", "") ?? "");
            AddTextField(panel, "CableType", "Typ kabla", symbol.Parameters?.GetValueOrDefault("CableType", "") ?? "");
            AddNumberField(panel, "CableLength", "Długość kabla (m)", symbol.CableLength);
            AddNumberField(panel, "CableCrossSection", "Przekrój (mm²)", symbol.CableCrossSection);
        }

        // ... Technical data visualization
        AddTechnicalData(panel, symbol, isRcd, isSpd, isFr, isKontrolkiFaz);

        // Show save button
        var btnSave = this.FindControl<Button>("BtnSave");
        if (btnSave != null) btnSave.IsVisible = true;
    }

    private void AddTechnicalData(StackPanel parent, SymbolItem symbol, bool isRcd, bool isSpd, bool isFr, bool isKontrolkiFaz)
    {
        var poleCount = _moduleTypeService.GetPoleCount(symbol);
        int poles = poleCount switch { ModulePoleCount.P1 => 1, ModulePoleCount.P2 => 2, ModulePoleCount.P3 => 3, ModulePoleCount.P4 => 4, _ => 1 };

        var techPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 15, 0, 10) };
        
        techPanel.Children.Add(new TextBlock 
        { 
            Text = "Dane techniczne i wymiary", 
            FontWeight = FontWeight.SemiBold, 
            Foreground = ResolveBrush("AccentBlue", "#3B82F6"),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        void AddRow(string label, string val)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 3) };
            row.Children.Add(new TextBlock { Text = label, Foreground = ResolveBrush("TextSecondary", "#9CA3AF"), FontSize = 11, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            var valBlock = new TextBlock { Text = val, Foreground = ResolveBrush("TextMain", "#FFFFFF"), FontSize = 11, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, MaxWidth = 130 };
            Grid.SetColumn(valBlock, 1);
            row.Children.Add(valBlock);
            techPanel.Children.Add(row);
            techPanel.Children.Add(new Border { Height = 1, Background = ResolveBrush("PanelBorder", "#4B5563"), Opacity = 0.3, Margin = new Thickness(0, 2) });
        }

        double widthModules = poles;
        int widthMm = (int)(widthModules * 18);

        if (isFr)
        {
            AddRow("Liczba biegunów", $"{poles}P");
            AddRow("Prąd znamionowy (In)", symbol.FrRatedCurrent ?? "63A");
            AddRow("Napięcie znamionowe", "230/400 V AC");
            AddRow("Częstotliwość znamionowa", "50/60 Hz");
            AddRow("Zastosowanie", "Rozłączanie izolacyjne robocze");
        }
        else if (isRcd)
        {
            AddRow("Typ", symbol.RcdType ?? "A");
            AddRow("Liczba biegunów", $"{poles}P");
            AddRow("Prąd znamionowy (In)", $"{symbol.RcdRatedCurrent} A");
            AddRow("Prąd różnicowy (IΔn)", $"{symbol.RcdResidualCurrent} mA");
            AddRow("Napięcie znamionowe", poles > 2 ? "400 V AC" : "230 V AC");
            AddRow("Częstotliwość znamionowa", "50/60 Hz");
            AddRow("Zdolność zwarciowa umowna", "10kA");
            AddRow("Zastosowanie", "Ochrona przeciwporażeniowa różnicowa");
        }
        else if (isSpd)
        {
            AddRow("Liczba biegunów", $"{poles}P");
            AddRow("Typ / Klasa", symbol.SpdType ?? "T1+T2");
            AddRow("Napięcie trwałej pracy (Uc)", $"{symbol.SpdVoltage} V");
            AddRow("Prąd wyładowczy max (Imax)", $"{symbol.SpdDischargeCurrent} kA");
            AddRow("Częstotliwość znamionowa", "50/60 Hz");
            AddRow("Zastosowanie", "Ochrona przeciwprzepięciowa instalacji");
        }
        else if (isKontrolkiFaz)
        {
            AddRow("Napięcie znamionowe", "230/400 V AC");
            AddRow("Sygnalizacja", "LED (L1, L2, L3)");
            AddRow("Zabezpieczenie wew.", symbol.PhaseIndicatorFuseRating ?? "2A gG");
            AddRow("Częstotliwość znamionowa", "50/60 Hz");
            AddRow("Zastosowanie", "Optyczna sygnalizacja zasilania");
        }
        else // MCB
        {
            string prot = symbol.ProtectionType ?? "B16";
            string charParam = "-";
            string curParam = "-";
            if (prot.Length >= 2 && char.IsLetter(prot[0]))
            {
                charParam = prot.Substring(0, 1);
                curParam = prot.Substring(1);
            }

            AddRow("Charakterystyka", charParam);
            AddRow("Liczba biegunów", $"{poles}P");
            AddRow("Prąd znamionowy (In)", $"{curParam} A");
            AddRow("Prąd wył. zwarciowy graniczny [Icu]", "6kA");
            AddRow("Zdolność zwarciowa łączeniowa [Icn]", "6kA");
            AddRow("Częstotliwość znamionowa", "50/60 Hz");
            AddRow("Napięcie znamionowe", poles > 1 ? "400 V AC" : "230 V AC");
            AddRow("Zastosowanie", "Zabezpieczenie nadprądowe obwodów");
        }

        // Wspólne wymiary wg norm DIN 43880 (1 moduł to 18mm)
        AddRow("Szerokość modułu", $"{widthModules} mod. / {widthMm} mm");
        AddRow("Głębokość", "68 mm");
        AddRow("Wysokość", "85 mm");
        AddRow("Montaż", "Szyna profilowa TH35 (DIN)");

        parent.Children.Add(techPanel);
    }

    /// </summary>
    public void ClearPanel()
    {
        _symbol = null;
        _inputs.Clear();

        var panel = this.FindControl<StackPanel>("FieldsPanel");
        if (panel != null)
        {
            panel.Children.Clear();
            panel.Children.Add(new TextBlock
            {
                Text = "Dwukliknij moduł aby edytować",
                Foreground = ResolveBrush("TextTertiary", "#6B7280"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20)
            });
        }

        var circuitNameText = this.FindControl<TextBlock>("CircuitNameText");
        if (circuitNameText != null) circuitNameText.Text = "Edycja obwodu";
        var circuitTypeText = this.FindControl<TextBlock>("CircuitTypeText");
        if (circuitTypeText != null) circuitTypeText.Text = "";

        var btnSave = this.FindControl<Button>("BtnSave");
        if (btnSave != null) btnSave.IsVisible = false;
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        if (_symbol == null) return;

        foreach (var kvp in _inputs)
        {
            var value = GetInputValue(kvp.Value);
            ApplyValueToSymbol(_symbol, kvp.Key, value);
        }

        _onSaved?.Invoke();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        ClearPanel();
        _onClosed?.Invoke();
    }

    // ═══ FIELD BUILDERS (ported from CircuitEditDialog) ═══

    private void Control_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnSave_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void AddTextField(StackPanel parent, string key, string label, string value)
    {
        var container = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2) };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            FontSize = 11
        });

        var textBox = new TextBox
        {
            Text = value,
            Watermark = DialogConstants.GetPlaceholder(key),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(8, 5),
            FontSize = 12
        };
        textBox.KeyDown += Control_KeyDown;
        container.Children.Add(textBox);

        _inputs[key] = textBox;
        parent.Children.Add(container);
    }

    private void AddNumberField(StackPanel parent, string key, string label, double value)
    {
        var container = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2) };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            FontSize = 11
        });

        var numBox = new NumericUpDown
        {
            Value = (decimal)value,
            Minimum = 0,
            Maximum = 100000,
            Watermark = DialogConstants.GetPlaceholder(key),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(4, 2),
            FontSize = 12
        };
        numBox.KeyDown += Control_KeyDown;
        container.Children.Add(numBox);

        _inputs[key] = numBox;
        parent.Children.Add(container);
    }

    private ComboBox AddComboFieldR(StackPanel parent, string key, string label, string value, string[] options)
    {
        var container = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2) };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            FontSize = 11
        });

        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedItem = options.Contains(value) ? value : options.FirstOrDefault(),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(8, 5),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        combo.KeyDown += Control_KeyDown;
        container.Children.Add(combo);

        _inputs[key] = combo;
        parent.Children.Add(container);
        return combo;
    }

    private void AddComboField(StackPanel parent, string key, string label, string value, string[] options)
    {
        var container = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2) };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            FontSize = 11
        });

        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedItem = options.Contains(value) ? value : options.FirstOrDefault(),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(8, 5),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        combo.KeyDown += Control_KeyDown;
        container.Children.Add(combo);

        _inputs[key] = combo;
        parent.Children.Add(container);
    }

    // ═══ VALUE HELPERS (ported from CircuitEditDialog) ═══

    private static string GetInputValue(Control control) => control switch
    {
        TextBox tb => tb.Text ?? "",
        NumericUpDown nud => nud.Value?.ToString() ?? "0",
        ComboBox cb => cb.SelectedItem?.ToString() ?? "",
        _ => ""
    };

    private static void ApplyValueToSymbol(SymbolItem symbol, string key, string value)
    {
        switch (key)
        {
            case "CircuitName": symbol.CircuitName = value; break;
            case "Location": symbol.Location = value; break;
            case "ProtectionType": symbol.ProtectionType = value; break;
            case "PowerW":
                if (double.TryParse(value, out var power)) symbol.PowerW = power;
                break;
            case "Phase": 
                symbol.Phase = value; 
                symbol.Parameters ??= new System.Collections.Generic.Dictionary<string, string>();
                symbol.Parameters["ManualPhase"] = "true";
                break;
            case "CableLength":
                if (double.TryParse(value, out var length)) symbol.CableLength = length;
                break;
            case "CableCrossSection":
                if (double.TryParse(value, out var cross)) symbol.CableCrossSection = cross;
                break;
            case "RcdPreset": ParseAndApplyRcdPreset(symbol, value); break;
            case "SpdPreset": ParseAndApplySpdPreset(symbol, value); break;
            case "Label": symbol.Label = value; break;
            case "FrType": symbol.FrType = value; break;
            case "FrRatedCurrent": symbol.FrRatedCurrent = value; break;
            case "PhaseIndicatorModel": symbol.PhaseIndicatorModel = value; break;
            case "PhaseIndicatorFuseRating": symbol.PhaseIndicatorFuseRating = value; break;
            case "ReferenceDesignation": symbol.ReferenceDesignation = value; break;
            case "CableDesig":
            case "CableType":
                symbol.Parameters ??= new System.Collections.Generic.Dictionary<string, string>();
                symbol.Parameters[key] = value;
                break;
        }
    }

    private static void ParseAndApplyRcdPreset(SymbolItem symbol, string preset)
    {
        if (string.IsNullOrWhiteSpace(preset)) { symbol.RcdRatedCurrent = 40; symbol.RcdResidualCurrent = 30; symbol.RcdType = "A"; return; }
        var parts = preset.Split(' ');
        if (parts.Length >= 3)
        {
            var ampParts = parts[0].Split('/');
            if (ampParts.Length == 2)
            {
                if (int.TryParse(ampParts[0].Replace("A", ""), out var rated)) symbol.RcdRatedCurrent = rated;
                if (int.TryParse(ampParts[1].Replace("mA", ""), out var residual)) symbol.RcdResidualCurrent = residual;
            }
            symbol.RcdType = parts[2];
        }
    }

    private static void ParseAndApplySpdPreset(SymbolItem symbol, string preset)
    {
        if (string.IsNullOrWhiteSpace(preset)) { symbol.SpdType = "T1+T2"; symbol.SpdVoltage = 275; symbol.SpdDischargeCurrent = 25; return; }
        var parts = preset.Split(' ');
        if (parts.Length >= 3)
        {
            symbol.SpdType = parts[0];
            if (int.TryParse(parts[1].Replace("V", ""), out var voltage)) symbol.SpdVoltage = voltage;
            if (int.TryParse(parts[2].Replace("kA", "").Replace(".5", ""), out var discharge)) symbol.SpdDischargeCurrent = discharge;
        }
    }

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}
