using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using FormsScreen = System.Windows.Forms.Screen;
using CtrlHanabi.Models;
using CtrlHanabi.Services;
using CtrlHanabi.ViewModels;
using Microsoft.Win32;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace CtrlHanabi;

public partial class FireworkOverlayWindow : Window
{
    private const double FrameDeltaSeconds = 0.016;
    private const double PerspectiveDistance = 720;
    private const double MaxDepthOffset = 280;
    private const double FuseDelaySeconds = 0.11;
    private const double FuseDarkSeconds = 0.055;
    private const double RocketBaseGravity = 260;
    private const double RocketProgressGravity = 320;
    private const double RocketLaunchAverageGravity = 520;
    private const double RocketLaunchVelocityScale = 0.90;
    private const double RocketMaxHorizontalAccel = 16;
    private const int MinimumBurstPetalCount = 168;
    private const int LaunchBlastParticleCount = 34;
    private const int KobanaPetalCount = 12;
    private const int MaximumKobanaBurstCount = 3;
    private const int KobanaCapacityParticleCount = 36;
    private const int EstimatedRocketTrailCapacity = 80;
    private const int EstimatedBurstTrailFrames = 72;
    private const int MaxParticleTrailSegments = 4;
    private const int MaxRocketTrailSegments = 8;
    private static readonly double[] KobanaBurstProgressThresholds = [0.50, 0.68, 0.82];
    private const int StarmineMaxShots = 7;
    private const double StarmineIntervalJitterSeconds = 0.2;
    private const double LeafAscentEmitProgressStep = 0.064;
    private const int GroundLeafStarBurstCount = 8;
    private const double StarmineLaunchAngleJitter = 50;
    private const double SingleLaunchAngleJitter = 10;
    private const int MaxConcurrentRockets = 15;
    private const bool DisableGpuPhysicsDuringStarmine = false;

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private static readonly nint HwndTopmost = -1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly DispatcherTimer _timer;
    private readonly List<Particle> _particles = [];
    private readonly List<TrailParticle> _trails = [];
    private readonly List<RenderTrail> _renderTrails = [];
    private readonly List<RenderParticle> _renderParticles = [];
    private readonly List<RenderRocket> _renderRockets = [];
    private readonly GpuParticlePhysics _gpuParticlePhysics = new();
    private readonly Random _random = new();
    private readonly FireworkOverlayViewModel _viewModel;
    private readonly ParticleSceneElement _scene = new();
    private readonly BurstPalette[] _burstPalettes =
    [
        new("strontium-red", WpfColor.FromRgb(246, 88, 76), WpfColor.FromRgb(255, 236, 232)),
        new("lithium-crimson", WpfColor.FromRgb(236, 74, 98), WpfColor.FromRgb(255, 233, 242)),
        new("sodium-yellow", WpfColor.FromRgb(255, 212, 94), WpfColor.FromRgb(255, 248, 216)),
        new("calcium-orange", WpfColor.FromRgb(255, 162, 82), WpfColor.FromRgb(255, 239, 214)),
        new("barium-green", WpfColor.FromRgb(126, 214, 108), WpfColor.FromRgb(233, 250, 224)),
        new("copper-blue", WpfColor.FromRgb(92, 170, 255), WpfColor.FromRgb(229, 242, 255)),
        new("potassium-violet", WpfColor.FromRgb(170, 136, 255), WpfColor.FromRgb(241, 236, 255))
    ];
    private readonly BurstPalette[] _kamuroPalettes =
    [
        new("kamuro-iron-bright", WpfColor.FromRgb(255, 236, 188), WpfColor.FromRgb(255, 250, 228)),
        new("kamuro-iron-ember", WpfColor.FromRgb(255, 205, 132), WpfColor.FromRgb(255, 238, 184)),
        new("kamuro-iron-deep", WpfColor.FromRgb(255, 172, 96), WpfColor.FromRgb(255, 222, 160))
    ];

    private DateTime _started;
    private readonly List<Rocket> _activeRockets = [];
    private readonly Queue<LaunchRequest> _launchQueue = new();
    private double _launchDelay;
    private bool _isStarmineActive;
    private bool _useConfiguredDisplayBounds;

    public FireworkOverlayWindow(HanabiSettings settings, ISettingsService settingsService)
    {
        _viewModel = new FireworkOverlayViewModel(settings, settingsService);
        InitializeComponent();
        RootHost.Children.Add(_scene);

        SourceInitialized += OnSourceInitialized;
        _useConfiguredDisplayBounds = false;
        ApplyOverlayBounds();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => UpdateFrame();
        Hide();
    }

