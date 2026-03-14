using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace DINBoard.Controls;

public class DinRailView : Control
{
    private SvgSource? _svgSource;

    public static readonly StyledProperty<string?> SvgContentProperty =
        AvaloniaProperty.Register<DinRailView, string?>(nameof(SvgContent), null);

    public string? SvgContent
    {
        get => GetValue(SvgContentProperty);
        set => SetValue(SvgContentProperty, value);
    }

    static DinRailView()
    {
        AffectsRender<DinRailView>(SvgContentProperty);
        AffectsMeasure<DinRailView>(SvgContentProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == SvgContentProperty)
        {
            LoadSvg(change.NewValue as string);
        }
    }

    private void LoadSvg(string? svgContent)
    {
        if (string.IsNullOrEmpty(svgContent))
        {
            _svgSource = null;
            InvalidateVisual();
            return;
        }

        try
        {
            _svgSource = SvgSource.LoadFromSvg(svgContent);
        }
        catch (ArgumentException)
        {
            _svgSource = null;
        }
        catch (InvalidOperationException)
        {
            _svgSource = null;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        base.Render(context);

        if (_svgSource?.Picture == null)
            return;

        var svgImage = new SvgImage { Source = _svgSource };

        // Użyj oryginalnych proporcji SVG zamiast rozciągania
        var picture = _svgSource.Picture;
        if (picture == null) return;

        double svgWidth = picture.CullRect.Width;
        double svgHeight = picture.CullRect.Height;

        if (svgWidth <= 0 || svgHeight <= 0) return;

        // Skalowanie proporcjonalne (Uniform)
        double scaleX = Bounds.Width / svgWidth;
        double scaleY = Bounds.Height / svgHeight;
        double scale = Math.Min(scaleX, scaleY);  // Zachowaj proporcje

        double destWidth = svgWidth * scale;
        double destHeight = svgHeight * scale;

        // Centruj w dostępnym obszarze
        double offsetX = (Bounds.Width - destWidth) / 2;
        double offsetY = (Bounds.Height - destHeight) / 2;

        var destRect = new Rect(offsetX, offsetY, destWidth, destHeight);

        if (destRect.Width > 0 && destRect.Height > 0)
        {
            context.DrawImage(svgImage, destRect);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = Width;
        double h = Height;

        if (double.IsNaN(w)) w = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        if (double.IsNaN(h)) h = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;

        return new Size(w, h);
    }

    public void SetRail(string svgContent, double width, double height)
    {
        Width = width;
        Height = height;
        SvgContent = svgContent;
    }
}
