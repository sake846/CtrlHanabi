using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace CtrlHanabi;

internal sealed class ParticleSceneElement : FrameworkElement
{
    private static readonly WpfBrush RocketCoreBrush = CreateFrozenBrush(WpfColor.FromRgb(255, 215, 0));
    private static readonly WpfBrush RocketGlowBrush = new RadialGradientBrush(Colors.White, Colors.Transparent);
    private static readonly WpfBrush TubeBodyBrush = CreateFrozenBrush(new LinearGradientBrush(
        new GradientStopCollection
        {
            new(WpfColor.FromRgb(78, 64, 52), 0.0),
            new(WpfColor.FromRgb(48, 37, 28), 0.35),
            new(WpfColor.FromRgb(98, 82, 66), 0.5),
            new(WpfColor.FromRgb(42, 31, 24), 0.82),
            new(WpfColor.FromRgb(70, 56, 44), 1.0)
        },
        new WpfPoint(0, 0),
        new WpfPoint(1, 0)));
    private static readonly WpfBrush TubeRimBrush = CreateFrozenBrush(new LinearGradientBrush(
        new GradientStopCollection
        {
            new(WpfColor.FromRgb(126, 112, 96), 0.0),
            new(WpfColor.FromRgb(74, 58, 44), 0.45),
            new(WpfColor.FromRgb(138, 120, 98), 1.0)
        },
        new WpfPoint(0, 0),
        new WpfPoint(1, 0)));
    private static readonly WpfBrush TubeInnerBrush = CreateFrozenBrush(new RadialGradientBrush(
        new GradientStopCollection
        {
            new(WpfColor.FromRgb(24, 20, 18), 0.0),
            new(WpfColor.FromRgb(10, 8, 7), 0.75),
            new(WpfColor.FromRgb(46, 37, 30), 1.0)
        }));
    private static readonly WpfPen TubeEdgePen = CreateFrozenPen(WpfColor.FromArgb(180, 24, 19, 15), 1.1);
    private static readonly WpfBrush RocketDimCoreBrush = CreateFrozenBrush(WpfColor.FromArgb(200, 255, 215, 0));
    private static readonly WpfBrush RocketDimGlowBrush = CreateFrozenBrush(WpfColor.FromArgb(110, 255, 245, 225));

    private IReadOnlyList<RenderTrail> _trails = Array.Empty<RenderTrail>();
    private IReadOnlyList<RenderParticle> _particles = Array.Empty<RenderParticle>();
    private RenderRocket? _rocket;

    static ParticleSceneElement()
    {
        if (RocketGlowBrush is Freezable freezable && freezable.CanFreeze)
        {
            freezable.Freeze();
        }
    }

    public void UpdateScene(IReadOnlyList<RenderTrail> trails, IReadOnlyList<RenderParticle> particles, RenderRocket? rocket)
    {
        _trails = trails;
        _particles = particles;
        _rocket = rocket;
        InvalidateVisual();
    }

    public void ClearScene()
    {
        _trails = Array.Empty<RenderTrail>();
        _particles = Array.Empty<RenderParticle>();
        _rocket = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        foreach (var trail in _trails)
        {
            drawingContext.DrawEllipse(
                CreateBrush(trail.Color, trail.Opacity),
                null,
                new WpfPoint(trail.X, trail.Y),
                trail.Size / 2,
                trail.Size / 2);
        }

        if (_rocket is RenderRocket rocket)
        {
            drawingContext.DrawRoundedRectangle(TubeBodyBrush, TubeEdgePen, new Rect(rocket.OriginX - 7.5, rocket.OriginY - 11.5, 15, 23), 3.2, 3.2);
            drawingContext.DrawRoundedRectangle(TubeRimBrush, null, new Rect(rocket.OriginX - 8.2, rocket.OriginY - 12.4, 16.4, 4.8), 2.6, 2.6);
            drawingContext.DrawEllipse(TubeInnerBrush, null, new WpfPoint(rocket.OriginX, rocket.OriginY - 9.9), 5.2, 1.85);
            if (!rocket.FuseHidden)
            {
                drawingContext.DrawEllipse(RocketDimGlowBrush, null, new WpfPoint(rocket.X, rocket.Y), 1.75, 1.75);
                drawingContext.DrawEllipse(RocketDimCoreBrush, null, new WpfPoint(rocket.X, rocket.Y), 0.625, 0.625);
            }
        }

        foreach (var particle in _particles)
        {
            drawingContext.DrawEllipse(
                CreateBrush(particle.GlowColor, particle.GlowOpacity),
                null,
                new WpfPoint(particle.X, particle.Y),
                particle.GlowSize / 2,
                particle.GlowSize / 2);
            drawingContext.DrawEllipse(
                CreateBrush(particle.CoreColor, particle.CoreOpacity),
                null,
                new WpfPoint(particle.X, particle.Y),
                particle.CoreSize / 2,
                particle.CoreSize / 2);
        }
    }

    private static WpfBrush CreateFrozenBrush(WpfColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static WpfBrush CreateFrozenBrush(WpfBrush brush)
    {
        if (brush is Freezable freezable && freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return brush;
    }

    private static WpfPen CreateFrozenPen(WpfColor color, double thickness)
    {
        var pen = new WpfPen(new SolidColorBrush(color), thickness);
        if (pen.Brush.CanFreeze)
        {
            pen.Brush.Freeze();
        }

        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
    }

    private static WpfBrush CreateBrush(WpfColor color, double opacity)
    {
        var brush = new SolidColorBrush(color) { Opacity = Math.Clamp(opacity, 0, 1) };
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}

internal readonly record struct RenderTrail(double X, double Y, double Size, WpfColor Color, double Opacity);

internal readonly record struct RenderParticle(
    double X,
    double Y,
    double GlowSize,
    double CoreSize,
    WpfColor GlowColor,
    WpfColor CoreColor,
    double GlowOpacity,
    double CoreOpacity);

internal readonly record struct RenderRocket(double X, double Y, double OriginX, double OriginY, WpfColor TrailColor, bool FuseHidden);
