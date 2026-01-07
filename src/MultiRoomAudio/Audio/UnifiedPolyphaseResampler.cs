using Microsoft.Extensions.Logging;
using Sendspin.SDK.Audio;
using Sendspin.SDK.Models;

namespace MultiRoomAudio.Audio;

/// <summary>
/// Unified polyphase resampler that handles both static rate conversion and dynamic sync adjustment.
/// Eliminates warbling artifacts by performing high-quality resampling in a single pass.
/// </summary>
/// <remarks>
/// This resampler combines the functionality of SampleRateConverter and ResamplingAudioSampleSource
/// into a single component. It uses a polyphase filter bank with Kaiser-windowed sinc interpolation
/// and supports dynamic playback rate adjustment for sync correction.
///
/// Key features:
/// - Configurable quality presets (HighestQuality, MediumQuality, LowResource)
/// - Runtime quality switching (rebuilds filter bank on next Read)
/// - Fractional phase interpolation for arbitrary conversion ratios
/// - Dynamic playback rate adjustment via ITimedAudioBuffer.TargetPlaybackRateChanged
/// - Thread-safe rate updates (atomic double, no locks needed)
/// - Same-rate passthrough optimization
///
/// The effective conversion ratio is: (outputRate / inputRate) * playbackRate
/// where playbackRate is typically 0.96-1.04 for sync correction.
/// </remarks>
public sealed class UnifiedPolyphaseResampler : IAudioSampleSource, IDisposable
{
    private readonly IAudioSampleSource _source;
    private readonly ITimedAudioBuffer? _buffer;
    private readonly ILogger? _logger;
    private readonly int _inputRate;
    private readonly int _outputRate;
    private readonly int _channels;
    private readonly double _baseRatio;
    private readonly AudioFormat _outputFormat;

    // Quality settings
    private ResamplerQuality _quality;
    private int _polyphaseCount;
    private int _filterTaps;
    private double[][] _polyphaseFilters;
    private bool _filterBankDirty;

    // Playback rate (1.0 = normal, clamped to 0.96-1.04)
    // Thread safety note: This field is written by the event handler thread (OnTargetPlaybackRateChanged)
    // and read by the audio callback thread (Read). On x64/ARM64 (our target platforms),
    // aligned 64-bit reads/writes are atomic at the hardware level. The bounded range [0.96, 1.04]
    // ensures any transient inconsistency is benign.
    private double _playbackRate = 1.0;
    private const double MinRate = 0.96;
    private const double MaxRate = 1.04;

    // Resampling state
    private float[] _inputBuffer;
    private float[] _historyBuffer;
    private int _historyLength;
    private double _inputPosition; // Current position in input (fractional)

    // Diagnostic tracking
    private DateTime _lastRateLogTime = DateTime.MinValue;
    private int _rateChangeCount;
    private double _lastLoggedRate = 1.0;

    // Constants
    private const double KaiserBeta = 6.0;

    // Disposal tracking
    private bool _disposed;

    /// <inheritdoc/>
    public AudioFormat Format => _outputFormat;

    /// <summary>
    /// Gets the input sample rate.
    /// </summary>
    public int InputRate => _inputRate;

    /// <summary>
    /// Gets the output sample rate.
    /// </summary>
    public int OutputRate => _outputRate;

    /// <summary>
    /// Gets or sets the current playback rate. Values are clamped to [0.96, 1.04] range.
    /// </summary>
    public double PlaybackRate
    {
        get => _playbackRate;
        set => _playbackRate = Math.Clamp(value, MinRate, MaxRate);
    }

    /// <summary>
    /// Gets or sets the resampler quality preset. Changing this will rebuild the filter bank on next Read.
    /// </summary>
    public ResamplerQuality Quality
    {
        get => _quality;
        set
        {
            if (_quality != value)
            {
                _quality = value;
                _filterBankDirty = true;
                _logger?.LogInformation("Resampler quality changed to {Quality}, filter bank will rebuild", value);
            }
        }
    }

    /// <summary>
    /// Creates a new unified polyphase resampler.
    /// </summary>
    /// <param name="source">Source audio sample source.</param>
    /// <param name="inputRate">Input sample rate in Hz.</param>
    /// <param name="outputRate">Output sample rate in Hz.</param>
    /// <param name="buffer">Optional timed buffer to subscribe to rate changes for sync correction.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="quality">Quality preset (default: MediumQuality).</param>
    public UnifiedPolyphaseResampler(
        IAudioSampleSource source,
        int inputRate,
        int outputRate,
        ITimedAudioBuffer? buffer = null,
        ILogger? logger = null,
        ResamplerQuality quality = ResamplerQuality.MediumQuality)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _buffer = buffer;
        _logger = logger;
        _inputRate = inputRate;
        _outputRate = outputRate;
        _channels = source.Format.Channels;
        _baseRatio = (double)outputRate / inputRate;
        _quality = quality;

