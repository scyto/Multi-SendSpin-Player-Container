using System.Runtime.InteropServices;
using Sendspin.SDK.Audio;
using Sendspin.SDK.Models;
using static MultiRoomAudio.Audio.PulseAudio.PulseAudioNative;

namespace MultiRoomAudio.Audio.PulseAudio;

/// <summary>
/// IAudioPlayer implementation using PulseAudio's async pa_stream API.
/// Uses callbacks for accurate real-time latency measurement.
/// </summary>
/// <remarks>
/// This player uses a threaded mainloop with write callbacks. PulseAudio calls
/// our write callback when it needs audio data, and we query the actual latency
/// at that moment for accurate sync correction.
/// </remarks>
public class PulseAudioPlayer : IAudioPlayer
{
    private readonly ILogger<PulseAudioPlayer> _logger;
    private readonly object _lock = new();

    // PulseAudio async API handles
    private IntPtr _mainloop = IntPtr.Zero;
    private IntPtr _context = IntPtr.Zero;
    private IntPtr _stream = IntPtr.Zero;

    // CRITICAL: Store callbacks as fields to prevent GC collection
    // If these get collected, PulseAudio will call garbage memory and crash
    private ContextNotifyCallback? _contextStateCallback;
    private StreamNotifyCallback? _streamStateCallback;
    private StreamRequestCallback? _writeCallback;
    private StreamNotifyCallback? _underflowCallback;

    private IAudioSampleSource? _sampleSource;
    private AudioFormat? _currentFormat;
    private string? _sinkName;
    private bool _disposed;

    private volatile bool _isPlaying;
    private volatile bool _isPaused;
    private volatile bool _contextReady;
    private volatile bool _streamReady;

    // Pre-allocated buffers for the write callback
    private float[]? _sampleBuffer;
    private byte[]? _byteBuffer;

    /// <summary>
    /// Target buffer size in milliseconds. PulseAudio will request ~this much audio.
    /// </summary>
    private const int BufferMs = 50;

    /// <summary>
    /// Initial latency estimate before real measurements are available.
    /// </summary>
    private const int InitialLatencyEstimateMs = 70;

    /// <summary>
    /// Frames to request per write. At 48kHz, 1024 frames = ~21ms.
    /// </summary>
    private const int FramesPerWrite = 1024;

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    private const int ConnectionTimeoutMs = 10000;

    /// <summary>
    /// Number of underflows before logging a warning.
    /// </summary>
    private const int UnderflowWarningThreshold = 5;

    private int _underflowCount;
    private ulong _lastMeasuredLatencyUs;

    public AudioPlayerState State { get; private set; } = AudioPlayerState.Uninitialized;

    private volatile float _volume = 1.0f;
    private volatile bool _isMuted;

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    /// <summary>
    /// Gets the output latency in milliseconds, measured from write callbacks.
    /// </summary>
    public int OutputLatencyMs { get; private set; }

    public event EventHandler<AudioPlayerState>? StateChanged;
    public event EventHandler<AudioPlayerError>? ErrorOccurred;

    /// <summary>
    /// Initializes a new instance of the PulseAudioPlayer.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="sinkName">
    /// Optional PulseAudio sink name. If null, uses the default sink.
    /// </param>
    public PulseAudioPlayer(ILogger<PulseAudioPlayer> logger, string? sinkName = null)
    {
        _logger = logger;
        _sinkName = sinkName;
    }

