namespace MultiRoomAudio.Audio;

/// <summary>
/// Resampler quality presets affecting filter complexity and CPU usage.
/// </summary>
public enum ResamplerQuality
{
    /// <summary>
    /// 128 phases, 48 taps. Best quality, highest CPU/memory (~48KB filter bank).
    /// </summary>
    HighestQuality,

    /// <summary>
    /// 64 phases, 32 taps. Good balance of quality and performance (~16KB filter bank). [DEFAULT]
    /// </summary>
    MediumQuality,

    /// <summary>
    /// 32 phases, 24 taps. Lower resource usage for constrained devices (~6KB filter bank).
    /// </summary>
    LowResource
}

/// <summary>
/// Extension methods for <see cref="ResamplerQuality"/>.
/// </summary>
public static class ResamplerQualityExtensions
{
    /// <summary>
    /// Gets the filter parameters (phases, taps) for the specified quality preset.
    /// </summary>
    /// <param name="quality">The quality preset.</param>
    /// <returns>A tuple of (phases, taps) for filter bank design.</returns>
    public static (int phases, int taps) GetParameters(this ResamplerQuality quality) => quality switch
    {
        ResamplerQuality.HighestQuality => (128, 48),
        ResamplerQuality.MediumQuality => (64, 32),
        ResamplerQuality.LowResource => (32, 24),
        _ => (64, 32)
    };
}
