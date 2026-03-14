using CommunityToolkit.Mvvm.ComponentModel;

namespace DINBoard.Models;

/// <summary>
/// Reprezentuje ramkę grupy na canvasie — obejmuje wszystkie moduły w danej grupie.
/// </summary>
public partial class GroupFrameInfo : ObservableObject
{
    [ObservableProperty]
    private string _groupName = "";

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;
}
