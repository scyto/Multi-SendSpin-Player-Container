using Sendspin.SDK.Audio;
using Sendspin.SDK.Models;

namespace MultiRoomAudio.Audio.Alsa;

/// <summary>
/// IAudioPlayer implementation using direct ALSA libasound API.
/// Designed for Docker environments with raw ALSA device access.
/// </summary>
/// <remarks>
/// This player uses ALSA's PCM API directly via P/Invoke, allowing playback
/// to any ALSA device including software-defined devices from asound.conf.
/// Audio data is written on a dedicated thread using blocking I/O.
/// </remarks>
public class AlsaPlayer : IAudioPlayer
{
    private readonly ILogger<AlsaPlayer> _logger;
    private readonly object _lock = new();

    private IntPtr _pcmHandle = IntPtr.Zero;
    private IAudioSampleSource? _sampleSource;
    private AudioFormat? _currentFormat;
    private string _deviceName;
    private bool _disposed;

    private Thread? _playbackThread;
    private CancellationTokenSource? _playbackCts;
    private volatile bool _isPlaying;
    private volatile bool _isPaused;

    // Pre-allocated buffers for the playback thread
    private float[]? _sampleBuffer;
    private byte[]? _byteBuffer;

    /// <summary>
    /// Target latency in microseconds (50ms = 50000μs).
    /// </summary>
    private const uint TargetLatencyUs = 50_000;

    /// <summary>
    /// Frames per write operation. At 48kHz, 1024 frames = ~21ms of audio.
    /// </summary>
    private const int FramesPerWrite = 1024;

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

    public int OutputLatencyMs { get; private set; }

    public event EventHandler<AudioPlayerState>? StateChanged;
    public event EventHandler<AudioPlayerError>? ErrorOccurred;

    /// <summary>
    /// Initializes a new instance of the AlsaPlayer.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="deviceName">
    /// ALSA device name. Can be:
    /// - "default" or null for default device
    /// - "hw:0,0" for direct hardware access
    /// - "plughw:0,0" for plugin-wrapped hardware
    /// - Custom device from asound.conf (e.g., "zone1")
    /// </param>
    public AlsaPlayer(ILogger<AlsaPlayer> logger, string? deviceName = null)
    {
        _logger = logger;
        _deviceName = deviceName ?? "default";
    }

