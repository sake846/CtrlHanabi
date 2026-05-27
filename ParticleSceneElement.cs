using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace CtrlHanabi;

internal sealed class ParticleSceneElement : FrameworkElement
{
    private static readonly WpfBrush RocketCoreBrush = CreateFrozenBrush(WpfColor.FromRgb(255, 215, 0));
    private static readonly WpfBrush TubeGlowBrush = CreateFrozenBrush(WpfColor.FromArgb(90, 255, 210, 135));
    private static readonly WpfBrush RocketGlowBrush = new RadialGradientBrush(Colors.White, Colors.Transparent);
    private static readonly WpfBrush TubeBrush = CreateFrozenBrush(WpfColor.FromRgb(37, 28, 20));
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
            drawingContext.DrawEllipse(TubeGlowBrush, null, new WpfPoint(rocket.OriginX, rocket.OriginY), 13, 6);
            drawingContext.DrawRoundedRectangle(TubeBrush, null, new Rect(rocket.OriginX - 7, rocket.OriginY - 11, 14, 22), 3, 3);
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
