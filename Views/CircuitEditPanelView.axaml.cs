using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Constants;
using Material.Icons;
using Material.Icons.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using DINBoard.ViewModels;

namespace DINBoard.Views;

public partial class CircuitEditPanelView : UserControl
{
    private SymbolItem? _symbol;
    private readonly Dictionary<string, Control> _inputs = new();
    private Action? _onSaved;
    private Action? _onClosed;
    private readonly IModuleTypeService _moduleTypeService;
    private readonly InductionOvenCalculatorService _inductionOvenCalculator = new();
    private Window? _inductionOvenCalculatorWindow;

    public CircuitEditPanelView()
    {
        _moduleTypeService = ((App)Application.Current!).Services.GetRequiredService<IModuleTypeService>();
        InitializeComponent();
    }

    /// <summary>
    /// Laduje symbol do edycji w panelu.
    /// </summary>
    public void LoadSymbol(SymbolItem symbol, Action? onSaved = null, Action? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        _symbol = symbol;
        _onSaved = onSaved;
        _onClosed = onClosed;
        _inputs.Clear();

        var moduleType = _moduleTypeService.GetModuleTypeName(symbol);
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
        
        foreach (var field in CircuitEditFieldDefinitionProvider.GetFields(symbol, _moduleTypeService))
        {
            AddField(panel, field);
        }

        // ... Technical data visualization
        AddTechnicalData(panel, symbol, isRcd, isSpd, isFr, isKontrolkiFaz);

        if (IsInductionOvenCalculatorEnabled(symbol))
        {
            AddInductionOvenCalculatorLauncher(panel, symbol);
        }

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

    private bool IsInductionOvenCalculatorEnabled(SymbolItem symbol)
    {
        if (TryGetScenarioFlag(symbol.Parameters, GroupScenarioConstants.InductionWithOvenEnabled))
        {
            return true;
        }

        if (DataContext is not MainViewModel mainViewModel || string.IsNullOrWhiteSpace(symbol.Group))
        {
            return false;
        }

        return mainViewModel.Symbols
            .Where(candidate => string.Equals(candidate.Group, symbol.Group, StringComparison.Ordinal))
            .Any(candidate => TryGetScenarioFlag(candidate.Parameters, GroupScenarioConstants.InductionWithOvenEnabled));
    }

    private void AddInductionOvenCalculatorLauncher(StackPanel parent, SymbolItem symbol)
    {
        var sectionBorder = new Border
        {
            Margin = new Thickness(0, 14, 0, 6),
            Padding = new Thickness(10),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6)
        };

        var sectionPanel = new StackPanel { Spacing = 6 };
        sectionBorder.Child = sectionPanel;

        sectionPanel.Children.Add(new TextBlock
        {
            Text = "Kalkulator: indukcja",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("AccentOrange", "#F59E0B")
        });

        sectionPanel.Children.Add(new TextBlock
        {
            Text = "Otworz kalkulator w osobnym oknie, aby wygodniej porownac warianty 1F i 2F.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#9CA3AF"),
            TextWrapping = TextWrapping.Wrap
        });

        var openButton = new Button
        {
            Content = "Otworz kalkulator indukcji",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ResolveBrush("AccentBlue", "#3B82F6"),
            Foreground = ResolveBrush("White", "#FFFFFF"),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(10, 7),
            Margin = new Thickness(0, 4, 0, 0)
        };

        openButton.Click += (_, _) => OpenInductionOvenCalculatorWindow(symbol);
        sectionPanel.Children.Add(openButton);