    public void ShowFirework(WpfPoint screenPoint, bool forceStarmine = false)
    {
        _viewModel.ReloadSettings();
        _useConfiguredDisplayBounds = forceStarmine;
        ApplyOverlayBounds();
        PrepareEffectStorage();
        ClearEffectStorage();
        QueueLaunchPattern(screenPoint, forceStarmine);
        _activeRockets.Clear();
        _launchDelay = 0;
        _started = DateTime.UtcNow;
        Show();
        BringOverlayToFrontWithoutActivation();
        _timer.Start();
    }


    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        var extendedStyle = GetWindowLongPtr(hwnd, GwlExStyle);
        SetWindowLongPtr(hwnd, GwlExStyle, extendedStyle | (nint)(WsExTransparent | WsExNoActivate));
    }

    private void BringOverlayToFrontWithoutActivation()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private void UpdateFrame()
    {
        try
        {
            UpdateFrameCore();
        }
        catch (Exception ex)
        {
            LogOverlayFailure("UpdateFrame failed: " + ex);
            _timer.Stop();
            _gpuParticlePhysics.Reset();
            Hide();
        }
    }

    private void UpdateFrameCore()
    {
        if (_isStarmineActive && _activeRockets.Count == 0 && _launchQueue.Count == 0)
        {
            _isStarmineActive = false;
        }

        if (_launchQueue.Count > 0)
        {
            _launchDelay -= FrameDeltaSeconds;
            while (_launchDelay <= 0 && _activeRockets.Count < MaxConcurrentRockets && _launchQueue.Count > 0)
            {
                SpawnNextLaunch();
            }
        }
        UpdateRockets();
        UpdateParticles();
        if (_activeRockets.Count == 0 && _launchQueue.Count == 0 && _particles.Count == 0 && _trails.Count == 0)
        {
            StopEffect();
            return;
        }

        RenderFrame();
    }

    private void UpdateRockets()
    {
        if (_activeRockets.Count == 0)
        {
            return;
        }

        for (var i = _activeRockets.Count - 1; i >= 0; i--)
        {
            var rocket = _activeRockets[i];
            var totalRise = Math.Max(rocket.OriginY - rocket.TargetY, 1);
            var progress = Math.Clamp((rocket.OriginY - rocket.Y) / totalRise, 0, 1);
            var gravity = RocketBaseGravity + (progress * RocketProgressGravity);
            rocket.Vy += gravity * FrameDeltaSeconds;
            rocket.Vx += rocket.HorizontalAccel * FrameDeltaSeconds;
            rocket.X += rocket.Vx * FrameDeltaSeconds;
            rocket.Y += rocket.Vy * FrameDeltaSeconds;
            EmitAscentEffect(rocket, progress);

            var startedFalling = rocket.Vy >= 0;
            var highEnough = rocket.Y <= (rocket.TargetY + 8);

            if (!rocket.FuseStarted && startedFalling && highEnough)
            {
                rocket.FuseStarted = true;
                rocket.ApexX = rocket.X;
                rocket.ApexY = rocket.Y;
            }

            if (rocket.FuseStarted)
            {
                rocket.BurstDelay += FrameDeltaSeconds;
                rocket.FuseHidden = true;
            }
            else
            {
                rocket.BurstDelay = 0;
                rocket.FuseHidden = false;
            }

            var shouldBurst = rocket.BurstDelay >= FuseDelaySeconds;
            if (!shouldBurst)
            {
                _activeRockets[i] = rocket;
                continue;
            }

            SpawnBurst(rocket);
            _activeRockets.RemoveAt(i);
        }
    }

    private void QueueLaunchPattern(WpfPoint screenPoint, bool forceStarmine)
    {
        _launchQueue.Clear();
        var launchPlan = _viewModel.BuildLaunchPlan(screenPoint, forceStarmine, Left, Top, Width, Height);
        _isStarmineActive = launchPlan.IsStarmineActive;
        foreach (var request in launchPlan.Requests)
        {
            _launchQueue.Enqueue(request);
        }
    }

    private void SpawnNextLaunch()
    {
        if (_launchQueue.Count == 0)
        {
            return;
        }

        var launch = _launchQueue.Dequeue();
        var launchY = GetLaunchY(new WpfPoint(launch.TargetX + Left, launch.TargetY + Top));
        var startX = launch.IsStarmine ? launch.TargetX : launch.TargetX + ((_random.NextDouble() - 0.5) * 36);
        var altitudeFactor = 1 - Math.Clamp(launch.TargetY / Math.Max(Height, 1), 0, 1);
        var jitterScale = 1.2 + (altitudeFactor * 2.0);
        var launchAngleJitter = launch.IsStarmine ? StarmineLaunchAngleJitter : SingleLaunchAngleJitter;
        var arcPull = ((launch.TargetX - startX) * 1.8) + ((_random.NextDouble() - 0.5) * launchAngleJitter * jitterScale);
        var travel = Math.Max(launchY - launch.TargetY, 120);
        var curveGuideType = PickCurveGuideType(launch.IsStarmine);
        var burstKind = PickBurstKind(allowKamuro: !launch.IsStarmine);
        var chrysanthemumCharcoalPalette = _kamuroPalettes[_random.Next(_kamuroPalettes.Length)];
        var silverDragonTone = PickSilverDragonTone();
        var burstPalette = burstKind == BurstKind.KamuroGiku
            ? PickKamuroPalette()
            : PickBurstPalette();

        _activeRockets.Add(new Rocket
        {
            X = startX,
            Y = launchY,
            OriginX = startX,
            OriginY = launchY,
            TargetX = launch.TargetX,
            TargetY = launch.TargetY,
            ApexX = launch.TargetX,
            ApexY = launch.TargetY,
            Vy = CalculateRocketLaunchVelocity(travel),
            Vx = arcPull,
            SwayPhase = _random.NextDouble() * Math.PI * 2,
            HorizontalAccel = (_random.NextDouble() - 0.5) * (RocketMaxHorizontalAccel * 2),
            BurstDelay = 0,
            FuseHidden = false,
            FuseStarted = false,
            TrailColor = burstPalette.Outer,
            CurveGuide = curveGuideType,
            LastTrailEmitProgress = 0,
            KobanaBurstCount = 0,
            PrevX = startX,
            PrevY = launchY,
            BurstKind = burstKind,
            BurstPalette = burstPalette,
            ChrysanthemumCharcoalPalette = chrysanthemumCharcoalPalette,
            SilverDragonTone = silverDragonTone,
            IsStarmine = launch.IsStarmine,
            BurstScale = launch.BurstScale
        });

        EmitLaunchBlast(startX, launchY);
        if (launch.IsStarmine)
        {
            EmitGroundLeafStars(startX, launchY);
        }
        if (launch.IsStarmine && _launchQueue.Count > 0)
        {
            var nextDelay = _launchQueue.Peek().DelaySeconds;
            _launchDelay = nextDelay;
        }
        else
        {
            _launchDelay = launch.DelaySeconds + (_random.NextDouble() * StarmineIntervalJitterSeconds);
        }
    }

    private void SpawnBurst(Rocket rocket)
    {
        var x = rocket.ApexX;
        var y = rocket.ApexY;
        var petalCount = Math.Max(_viewModel.Settings.ParticleCount * 2, MinimumBurstPetalCount);
        var outerRadius = _viewModel.Settings.ExplosionRadius * 1.18 * rocket.BurstScale;
        var isChrysanthemum = rocket.BurstKind == BurstKind.Chrysanthemum;
        var isBotan = rocket.BurstKind == BurstKind.Botan;
        var isKamuro = rocket.BurstKind == BurstKind.KamuroGiku;
        var chrysanthemumTransitionColor = PickWeightedBurstPalette().Outer;

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
                PrevX = x,
                PrevY = y,
                BurstX = x,
                BurstY = y,
                Kind = rocket.BurstKind,
                Z = 0,
                PrevZ = 0,
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
                StartColor = isChrysanthemum ? rocket.ChrysanthemumCharcoalPalette.Shell : rocket.BurstPalette.Shell,
                EndColor = isChrysanthemum ? rocket.ChrysanthemumCharcoalPalette.Outer : rocket.BurstPalette.Outer,
                TransitionColor = isChrysanthemum
                    ? chrysanthemumTransitionColor
                    : rocket.BurstPalette.Outer,
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
        var useGpuPhysics = !_isStarmineActive || !DisableGpuPhysicsDuringStarmine;
        if (!useGpuPhysics)
        {
            _gpuParticlePhysics.Reset();
        }

        var gpuIntegratedParticleCount = useGpuPhysics
            ? _gpuParticlePhysics.TryApplyPending(_particles)
            : 0;
        var particleCountBeforeUpdate = _particles.Count;
        var particleWriteIndex = _particles.Count - 1;
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            var age = 1 - (p.Life / p.InitialLife);
            if (i >= gpuIntegratedParticleCount)
            {
                p.PrevX = p.X;
                p.PrevY = p.Y;
                p.PrevZ = p.Z;
                p.Vy += (82 + age * 20) * FrameDeltaSeconds;
                p.Vx *= p.Drag;
                p.Vy *= p.Drag;
                p.Vz *= p.Drag;
                p.X += p.Vx * FrameDeltaSeconds;
                p.Y += p.Vy * FrameDeltaSeconds;
                p.Z = Math.Clamp(p.Z + p.Vz * FrameDeltaSeconds, -MaxDepthOffset, MaxDepthOffset);
                p.Life -= FrameDeltaSeconds * p.Decay;
            }

            if (p.Life > 0)
            {
                age = 1 - (p.Life / p.InitialLife);
                var color = GetParticleColor(p, age);
                p.Age = age;
                p.CurrentColor = color;
                var botanBloom = p.Kind == BurstKind.Botan ? GetBotanBloomFactor(age) : 1;
                var kamuroGlow = p.Kind == BurstKind.KamuroGiku ? GetKamuroGlowFactor(age) : 1;
                var centerFade = p.Kind == BurstKind.KamuroGiku ? GetKamuroCenterFadeFactor(p, age) : 1;
                var softFade = p.Kind == BurstKind.KamuroGiku ? GetKamuroSoftFade(age) : 1;
                var lifeFactor = p.Kind == BurstKind.KamuroGiku
                    ? Math.Clamp((p.Life * 0.7) + (centerFade * 0.3), 0, 1)
                    : p.Life;
                var fadeBase = lifeFactor * botanBloom * kamuroGlow * centerFade * softFade;
                p.CurrentCoreColor = LerpColor(color, p.TransitionColor, 0.3 + age * 0.2);
                p.RenderGlowSize = p.Size * (2.25 - age * 0.2) * kamuroGlow;
                p.RenderCoreSize = p.Size * (0.92 - age * 0.05) * (0.92 + kamuroGlow * 0.08);
                p.RenderGlowOpacityBase = fadeBase * 0.11;
                p.RenderCoreOpacityBase = fadeBase * 0.68;
                var trailLife = p.Kind == BurstKind.KamuroGiku
                    ? p.Life * (0.3 + p.TrailStrength * 1.05)
                    : p.Life * (0.3 + p.TrailStrength * 0.2);
                var trailSize = p.Kind == BurstKind.KamuroGiku
                    ? p.Size * (0.30 + p.TrailStrength * 0.032)
                    : p.Size * (0.92 + p.TrailStrength * 0.14);
                var dx = p.X - p.PrevX;
                var dy = p.Y - p.PrevY;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var segmentSpacing = Math.Max(1.35, trailSize * 0.55);
                var segments = Math.Clamp((int)Math.Ceiling(distance / segmentSpacing), 1, MaxParticleTrailSegments);
                var segmentStep = 1.0 / segments;
                var trailColor = WithAlpha(color, 168);
                for (var segment = 1; segment <= segments; segment++)
                {
                    var segmentT = segment * segmentStep;
                    _trails.Add(new TrailParticle(
                        p.PrevX + (dx * segmentT),
                        p.PrevY + (dy * segmentT),
                        p.PrevZ + ((p.Z - p.PrevZ) * segmentT),
                        p.BurstX,
                        p.BurstY,
                        trailLife * (0.9 + segmentT * 0.1),
                        trailSize,
                        trailColor));
                }

                _particles[particleWriteIndex--] = p;
            }
        }

        if (particleWriteIndex >= 0)
        {
            _particles.RemoveRange(0, particleWriteIndex + 1);
        }

        if (useGpuPhysics)
        {
            var canReuseGpuParticleBuffer = gpuIntegratedParticleCount == particleCountBeforeUpdate
                && _particles.Count == particleCountBeforeUpdate;
            _gpuParticlePhysics.ScheduleUpdate(_particles, FrameDeltaSeconds, MaxDepthOffset, canReuseGpuParticleBuffer);
        }

        var trailWriteIndex = 0;
        for (var i = 0; i < _trails.Count; i++)
        {
            var t = _trails[i];
            t.Life -= FrameDeltaSeconds * 1.2;
            t.Size *= 0.992;
            if (t.Life > 0 && t.Size > 0.8)
            {
                _trails[trailWriteIndex++] = t;
            }
        }

        if (trailWriteIndex < _trails.Count)
        {
            _trails.RemoveRange(trailWriteIndex, _trails.Count - trailWriteIndex);
        }
    }

    private void RenderFrame()
    {
        _renderTrails.Clear();
        _renderTrails.EnsureCapacity(_trails.Count);
        for (var i = 0; i < _trails.Count; i++)
        {
            var t = _trails[i];
            var perspective = GetPerspectiveScale(t.Z);
            var projectedX = t.BurstX + ((t.X - t.BurstX) * perspective);
            var projectedY = t.BurstY + ((t.Y - t.BurstY) * perspective);
            _renderTrails.Add(new RenderTrail(projectedX, projectedY, t.Size, t.Color, Math.Clamp(t.Life, 0, 1)));
        }

        _renderParticles.Clear();
        _renderParticles.EnsureCapacity(_particles.Count);
        var shimmerTime = _started.Ticks / (double)TimeSpan.TicksPerSecond * 7;
        for (var i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            var age = p.Age;
            var shimmer = p.Twinkle ? 0.86 + 0.14 * Math.Sin(shimmerTime + p.FlickerPhase + age * 18) : 1;
            var perspective = GetPerspectiveScale(p.Z);
            var projectedX = p.BurstX + ((p.X - p.BurstX) * perspective);
            var projectedY = p.BurstY + ((p.Y - p.BurstY) * perspective);
            _renderParticles.Add(new RenderParticle(
                projectedX,
                projectedY,
                p.RenderGlowSize,
                p.RenderCoreSize,
                p.CurrentColor,
                p.CurrentCoreColor,
                p.RenderGlowOpacityBase * shimmer,
                Math.Clamp(p.RenderCoreOpacityBase * shimmer, 0, 1)));
        }

        _renderRockets.Clear();
        _renderRockets.EnsureCapacity(_activeRockets.Count);
        for (var i = 0; i < _activeRockets.Count; i++)
        {
            var rocket = _activeRockets[i];
            _renderRockets.Add(new RenderRocket(rocket.X, rocket.Y, rocket.OriginX, rocket.OriginY, rocket.TrailColor, rocket.FuseHidden));
        }

        _scene.UpdateScene(
            _renderTrails,
            _renderParticles,
            _renderRockets);
    }

    private void StopEffect()
    {
        _isStarmineActive = false;
        _timer.Stop();
        _gpuParticlePhysics.Reset();
        _scene.ClearScene();
        Hide();
    }

    private void ApplyOverlayBounds()
    {
        if (_useConfiguredDisplayBounds)
        {
            ApplyConfiguredDisplayBounds();
            return;
        }

        ApplyVirtualScreenBounds();
    }

    private void ApplyVirtualScreenBounds()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void ApplyConfiguredDisplayBounds()
    {
        var screen = ResolveConfiguredScreen();
        Left = screen.Bounds.Left;
        Top = screen.Bounds.Top;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;
    }

    private FormsScreen ResolveConfiguredScreen()
    {
        var screens = FormsScreen.AllScreens;
        if (screens.Length == 0)
        {
            return FormsScreen.PrimaryScreen ?? throw new InvalidOperationException("No display is available.");
        }

        var configuredDisplayIndex = _viewModel.Settings.StarmineDisplayIndex;
        var orderedScreens = screens
            .OrderByDescending(screen => screen.Primary)
            .ThenBy(screen => screen.Bounds.Left)
            .ThenBy(screen => screen.Bounds.Top)
            .ToArray();

        var zeroBasedIndex = configuredDisplayIndex - 1;
        if (zeroBasedIndex < 0 || zeroBasedIndex >= orderedScreens.Length)
        {
            zeroBasedIndex = 0;
        }

        return orderedScreens[zeroBasedIndex];
    }

    private double GetLaunchY(WpfPoint screenPoint)
    {
        var screen = FormsScreen.FromPoint(new((int)Math.Round(screenPoint.X), (int)Math.Round(screenPoint.Y)));
        return screen.WorkingArea.Bottom - Top - 8;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _viewModel.ReloadSettings();
        ApplyOverlayBounds();
    }

    private static void LogOverlayFailure(string message)
    {
        RuntimeLogging.AppendD3D11Log(message);
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _gpuParticlePhysics.Dispose();
        base.OnClosed(e);
    }

    private BurstPalette PickBurstPalette() => PickWeightedBurstPalette();
    private BurstPalette PickKamuroPalette() => _kamuroPalettes[_random.Next(_kamuroPalettes.Length)];

    private BurstPalette PickWeightedBurstPalette()
    {
        // green/blue/violet: 20% each, others: 10% each
        var roll = _random.NextDouble();
        if (roll < 0.10) return _burstPalettes[0]; // red
        if (roll < 0.20) return _burstPalettes[1]; // crimson
        if (roll < 0.30) return _burstPalettes[2]; // yellow
        if (roll < 0.40) return _burstPalettes[3]; // orange
        if (roll < 0.60) return _burstPalettes[4]; // green
        if (roll < 0.80) return _burstPalettes[5]; // blue
        return _burstPalettes[6]; // violet
    }

    private BurstKind PickBurstKind(bool allowKamuro = true)
    {
        var roll = _random.NextDouble();
        if (!allowKamuro)
        {
            return roll < 0.5 ? BurstKind.Chrysanthemum : BurstKind.Botan;
        }

        if (roll < 0.45)
        {
            return BurstKind.Chrysanthemum;
        }

        if (roll < 0.90)
        {
            return BurstKind.Botan;
        }

        return BurstKind.KamuroGiku;
    }

    private void EmitAscentEffect(Rocket rocket, double progress)
    {
        if (rocket.CurveGuide == CurveGuideType.LeafSplay)
        {
            EmitLeafSplayTrail(rocket, progress);
            return;
        }

        if (rocket.CurveGuide == CurveGuideType.SilverDragon)
        {
            EmitSilverDragonTrail(rocket, progress);
            return;
        }

        if (rocket.CurveGuide == CurveGuideType.Kobana)
        {
            EmitKobanaBursts(rocket, progress);
        }
    }

    private void EmitLaunchBlast(double x, double y)
    {
        for (var i = 0; i < LaunchBlastParticleCount; i++)
        {
            var heat = _random.NextDouble();
            var lift = _random.NextDouble();
            var spread = 4 + lift * 15;
            var px = x + ((_random.NextDouble() - 0.5) * spread);
            var py = y - 7 - (lift * 30) + ((_random.NextDouble() - 0.5) * 5);
            var flame = LerpColor(
                WpfColor.FromRgb(255, 92, 28),
                WpfColor.FromRgb(255, 245, 174),
                heat);

            _trails.Add(new TrailParticle(
                px,
                py,
                0,
                px,
                py,
                0.22 + (_random.NextDouble() * 0.28),
                4.0 + ((1 - lift) * 7.5) + (_random.NextDouble() * 2.0),
                WithAlpha(flame, (byte)(170 + _random.Next(70)))));
        }
    }

    private void EmitSilverDragonTrail(Rocket rocket, double progress)
    {
        var heat = 1 - progress;
        var charcoal = rocket.SilverDragonTone == SilverDragonTone.Charcoal
            ? rocket.ChrysanthemumCharcoalPalette.Shell
            : WpfColor.FromRgb(196, 208, 220);
        var ember = rocket.SilverDragonTone == SilverDragonTone.Charcoal
            ? rocket.ChrysanthemumCharcoalPalette.Outer
            : WpfColor.FromRgb(244, 250, 255);
        var body = LerpColor(charcoal, ember, 0.42 + (heat * 0.44) + (_random.NextDouble() * 0.08));
        var dx = rocket.X - rocket.PrevX;
        var dy = rocket.Y - rocket.PrevY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var steps = Math.Clamp((int)Math.Ceiling(distance / 2.8), 1, MaxRocketTrailSegments);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (double)steps;
            var px = rocket.PrevX + (dx * t);
            var py = rocket.PrevY + (dy * t);
            _trails.Add(new TrailParticle(
                px,
                py,
                0,
                px,
                py,
                1.05 + _random.NextDouble() * 0.5,
                3.1 + _random.NextDouble() * 1.8,
                WithAlpha(body, 198)));
        }

        rocket.PrevX = rocket.X;
        rocket.PrevY = rocket.Y;
        rocket.LastTrailEmitProgress = progress;
    }

    private void EmitLeafSplayTrail(Rocket rocket, double progress)
    {
        if (rocket.IsStarmine)
        {
            return;
        }

        if ((progress - rocket.LastTrailEmitProgress) < LeafAscentEmitProgressStep)
        {
            return;
        }

        EmitGroundLeafStars(rocket.OriginX, rocket.OriginY);
        rocket.LastTrailEmitProgress = progress;
    }

    private void EmitGroundLeafStars(double originX, double originY)
    {
        var transitionColor = PickWeightedBurstPalette().Outer;
        var botanPalette = PickWeightedBurstPalette();

        for (var i = 0; i < GroundLeafStarBurstCount; i++)
        {
            var isBotanStar = _random.NextDouble() < 0.5;
            var kamuroPalette = _kamuroPalettes[_random.Next(_kamuroPalettes.Length)];
            var baseX = originX + ((_random.NextDouble() - 0.5) * 16);
            var baseY = originY - (_random.NextDouble() * 1.5);
            var yaw = _random.NextDouble() * Math.PI * 2;
            var lateralSpeed = 132 + (_random.NextDouble() * 56);
            var speedX = Math.Cos(yaw) * lateralSpeed;
            var speedZ = Math.Sin(yaw) * lateralSpeed;
            var speedY = -((176 + _random.NextDouble() * 6) * 5);

            _particles.Add(new Particle
            {
                X = baseX,
                Y = baseY,
                PrevX = baseX,
                PrevY = baseY,
                BurstX = baseX,
                BurstY = baseY,
                Kind = isBotanStar ? BurstKind.Botan : BurstKind.Chrysanthemum,
                Z = 0,
                PrevZ = 0,
                Vx = speedX,
                Vy = speedY,
                Vz = speedZ,
                Life = 1,
                InitialLife = 1,
                Decay = isBotanStar
                    ? 0.64 + _random.NextDouble() * 0.14
                    : 0.57 + _random.NextDouble() * 0.21,
                Size = isBotanStar
                    ? 3.0 + _random.NextDouble() * 1.5
                    : 2.4 + _random.NextDouble() * 1.5,
                StartColor = isBotanStar ? botanPalette.Shell : kamuroPalette.Shell,
                EndColor = isBotanStar ? botanPalette.Outer : kamuroPalette.Outer,
                TransitionColor = isBotanStar ? botanPalette.Outer : transitionColor,
                TrailStrength = isBotanStar ? 0.8 : 1.45,
                Drag = isBotanStar
                    ? 0.958 + _random.NextDouble() * 0.008
                    : 0.968 + _random.NextDouble() * 0.008,
                FlickerPhase = _random.NextDouble() * Math.PI * 2,
                Twinkle = true
            });
        }
    }

    private void EmitKobanaBursts(Rocket rocket, double progress)
    {
        if (rocket.KobanaBurstCount >= MaximumKobanaBurstCount)
        {
            return;
        }

        if (progress < KobanaBurstProgressThresholds[rocket.KobanaBurstCount])
        {
            return;
        }

        SpawnKobanaBurst(rocket.X, rocket.Y);
        rocket.KobanaBurstCount++;
    }

    private void SpawnKobanaBurst(double x, double y)
    {
        var color = _burstPalettes[_random.Next(_burstPalettes.Length)];
        for (var i = 0; i < KobanaPetalCount; i++)
        {
            var angle = (Math.PI * 2 * i) / KobanaPetalCount;
            var speed = 46 + _random.NextDouble() * 26;
            _particles.Add(new Particle
            {
                X = x,
                Y = y,
                BurstX = x,
                BurstY = y,
                Kind = BurstKind.Botan,
                Z = 0,
                Vx = Math.Cos(angle) * speed,
                Vy = Math.Sin(angle) * speed,
                Vz = (_random.NextDouble() - 0.5) * 15,
                Life = 1.0 + _random.NextDouble() * 0.25,
                InitialLife = 1.0,
                Decay = 0.9 + _random.NextDouble() * 0.25,
                Size = 1.5 + _random.NextDouble() * 0.9,
                StartColor = color.Shell,
                EndColor = color.Outer,
                TransitionColor = color.Outer,
                TrailStrength = 0.42,
                Drag = 0.935 + _random.NextDouble() * 0.02,
                FlickerPhase = _random.NextDouble() * Math.PI * 2,
                Twinkle = false
            });
        }
    }

    private CurveGuideType PickCurveGuideType(bool preferLeafSplay = false)
    {
        if (preferLeafSplay)
        {
            return CurveGuideType.LeafSplay;
        }

        var roll = _random.NextDouble();
        if (roll < 0.30)
        {
            return CurveGuideType.None;
        }

        if (roll < 0.90)
        {
            return CurveGuideType.SilverDragon;
        }

        return CurveGuideType.Kobana;
    }

    private SilverDragonTone PickSilverDragonTone() => _random.Next(2) == 0 ? SilverDragonTone.Charcoal : SilverDragonTone.Silver;

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

    private static WpfColor GetParticleColor(Particle particle, double age)
    {
        if (particle.Kind != BurstKind.Chrysanthemum)
        {
            return LerpColor(particle.StartColor, particle.EndColor, Math.Clamp(age * 1.08, 0, 1));
        }

        // Chrysanthemum: start with Fe-like spark color, then shift into 7-tone flame palette.
        const double switchAge = 0.25;
        if (age <= switchAge)
        {
            var earlyT = Math.Clamp(age / switchAge, 0, 1);
            return LerpColor(particle.StartColor, particle.EndColor, earlyT);
        }

        var transitionWindow = Math.Max((1 - switchAge) * 0.10, 0.001);
        var lateT = Math.Clamp((age - switchAge) / transitionWindow, 0, 1);
        return LerpColor(particle.EndColor, particle.TransitionColor, lateT);
    }

    private static double GetPerspectiveScale(double z)
    {
        var clampedZ = Math.Clamp(z, -MaxDepthOffset, MaxDepthOffset);
        var denominator = PerspectiveDistance - clampedZ;
        return Math.Clamp(PerspectiveDistance / denominator, 0.72, 1.45);
    }

    private static double CalculateRocketLaunchVelocity(double travel)
    {
        return -Math.Sqrt(2 * RocketLaunchAverageGravity * travel) * RocketLaunchVelocityScale;
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

    private void PrepareEffectStorage()
    {
        var starmineLaneCount = Math.Max(1, _viewModel.GetEnabledStarmineLaneCount());
        var starmineQueuedShots = 20 * starmineLaneCount;
        var capacityShots = Math.Max(StarmineMaxShots, starmineQueuedShots);
        var burstParticles = Math.Max(_viewModel.Settings.ParticleCount * 2, MinimumBurstPetalCount) * capacityShots;
        var burstTrailCount = burstParticles * EstimatedBurstTrailFrames;
        var totalParticleCapacity = burstParticles + (KobanaCapacityParticleCount * capacityShots);
        var totalTrailCapacity = (LaunchBlastParticleCount * capacityShots) + (EstimatedRocketTrailCapacity * capacityShots) + burstTrailCount;

        _particles.EnsureCapacity(totalParticleCapacity);
        _trails.EnsureCapacity(totalTrailCapacity);
        _renderParticles.EnsureCapacity(totalParticleCapacity);
        _renderTrails.EnsureCapacity(totalTrailCapacity);
        _renderRockets.EnsureCapacity(MaxConcurrentRockets);
    }


    private void ClearEffectStorage()
    {
        _gpuParticlePhysics.Reset();
        _particles.Clear();
        _trails.Clear();
        _renderParticles.Clear();
        _renderTrails.Clear();
        _renderRockets.Clear();
        _scene.ClearScene();
    }





    private sealed record BurstPalette(string Name, WpfColor Outer, WpfColor Shell);

    private enum BurstKind
    {
        Chrysanthemum,
        Botan,
        KamuroGiku
    }

    private enum CurveGuideType
    {
        None,
        LeafSplay,
        SilverDragon,
        Kobana
    }

    private enum SilverDragonTone
    {
        Charcoal,
        Silver
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
        public double HorizontalAccel { get; set; }
        public double SwayPhase { get; set; }
        public double BurstDelay { get; set; }
        public bool FuseHidden { get; set; }
        public bool FuseStarted { get; set; }
        public WpfColor TrailColor { get; set; }
        public CurveGuideType CurveGuide { get; set; }
        public double LastTrailEmitProgress { get; set; }
        public int KobanaBurstCount { get; set; }
        public double PrevX { get; set; }
        public double PrevY { get; set; }
        public BurstKind BurstKind { get; set; }
        public BurstPalette BurstPalette { get; set; } = null!;
        public BurstPalette ChrysanthemumCharcoalPalette { get; set; } = null!;
        public SilverDragonTone SilverDragonTone { get; set; }
        public bool IsStarmine { get; set; }
        public double BurstScale { get; set; }
    }

    private struct Particle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double PrevX { get; set; }
        public double PrevY { get; set; }
        public double BurstX { get; set; }
        public double BurstY { get; set; }
        public BurstKind Kind { get; set; }
        public double Z { get; set; }
        public double PrevZ { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Vz { get; set; }
        public double Life { get; set; }
        public double InitialLife { get; set; }
        public double Decay { get; set; }
        public double Size { get; set; }
        public WpfColor StartColor { get; set; }
        public WpfColor EndColor { get; set; }
        public WpfColor TransitionColor { get; set; }
        public WpfColor CurrentColor { get; set; }
        public WpfColor CurrentCoreColor { get; set; }
        public double Age { get; set; }
        public double RenderGlowSize { get; set; }
        public double RenderCoreSize { get; set; }
        public double RenderGlowOpacityBase { get; set; }
        public double RenderCoreOpacityBase { get; set; }
        public double TrailStrength { get; set; }
        public double Drag { get; set; }
        public double FlickerPhase { get; set; }
        public bool Twinkle { get; set; }
    }

    private struct TrailParticle(double x, double y, double z, double burstX, double burstY, double life, double size, WpfColor color)
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

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

}
