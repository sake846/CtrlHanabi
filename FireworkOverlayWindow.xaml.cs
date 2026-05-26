using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CtrlHanabi.Models;

namespace CtrlHanabi;

public partial class FireworkOverlayWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly List<Particle> _particles = [];
    private readonly List<TrailParticle> _trails = [];
    private readonly Random _random = new();
    private readonly Services.SettingsService _settingsService = new();

    private HanabiSettings _settings;
    private DateTime _started;
    private bool _isBursting;
    private Rocket? _rocket;

    public FireworkOverlayWindow(HanabiSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        ApplyVirtualScreenBounds();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => UpdateFrame();
        Hide();
    }

    public void ShowFirework(Point screenPoint)
    {
        _settings = _settingsService.Load();
        ParticleCanvas.Children.Clear();
        _particles.Clear();
        _trails.Clear();

        var localX = screenPoint.X - Left;
        var localY = screenPoint.Y - Top;
        var launchHeight = _settings.RocketLaunchHeight;

        _rocket = new Rocket
        {
            X = localX,
            Y = localY + launchHeight,
            TargetX = localX,
            TargetY = localY,
            Vy = -(launchHeight / 0.55),
            Vx = (_random.NextDouble() - 0.5) * 60
        };

        _isBursting = false;
        _started = DateTime.UtcNow;
        Show();
        Activate();
        _timer.Start();
    }

    private void UpdateFrame()
    {
        ParticleCanvas.Children.Clear();

        if (!_isBursting)
        {
            UpdateRocket();
            RenderRocketAndTrail();
            return;
        }

        UpdateParticles();
        RenderParticlesAndTrail();

        if (!_particles.Any() && !_trails.Any())
        {
            _timer.Stop();
            Hide();
        }
    }

    private void UpdateRocket()
    {
        if (_rocket is null)
        {
            return;
        }

        var dt = 0.016;
        _rocket.Vy += 320 * dt;
        _rocket.X += _rocket.Vx * dt;
        _rocket.Y += _rocket.Vy * dt;

        _trails.Add(new TrailParticle(_rocket.X, _rocket.Y, 0.75, 3, Colors.Gold));

        var reached = _rocket.Y <= _rocket.TargetY || (DateTime.UtcNow - _started).TotalMilliseconds > 900;
        if (!reached)
        {
            return;
        }

        SpawnBurst(_rocket.TargetX, _rocket.TargetY);
        _rocket = null;
        _isBursting = true;
    }

    private void SpawnBurst(double x, double y)
    {
        for (var i = 0; i < _settings.ParticleCount; i++)
        {
            var angle = 2 * Math.PI * i / _settings.ParticleCount + (_random.NextDouble() - 0.5) * 0.12;
            var speed = _settings.ExplosionRadius * (0.8 + _random.NextDouble() * 0.75);
            var color = FromHsv(_random.NextDouble() * 360, 0.85, 1.0);

            _particles.Add(new Particle
            {
                X = x,
                Y = y,
                Vx = Math.Cos(angle) * speed,
                Vy = Math.Sin(angle) * speed,
                Life = 1,
                Decay = 0.8 + _random.NextDouble() * 0.45,
                Size = 3 + _random.NextDouble() * 3,
                Color = color
            });
        }
    }

    private void UpdateParticles()
    {
        var dt = 0.016;
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Vy += 300 * dt;
            p.Vx *= 0.992;
            p.Vy *= 0.992;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Life -= dt * p.Decay;

            _trails.Add(new TrailParticle(p.X, p.Y, p.Life * 0.45, p.Size * 0.75, p.Color));

            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
            }
        }

        for (var i = _trails.Count - 1; i >= 0; i--)
        {
            var t = _trails[i];
            t.Life -= dt * 1.5;
            t.Size *= 0.985;
            if (t.Life <= 0 || t.Size <= 0.8)
            {
                _trails.RemoveAt(i);
            }
        }
    }

    private void RenderRocketAndTrail()
    {
        DrawTrails();

        if (_rocket is null)
        {
            return;
        }

        var rocketGlow = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new RadialGradientBrush(Colors.White, Colors.Transparent),
            Opacity = 0.8
        };
        Canvas.SetLeft(rocketGlow, _rocket.X - 7);
        Canvas.SetTop(rocketGlow, _rocket.Y - 7);
        ParticleCanvas.Children.Add(rocketGlow);

        var rocketCore = new Ellipse
        {
            Width = 5,
            Height = 5,
            Fill = Brushes.Gold,
            Opacity = 1
        };
        Canvas.SetLeft(rocketCore, _rocket.X - 2.5);
        Canvas.SetTop(rocketCore, _rocket.Y - 2.5);
        ParticleCanvas.Children.Add(rocketCore);
    }

    private void RenderParticlesAndTrail()
    {
        DrawTrails();

        foreach (var p in _particles)
        {
            var glow = new Ellipse
            {
                Width = p.Size * 2.3,
                Height = p.Size * 2.3,
                Fill = new SolidColorBrush(p.Color),
                Opacity = p.Life * 0.24
            };
            Canvas.SetLeft(glow, p.X - glow.Width / 2);
            Canvas.SetTop(glow, p.Y - glow.Height / 2);
            ParticleCanvas.Children.Add(glow);

            var core = new Ellipse
            {
                Width = p.Size,
                Height = p.Size,
                Fill = Brushes.White,
                Opacity = p.Life
            };
            Canvas.SetLeft(core, p.X - p.Size / 2);
            Canvas.SetTop(core, p.Y - p.Size / 2);
            ParticleCanvas.Children.Add(core);
        }
    }

    private void DrawTrails()
    {
        foreach (var t in _trails)
        {
            var trail = new Ellipse
            {
                Width = t.Size,
                Height = t.Size,
                Fill = new SolidColorBrush(t.Color),
                Opacity = t.Life
            };
            Canvas.SetLeft(trail, t.X - t.Size / 2);
            Canvas.SetTop(trail, t.Y - t.Size / 2);
            ParticleCanvas.Children.Add(trail);
        }
    }

    private void ApplyVirtualScreenBounds()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ApplyVirtualScreenBounds();

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        var m = v - c;
        (double r1, double g1, double b1) = h switch
        {
            < 60 => (c, x, 0),
            < 120 => (x, c, 0),
            < 180 => (0, c, x),
            < 240 => (0, x, c),
            < 300 => (x, 0, c),
            _ => (c, 0, x)
        };

        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    private sealed class Rocket
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
    }

    private sealed class Particle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Life { get; set; }
        public double Decay { get; set; }
        public double Size { get; set; }
        public Color Color { get; set; }
    }

    private sealed class TrailParticle(double x, double y, double life, double size, Color color)
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Life { get; set; } = life;
        public double Size { get; set; } = size;
        public Color Color { get; set; } = color;
    }
}