    public Task InitializeAsync(AudioFormat format, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            try
            {
                _logger.LogInformation(
                    "Initializing ALSA player with format: {SampleRate}Hz, {Channels}ch, device: {Device}",
                    format.SampleRate, format.Channels, _deviceName);

                // Open the ALSA device
                var result = AlsaNative.Open(
                    out _pcmHandle,
                    _deviceName,
                    AlsaNative.StreamType.Playback,
                    0);  // Blocking mode

                if (result < 0)
                {
                    var errorMsg = AlsaNative.GetErrorMessage(result);
                    throw new InvalidOperationException(
                        $"Failed to open ALSA device '{_deviceName}': {errorMsg}");
                }

                // Configure PCM parameters using simplified API
                result = AlsaNative.SetParams(
                    _pcmHandle,
                    AlsaNative.Format.FLOAT_LE,
                    AlsaNative.Access.RwInterleaved,
                    (uint)format.Channels,
                    (uint)format.SampleRate,
                    1,  // Allow software resampling
                    TargetLatencyUs);

                if (result < 0)
                {
                    var errorMsg = AlsaNative.GetErrorMessage(result);
                    AlsaNative.Close(_pcmHandle);
                    _pcmHandle = IntPtr.Zero;
                    throw new InvalidOperationException(
                        $"Failed to set ALSA parameters: {errorMsg}");
                }

                _currentFormat = format;

                // Calculate latency from target
                OutputLatencyMs = (int)(TargetLatencyUs / 1000);

                // Pre-allocate buffers
                var samplesPerWrite = FramesPerWrite * format.Channels;
                _sampleBuffer = new float[samplesPerWrite];
                _byteBuffer = new byte[samplesPerWrite * sizeof(float)];

                SetState(AudioPlayerState.Stopped);

                _logger.LogInformation(
                    "ALSA player initialized. Device: {Device}, Latency: {Latency}ms",
                    _deviceName, OutputLatencyMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ALSA player");
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
            if (_pcmHandle == IntPtr.Zero)
            {
                _logger.LogWarning("Cannot play - not initialized");
                return;
            }

            if (_isPlaying && !_isPaused)
            {
                _logger.LogDebug("Already playing");
                return;
            }

            if (_isPaused)
            {
                // Resume from pause
                _isPaused = false;
                SetState(AudioPlayerState.Playing);
                _logger.LogInformation("Playback resumed");
                return;
            }

            // Prepare the PCM device
            var result = AlsaNative.Prepare(_pcmHandle);
            if (result < 0)
            {
                _logger.LogWarning("Failed to prepare ALSA device: {Error}",
                    AlsaNative.GetErrorMessage(result));
            }

            // Start playback thread
            _playbackCts = new CancellationTokenSource();
            _isPlaying = true;
            _isPaused = false;

            _playbackThread = new Thread(PlaybackLoop)
            {
                Name = "ALSA-Playback",
                IsBackground = true
            };

            try
            {
                _playbackThread.Priority = ThreadPriority.AboveNormal;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Could not set thread priority to AboveNormal. Audio may have higher latency.");
            }

            _playbackThread.Start();

            SetState(AudioPlayerState.Playing);
            _logger.LogInformation("Playback started");
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _isPaused = true;
            SetState(AudioPlayerState.Paused);
            _logger.LogInformation("Playback paused");
        }
    }

    public void Stop()
    {
        Thread? threadToJoin = null;

        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _isPlaying = false;
            _isPaused = false;
            _playbackCts?.Cancel();
            threadToJoin = _playbackThread;
        }

        // Wait for thread outside lock
        if (threadToJoin != null)
        {
            if (!threadToJoin.Join(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning(
                    "Playback thread did not exit within 5 seconds");
            }
        }

        lock (_lock)
        {
            _playbackThread = null;
            _playbackCts?.Dispose();
            _playbackCts = null;

            // Drain any remaining audio
            if (_pcmHandle != IntPtr.Zero)
            {
                AlsaNative.Drop(_pcmHandle);
            }

            SetState(AudioPlayerState.Stopped);
            _logger.LogInformation("Playback stopped");
        }
    }

    public async Task SwitchDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Switching ALSA device to: {Device}", deviceId ?? "default");

        var wasPlaying = State == AudioPlayerState.Playing;
        var savedSource = _sampleSource;
        var savedFormat = _currentFormat;
        var savedDeviceName = _deviceName;
        IntPtr oldHandle = IntPtr.Zero;

        // Stop current playback
        Stop();

        // Save the old handle
        lock (_lock)
        {
            oldHandle = _pcmHandle;
            _pcmHandle = IntPtr.Zero;
        }

        // Update device name
        _deviceName = deviceId ?? "default";

        // Reinitialize with new device
        if (savedFormat != null)
        {
            try
            {
                await InitializeAsync(savedFormat, cancellationToken);

                // Close old handle after successful initialization
                if (oldHandle != IntPtr.Zero)
                {
                    AlsaNative.Close(oldHandle);
                    oldHandle = IntPtr.Zero;
                }

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
                _logger.LogError(ex, "Device switch failed, restoring previous state");

                // Restore old handle on failure
                lock (_lock)
                {
                    if (_pcmHandle == IntPtr.Zero && oldHandle != IntPtr.Zero)
                    {
                        _pcmHandle = oldHandle;
                        oldHandle = IntPtr.Zero;
                        _deviceName = savedDeviceName;
                        SetState(AudioPlayerState.Stopped);
                    }
                }

                OnError("Device switch failed", ex);
                throw;
            }
            finally
            {
                // Clean up old handle if not restored
                if (oldHandle != IntPtr.Zero)
                {
                    AlsaNative.Close(oldHandle);
                }
            }
        }
    }