    public Task InitializeAsync(AudioFormat format, CancellationToken cancellationToken = default)
    {
        // Stop any existing playback first
        if (_isPlaying)
        {
            _logger.LogDebug("Stopping active playback before re-initialization");
            Stop();
        }

        lock (_lock)
        {
            try
            {
                // Clean up any existing resources
                CleanupResources();

                _logger.LogInformation(
                    "Initializing PulseAudio player: {SampleRate}Hz, {Channels}ch, FLOAT32, sink: {Sink}",
                    format.SampleRate, format.Channels, _sinkName ?? "default");

                // Create the threaded mainloop
                _mainloop = ThreadedMainloopNew();
                if (_mainloop == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create PulseAudio mainloop");
                }

                // Get the mainloop API
                var api = ThreadedMainloopGetApi(_mainloop);
                if (api == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get PulseAudio mainloop API");
                }

                // Create the context
                _context = ContextNew(api, "MultiRoomAudio");
                if (_context == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create PulseAudio context");
                }

                // Set up context state callback (store delegate to prevent GC)
                _contextStateCallback = OnContextStateChanged;
                ContextSetStateCallback(_context, _contextStateCallback, IntPtr.Zero);

                // Start the mainloop
                if (ThreadedMainloopStart(_mainloop) < 0)
                {
                    throw new InvalidOperationException("Failed to start PulseAudio mainloop");
                }

                // Connect to PulseAudio server
                ThreadedMainloopLock(_mainloop);
                try
                {
                    if (ContextConnect(_context, null, 0, IntPtr.Zero) < 0)
                    {
                        throw new InvalidOperationException("Failed to connect to PulseAudio server");
                    }

                    // Wait for context to be ready
                    var timeout = DateTime.UtcNow.AddMilliseconds(ConnectionTimeoutMs);
                    while (!_contextReady)
                    {
                        var state = ContextGetState(_context);
                        if (state == ContextState.Failed || state == ContextState.Terminated)
                        {
                            throw new InvalidOperationException($"PulseAudio context failed: {state}");
                        }

                        if (DateTime.UtcNow > timeout)
                        {
                            throw new TimeoutException("Timeout waiting for PulseAudio context");
                        }

                        ThreadedMainloopWait(_mainloop);
                    }
                }
                finally
                {
                    ThreadedMainloopUnlock(_mainloop);
                }

                _logger.LogDebug("PulseAudio context connected");

                // Create the stream
                var sampleSpec = new SampleSpec
                {
                    Format = SampleFormat.FLOAT32LE,
                    Rate = (uint)format.SampleRate,
                    Channels = (byte)format.Channels
                };

                ThreadedMainloopLock(_mainloop);
                try
                {
                    _stream = StreamNew(_context, "Sendspin Audio", ref sampleSpec, IntPtr.Zero);
                    if (_stream == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to create PulseAudio stream");
                    }

                    // Set up stream callbacks (store delegates to prevent GC)
                    _streamStateCallback = OnStreamStateChanged;
                    _writeCallback = OnWriteCallback;
                    _underflowCallback = OnUnderflow;

                    StreamSetStateCallback(_stream, _streamStateCallback, IntPtr.Zero);
                    StreamSetWriteCallback(_stream, _writeCallback, IntPtr.Zero);
                    StreamSetUnderflowCallback(_stream, _underflowCallback, IntPtr.Zero);

                    // Configure buffer attributes
                    var targetLatencyBytes = BytesForMs(ref sampleSpec, BufferMs);
                    var bufferAttr = new BufferAttr
                    {
                        MaxLength = uint.MaxValue,
                        TLength = targetLatencyBytes,
                        PreBuf = targetLatencyBytes / 2,
                        MinReq = uint.MaxValue,
                        FragSize = uint.MaxValue
                    };

                    // Connect stream for playback with timing flags
                    var flags = StreamFlags.InterpolateTiming |
                                StreamFlags.AutoTimingUpdate |
                                StreamFlags.AdjustLatency;

                    if (StreamConnectPlayback(_stream, _sinkName, ref bufferAttr, flags, IntPtr.Zero, IntPtr.Zero) < 0)
                    {
                        throw new InvalidOperationException("Failed to connect PulseAudio stream");
                    }

                    // Wait for stream to be ready
                    var timeout = DateTime.UtcNow.AddMilliseconds(ConnectionTimeoutMs);
                    while (!_streamReady)
                    {
                        var state = StreamGetState(_stream);
                        if (state == StreamState.Failed || state == StreamState.Terminated)
                        {
                            throw new InvalidOperationException($"PulseAudio stream failed: {state}");
                        }

                        if (DateTime.UtcNow > timeout)
                        {
                            throw new TimeoutException("Timeout waiting for PulseAudio stream");
                        }

                        ThreadedMainloopWait(_mainloop);
                    }
                }
                finally
                {
                    ThreadedMainloopUnlock(_mainloop);
                }

                _currentFormat = format;

                // Set initial latency estimate; will be updated by write callback
                OutputLatencyMs = InitialLatencyEstimateMs;

                // Pre-allocate buffers
                var samplesPerWrite = FramesPerWrite * format.Channels;
                _sampleBuffer = new float[samplesPerWrite];
                _byteBuffer = new byte[samplesPerWrite * sizeof(float)];

                SetState(AudioPlayerState.Stopped);

                _logger.LogInformation(
                    "PulseAudio player initialized (async API). Sink: {Sink}, Initial latency estimate: {Latency}ms",
                    _sinkName ?? "default", OutputLatencyMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PulseAudio player");
                CleanupResources();
                SetState(AudioPlayerState.Error);
                OnError("Initialization failed", ex);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public void SetSampleSource(IAudioSampleSource source)
    {
        lock (_lock)
        {
            _sampleSource = source;
            _logger.LogDebug("Sample source set");
        }
    }

    public void Play()
    {
        lock (_lock)
        {
            if (_stream == IntPtr.Zero)
            {
                _logger.LogWarning("Cannot play - not initialized");
                return;
            }

            if (_isPlaying && !_isPaused)
            {
                _logger.LogDebug("Already playing");
                return;
            }

            _isPlaying = true;

            if (_isPaused)
            {
                // Uncork the stream to resume
                _isPaused = false;
                ThreadedMainloopLock(_mainloop);
                try
                {
                    StreamCork(_stream, 0, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    ThreadedMainloopUnlock(_mainloop);
                }
                SetState(AudioPlayerState.Playing);
                _logger.LogInformation("Playback resumed");
                return;
            }

            // Stream is already connected and write callback will be called
            // Just mark as playing
            SetState(AudioPlayerState.Playing);
            _logger.LogInformation("Playback started");
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (!_isPlaying || _stream == IntPtr.Zero)
                return;

            _isPaused = true;

            // Cork (pause) the stream
            ThreadedMainloopLock(_mainloop);
            try
            {
                StreamCork(_stream, 1, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                ThreadedMainloopUnlock(_mainloop);
            }

            SetState(AudioPlayerState.Paused);
            _logger.LogInformation("Playback paused");
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isPlaying = false;
            _isPaused = false;

            if (_stream != IntPtr.Zero && _mainloop != IntPtr.Zero)
            {
                ThreadedMainloopLock(_mainloop);
                try
                {
                    // Flush any remaining audio
                    StreamFlush(_stream, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    ThreadedMainloopUnlock(_mainloop);
                }
            }

            SetState(AudioPlayerState.Stopped);
            _logger.LogInformation("Playback stopped");
        }
    }

    public async Task SwitchDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Switching PulseAudio sink to: {Sink}", deviceId ?? "default");

        var wasPlaying = State == AudioPlayerState.Playing;
        var savedSource = _sampleSource;
        var savedFormat = _currentFormat;

        Stop();

        _sinkName = deviceId;

        if (savedFormat != null)
        {
            try
            {
                // Clean up old resources
                lock (_lock)
                {
                    CleanupResources();
                }

                await InitializeAsync(savedFormat, cancellationToken);

                if (savedSource != null)
                {
                    SetSampleSource(savedSource);
                }

                if (wasPlaying)
                {
                    Play();
                }

                _logger.LogInformation("Device switch complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device switch failed");
                OnError("Device switch failed", ex);
                throw;
            }
        }
    }

    /// <summary>
    /// Called by PulseAudio when the context state changes.
    /// </summary>
    private void OnContextStateChanged(IntPtr context, IntPtr userdata)
    {
        var state = ContextGetState(context);
        _logger.LogDebug("PulseAudio context state: {State}", state);

        if (state == ContextState.Ready)
        {
            _contextReady = true;
            ThreadedMainloopSignal(_mainloop, 0);
        }
        else if (state == ContextState.Failed || state == ContextState.Terminated)
        {
            _contextReady = false;
            ThreadedMainloopSignal(_mainloop, 0);
            _logger.LogWarning("PulseAudio context disconnected: {State}", state);
        }
    }

    /// <summary>
    /// Called by PulseAudio when the stream state changes.
    /// </summary>
    private void OnStreamStateChanged(IntPtr stream, IntPtr userdata)
    {
        var state = StreamGetState(stream);
        _logger.LogDebug("PulseAudio stream state: {State}", state);

        if (state == StreamState.Ready)
        {
            _streamReady = true;
            ThreadedMainloopSignal(_mainloop, 0);
        }
        else if (state == StreamState.Failed || state == StreamState.Terminated)
        {
            _streamReady = false;
            ThreadedMainloopSignal(_mainloop, 0);
            _logger.LogWarning("PulseAudio stream disconnected: {State}", state);
        }
    }

    /// <summary>
    /// Called by PulseAudio when it needs more audio data.
    /// This is where we query the REAL latency and provide samples.
    /// </summary>
    private void OnWriteCallback(IntPtr stream, UIntPtr nbytes, IntPtr userdata)
    {
        if (!_isPlaying || _isPaused || _disposed)
        {
            // Write silence if not playing
            WriteSilence(stream, nbytes);
            return;
        }

        // Query the REAL latency - this is the key benefit of the async API
        if (StreamGetLatency(stream, out var latencyUs, out var negative) == 0 && negative == 0)
        {
            _lastMeasuredLatencyUs = latencyUs;
            var newLatencyMs = (int)(latencyUs / 1000);

            // Update OutputLatencyMs if significantly different (avoid jitter)
            if (Math.Abs(newLatencyMs - OutputLatencyMs) > 5)
            {
                OutputLatencyMs = newLatencyMs;
                _logger.LogDebug("Measured latency: {Latency}ms", newLatencyMs);
            }
        }

        var source = _sampleSource;
        var sampleBuffer = _sampleBuffer;
        var byteBuffer = _byteBuffer;

        if (source == null || sampleBuffer == null || byteBuffer == null)
        {
            WriteSilence(stream, nbytes);
            return;
        }

        var bytesRequested = (int)(ulong)nbytes;
        var bytesPerSample = sizeof(float);
        var samplesRequested = bytesRequested / bytesPerSample;

        // Ensure we don't exceed buffer size
        if (samplesRequested > sampleBuffer.Length)
        {
            samplesRequested = sampleBuffer.Length;
        }

        // Read samples from the source
        var samplesRead = source.Read(sampleBuffer, 0, samplesRequested);

        if (samplesRead == 0)
        {
            WriteSilence(stream, nbytes);
            return;
        }

        // Apply volume and mute
        var vol = IsMuted ? 0f : Volume;
        for (int i = 0; i < samplesRead; i++)
        {
            sampleBuffer[i] *= vol;
        }

        // Convert to bytes
        Buffer.BlockCopy(sampleBuffer, 0, byteBuffer, 0, samplesRead * bytesPerSample);

        // Write to stream
        unsafe
        {
            fixed (byte* ptr = byteBuffer)
            {
                var result = StreamWrite(
                    stream,
                    (IntPtr)ptr,
                    (UIntPtr)(samplesRead * bytesPerSample),
                    IntPtr.Zero,
                    0,
                    SeekMode.Relative);

                if (result < 0)
                {
                    _logger.LogDebug("PulseAudio stream write failed");
                }
            }
        }
    }

    /// <summary>
    /// Called when an underflow occurs (buffer ran out of data).
    /// </summary>
    private void OnUnderflow(IntPtr stream, IntPtr userdata)
    {
        _underflowCount++;

        if (_underflowCount == UnderflowWarningThreshold)
        {
            _logger.LogWarning(
                "Audio underflow detected ({Count} times). Consider increasing buffer size.",
                _underflowCount);
        }
        else if (_underflowCount % 100 == 0)
        {
            _logger.LogDebug("Underflow count: {Count}", _underflowCount);
        }
    }

    /// <summary>
    /// Write silence to the stream.
    /// </summary>
    private void WriteSilence(IntPtr stream, UIntPtr nbytes)
    {
        var bytesRequested = (int)(ulong)nbytes;
        var silenceBuffer = new byte[bytesRequested];

        unsafe
        {
            fixed (byte* ptr = silenceBuffer)
            {
                StreamWrite(stream, (IntPtr)ptr, nbytes, IntPtr.Zero, 0, SeekMode.Relative);
            }
        }
    }

    /// <summary>
    /// Clean up all PulseAudio resources.
    /// </summary>
    private void CleanupResources()
    {
        _contextReady = false;
        _streamReady = false;

        if (_stream != IntPtr.Zero)
        {
            if (_mainloop != IntPtr.Zero)
            {
                ThreadedMainloopLock(_mainloop);
                try
                {
                    StreamDisconnect(_stream);
                }
                finally
                {
                    ThreadedMainloopUnlock(_mainloop);
                }
            }
            StreamUnref(_stream);
            _stream = IntPtr.Zero;
        }

        if (_context != IntPtr.Zero)
        {
            if (_mainloop != IntPtr.Zero)
            {
                ThreadedMainloopLock(_mainloop);
                try
                {
                    ContextDisconnect(_context);
                }
                finally
                {
                    ThreadedMainloopUnlock(_mainloop);
                }
            }
            ContextUnref(_context);
            _context = IntPtr.Zero;
        }

        if (_mainloop != IntPtr.Zero)
        {
            ThreadedMainloopStop(_mainloop);
            ThreadedMainloopFree(_mainloop);
            _mainloop = IntPtr.Zero;
        }

        // Clear callback references (safe to do after mainloop is stopped)
        _contextStateCallback = null;
        _streamStateCallback = null;
        _writeCallback = null;
        _underflowCallback = null;

        _sampleBuffer = null;
        _byteBuffer = null;
    }

    private void SetState(AudioPlayerState newState)
    {
        if (State != newState)
        {
            var oldState = State;
            State = newState;
            _logger.LogDebug("State changed: {OldState} -> {NewState}", oldState, newState);
            StateChanged?.Invoke(this, newState);
        }
    }

    private void OnError(string message, Exception? ex = null)
    {
        ErrorOccurred?.Invoke(this, new AudioPlayerError(message, ex));
    }

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
        }

        Stop();

        lock (_lock)
        {
            CleanupResources();
        }

        _logger.LogInformation("PulseAudio player disposed");
        return ValueTask.CompletedTask;
    }
}