        parent.Children.Add(sectionBorder);
    }

    private void OpenInductionOvenCalculatorWindow(SymbolItem symbol)
    {
        if (_inductionOvenCalculatorWindow is { IsVisible: true })
        {
            _inductionOvenCalculatorWindow.Activate();
            return;
        }

        var calculatorHost = new StackPanel { Spacing = 0 };
        AddInductionOvenCalculator(calculatorHost, symbol);

        var owner = TopLevel.GetTopLevel(this) as Window;
        var calculatorWindow = new Window
        {
            Title = "Kalkulator indukcji",
            Width = 620,
            Height = 760,
            MinWidth = 560,
            MinHeight = 640,
            CanResize = true,
            WindowStartupLocation = owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen
        };

        calculatorWindow.Content = new TabControl
        {
            ItemsSource = new object[]
            {
                new TabItem
                {
                    Header = "Kalkulator",
                    Content = new Border
                    {
                        Padding = new Thickness(10),
                        Background = ResolveBrush("PanelBackground", "#1F2937"),
                        Child = new ScrollViewer
                        {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            Content = calculatorHost
                        }
                    }
                },
                new TabItem
                {
                    Header = "Pomoc",
                    Content = BuildInductionCalculatorHelpContent()
                }
            }
        };

        calculatorWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_inductionOvenCalculatorWindow, calculatorWindow))
            {
                _inductionOvenCalculatorWindow = null;
            }
        };

        _inductionOvenCalculatorWindow = calculatorWindow;
        if (owner != null)
        {
            calculatorWindow.Show(owner);
            return;
        }

        calculatorWindow.Show();
    }

    private Control BuildInductionCalculatorHelpContent()
    {
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = "Pomoc - IEC 60364-5-52",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("AccentOrange", "#F59E0B")
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Kalkulator wspiera dobor orientacyjny dla relacji IB <= In <= Iz oraz podglad obciazenia faz.",
            FontSize = 11,
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Tryb prosty (zalecany): Cu/PVC/30C/bez grupowania/MCB B oraz przekroje referencyjne 1.5/2.5/4/6 mm2.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Legenda metody ulozenia przewodu:",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("TextMain", "#FFFFFF")
        });

        panel.Children.Add(new TextBlock
        {
            Text = "C - przewod/kabel ulozony bezposrednio na powierzchni lub bezposrednio w murze/betonie, bez peszla, rurki i bez koryta.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "B2 - przewod wielozylowy prowadzony w peszlu, rurce albo korycie, takze zalany w scianie lub posadzce.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Jak uzywac: w zakladce Kalkulator ustaw pole \"Sposob ulozenia przewodu (IEC)\" na C lub B2. Wynik Iz i ocena przeciazenia sa liczone dla wybranej metody.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Dodatkowe korekty Iz: material przewodu (Cu/Al), temperatura otoczenia i wspolczynnik grupowania kG.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Model spadku napiecia: 1F (L-N) oraz 2x230V (L-N + L-N) dla wariantu dwufazowego.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Weryfikacja rozszerzona TN/TT to checklista orientacyjna (SWZ uproszczone dla TN oraz RA*IΔn <= 50V dla TT), nie pelny protokol pomiarowy.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Uwaga projektowa: wartosci maja charakter pomocniczy. Finalny dobor musi uwzgledniac warunki rzeczywiste (temperatura, grupowanie, tor prowadzenia, dokumentacja producenta).",
            FontSize = 10,
            Foreground = ResolveBrush("AccentOrange", "#F59E0B"),
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            Padding = new Thickness(12),
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = panel
            }
        };
    }

    private void AddInductionOvenCalculator(StackPanel parent, SymbolItem symbol)
    {
        var groupSymbols = GetGroupSymbols(symbol);

        var inductionMcb = groupSymbols
            .FirstOrDefault(candidate => _moduleTypeService.IsMcb(candidate) && _moduleTypeService.GetPoleCount(candidate) is ModulePoleCount.P2 or ModulePoleCount.P3);

        double inductionPower = inductionMcb?.PowerW > 0 ? inductionMcb.PowerW : 7360;
        double cableLength = Math.Max(1, inductionMcb?.CableLength > 0 ? inductionMcb.CableLength : symbol.CableLength);
        double cableCrossSection = Math.Max(1.5, inductionMcb?.CableCrossSection > 0 ? inductionMcb.CableCrossSection : symbol.CableCrossSection);

        var sectionBorder = new Border
        {
            Margin = new Thickness(0, 14, 0, 6),
            Padding = new Thickness(10),
            Background = ResolveBrush("PanelBackgroundAlt", "#2D3748"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6)
        };

        var sectionPanel = new StackPanel { Spacing = 6 };
        sectionBorder.Child = sectionPanel;

        sectionPanel.Children.Add(new TextBlock
        {
            Text = "Kalkulator: indukcja",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("AccentOrange", "#F59E0B")
        });

        sectionPanel.Children.Add(new TextBlock
        {
            Text = "IEC 60364-5-52: IB <= In <= Iz, kontrola spadku napięcia i podgląd obciążenia faz.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#9CA3AF"),
            TextWrapping = TextWrapping.Wrap
        });

        sectionPanel.Children.Add(new TextBlock
        {
            Text = "Asymetria obciazenia faz to kryterium projektowe. Asymetria napiecia (np. EN 50160) dotyczy parametrow sieci, nie tego kalkulatora.",
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#9CA3AF"),
            TextWrapping = TextWrapping.Wrap
        });

        var calculatorModeInput = new ComboBox
        {
            ItemsSource = new[]
            {
                "Prosty (zalecany)",
                "Rozszerzony"
            },
            SelectedIndex = 0,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2),
            FontSize = 11
        };
        var calculationVariantInput = new ComboBox
        {
            ItemsSource = new[]
            {
                "2F (L1+L2)",
                "1F (L1)"
            },
            SelectedIndex = 0,
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2),
            FontSize = 11
        };

        AddCalculatorRow(sectionPanel, "Tryb kalkulatora", calculatorModeInput);
        AddCalculatorRow(sectionPanel, "Wariant obliczenia", calculationVariantInput);
        sectionPanel.Children.Add(new TextBlock
        {
            Text = "Tryb prosty: Cu/PVC/30C/bez grupowania/MCB B. Pola rozszerzone sa ignorowane.",
            FontSize = 9,
            Foreground = ResolveBrush("TextSecondary", "#9CA3AF"),
            TextWrapping = TextWrapping.Wrap
        });

        var inductionPowerInput = CreateCalculatorNumericInput(inductionPower, 100, 25000, 10);
        var cableLengthInput = CreateCalculatorNumericInput(cableLength, 1, 300, 1);
        var cableCrossSectionInput = CreateCalculatorNumericInput(cableCrossSection, 1, 16, 0.5m);
        var cosPhiInput = CreateCalculatorNumericInput(0.9, 0.6, 1.0, 0.01m);
        var powerManagementLimitInput = CreateCalculatorNumericInput(inductionPower, 0, 25000, 100);
        var simultaneityFactorInput = CreateCalculatorNumericInput(1.0, 0.1, 1.0, 0.05m);
        var ambientTemperatureInput = CreateCalculatorNumericInput(30.0, 10, 60, 1);
        var groupingFactorInput = CreateCalculatorNumericInput(1.0, 0.5, 1.0, 0.05m);
        var cableInstallationMethodInput = new ComboBox
        {
            ItemsSource = new[]
            {
                "B2 - peszel/rurka/koryto w scianie lub posadzce",
                "C - ulozenie bezposrednie bez oslon"
            },
            SelectedIndex = 0,
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2),
            FontSize = 11
        };
        var conductorMaterialInput = new ComboBox
        {
            ItemsSource = new[]
            {
                "Cu - miedz",
                "Al - aluminium"
            },
            SelectedIndex = 0,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2),
            FontSize = 11
        };
        var extendedVerificationEnabledInput = new CheckBox
        {
            IsChecked = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var earthingSystemInput = new ComboBox
        {
            ItemsSource = new[]
            {
                "TN",
                "TT"
            },
            SelectedIndex = 0,
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2),
            FontSize = 11
        };
        var mcbCurveInput = new ComboBox
        {
            ItemsSource = new[]
            {
                "B",
                "C",
                "D"
            },
            SelectedIndex = 0,
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2),
            FontSize = 11
        };
        var faultLoopImpedanceInput = CreateCalculatorNumericInput(0.0, 0, 25, 0.01m);
        var earthResistanceInput = CreateCalculatorNumericInput(0.0, 0, 2000, 1);
        var rcdResidualCurrentInput = CreateCalculatorNumericInput(30.0, 10, 1000, 1);

        AddCalculatorRow(sectionPanel, "Moc indukcji [W]", inductionPowerInput);
        AddCalculatorRow(sectionPanel, "Długość przewodu [m]", cableLengthInput);
        AddCalculatorRow(sectionPanel, "Przekrój przewodu [mm2]", cableCrossSectionInput);
        AddCalculatorRow(sectionPanel, "Sposob ulozenia przewodu (IEC)", cableInstallationMethodInput);
        AddCalculatorRow(sectionPanel, "Material przewodu", conductorMaterialInput);
        AddCalculatorRow(sectionPanel, "Temperatura otoczenia [C]", ambientTemperatureInput);
        AddCalculatorRow(sectionPanel, "Wsp. grupowania kG [0.5-1]", groupingFactorInput);
        AddCalculatorRow(sectionPanel, "cosφ", cosPhiInput);

        AddCalculatorRow(sectionPanel, "Limit PowerManagement [W] (0 = brak)", powerManagementLimitInput);
        AddCalculatorRow(sectionPanel, "Wsp. jednoczesnosci k_j [0-1]", simultaneityFactorInput);
        sectionPanel.Children.Add(new TextBlock
        {
            Text = "Weryfikacja rozszerzona (orientacyjna):",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        AddCalculatorRow(sectionPanel, "Wlacz weryfikacje TN/TT", extendedVerificationEnabledInput);
        AddCalculatorRow(sectionPanel, "Uklad sieci", earthingSystemInput);
        AddCalculatorRow(sectionPanel, "Krzywa MCB (SWZ uproszcz.)", mcbCurveInput);
        AddCalculatorRow(sectionPanel, "Zs [ohm] (TN)", faultLoopImpedanceInput);
        AddCalculatorRow(sectionPanel, "RA [ohm] (TT)", earthResistanceInput);
        AddCalculatorRow(sectionPanel, "IΔn RCD [mA]", rcdResidualCurrentInput);

        var calculateButton = new Button
        {
            Content = "Przelicz",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ResolveBrush("AccentBlue", "#3B82F6"),
            Foreground = ResolveBrush("White", "#FFFFFF"),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 4, 0, 0)
        };

        var twoPhaseResult = new TextBlock
        {
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("TextMain", "#FFFFFF")
        };
        var singlePhaseResult = new TextBlock
        {
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("TextMain", "#FFFFFF")
        };
        var notesResult = new TextBlock
        {
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("AccentOrange", "#F59E0B")
        };

        var twoPhaseChart = AddPhaseLoadChart(sectionPanel, "Graficzne obciążenie faz (2F)");
        var singlePhaseChart = AddPhaseLoadChart(sectionPanel, "Graficzne obciążenie faz (1F)");

        sectionPanel.Children.Add(calculateButton);
        sectionPanel.Children.Add(new Border { Height = 1, Background = ResolveBrush("PanelBorder", "#4B5563"), Opacity = 0.35, Margin = new Thickness(0, 2) });
        sectionPanel.Children.Add(twoPhaseResult);
        sectionPanel.Children.Add(new Border { Height = 1, Background = ResolveBrush("PanelBorder", "#4B5563"), Opacity = 0.2, Margin = new Thickness(0, 1) });
        sectionPanel.Children.Add(singlePhaseResult);
        sectionPanel.Children.Add(notesResult);

        void Recalculate()
        {
            bool isSimpleMode = calculatorModeInput.SelectedIndex != 1;
            bool preferSinglePhase = calculationVariantInput.SelectedIndex == 1;
            cosPhiInput.IsEnabled = !isSimpleMode;
            powerManagementLimitInput.IsEnabled = !isSimpleMode;
            simultaneityFactorInput.IsEnabled = !isSimpleMode;
            conductorMaterialInput.IsEnabled = !isSimpleMode;
            ambientTemperatureInput.IsEnabled = !isSimpleMode;
            groupingFactorInput.IsEnabled = !isSimpleMode;
            extendedVerificationEnabledInput.IsEnabled = !isSimpleMode;
            earthingSystemInput.IsEnabled = !isSimpleMode;
            mcbCurveInput.IsEnabled = !isSimpleMode;
            faultLoopImpedanceInput.IsEnabled = !isSimpleMode;
            earthResistanceInput.IsEnabled = !isSimpleMode;
            rcdResidualCurrentInput.IsEnabled = !isSimpleMode;
            var selectedMethod = cableInstallationMethodInput.SelectedIndex == 1
                ? InductionCableInstallationMethod.C
                : InductionCableInstallationMethod.B2;
            var selectedMaterial = isSimpleMode
                ? InductionConductorMaterial.Copper
                : (conductorMaterialInput.SelectedIndex == 1 ? InductionConductorMaterial.Aluminum : InductionConductorMaterial.Copper);
            var selectedEarthingSystem = isSimpleMode
                ? InductionEarthingSystem.TN
                : (earthingSystemInput.SelectedIndex == 1 ? InductionEarthingSystem.TT : InductionEarthingSystem.TN);
            var selectedMcbCurve = isSimpleMode
                ? InductionMcbCurve.B
                : mcbCurveInput.SelectedIndex switch
                {
                    1 => InductionMcbCurve.C,
                    2 => InductionMcbCurve.D,
                    _ => InductionMcbCurve.B
                };
            double selectedCrossSection = isSimpleMode
                ? NormalizeSimpleCrossSection((double)(cableCrossSectionInput.Value ?? 0))
                : (double)(cableCrossSectionInput.Value ?? 0);
            double selectedCosPhi = isSimpleMode ? 0.9 : (double)(cosPhiInput.Value ?? 0.9m);
            double selectedPowerManagementLimit = isSimpleMode ? 0.0 : (double)(powerManagementLimitInput.Value ?? 0);
            double selectedSimultaneity = isSimpleMode ? 1.0 : (double)(simultaneityFactorInput.Value ?? 1m);
            double selectedAmbientTemperature = isSimpleMode ? 30.0 : (double)(ambientTemperatureInput.Value ?? 30m);
            double selectedGroupingFactor = isSimpleMode ? 1.0 : (double)(groupingFactorInput.Value ?? 1m);
            bool enableExtendedVerification = !isSimpleMode && extendedVerificationEnabledInput.IsChecked == true;
            double selectedFaultLoopImpedance = isSimpleMode ? 0.0 : (double)(faultLoopImpedanceInput.Value ?? 0m);
            double selectedEarthResistance = isSimpleMode ? 0.0 : (double)(earthResistanceInput.Value ?? 0m);
            double selectedRcdResidualCurrent = isSimpleMode ? 30.0 : (double)(rcdResidualCurrentInput.Value ?? 30m);

            var input = new InductionOvenCalculatorInput(
                (double)(inductionPowerInput.Value ?? 0),
                (double)(cableLengthInput.Value ?? 0),
                selectedCrossSection,
                selectedCosPhi,
                PhaseVoltageV: 230.0,
                PowerManagementLimitW: selectedPowerManagementLimit,
                SimultaneityFactor: selectedSimultaneity,
                CableInstallationMethod: selectedMethod,
                ConductorMaterial: selectedMaterial,
                AmbientTemperatureC: selectedAmbientTemperature,
                GroupingCorrectionFactor: selectedGroupingFactor,
                EnableExtendedVerification: enableExtendedVerification,
                EarthingSystem: selectedEarthingSystem,
                McbCurve: selectedMcbCurve,
                FaultLoopImpedanceOhm: selectedFaultLoopImpedance,
                EarthResistanceOhm: selectedEarthResistance,
                RcdResidualCurrentmA: selectedRcdResidualCurrent);

            var result = _inductionOvenCalculator.Calculate(input);

            twoPhaseResult.Text = isSimpleMode
                ? BuildSimpleScenarioText(result.TwoPhaseInductionScenario)
                : BuildScenarioText(result.TwoPhaseInductionScenario);
            singlePhaseResult.Text = isSimpleMode
                ? BuildSimpleScenarioText(result.SinglePhaseInductionScenario)
                : BuildScenarioText(result.SinglePhaseInductionScenario);

            if (isSimpleMode)
            {
                twoPhaseResult.IsVisible = !preferSinglePhase;
                singlePhaseResult.IsVisible = preferSinglePhase;
                twoPhaseChart.ChartPanel.IsVisible = !preferSinglePhase;
                singlePhaseChart.ChartPanel.IsVisible = preferSinglePhase;
            }
            else
            {
                twoPhaseResult.IsVisible = true;
                singlePhaseResult.IsVisible = true;
                twoPhaseChart.ChartPanel.IsVisible = true;
                singlePhaseChart.ChartPanel.IsVisible = true;
            }

            var notes = result.TwoPhaseInductionScenario.Notes
                .Concat(result.SinglePhaseInductionScenario.Notes)
                .Distinct()
                .ToList();
            if (isSimpleMode)
            {
                notes.Insert(0, "Tryb prosty: zalozenia stale Cu/PVC/30C/bez grupowania/MCB B.");
            }
            notesResult.Text = notes.Count == 0
                ? "Uwagi: brak ostrzeżeń dla podanych danych."
                : $"Uwagi: {string.Join(" ", notes)}";

            UpdatePhaseLoadChart(twoPhaseChart, result.TwoPhaseInductionScenario);
            UpdatePhaseLoadChart(singlePhaseChart, result.SinglePhaseInductionScenario);
        }

        calculateButton.Click += (_, _) => Recalculate();
        calculatorModeInput.SelectionChanged += (_, _) => Recalculate();
        calculationVariantInput.SelectionChanged += (_, _) => Recalculate();
        Recalculate();

        parent.Children.Add(sectionBorder);
    }

    private sealed record PhaseLoadBarSet(
        StackPanel ChartPanel,
        Border L1FillBar,
        TextBlock L1ValueText,
        TextBlock L1UsageText,
        Border L2FillBar,
        TextBlock L2ValueText,
        TextBlock L2UsageText,
        Border L3FillBar,
        TextBlock L3ValueText,
        TextBlock L3UsageText,
        TextBlock SummaryText);

    private static string BuildSimpleScenarioText(InductionOvenScenarioResult scenario)
    {
        const double voltageDropLimitPercent = 3.0;
        string inductionBreaker = scenario.RecommendedInductionBreakerA.HasValue
            ? $"{scenario.RecommendedInductionBreakerA.Value}A"
            : "brak";

        bool breakerOk = scenario.RecommendedInductionBreakerA.HasValue && scenario.InductionBreakerFitsCable;
        bool voltageDropOk = scenario.InductionVoltageDropPercent <= voltageDropLimitPercent + 0.001;
        bool simpleStatusOk = breakerOk && voltageDropOk;

        string statusLabel = simpleStatusOk ? "OK" : "NIE";
        string voltageStatus = voltageDropOk ? "OK" : "za duzy";
        string breakerStatus = breakerOk ? "OK" : "brak poprawnego doboru";

        return $"{scenario.ScenarioName}\n" +
               $"Ib: {scenario.InductionCurrentA:F1}A | Iz: {scenario.CableCapacityA:F1}A | MCB: {inductionBreaker} ({breakerStatus})\n" +
               $"dU: {scenario.InductionVoltageDropPercent:F2}% ({voltageStatus}, limit {voltageDropLimitPercent:F1}%)\n" +
               $"Wynik: {statusLabel}";
    }

    private static double NormalizeSimpleCrossSection(double rawCrossSection)
    {
        var allowed = new[] { 1.5, 2.5, 4.0, 6.0 };
        double nearest = allowed[0];
        double minDistance = double.MaxValue;

        foreach (var candidate in allowed)
        {
            double distance = Math.Abs(rawCrossSection - candidate);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private static string BuildScenarioText(InductionOvenScenarioResult scenario)
    {
        string inductionBreaker = scenario.RecommendedInductionBreakerA.HasValue
            ? $"{scenario.RecommendedInductionBreakerA.Value}A"
            : "brak";

        string powerManagementInfo = scenario.IsPowerManagementLimiting
            ? " (limit PM aktywny)"
            : "";
        string installationMethodCode = InductionOvenCalculatorService.GetInstallationMethodCode(scenario.CableInstallationMethod);
        string conductorCode = InductionOvenCalculatorService.GetConductorMaterialCode(scenario.ConductorMaterial);
        string extendedVerificationInfo = scenario.IsExtendedVerificationEnabled
            ? $"\n{scenario.ExtendedVerificationSummary}"
            : "";

        string layoutInfo;
        if (IsSinglePhaseScenario(scenario))
        {
            layoutInfo = "Rozklad faz: jednofazowy (obciazona tylko L1).";
        }
        else if (IsTwoPhaseScenario(scenario))
        {
            var (statusLabel, _) = AssessPhaseImbalance(scenario.PhaseImbalancePercent);
            layoutInfo =
                $"Balans faz aktywnych L1/L2: {scenario.PhaseImbalancePercent:F1}% ({statusLabel}). Uklad 2-fazowy: L3 nieaktywna.";
        }
        else
        {
            var (statusLabel, _) = AssessPhaseImbalance(scenario.PhaseImbalancePercent);
            layoutInfo =
                $"Asymetria obciazenia L1/L2/L3: {scenario.PhaseImbalancePercent:F1}% ({statusLabel}).";
        }

        var phasePower = CalculatePhasePowerDistribution(scenario);

        return $"{scenario.ScenarioName}\n" +
               $"Moc: nominalna {scenario.NominalInductionPowerW:F0}W | po k_j {scenario.SimultaneousInductionPowerW:F0}W | efektywna {scenario.EffectiveInductionPowerW:F0}W{powerManagementInfo}\n" +
               $"Indukcja: {scenario.InductionCurrentA:F1}A, sugerowane MCB: {inductionBreaker}, dU: {scenario.InductionVoltageDropPercent:F2}% [{scenario.VoltageDropModelCode}]\n" +
               $"Fazy: L1 {scenario.L1CurrentA:F1}A | L2 {scenario.L2CurrentA:F1}A | L3 {scenario.L3CurrentA:F1}A\n" +
               $"Moc na faze: L1 {FormatPowerKw(phasePower.L1PowerW)} | L2 {FormatPowerKw(phasePower.L2PowerW)} | L3 {FormatPowerKw(phasePower.L3PowerW)}\n" +
               $"{layoutInfo}\n" +
               $"Iz: bazowe {scenario.BaseCableCapacityA:F1}A | kT {scenario.TemperatureCorrectionFactor:F2} | kM {scenario.MaterialCorrectionFactor:F2} | kG {scenario.GroupingCorrectionFactor:F2} => {scenario.CableCapacityA:F1}A ({installationMethodCode}, {conductorCode}, Ta={scenario.AmbientTemperatureC:F0}C){extendedVerificationInfo}";
    }

    private PhaseLoadBarSet AddPhaseLoadChart(StackPanel parent, string title)
    {
        var chartPanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 4, 0, 0)
        };

        chartPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB")
        });

        var activePhaseBrush = ResolveBrush("AccentBlue", "#3B82F6");
        var (l1FillBar, l1ValueText, l1UsageText) = AddPhaseLoadRow(chartPanel, "L1", activePhaseBrush);
        var (l2FillBar, l2ValueText, l2UsageText) = AddPhaseLoadRow(chartPanel, "L2", activePhaseBrush);
        var (l3FillBar, l3ValueText, l3UsageText) = AddPhaseLoadRow(chartPanel, "L3", activePhaseBrush);

        var summaryText = new TextBlock
        {
            FontSize = 9,
            Foreground = ResolveBrush("TextSecondary", "#9CA3AF"),
            TextWrapping = TextWrapping.Wrap
        };
        chartPanel.Children.Add(summaryText);

        parent.Children.Add(chartPanel);

        return new PhaseLoadBarSet(
            chartPanel,
            l1FillBar,
            l1ValueText,
            l1UsageText,
            l2FillBar,
            l2ValueText,
            l2UsageText,
            l3FillBar,
            l3ValueText,
            l3UsageText,
            summaryText);
    }

    private (Border FillBar, TextBlock ValueText, TextBlock UsageText) AddPhaseLoadRow(StackPanel parent, string phaseLabel, IBrush fillBrush)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("32,160,Auto"),
            Margin = new Thickness(0, 0, 0, 1)
        };

        row.Children.Add(new TextBlock
        {
            Text = phaseLabel,
            FontSize = 10,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var barFill = new Border
        {
            Height = 8,
            Width = 0,
            Background = fillBrush,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var barTrack = new Border
        {
            Width = 160,
            Height = 8,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Child = barFill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };
        Grid.SetColumn(barTrack, 1);
        row.Children.Add(barTrack);

        var valueText = new TextBlock
        {
            Text = "0.0 A",
            FontSize = 9,
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var usageText = new TextBlock
        {
            Text = "wykorzystanie Iz: 0%",
            FontSize = 8,
            Foreground = ResolveBrush("TextSecondary", "#9CA3AF"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var valuePanel = new StackPanel
        {
            Spacing = 0,
            Margin = new Thickness(8, 0, 0, 0)
        };
        valuePanel.Children.Add(valueText);
        valuePanel.Children.Add(usageText);

        Grid.SetColumn(valuePanel, 2);
        row.Children.Add(valuePanel);

        parent.Children.Add(row);
        return (barFill, valueText, usageText);
    }

    private static void UpdatePhaseLoadChart(PhaseLoadBarSet barSet, InductionOvenScenarioResult scenario)
    {
        const double maxBarWidth = 160.0;
        double referenceCurrent = Math.Max(1.0, scenario.CableCapacityA);
        bool isOverloaded = IsScenarioOverloaded(scenario, referenceCurrent);
        bool isSinglePhase = IsSinglePhaseScenario(scenario);
        bool isTwoPhase = IsTwoPhaseScenario(scenario);
        string installationMethodCode = InductionOvenCalculatorService.GetInstallationMethodCode(scenario.CableInstallationMethod);
        var phasePower = CalculatePhasePowerDistribution(scenario);

        var activeFillBrush = ResolveBrush("AccentBlue", "#3B82F6");
        var inactiveFillBrush = ResolveBrush("TextTertiary", "#6B7280");
        var textMainBrush = ResolveBrush("TextMain", "#FFFFFF");
        var textSecondaryBrush = ResolveBrush("TextSecondary", "#9CA3AF");
        var overloadBrush = ResolveBrush("AccentRed", "#EF4444");

        static void UpdateBar(
            Border fillBar,
            TextBlock valueText,
            TextBlock usageText,
            double currentA,
            double phasePowerW,
            double referenceA,
            double maxWidth,
            IBrush activeBrush,
            IBrush textMain,
            IBrush textSecondary,
            IBrush overload)
        {
            double ratio = referenceA <= 0 ? 0 : currentA / referenceA;
            fillBar.Background = activeBrush;
            fillBar.Width = Math.Clamp(ratio, 0, 1) * maxWidth;
            valueText.Text = $"{currentA:F1} A | {FormatPowerKw(phasePowerW)}";
            usageText.Text = $"wykorzystanie Iz: {ratio * 100:F0}%";

            bool phaseOverloaded = ratio > 1.0;
            valueText.Foreground = phaseOverloaded ? overload : textMain;
            usageText.Foreground = phaseOverloaded ? overload : textSecondary;
        }

        UpdateBar(barSet.L1FillBar, barSet.L1ValueText, barSet.L1UsageText, scenario.L1CurrentA, phasePower.L1PowerW, referenceCurrent, maxBarWidth, activeFillBrush, textMainBrush, textSecondaryBrush, overloadBrush);
        UpdateBar(barSet.L2FillBar, barSet.L2ValueText, barSet.L2UsageText, scenario.L2CurrentA, phasePower.L2PowerW, referenceCurrent, maxBarWidth, activeFillBrush, textMainBrush, textSecondaryBrush, overloadBrush);
        UpdateBar(barSet.L3FillBar, barSet.L3ValueText, barSet.L3UsageText, scenario.L3CurrentA, phasePower.L3PowerW, referenceCurrent, maxBarWidth, activeFillBrush, textMainBrush, textSecondaryBrush, overloadBrush);

        if (isSinglePhase)
        {
            barSet.L2FillBar.Background = inactiveFillBrush;
            barSet.L3FillBar.Background = inactiveFillBrush;
            barSet.L2ValueText.Foreground = textSecondaryBrush;
            barSet.L3ValueText.Foreground = textSecondaryBrush;
            barSet.L2UsageText.Foreground = textSecondaryBrush;
            barSet.L3UsageText.Foreground = textSecondaryBrush;
            barSet.L2ValueText.Text = $"{scenario.L2CurrentA:F1} A | {FormatPowerKw(phasePower.L2PowerW)}";
            barSet.L3ValueText.Text = $"{scenario.L3CurrentA:F1} A | {FormatPowerKw(phasePower.L3PowerW)}";
            barSet.L2UsageText.Text = "faza nieaktywna w ukladzie 1F";
            barSet.L3UsageText.Text = "faza nieaktywna w ukladzie 1F";

            barSet.SummaryText.Foreground = isOverloaded
                ? overloadBrush
                : textSecondaryBrush;
            barSet.SummaryText.Text =
                $"Pef: {FormatPowerKw(scenario.EffectiveInductionPowerW)} | moc faz: L1 {FormatPowerKw(phasePower.L1PowerW)}, L2 {FormatPowerKw(phasePower.L2PowerW)}, L3 {FormatPowerKw(phasePower.L3PowerW)} | Iz({installationMethodCode}): {scenario.CableCapacityA:F1} A | przeciazenie: {(isOverloaded ? "TAK" : "NIE")} | uklad 1F: obciazona L1";
            return;
        }

        if (isTwoPhase)
        {
            barSet.L3FillBar.Background = inactiveFillBrush;
            barSet.L3ValueText.Foreground = textSecondaryBrush;
            barSet.L3UsageText.Foreground = textSecondaryBrush;
            barSet.L3ValueText.Text = $"{scenario.L3CurrentA:F1} A | {FormatPowerKw(phasePower.L3PowerW)}";
            barSet.L3UsageText.Text = "faza nieaktywna w ukladzie 2F";

            var (assessmentLabel, assessmentBrush) = AssessPhaseImbalance(scenario.PhaseImbalancePercent);
            barSet.SummaryText.Foreground = isOverloaded
                ? overloadBrush
                : assessmentBrush;
            barSet.SummaryText.Text =
                $"Pef: {FormatPowerKw(scenario.EffectiveInductionPowerW)} | moc faz: L1 {FormatPowerKw(phasePower.L1PowerW)}, L2 {FormatPowerKw(phasePower.L2PowerW)}, L3 {FormatPowerKw(phasePower.L3PowerW)} | Iz({installationMethodCode}): {scenario.CableCapacityA:F1} A | przeciazenie: {(isOverloaded ? "TAK" : "NIE")} | balans L1/L2: {scenario.PhaseImbalancePercent:F1}% ({assessmentLabel}) | L3 nieaktywna";
            return;
        }

        barSet.L3FillBar.Background = activeFillBrush;
        var (threePhaseAssessmentLabel, threePhaseAssessmentBrush) = AssessPhaseImbalance(scenario.PhaseImbalancePercent);
        barSet.SummaryText.Foreground = isOverloaded
            ? overloadBrush
            : threePhaseAssessmentBrush;
        barSet.SummaryText.Text =
            $"Pef: {FormatPowerKw(scenario.EffectiveInductionPowerW)} | moc faz: L1 {FormatPowerKw(phasePower.L1PowerW)}, L2 {FormatPowerKw(phasePower.L2PowerW)}, L3 {FormatPowerKw(phasePower.L3PowerW)} | Iz({installationMethodCode}): {scenario.CableCapacityA:F1} A | przeciazenie: {(isOverloaded ? "TAK" : "NIE")} | asymetria L1/L2/L3: {scenario.PhaseImbalancePercent:F1}% ({threePhaseAssessmentLabel})";
    }

    private static bool IsScenarioOverloaded(InductionOvenScenarioResult scenario, double referenceCurrent)
    {
        const double epsilon = 0.001;
        return scenario.L1CurrentA > referenceCurrent + epsilon
            || scenario.L2CurrentA > referenceCurrent + epsilon
            || scenario.L3CurrentA > referenceCurrent + epsilon;
    }

    private static string FormatPowerKw(double powerW)
        => $"{powerW / 1000.0:F2} kW";

    private static (double L1PowerW, double L2PowerW, double L3PowerW) CalculatePhasePowerDistribution(InductionOvenScenarioResult scenario)
    {
        const double epsilon = 0.001;
        double totalCurrent = scenario.L1CurrentA + scenario.L2CurrentA + scenario.L3CurrentA;
        if (scenario.EffectiveInductionPowerW <= epsilon || totalCurrent <= epsilon)
        {
            return (0, 0, 0);
        }

        double wattPerAmp = scenario.EffectiveInductionPowerW / totalCurrent;
        return (
            Math.Max(0, scenario.L1CurrentA * wattPerAmp),
            Math.Max(0, scenario.L2CurrentA * wattPerAmp),
            Math.Max(0, scenario.L3CurrentA * wattPerAmp));
    }

    private static bool IsSinglePhaseScenario(InductionOvenScenarioResult scenario)
        => scenario.ScenarioName.StartsWith("1F", StringComparison.OrdinalIgnoreCase);

    private static bool IsTwoPhaseScenario(InductionOvenScenarioResult scenario)
        => scenario.ScenarioName.StartsWith("2F", StringComparison.OrdinalIgnoreCase);

    private static (string Label, IBrush Brush) AssessPhaseImbalance(double imbalancePercent)
    {
        if (imbalancePercent < 10.0)
        {
            return ("OK", ResolveBrush("AccentGreen", "#10B981"));
        }

        if (imbalancePercent <= 20.0)
        {
            return ("uwaga", ResolveBrush("AccentOrange", "#F59E0B"));
        }

        return ("do poprawy", ResolveBrush("AccentRed", "#EF4444"));
    }

    private NumericUpDown CreateCalculatorNumericInput(double value, double min, double max, decimal increment)
    {
        return new NumericUpDown
        {
            Value = (decimal)value,
            Minimum = (decimal)min,
            Maximum = (decimal)max,
            Increment = increment,
            Width = 112,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ResolveBrush("PanelBackground", "#1F2937"),
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            BorderBrush = ResolveBrush("PanelBorder", "#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2),
            FontSize = 11
        };
    }

    private void AddCalculatorRow(StackPanel parent, string label, Control inputControl)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 1)
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = ResolveBrush("TextSecondary", "#D1D5DB"),
            VerticalAlignment = VerticalAlignment.Center
        });

        Grid.SetColumn(inputControl, 1);
        row.Children.Add(inputControl);
        parent.Children.Add(row);
    }

    private IReadOnlyList<SymbolItem> GetGroupSymbols(SymbolItem symbol)
    {
        if (DataContext is MainViewModel mainViewModel && !string.IsNullOrWhiteSpace(symbol.Group))
        {
            return mainViewModel.Symbols
                .Where(candidate => string.Equals(candidate.Group, symbol.Group, StringComparison.Ordinal))
                .OrderBy(candidate => candidate.X)
                .ToList();
        }

        return new[] { symbol };
    }

    private static bool TryGetScenarioFlag(IDictionary<string, string> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var raw))
        {
            return false;
        }

        return bool.TryParse(raw, out var value) && value;
    }

    /// <summary>
    /// Czyści panel edycji.
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
            CircuitEditValueApplier.Apply(_symbol, kvp.Key, value);
        }

        _onSaved?.Invoke();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        ClearPanel();
        _onClosed?.Invoke();
    }

    // === FIELD BUILDERS (ported from CircuitEditDialog) ===

    private void Control_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnSave_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void AddField(StackPanel parent, CircuitEditFieldDefinition field)
    {
        switch (field.Kind)
        {
            case CircuitEditFieldKind.Text:
                AddTextField(parent, field.Key, field.Label, field.TextValue);
                break;
            case CircuitEditFieldKind.Number:
                AddNumberField(parent, field.Key, field.Label, field.NumberValue);
                break;
            case CircuitEditFieldKind.Combo:
                AddComboField(parent, field.Key, field.Label, field.TextValue, field.Options);
                break;
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

    private static string GetInputValue(Control control) => control switch
    {
        TextBox tb => tb.Text ?? "",
        NumericUpDown nud => nud.Value?.ToString() ?? "0",
        ComboBox cb => cb.SelectedItem?.ToString() ?? "",
        _ => ""
    };

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}
