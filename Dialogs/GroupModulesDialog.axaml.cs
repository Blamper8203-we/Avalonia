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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace DINBoard.Dialogs;

public partial class GroupModulesDialog : Window
{
    private readonly GroupViewModel? _group;
    private readonly Dictionary<SymbolItem, Dictionary<string, Control>> _moduleInputs = new();
    private readonly IModuleTypeService _moduleTypeService;

    // Predefiniowane typy RCD
    private static readonly string[] RcdPresets = DialogConstants.RcdPresets;

    // Predefiniowane zabezpieczenia MCB
    private static readonly string[] ProtectionPresets = DialogConstants.ProtectionPresets;

    public bool WasModified { get; private set; }

    public GroupModulesDialog()
    {
        _moduleTypeService = ((App)Application.Current!).Services.GetRequiredService<IModuleTypeService>();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public GroupModulesDialog(GroupViewModel group) : this()
    {
        ArgumentNullException.ThrowIfNull(group);
        _group = group;

        var groupNameText = this.FindControl<TextBlock>("GroupNameText");
        if (groupNameText != null)
        {
            groupNameText.Text = group.Name;
        }

        var panel = this.FindControl<StackPanel>("ModulesPanel");
        if (panel == null) return;

        // Add main symbol (RCD) first
        if (group.MainSymbol != null)
        {
            AddModuleCard(panel, group.MainSymbol, true);
        }

        // Add sub symbols (MCBs)
        foreach (var symbol in group.SubSymbols)
        {
            AddModuleCard(panel, symbol, false);
        }
    }

    private void AddModuleCard(StackPanel panel, SymbolItem symbol, bool isMain)
    {
        var moduleType = GroupViewModel.GetSymbolType(symbol, _moduleTypeService);
        var isRcd = moduleType == "RCD";

        // Module card container
        var card = new Border
        {
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var cardContent = new StackPanel { Spacing = 10 };

        // Header with icon and type
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var iconBorder = new Border
        {
            Background = isRcd
                ? ResolveBrush("AccentGreen", "#10B981")
                : ResolveBrush("AccentBlue", "#3B82F6"),
            CornerRadius = new CornerRadius(4),
            Width = 28,
            Height = 28
        };

        var icon = new MaterialIcon
        {
            Kind = isRcd ? MaterialIconKind.ShieldCheck : MaterialIconKind.LightningBolt,
            Width = 16,
            Height = 16,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = icon;
        header.Children.Add(iconBorder);

        var headerText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headerText.Children.Add(new TextBlock
        {
            Text = isRcd ? "RCD (Wyłącznik różnicowoprądowy)" : $"MCB #{symbol.ModuleNumber}",
            FontWeight = FontWeight.Bold,
            FontSize = 13,
            Foreground = ResolveBrush("TextMain", "#FFFFFF")
        });
        if (!string.IsNullOrEmpty(symbol.CircuitName))
        {
            headerText.Children.Add(new TextBlock
            {
                Text = symbol.CircuitName,
                FontSize = 11,
                Foreground = ResolveBrush("TextTertiary", "#9CA3AF")
            });
        }
        header.Children.Add(headerText);
        cardContent.Children.Add(header);

        // Separator
        cardContent.Children.Add(new Border
        {
            Background = ResolveBrush("PanelBorder", "#374151"),
            Height = 1,
            Margin = new Thickness(0, 4)
        });

        // Input fields based on module type
        var inputs = new Dictionary<string, Control>();

        if (isRcd)
        {
            // RCD fields
            AddTextField(cardContent, inputs, "CircuitName", "Nazwa", symbol.CircuitName ?? "");
            var currentRcdValue = $"{symbol.RcdRatedCurrent}A/{symbol.RcdResidualCurrent}mA Typ {symbol.RcdType}";
            AddComboField(cardContent, inputs, "RcdPreset", "Typ RCD", currentRcdValue, RcdPresets);
        }
        else
        {
            // MCB fields
            AddTextField(cardContent, inputs, "CircuitName", "Nazwa obwodu", symbol.CircuitName ?? "");
            AddTextField(cardContent, inputs, "Location", "Lokalizacja", symbol.Location ?? "");
            AddComboField(cardContent, inputs, "ProtectionType", "Zabezpieczenie", symbol.ProtectionType ?? "B16", ProtectionPresets);
            AddNumberField(cardContent, inputs, "PowerW", "Moc (W)", (int)symbol.PowerW);
            
            // Dynamiczne opcje faz w zależności od typu modułu (1P/2P/3P/4P)
            var poleCount = _moduleTypeService.GetPoleCount(symbol);
            var isThreePhase = poleCount == ModulePoleCount.P3 || poleCount == ModulePoleCount.P4;
            
            string[] phaseOptions = isThreePhase 
                ? new[] { "L1+L2+L3", "L1", "L2", "L3" }  // 3-fazowe na pierwszym miejscu
                : new[] { "L1", "L2", "L3", "L1+L2+L3" }; // 1-fazowe domyślnie
            
            AddComboField(cardContent, inputs, "Phase", "Faza", symbol.Phase, phaseOptions);
            AddNumberField(cardContent, inputs, "CableLength", "Długość kabla (m)", (int)symbol.CableLength);
            AddNumberField(cardContent, inputs, "CableCrossSection", "Przekrój (mm²)", symbol.CableCrossSection);
        }

        _moduleInputs[symbol] = inputs;

        card.Child = cardContent;
        panel.Children.Add(card);
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

    private void AddTextField(StackPanel parent, Dictionary<string, Control> inputs, string key, string label, string value)
    {
        var container = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("100,*") };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11
        });

        var textBox = new TextBox
        {
            Text = value,
            Watermark = GetPlaceholder(key),
            Background = ResolveBrush("PanelBackgroundAlt", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(8, 4),
            FontSize = 11
        };
        Grid.SetColumn(textBox, 1);
        container.Children.Add(textBox);

        inputs[key] = textBox;
        parent.Children.Add(container);
    }

    private void AddNumberField(StackPanel parent, Dictionary<string, Control> inputs, string key, string label, double value)
    {
        var container = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("100,*") };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11
        });