        // Create output format with new sample rate
        _outputFormat = new AudioFormat
        {
            SampleRate = outputRate,
            Channels = source.Format.Channels,
            Codec = source.Format.Codec
        };

        // Initialize filter bank
        (_polyphaseCount, _filterTaps) = quality.GetParameters();
        _polyphaseFilters = BuildPolyphaseBank();

        // Allocate buffers
        var maxInputNeeded = (int)Math.Ceiling(8192 / _baseRatio) + _filterTaps * 2;
        _inputBuffer = new float[maxInputNeeded * _channels];
        _historyBuffer = new float[_filterTaps * _channels];
        _historyLength = 0;
        _inputPosition = 0;

        // Subscribe to rate changes from the SDK's sync correction
        if (_buffer != null)
        {
            _buffer.TargetPlaybackRateChanged += OnTargetPlaybackRateChanged;
        }

        _logger?.LogInformation(
            "Unified polyphase resampler: {InputRate}Hz -> {OutputRate}Hz ({Ratio:F2}x), quality={Quality} ({Phases} phases, {Taps} taps)",
            inputRate, outputRate, _baseRatio, quality, _polyphaseCount, _filterTaps);
    }

    private void OnTargetPlaybackRateChanged(double newRate)
    {
        PlaybackRate = newRate;
        _rateChangeCount++;

        var now = DateTime.UtcNow;
        var timeSinceLastLog = now - _lastRateLogTime;

        // Log significant rate changes or periodic summary
        var rateDelta = Math.Abs(newRate - _lastLoggedRate);
        var isSignificantChange = rateDelta > 0.001; // >0.1% change

        if (_logger != null && (isSignificantChange || timeSinceLastLog.TotalSeconds >= 5))
        {
            var ratePercent = (newRate - 1.0) * 100;
            var effectiveRatio = _baseRatio * newRate;

            _logger.LogDebug(
                "Resampler: rate={Rate:F4} ({Percent:+0.00;-0.00}%), effectiveRatio={EffectiveRatio:F4}, changes={Count}",
                newRate, ratePercent, effectiveRatio, _rateChangeCount);

            _lastRateLogTime = now;
            _lastLoggedRate = newRate;
        }
    }

    /// <inheritdoc/>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed)
            return 0;

        // Rebuild filter bank if quality changed
        if (_filterBankDirty)
        {
            RebuildFilterBank();
        }

        // Same-rate passthrough optimization (when inputRate == outputRate and rate == 1.0)
        if (_inputRate == _outputRate && Math.Abs(_playbackRate - 1.0) < 0.0001)
        {
            return _source.Read(buffer, offset, count);
        }

        return ReadWithResampling(buffer, offset, count);
    }

    private int ReadWithResampling(float[] buffer, int offset, int count)
    {
        var outputFrames = count / _channels;
        var outputSamplesWritten = 0;

        // Calculate effective ratio including playback rate adjustment
        var effectiveRatio = _baseRatio * _playbackRate;
        var inputStep = 1.0 / effectiveRatio;

        // Calculate how many input frames we need
        var inputFramesNeeded = (int)Math.Ceiling(outputFrames * inputStep) + _filterTaps + 1;

        // Read input samples
        var inputSamples = inputFramesNeeded * _channels;
        if (inputSamples > _inputBuffer.Length)
            inputSamples = _inputBuffer.Length;

        var samplesRead = _source.Read(_inputBuffer, 0, inputSamples);
        var inputFramesRead = samplesRead / _channels;

        if (inputFramesRead == 0)
        {
            // Fill with silence
            Array.Fill(buffer, 0f, offset, count);
            return count;
        }

        var halfTaps = _filterTaps / 2;
        var outputIdx = offset;

        // Process each output frame
        while (outputSamplesWritten < outputFrames * _channels && _inputPosition < inputFramesRead - halfTaps)
        {
            var intPos = (int)_inputPosition;
            var frac = _inputPosition - intPos;

            // Calculate which polyphase filters to use
            var fractionalPhase = frac * _polyphaseCount;
            var phase0 = (int)fractionalPhase;
            var phase1 = (phase0 + 1) % _polyphaseCount;
            var phaseFrac = fractionalPhase - phase0;

            // Get the two adjacent polyphase filters
            var filter0 = _polyphaseFilters[phase0];
            var filter1 = _polyphaseFilters[phase1];
            var filterLen = filter0.Length;

            // Process each channel
            for (int ch = 0; ch < _channels; ch++)
            {
                double sum0 = 0;
                double sum1 = 0;

                // Convolve with both filters
                for (int t = 0; t < filterLen; t++)
                {
                    var idx = intPos - halfTaps + t;
                    float sample;

                    if (idx >= 0 && idx < inputFramesRead)
                    {
                        sample = _inputBuffer[idx * _channels + ch];
                    }
                    else if (idx < 0 && _historyLength > 0)
                    {
                        var histIdx = _historyLength + idx;
                        sample = histIdx >= 0 ? _historyBuffer[histIdx * _channels + ch] : 0f;
                    }
                    else
                    {
                        sample = 0f;
                    }

                    sum0 += sample * filter0[t];
                    sum1 += sample * filter1[t];
                }

                // Interpolate between filter outputs
                var result = sum0 + (sum1 - sum0) * phaseFrac;
                buffer[outputIdx + ch] = (float)result;
            }

            outputIdx += _channels;
            outputSamplesWritten += _channels;

            // Advance input position
            _inputPosition += inputStep;
        }

        // Update history for next call
        UpdateHistory(inputFramesRead);

        // Wrap input position
        _inputPosition -= inputFramesRead;
        if (_inputPosition < 0)
            _inputPosition = 0;

        // Fill remaining with silence if needed
        if (outputSamplesWritten < count)
        {
            Array.Fill(buffer, 0f, offset + outputSamplesWritten, count - outputSamplesWritten);
        }

        return count;
    }

    private void UpdateHistory(int inputFramesRead)
    {
        var historyFrames = Math.Min(_filterTaps, inputFramesRead);
        var startFrame = inputFramesRead - historyFrames;

        // Ensure history buffer is large enough
        var neededSize = historyFrames * _channels;
        if (_historyBuffer.Length < neededSize)
        {
            _historyBuffer = new float[neededSize];
        }

        Array.Copy(_inputBuffer, startFrame * _channels, _historyBuffer, 0, historyFrames * _channels);
        _historyLength = historyFrames;
    }

    private void RebuildFilterBank()
    {
        (_polyphaseCount, _filterTaps) = _quality.GetParameters();
        _polyphaseFilters = BuildPolyphaseBank();
        _filterBankDirty = false;

        // Resize history buffer for new tap count
        _historyBuffer = new float[_filterTaps * _channels];
        _historyLength = 0;

        _logger?.LogInformation(
            "Filter bank rebuilt: {Phases} phases, {Taps} taps",
            _polyphaseCount, _filterTaps);
    }

    private double[][] BuildPolyphaseBank()
    {
        // Design the prototype lowpass filter
        var prototypeLength = _polyphaseCount * _filterTaps;
        var cutoff = Math.Min(1.0 / _baseRatio, 1.0);
        var prototype = DesignLowPassFilter(prototypeLength, cutoff);

        // Decompose into polyphase bank
        var bank = new double[_polyphaseCount][];

        for (int p = 0; p < _polyphaseCount; p++)
        {
            bank[p] = new double[_filterTaps];
            for (int t = 0; t < _filterTaps; t++)
            {
                var idx = t * _polyphaseCount + p;
                bank[p][t] = idx < prototypeLength ? prototype[idx] * _polyphaseCount : 0;
            }
        }

        return bank;
    }

    private static double[] DesignLowPassFilter(int taps, double cutoff)
    {
        var filter = new double[taps];
        var halfTaps = taps / 2;
        double sum = 0;

        for (int i = 0; i < taps; i++)
        {
            var x = i - halfTaps;
            var sinc = x == 0 ? cutoff : Math.Sin(Math.PI * cutoff * x) / (Math.PI * x);
            var window = Kaiser(i, taps, KaiserBeta);
            filter[i] = sinc * window;
            sum += filter[i];
        }

        // Normalize
        if (sum > 0)
        {
            for (int i = 0; i < taps; i++)
            {
                filter[i] /= sum;
            }
        }

        return filter;
    }

    private static double Kaiser(int n, int length, double beta)
    {
        var alpha = (length - 1) / 2.0;
        var x = (n - alpha) / alpha;
        var xSquared = x * x;

        // Clamp to avoid negative values due to floating point errors
        if (xSquared >= 1.0)
            return 0.0;

        var arg = beta * Math.Sqrt(1.0 - xSquared);
        return BesselI0(arg) / BesselI0(beta);
    }

    private static double BesselI0(double x)
    {
        double sum = 1;
        double term = 1;
        var halfX = x / 2;

        for (int k = 1; k < 25; k++)
        {
            term *= (halfX / k) * (halfX / k);
            sum += term;
            if (term < 1e-10 * sum)
                break;
        }

        return sum;
    }

    /// <summary>
    /// Disposes resources and unsubscribes from events to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from rate change events to prevent memory leak
        if (_buffer != null)
        {
            _buffer.TargetPlaybackRateChanged -= OnTargetPlaybackRateChanged;
        }

        // Dispose source if it's disposable
        if (_source is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
