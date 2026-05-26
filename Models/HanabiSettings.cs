namespace CtrlHanabi.Models;

public sealed class HanabiSettings
{
    public int DoubleTapThresholdMs { get; init; } = 320;
    public int CooldownMs { get; init; } = 500;
    public int ParticleCount { get; init; } = 90;
    public int DurationMs { get; init; } = 1200;
    public double ExplosionRadius { get; init; } = 110;
    public double RocketLaunchHeight { get; init; } = 260;

    public static HanabiSettings Default => new();
}