        var numBox = new NumericUpDown
        {
            Value = (decimal)value,
            Minimum = 0,
            Maximum = 100000,
            Watermark = GetPlaceholder(key),
            Background = ResolveBrush("PanelBackgroundAlt", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(4, 2),
            FontSize = 11
        };
        Grid.SetColumn(numBox, 1);
        container.Children.Add(numBox);

        inputs[key] = numBox;
        parent.Children.Add(container);
    }

    private void AddComboField(StackPanel parent, Dictionary<string, Control> inputs, string key, string label, string value, string[] options)
    {
        var container = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("100,*") };

        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11
        });

        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedItem = options.Contains(value) ? value : options.FirstOrDefault(),
            Background = ResolveBrush("PanelBackgroundAlt", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            Padding = new Thickness(8, 4),
            FontSize = 11
        };
        Grid.SetColumn(combo, 1);
        container.Children.Add(combo);

        inputs[key] = combo;
        parent.Children.Add(container);
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        // Apply changes to all modules
        foreach (var kvp in _moduleInputs)
        {
            var symbol = kvp.Key;
            var inputs = kvp.Value;

            foreach (var input in inputs)
            {
                var value = GetInputValue(input.Value);
                ApplyValueToSymbol(symbol, input.Key, value);
            }
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
            case "Location":
                symbol.Location = value;
                break;
        }
    }

    /// <summary>
    /// Parsuje preset RCD (np. "40A/30mA Typ A") i ustawia wartości na symbolu
    /// </summary>
    private static void ParseAndApplyRcdPreset(SymbolItem symbol, string preset)
    {
        // Format: "40A/30mA Typ A"
        try
        {
            var parts = preset.Split(' ');
            if (parts.Length >= 3)
            {
                var ampParts = parts[0].Split('/');
                if (ampParts.Length == 2)
                {
                    // Parse rated current (40A -> 40)
                    if (int.TryParse(ampParts[0].Replace("A", ""), out var rated))
                        symbol.RcdRatedCurrent = rated;

                    // Parse residual current (30mA -> 30)
                    if (int.TryParse(ampParts[1].Replace("mA", ""), out var residual))
                        symbol.RcdResidualCurrent = residual;
                }

                // Parse type (Typ A -> A)
                if (parts.Length >= 3)
                    symbol.RcdType = parts[2];
            }
        }
        catch
        {
            // Fallback - ustaw domyślne
            symbol.RcdRatedCurrent = 40;
            symbol.RcdResidualCurrent = 30;
            symbol.RcdType = "A";
        }
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