    private void PlaybackLoop()
    {
        _logger.LogDebug("Playback thread started");

        try
        {
            var ct = _playbackCts?.Token ?? CancellationToken.None;

            while (_isPlaying && !ct.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                var source = _sampleSource;
                var buffer = _sampleBuffer;
                var byteBuffer = _byteBuffer;
                var format = _currentFormat;

                if (source == null || buffer == null || byteBuffer == null ||
                    format == null || _pcmHandle == IntPtr.Zero)
                {
                    Thread.Sleep(10);
                    continue;
                }

                // Read samples from source
                var samplesRead = source.Read(buffer, 0, buffer.Length);

                if (samplesRead == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                // Apply volume and mute
                var vol = IsMuted ? 0f : Volume;
                for (int i = 0; i < samplesRead; i++)
                {
                    buffer[i] *= vol;
                }

                // Convert float samples to bytes
                Buffer.BlockCopy(buffer, 0, byteBuffer, 0, samplesRead * sizeof(float));

                // Calculate frames
                var frames = samplesRead / format.Channels;

                // Write to ALSA
                unsafe
                {
                    fixed (byte* ptr = byteBuffer)
                    {
                        var written = AlsaNative.WriteInterleaved(
                            _pcmHandle,
                            (IntPtr)ptr,
                            (nuint)frames);

                        if (written < 0)
                        {
                            // Handle errors
                            var errorCode = (int)written;
                            HandlePlaybackError(errorCode);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in playback thread");
            OnError("Playback error", ex);
        }

        _logger.LogDebug("Playback thread exited");
    }

    /// <summary>
    /// Handles ALSA playback errors with recovery attempts.
    /// </summary>
    private void HandlePlaybackError(int errorCode)
    {
        // Try to recover from common errors
        if (errorCode == AlsaNative.EPIPE)
        {
            // Underrun - buffer ran empty
            _logger.LogDebug("ALSA underrun occurred, recovering...");
            var recoverResult = AlsaNative.Recover(_pcmHandle, errorCode, 1);
            if (recoverResult < 0)
            {
                _logger.LogWarning("Failed to recover from underrun: {Error}",
                    AlsaNative.GetErrorMessage(recoverResult));
            }
        }
        else if (errorCode == AlsaNative.ESTRPIPE)
        {
            // Suspend - device was suspended
            _logger.LogDebug("ALSA device suspended, attempting resume...");

            // Try to resume
            int resumeResult;
            while ((resumeResult = AlsaNative.Resume(_pcmHandle)) == AlsaNative.EAGAIN)
            {
                Thread.Sleep(10);
            }

            if (resumeResult < 0)
            {
                // Resume failed, try full recovery
                var recoverResult = AlsaNative.Recover(_pcmHandle, errorCode, 1);
                if (recoverResult < 0)
                {
                    _logger.LogWarning("Failed to recover from suspend: {Error}",
                        AlsaNative.GetErrorMessage(recoverResult));
                }
            }
        }
        else if (errorCode == AlsaNative.EAGAIN)
        {
            // Non-blocking would return this - just wait
            Thread.Sleep(1);
        }
        else
        {
            // Other error - try generic recovery
            _logger.LogWarning("ALSA write error: {Error}",
                AlsaNative.GetErrorMessage(errorCode));
            var recoverResult = AlsaNative.Recover(_pcmHandle, errorCode, 1);
            if (recoverResult < 0)
            {
                _logger.LogError("Failed to recover from error: {Error}",
                    AlsaNative.GetErrorMessage(recoverResult));
                Thread.Sleep(10);
            }
        }
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

        // Stop playback
        Stop();

        IntPtr handleToClose;
        lock (_lock)
        {
            handleToClose = _pcmHandle;
            _pcmHandle = IntPtr.Zero;

            _sampleBuffer = null;
            _byteBuffer = null;
        }

        // Close the handle outside the lock
        if (handleToClose != IntPtr.Zero)
        {
            AlsaNative.Close(handleToClose);
        }

        _logger.LogInformation("ALSA player disposed");
        return ValueTask.CompletedTask;
    }
}
