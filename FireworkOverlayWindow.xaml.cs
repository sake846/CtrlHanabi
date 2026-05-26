using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FormsScreen = System.Windows.Forms.Screen;
using CtrlHanabi.Models;
using Microsoft.Win32;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace CtrlHanabi;

public partial class FireworkOverlayWindow : Window
{
    private const double PerspectiveDistance = 720;
    private const double MaxDepthOffset = 280;
    private const double FuseDelaySeconds = 0.11;
    private const double FuseDarkSeconds = 0.055;

    private readonly DispatcherTimer _timer;
    private readonly List<Particle> _particles = [];
    private readonly List<TrailParticle> _trails = [];
    private readonly List<RenderTrail> _renderTrails = [];
    private readonly List<RenderParticle> _renderParticles = [];
    private readonly Random _random = new();
    private readonly Services.SettingsService _settingsService = new();
    private readonly ParticleSceneElement _scene = new();
    private readonly BurstPalette[] _burstPalettes =
    [
        new("gold", WpfColor.FromRgb(255, 197, 106), WpfColor.FromRgb(255, 246, 220)),
        new("amber", WpfColor.FromRgb(255, 156, 92), WpfColor.FromRgb(255, 244, 224)),
        new("scarlet", WpfColor.FromRgb(255, 114, 98), WpfColor.FromRgb(255, 238, 232)),
        new("rose", WpfColor.FromRgb(255, 132, 150), WpfColor.FromRgb(255, 240, 242)),
        new("green", WpfColor.FromRgb(138, 212, 118), WpfColor.FromRgb(239, 249, 230)),
        new("blue", WpfColor.FromRgb(115, 176, 255), WpfColor.FromRgb(236, 244, 255)),
        new("violet", WpfColor.FromRgb(176, 140, 255), WpfColor.FromRgb(244, 239, 255))
    ];
    private readonly BurstPalette[] _kamuroPalettes =
    [
        new("kamuro-gold", WpfColor.FromRgb(255, 198, 96), WpfColor.FromRgb(255, 244, 214)),
        new("kamuro-amber", WpfColor.FromRgb(255, 174, 86), WpfColor.FromRgb(255, 238, 201)),
        new("kamuro-deep", WpfColor.FromRgb(255, 152, 72), WpfColor.FromRgb(255, 228, 184))
    ];

    private HanabiSettings _settings;
    private DateTime _started;
    private bool _isBursting;
    private Rocket? _rocket;
    private BurstPalette _currentPalette;
    private BurstKind _currentBurstKind;

    public FireworkOverlayWindow(HanabiSettings settings)
    {
        _settings = settings;
        _currentPalette = _burstPalettes[0];
        _currentBurstKind = BurstKind.Chrysanthemum;
        InitializeComponent();
        RootHost.Children.Add(_scene);

        ApplyVirtualScreenBounds();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => UpdateFrame();
        Hide();
    }

    public void ShowFirework(WpfPoint screenPoint)
    {
        _settings = _settingsService.Load();
        _particles.Clear();
        _trails.Clear();
        _renderParticles.Clear();
        _renderTrails.Clear();
        _scene.ClearScene();

        var localX = screenPoint.X - Left;
        var localY = screenPoint.Y - Top;
        var launchY = GetLaunchY(screenPoint);
        var startX = localX + (_random.NextDouble() - 0.5) * 36;
        var arcPull = (localX - startX) * 1.8;
        var travel = Math.Max(launchY - localY, 120);
        _currentBurstKind = PickBurstKind();
        _currentPalette = _currentBurstKind == BurstKind.KamuroGiku
            ? PickKamuroPalette()
            : PickBurstPalette();

        _rocket = new Rocket
        {
            X = startX,
            Y = launchY,
            OriginX = startX,
            OriginY = launchY,
            TargetX = localX,
            TargetY = localY,
            ApexX = localX,
            ApexY = localY,
            Vy = CalculateRocketLaunchVelocity(travel),
            Vx = arcPull,
            SwayPhase = _random.NextDouble() * Math.PI * 2,
            BurstDelay = 0,
            FuseHidden = false,
            FuseStarted = false,
            TrailColor = _currentPalette.Outer
        };

        _isBursting = false;
        _started = DateTime.UtcNow;
        Show();
        Activate();
        _timer.Start();
    }

    private void UpdateFrame()
    {
        if (!_isBursting)
        {
            UpdateRocket();
            RenderFrame();
            return;
        }

        UpdateParticles();
        RenderFrame();

        if (!_particles.Any() && !_trails.Any())
        {
            _timer.Stop();
            _scene.ClearScene();
            Hide();
        }
    }

