using CtrlHanabi.Models;
using CtrlHanabi.Services;
using WpfPoint = System.Windows.Point;

namespace CtrlHanabi.ViewModels;

internal sealed class FireworkOverlayViewModel
{
    private const int StarmineFixedShots = 20;
    private const double StarmineBaseIntervalSeconds = 0.2;
    private const double StarmineIntervalJitterSeconds = 0.2;
    private const double StarmineLastPairGapSeconds = 0.05;
    private static readonly double[] StarmineLaunchXFractions = [0.25, 0.5, 0.75];

    private readonly Random _random = new();
    private readonly ISettingsService _settingsService;

    public HanabiSettings Settings { get; private set; }

    public FireworkOverlayViewModel(HanabiSettings settings, ISettingsService settingsService)
    {
        Settings = settings;
        _settingsService = settingsService;
    }

    public void ReloadSettings()
    {
        Settings = _settingsService.Load();
    }

    public int GetEnabledStarmineLaneCount()
    {
        var count = 0;
        if (Settings.StarmineLaneLeftEnabled) count++;
        if (Settings.StarmineLaneCenterEnabled) count++;
        if (Settings.StarmineLaneRightEnabled) count++;
        return count;
    }

    public LaunchPlan BuildLaunchPlan(WpfPoint localPoint, bool forceStarmine, double width, double height)
    {
        if (!forceStarmine)
        {
            return new LaunchPlan(false, [new LaunchRequest(localPoint.X, localPoint.Y, 0, false)]);
        }

        Span<double> enabledLaneFractions = stackalloc double[3];
        var enabledLaneCount = 0;
        if (Settings.StarmineLaneLeftEnabled) enabledLaneFractions[enabledLaneCount++] = StarmineLaunchXFractions[0];
        if (Settings.StarmineLaneCenterEnabled) enabledLaneFractions[enabledLaneCount++] = StarmineLaunchXFractions[1];
        if (Settings.StarmineLaneRightEnabled) enabledLaneFractions[enabledLaneCount++] = StarmineLaunchXFractions[2];

        if (enabledLaneCount == 0)
        {
            return new LaunchPlan(false, []);
        }

        var requests = new List<LaunchRequest>(StarmineFixedShots * enabledLaneCount);
        Span<int> laneOrder = stackalloc int[3];
        for (var i = 0; i < StarmineFixedShots; i++)
        {
            var waveDelay = i switch
            {
                0 => 0,
                19 => StarmineLastPairGapSeconds,
                < 20 => 0.5,
                _ => StarmineBaseIntervalSeconds
            };
            var targetY = i < 8
                ? height * 0.50
                : i < 15
                    ? height * 0.40
                    : height * 0.20;
            var burstScale = i < 8
                ? 1.0
                : i < 15
                    ? 1.5
                    : 2.0;

            for (var lane = 0; lane < enabledLaneCount; lane++)
            {
                laneOrder[lane] = lane;
            }

            for (var lane = enabledLaneCount - 1; lane > 0; lane--)
            {
                var swapIndex = _random.Next(lane + 1);
                (laneOrder[lane], laneOrder[swapIndex]) = (laneOrder[swapIndex], laneOrder[lane]);
            }

            for (var orderIndex = 0; orderIndex < enabledLaneCount; orderIndex++)
            {
                var targetX = width * enabledLaneFractions[laneOrder[orderIndex]];
                var delay = orderIndex == 0 ? waveDelay : _random.NextDouble() * StarmineIntervalJitterSeconds;
                requests.Add(new LaunchRequest(targetX, targetY, delay, true, burstScale));
            }
        }

        return new LaunchPlan(true, requests);
    }
}

internal readonly record struct LaunchRequest(double TargetX, double TargetY, double DelaySeconds, bool IsStarmine, double BurstScale = 1.0);
internal readonly record struct LaunchPlan(bool IsStarmineActive, IReadOnlyList<LaunchRequest> Requests);
