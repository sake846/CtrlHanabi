namespace CtrlHanabi.Models;

public sealed class HanabiSettings
{
    private const int DefaultDoubleTapThresholdMs = 320;
    private const int DefaultCooldownMs = 500;
    private const int DefaultParticleCount = 90;
    private const double DefaultExplosionRadius = 110;

    public int DoubleTapThresholdMs { get; init; } = DefaultDoubleTapThresholdMs;
    public int CooldownMs { get; init; } = DefaultCooldownMs;
    public int ParticleCount { get; init; } = DefaultParticleCount;
    public double ExplosionRadius { get; init; } = DefaultExplosionRadius;

    public static HanabiSettings Default => new();
}