    private void UpdateRocket()
    {
        if (_rocket is null)
        {
            return;
        }

        const double dt = 0.016;
        var totalRise = Math.Max(_rocket.OriginY - _rocket.TargetY, 1);
        var progress = Math.Clamp((_rocket.OriginY - _rocket.Y) / totalRise, 0, 1);
        var gravity = 620 + (progress * 760);
        var sway = Math.Sin((progress * Math.PI * 1.4) + _rocket.SwayPhase) * (1 - progress) * 18;

        _rocket.Vy += gravity * dt;
        _rocket.Vx = (_rocket.Vx * 0.952) + (sway * dt);
        _rocket.X += _rocket.Vx * dt;
        _rocket.Y += _rocket.Vy * dt;

        var startedFalling = _rocket.Vy >= 0;
        var highEnough = _rocket.Y <= (_rocket.TargetY + 8);

        if (!_rocket.FuseStarted && startedFalling && highEnough)
        {
            _rocket.FuseStarted = true;
            _rocket.ApexX = _rocket.X;
            _rocket.ApexY = _rocket.Y;
        }

        if (_rocket.FuseStarted)
        {
            _rocket.BurstDelay += dt;
            _rocket.FuseHidden = true;
        }
        else
        {
            _rocket.BurstDelay = 0;
            _rocket.FuseHidden = false;
        }


        var shouldBurst = _rocket.BurstDelay >= FuseDelaySeconds;
        if (!shouldBurst)
        {
            return;
        }

        SpawnBurst(_rocket.ApexX, _rocket.ApexY);
        _rocket = null;
        _isBursting = true;
    }

    private void SpawnBurst(double x, double y)
    {
        var petalCount = Math.Max(_settings.ParticleCount * 2, 168);
        var outerRadius = _settings.ExplosionRadius * 1.18;
        var isBotan = _currentBurstKind == BurstKind.Botan;
        var isKamuro = _currentBurstKind == BurstKind.KamuroGiku;

        for (var i = 0; i < petalCount; i++)
        {
            var t = (i + 0.5) / petalCount;
            var zDirection = 1 - (2 * t);
            var phi = Math.Acos(Math.Clamp(zDirection, -1, 1));
            var theta = Math.PI * (3 - Math.Sqrt(5)) * i;
            var sinPhi = Math.Sin(phi);
            var dirX = Math.Cos(theta) * sinPhi;
            var dirY = Math.Sin(theta) * sinPhi;
            var radialFactor = 1.0;
            var speed = outerRadius * (isKamuro
                ? (1.56 + _random.NextDouble() * 0.05) * radialFactor * 0.8
                : isBotan
                ? (1.82 + _random.NextDouble() * 0.025) * radialFactor
                : 1.7866666666666666 + _random.NextDouble() * 0.025);
            var petalJitter = 1.0;

            _particles.Add(new Particle
            {
                X = x,
                Y = y,
                BurstX = x,
                BurstY = y,
                Kind = _currentBurstKind,
                Z = 0,
                Vx = dirX * speed * petalJitter,
                Vy = dirY * speed * petalJitter,
                Vz = zDirection * speed * (isBotan ? 0.98 : isKamuro ? 0.8 : 0.92),
                Life = 1,
                InitialLife = 1,
                Decay = isKamuro
                    ? 0.24 + _random.NextDouble() * 0.08
                    : isBotan
                    ? 0.64 + _random.NextDouble() * 0.14
                    : 0.57 + _random.NextDouble() * 0.21,
                Size = isKamuro
                    ? 3.2 + _random.NextDouble() * 1.8
                    : isBotan
                    ? 3.0 + _random.NextDouble() * 1.5
                    : 2.4 + _random.NextDouble() * 1.5,
                StartColor = _currentPalette.Shell,
                EndColor = _currentPalette.Outer,
                TrailStrength = isKamuro ? 7.6 : isBotan ? 0.8 : 1.45,
                Drag = isKamuro
                    ? 0.974 + _random.NextDouble() * 0.008
                    : isBotan
                    ? 0.958 + _random.NextDouble() * 0.008
                    : 0.968 + _random.NextDouble() * 0.008,
                FlickerPhase = _random.NextDouble() * Math.PI * 2,
                Twinkle = isKamuro ? i % 7 == 0 : isBotan ? i % 14 == 0 : i % 9 == 0
            });
        }
    }

