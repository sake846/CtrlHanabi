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
    private readonly SettingsService _settingsService = new();

    public HanabiSettings Settings { get; private set; }

    public FireworkOverlayViewModel(HanabiSettings settings)
    {
        Settings = settings;
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

    public LaunchPlan BuildLaunchPlan(WpfPoint screenPoint, bool forceStarmine, double left, double top, double width, double height)
    {
        var localX = screenPoint.X - left;
        var localY = screenPoint.Y - top;

        if (!forceStarmine)
        {
            return new LaunchPlan(false, [new LaunchRequest(localX, localY, 0, false)]);
        }

        var enabledLanes = GetEnabledStarmineLanes();
        if (enabledLanes.Count == 0)
        {
            return new LaunchPlan(false, []);
        }

        var requests = new List<LaunchRequest>(StarmineFixedShots * enabledLanes.Count);
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

            var laneOrder = new int[enabledLanes.Count];
            for (var lane = 0; lane < laneOrder.Length; lane++)
            {
                laneOrder[lane] = lane;
            }

            for (var lane = laneOrder.Length - 1; lane > 0; lane--)
            {
                var swapIndex = _random.Next(lane + 1);
                (laneOrder[lane], laneOrder[swapIndex]) = (laneOrder[swapIndex], laneOrder[lane]);
            }

            for (var orderIndex = 0; orderIndex < laneOrder.Length; orderIndex++)
            {
                var lane = enabledLanes[laneOrder[orderIndex]];
                var targetX = width * lane.XFraction;
                var delay = orderIndex == 0 ? waveDelay : _random.NextDouble() * StarmineIntervalJitterSeconds;
                requests.Add(new LaunchRequest(targetX, targetY, delay, true, burstScale));
            }
        }

        return new LaunchPlan(true, requests);
    }

    private List<StarmineLane> GetEnabledStarmineLanes()
    {
        var lanes = new List<StarmineLane>(3);
        if (Settings.StarmineLaneLeftEnabled) lanes.Add(new StarmineLane(StarmineLaunchXFractions[0]));
        if (Settings.StarmineLaneCenterEnabled) lanes.Add(new StarmineLane(StarmineLaunchXFractions[1]));
        if (Settings.StarmineLaneRightEnabled) lanes.Add(new StarmineLane(StarmineLaunchXFractions[2]));
        return lanes;
    }

    private readonly record struct StarmineLane(double XFraction);
}

internal readonly record struct LaunchRequest(double TargetX, double TargetY, double DelaySeconds, bool IsStarmine, double BurstScale = 1.0);
internal readonly record struct LaunchPlan(bool IsStarmineActive, IReadOnlyList<LaunchRequest> Requests);
