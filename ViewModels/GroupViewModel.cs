using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DINBoard.Models;
using DINBoard.Services;

namespace DINBoard.ViewModels;

/// <summary>
/// ViewModel dla grupy symboli wyświetlanej w panelu obwodów
/// </summary>
public partial class GroupViewModel : ObservableObject
{
    private readonly IModuleTypeService _moduleTypeService;

    public GroupViewModel(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    [ObservableProperty]
    private bool _isExpanded = true;

    public string GroupId { get; set; } = "";
    public int Order { get; set; }
    public string Name { get; set; } = "";
    public List<SymbolItem> Symbols { get; set; } = new();

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    /// <summary>
    /// Główny symbol grupy (RCD jeśli istnieje, w przeciwnym razie pierwszy)
    /// </summary>
    public SymbolItem? MainSymbol
    {
        get
        {
            if (Symbols == null || Symbols.Count == 0) return null;
            try
            {
                return Symbols.FirstOrDefault(s =>
                    s != null && (_moduleTypeService.IsRcd(s) || _moduleTypeService.IsSwitch(s))) ?? Symbols.FirstOrDefault();
            }
            catch (Exception ex)
            {
                AppLog.Error("Błąd w MainSymbol", ex);
                return Symbols.FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// Symbole podrzędne (wszystkie oprócz głównego) z przypisanymi numerami i info RCD
    /// </summary>
    public List<SymbolItem> SubSymbols
    {
        get
        {
            if (Symbols == null || Symbols.Count == 0) return new List<SymbolItem>();
            try
            {
                var main = MainSymbol;
                if (main == null) return Symbols.ToList();
                var subSymbols = Symbols.Where(s => s != null && s != main && !_moduleTypeService.IsRcd(s) && !_moduleTypeService.IsSwitch(s)).ToList();

                bool mainIsRcd = _moduleTypeService.IsRcd(main);
                for (int i = 0; i < subSymbols.Count; i++)
                {
                    subSymbols[i].ModuleNumber = i + 1;

                    if (mainIsRcd)
                    {
                        subSymbols[i].RcdSymbolId = main.Id;
                        if (main.RcdRatedCurrent > 0)
                        {
                            subSymbols[i].RcdRatedCurrent = main.RcdRatedCurrent;
                            subSymbols[i].RcdResidualCurrent = main.RcdResidualCurrent;
                            subSymbols[i].RcdType = main.RcdType;
                        }
                    }
                }

                return subSymbols;
            }
            catch (Exception ex)
            {
                AppLog.Error("Błąd w SubSymbols", ex);
                return new List<SymbolItem>();
            }
        }
    }

    public string MainType => _moduleTypeService.GetModuleTypeName(MainSymbol);

    public string MainLabel => MainSymbol?.Label ?? "Brak";

    public static string GetSymbolType(SymbolItem symbol, IModuleTypeService moduleTypeService)
    {
        ArgumentNullException.ThrowIfNull(moduleTypeService);
        return moduleTypeService.GetModuleTypeName(symbol);
    }

    /// <summary>
    /// Tworzy listę grup z symboli, sortując symbole w każdej grupie (RCD pierwsze, potem MCB)
    /// oraz same grupy deterministycznie (preferuj <see cref="CircuitGroup.Order"/> jeśli dostępne).
    /// </summary>
    public static List<GroupViewModel> CreateGroupsFromSymbols(
        IEnumerable<SymbolItem> symbols,
        IReadOnlyList<CircuitGroup>? projectGroups = null,
        IModuleTypeService? moduleTypeService = null)
    {
        ArgumentNullException.ThrowIfNull(moduleTypeService);

        var svc = moduleTypeService;
        var projectGroupById = (projectGroups ?? Array.Empty<CircuitGroup>())
            .GroupBy(g => g.Id)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var groupInfos = symbols
            .Where(s => !string.IsNullOrEmpty(s.Group))
            .GroupBy(s => s.Group!)
            .Select(g =>
            {
                var groupId = g.Key;
                var list = g.ToList();

                projectGroupById.TryGetValue(groupId, out var projectGroup);

                var customName = list.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.GroupName))?.GroupName?.Trim();
                var explicitOrder = projectGroup?.Order ?? 0;
                var parsedOrder = TryExtractPositiveNumber(customName);

                var orderKey = explicitOrder > 0
                    ? explicitOrder
                    : (parsedOrder ?? int.MaxValue);

                var displayName = !string.IsNullOrWhiteSpace(customName)
                    ? customName!
                    : (!string.IsNullOrWhiteSpace(projectGroup?.Name) ? projectGroup!.Name : string.Empty);

                return new
                {
                    GroupId = groupId,
                    Order = explicitOrder,
                    OrderKey = orderKey,
                    DisplayName = displayName,
                    MinY = list.Min(s => s.Y),
                    MinX = list.Min(s => s.X),
                    Symbols = SortSymbolsInGroup(list, svc)
                };
            })
            .OrderBy(g => g.OrderKey)
            .ThenBy(g => g.MinY)
            .ThenBy(g => g.MinX)
            .ThenBy(g => g.GroupId)
            .ToList();

        var result = new List<GroupViewModel>();
        int fallbackNumber = 1;

        foreach (var g in groupInfos)
        {
            var vm = new GroupViewModel(svc)
            {
                GroupId = g.GroupId,
                Order = g.Order,
                Symbols = g.Symbols
            };

            if (!string.IsNullOrWhiteSpace(g.DisplayName))
            {
                vm.Name = g.DisplayName;
            }
            else if (g.OrderKey != int.MaxValue)
            {
                vm.Name = $"Grupa - {g.OrderKey}";
            }
            else
            {
                vm.Name = $"Grupa - {fallbackNumber}";
            }

            fallbackNumber++;
            result.Add(vm);
        }

        return result;
    }

    private static int? TryExtractPositiveNumber(string? text)
        => CommonHelpers.TryExtractPositiveNumber(text);

    private static List<SymbolItem> SortSymbolsInGroup(List<SymbolItem> symbols, IModuleTypeService svc)
    {
        var head = symbols.FirstOrDefault(s => svc.IsRcd(s))
            ?? symbols.FirstOrDefault(s => svc.IsSwitch(s));
        double headX = head?.X ?? 0;

        return symbols.OrderBy(s =>
        {
            if (svc.IsRcd(s) || svc.IsSwitch(s)) return 0;
            if (svc.IsMcb(s)) return 1;
            return 2;
        })
        .ThenBy(s =>
        {
            if (svc.IsMcb(s))
                return Math.Abs(s.X - headX);
            return 0;
        })
        .ToList();
    }
}