    private void UpdateParticles()
    {
        const double dt = 0.016;
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            var age = 1 - (p.Life / p.InitialLife);
            p.Vy += (82 + age * 20) * dt;
            p.Vx *= p.Drag;
            p.Vy *= p.Drag;
            p.Vz *= p.Drag;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Z = Math.Clamp(p.Z + p.Vz * dt, -MaxDepthOffset, MaxDepthOffset);
            p.Life -= dt * p.Decay;

            var color = LerpColor(p.StartColor, p.EndColor, Math.Clamp(age * 1.08, 0, 1));
            var trailLife = p.Kind == BurstKind.KamuroGiku
                ? p.Life * (0.3 + p.TrailStrength * 1.05)
                : p.Life * (0.3 + p.TrailStrength * 0.2);
            var trailSize = p.Kind == BurstKind.KamuroGiku
                ? p.Size * (0.30 + p.TrailStrength * 0.032)
                : p.Size * (0.92 + p.TrailStrength * 0.14);
            _trails.Add(new TrailParticle(
                p.X,
                p.Y,
                p.Z,
                p.BurstX,
                p.BurstY,
                trailLife,
                trailSize,
                WithAlpha(color, 168)));

            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
            }
        }

        for (var i = _trails.Count - 1; i >= 0; i--)
        {
            var t = _trails[i];
            t.Life -= dt * 1.2;
            t.Size *= 0.992;
            if (t.Life <= 0 || t.Size <= 0.8)
            {
                _trails.RemoveAt(i);
            }
        }
    }

    private void RenderFrame()
    {
        _renderTrails.Clear();
        foreach (var t in _trails)
        {
            var perspective = GetPerspectiveScale(t.Z);
            var projectedX = t.BurstX + ((t.X - t.BurstX) * perspective);
            var projectedY = t.BurstY + ((t.Y - t.BurstY) * perspective);
            _renderTrails.Add(new RenderTrail(projectedX, projectedY, t.Size, t.Color, Math.Clamp(t.Life, 0, 1)));
        }

        _renderParticles.Clear();
        foreach (var p in _particles)
        {
            var age = 1 - (p.Life / p.InitialLife);
            var color = LerpColor(p.StartColor, p.EndColor, Math.Clamp(age * 1.06, 0, 1));
            var shimmer = p.Twinkle ? 0.86 + 0.14 * Math.Sin((_started.Ticks / (double)TimeSpan.TicksPerSecond * 7) + p.FlickerPhase + age * 18) : 1;
            var perspective = GetPerspectiveScale(p.Z);
            var projectedX = p.BurstX + ((p.X - p.BurstX) * perspective);
            var projectedY = p.BurstY + ((p.Y - p.BurstY) * perspective);
            var botanBloom = p.Kind == BurstKind.Botan ? GetBotanBloomFactor(age) : 1;
            var kamuroGlow = p.Kind == BurstKind.KamuroGiku ? GetKamuroGlowFactor(age) : 1;
            var centerFade = p.Kind == BurstKind.KamuroGiku ? GetKamuroCenterFadeFactor(p, age) : 1;
            var softFade = p.Kind == BurstKind.KamuroGiku ? GetKamuroSoftFade(age) : 1;
            var lifeFactor = p.Kind == BurstKind.KamuroGiku
                ? Math.Clamp((p.Life * 0.7) + (centerFade * 0.3), 0, 1)
                : p.Life;
            _renderParticles.Add(new RenderParticle(
                projectedX,
                projectedY,
                p.Size * (2.25 - age * 0.2) * kamuroGlow,
                p.Size * (0.92 - age * 0.05) * (0.92 + kamuroGlow * 0.08),
                color,
                LerpColor(p.StartColor, p.EndColor, 0.35 + age * 0.25),
                lifeFactor * 0.11 * shimmer * botanBloom * kamuroGlow * centerFade * softFade,
                Math.Clamp(lifeFactor * 0.68 * shimmer * botanBloom * kamuroGlow * centerFade * softFade, 0, 1)));
        }

        _scene.UpdateScene(
            _renderTrails,
            _renderParticles,
            _rocket is null
                ? null
                : new RenderRocket(_rocket.X, _rocket.Y, _rocket.OriginX, _rocket.OriginY, _rocket.TrailColor, _rocket.FuseHidden));
    }

    private void ApplyVirtualScreenBounds()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private double GetLaunchY(WpfPoint screenPoint)
    {
        var screen = FormsScreen.FromPoint(new((int)Math.Round(screenPoint.X), (int)Math.Round(screenPoint.Y)));
        return screen.WorkingArea.Bottom - Top - 8;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ApplyVirtualScreenBounds();

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }

    private BurstPalette PickBurstPalette() => _burstPalettes[_random.Next(_burstPalettes.Length)];
    private BurstPalette PickKamuroPalette() => _kamuroPalettes[_random.Next(_kamuroPalettes.Length)];

    private BurstKind PickBurstKind() => (BurstKind)_random.Next(3);

    private static WpfColor LerpColor(WpfColor from, WpfColor to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return WpfColor.FromArgb(
            (byte)Math.Round(from.A + (to.A - from.A) * t),
            (byte)Math.Round(from.R + (to.R - from.R) * t),
            (byte)Math.Round(from.G + (to.G - from.G) * t),
            (byte)Math.Round(from.B + (to.B - from.B) * t));
    }

    private static WpfColor WithAlpha(WpfColor color, byte alpha) => WpfColor.FromArgb(alpha, color.R, color.G, color.B);

    private static double GetPerspectiveScale(double z)
    {
        var clampedZ = Math.Clamp(z, -MaxDepthOffset, MaxDepthOffset);
        var denominator = PerspectiveDistance - clampedZ;
        return Math.Clamp(PerspectiveDistance / denominator, 0.72, 1.45);
    }

    private static double CalculateRocketLaunchVelocity(double travel)
    {
        const double averageGravity = 1000;
        return -Math.Sqrt(2 * averageGravity * travel);
    }

    private static double GetBotanBloomFactor(double age)
    {
        if (age <= 0.35)
        {
            return 0.24;
        }

        if (age >= 0.72)
        {
            return 1.18;
        }

        var t = (age - 0.35) / 0.37;
        return 0.24 + (0.94 * t * t * (3 - (2 * t)));
    }

    private static double GetKamuroGlowFactor(double age)
    {
        if (age <= 0.22)
        {
            return 0.84;
        }

        if (age >= 0.78)
        {
            return 1.12;
        }

        var t = (age - 0.22) / 0.56;
        return 0.84 + (0.28 * t * t * (3 - (2 * t)));
    }

    private static double GetKamuroCenterFadeFactor(Particle p, double age)
    {
        var dx = p.X - p.BurstX;
        var dy = p.Y - p.BurstY;
        var radial = Math.Sqrt((dx * dx) + (dy * dy) + (p.Z * p.Z));
        var radialNorm = Math.Clamp(radial / 220, 0, 1);

        // Center-out fade: center starts fading first, outer stars fade later.
        var startAge = 0.12 + (radialNorm * 0.20);
        var endAge = startAge + 0.18;
        var fadeT = Math.Clamp((age - startAge) / Math.Max(endAge - startAge, 0.001), 0, 1);
        var smooth = fadeT * fadeT * (3 - (2 * fadeT));
        var fade = 1 - smooth;
        var floor = radialNorm * 0.22;
        return Math.Clamp(Math.Max(fade, floor), 0, 1);
    }

    private static double GetKamuroSoftFade(double age)
    {
        if (age <= 0.62)
        {
            return 1;
        }

        var t = Math.Clamp((age - 0.62) / 0.38, 0, 1);
        var smooth = t * t * (3 - (2 * t));
        return 1 - (smooth * 0.78);
    }





    private sealed record BurstPalette(string Name, WpfColor Outer, WpfColor Shell);

    private enum BurstKind
    {
        Chrysanthemum,
        Botan,
        KamuroGiku
    }

    private sealed class Rocket
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double ApexX { get; set; }
        public double ApexY { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double SwayPhase { get; set; }
        public double BurstDelay { get; set; }
        public bool FuseHidden { get; set; }
        public bool FuseStarted { get; set; }
        public WpfColor TrailColor { get; set; }
    }

    private sealed class Particle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double BurstX { get; set; }
        public double BurstY { get; set; }
        public BurstKind Kind { get; set; }
        public double Z { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Vz { get; set; }
        public double Life { get; set; }
        public double InitialLife { get; set; }
        public double Decay { get; set; }
        public double Size { get; set; }
        public WpfColor StartColor { get; set; }
        public WpfColor EndColor { get; set; }
        public double TrailStrength { get; set; }
        public double Drag { get; set; }
        public double FlickerPhase { get; set; }
        public bool Twinkle { get; set; }
    }

    private sealed class TrailParticle(double x, double y, double z, double burstX, double burstY, double life, double size, WpfColor color)
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Z { get; set; } = z;
        public double BurstX { get; set; } = burstX;
        public double BurstY { get; set; } = burstY;
        public double Life { get; set; } = life;
        public double Size { get; set; } = size;
        public WpfColor Color { get; set; } = color;
    }
}
