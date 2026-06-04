namespace CtrlHanabi.Models;

public sealed class HanabiSettings
{
    private const int DefaultDoubleTapThresholdMs = 320;
    private const int DefaultCooldownMs = 500;
    private const int DefaultParticleCount = 90;
    private const double DefaultExplosionRadius = 110;
    private const int DefaultStarmineDisplayIndex = 1;

    public int DoubleTapThresholdMs { get; init; } = DefaultDoubleTapThresholdMs;
    public int CooldownMs { get; init; } = DefaultCooldownMs;
    public int ParticleCount { get; init; } = DefaultParticleCount;
    public double ExplosionRadius { get; init; } = DefaultExplosionRadius;
    public bool HourlyStarmineEnabled { get; init; }
    public bool StarmineLaneLeftEnabled { get; init; } = true;
    public bool StarmineLaneCenterEnabled { get; init; } = true;
    public bool StarmineLaneRightEnabled { get; init; } = true;
    public int StarmineDisplayIndex { get; init; } = DefaultStarmineDisplayIndex;
    public string? UiLanguage { get; init; }

    public static HanabiSettings Default => new();
}
