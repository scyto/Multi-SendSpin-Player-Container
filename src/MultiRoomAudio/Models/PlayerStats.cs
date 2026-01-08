namespace MultiRoomAudio.Models;

/// <summary>
/// Complete stats response for a player (Stats for Nerds).
/// </summary>
public record PlayerStatsResponse(
    string PlayerName,
    AudioFormatStats AudioFormat,
    SyncStats Sync,
    BufferStatsInfo Buffer,
    ClockSyncStats ClockSync,
    ThroughputStats Throughput,
    SyncCorrectionStats Correction
);

/// <summary>
/// Audio format information for input and output.
/// </summary>
public record AudioFormatStats(
    string InputFormat,
    int InputSampleRate,
    int InputChannels,
    string? InputBitrate,
    string OutputFormat,
    int OutputSampleRate,
    int OutputChannels,
    int OutputBitDepth
);

/// <summary>
/// Sync status information.
/// </summary>
public record SyncStats(
    double SyncErrorMs,
    bool IsWithinTolerance,
    bool IsPlaybackActive
);

/// <summary>
/// Buffer level and underrun/overrun statistics.
/// </summary>
public record BufferStatsInfo(
    int BufferedMs,
    int TargetMs,
    long Underruns,
    long Overruns
);

/// <summary>
/// Clock synchronization details.
/// </summary>
public record ClockSyncStats(
    bool IsSynchronized,
    double ClockOffsetMs,
    double UncertaintyMs,
    double DriftRatePpm,
    bool IsDriftReliable,
    int MeasurementCount,
    int OutputLatencyMs,
    int StaticDelayMs
);

/// <summary>
/// Sample throughput counters.
/// </summary>
public record ThroughputStats(
    long SamplesWritten,
    long SamplesRead,
    long SamplesDroppedOverflow
);

/// <summary>
/// Sync correction statistics.
/// Uses frame drop/insert when sync error exceeds 5ms threshold.
/// </summary>
public record SyncCorrectionStats(
    string Mode,
    long FramesDropped,
    long FramesInserted,
    int ThresholdMs
);
