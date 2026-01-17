using MultiRoomAudio.Models;
using Sendspin.SDK.Audio;
using Sendspin.SDK.Models;

namespace MultiRoomAudio.Audio.Mock;

/// <summary>
/// Mock audio backend for testing without real hardware.
/// Provides fake audio devices that simulate USB DACs with multiple profiles.
/// Channel counts are dynamically determined based on the active card profile.
/// </summary>
public class MockAudioBackend : IBackend
{
    private readonly ILogger<MockAudioBackend> _logger;
    private readonly Dictionary<string, int> _volumes = new();

    /// <summary>
    /// Pre-configured mock devices simulating typical USB DAC setups.
    /// </summary>
    private static readonly List<MockDeviceConfig> MockDeviceConfigs = new()
    {
        new("mock_living_room_dac", "Living Room DAC", "USB Audio DAC", 0, true),
        new("mock_kitchen_speakers", "Kitchen Speakers", "Generic USB Audio", 1, false),
        new("mock_bedroom_stereo", "Bedroom Stereo", "HiFi USB DAC Pro", 2, false),
        new("mock_office_speakers", "Office Speakers", "USB Sound Card", 3, false),
        new("mock_garage_audio", "Garage Audio", "USB Audio Adapter", 4, false),
        new("mock_patio_speakers", "Patio Speakers", "Outdoor USB DAC", 5, false),
        new("mock_basement_system", "Basement System", "Multi-channel USB DAC", 6, false),
        new("mock_guest_room", "Guest Room Audio", "Compact USB DAC", 7, false),
    };

    private record MockDeviceConfig(string Id, string Name, string Description, int Index, bool IsDefault);

    public MockAudioBackend(ILogger<MockAudioBackend> logger)
    {
        _logger = logger;
        _logger.LogInformation("Mock audio backend initialized with {Count} devices", MockDeviceConfigs.Count);
    }

    /// <summary>
    /// Gets the channel count for a profile name.
    /// </summary>
    private static int GetChannelCountForProfile(string? profileName)
    {
        return profileName?.ToLowerInvariant() switch
        {
            "output:analog-surround-71" => 8,
            "output:analog-surround-51" => 6,
            "output:analog-stereo" => 2,
            "output:iec958-stereo" => 2,
            "off" => 0,
            _ => 2 // Default to stereo
        };
    }

    /// <inheritdoc />
    public string Name => "mock";

    /// <inheritdoc />
    public IEnumerable<AudioDevice> GetOutputDevices()
    {
        var devices = CreateMockDevices();
        _logger.LogDebug("Returning {Count} mock audio devices", devices.Count);
        return devices;
    }

    /// <inheritdoc />
    public AudioDevice? GetDevice(string deviceId)
    {
        var devices = CreateMockDevices();

        // Try by index
        if (int.TryParse(deviceId, out var index))
        {
            return devices.FirstOrDefault(d => d.Index == index);
        }

        // Try by ID
        return devices.FirstOrDefault(d =>
            d.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains(deviceId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public AudioDevice? GetDefaultDevice()
    {
        return CreateMockDevices().FirstOrDefault(d => d.IsDefault);
    }

    /// <inheritdoc />
    public DeviceCapabilities? GetDeviceCapabilities(string? deviceId)
    {
        // Get the device to determine its current channel count
        var device = string.IsNullOrEmpty(deviceId) ? GetDefaultDevice() : GetDevice(deviceId);
        var maxChannels = device?.MaxChannels ?? 2;

        return new DeviceCapabilities(
            SupportedSampleRates: new[] { 44100, 48000, 88200, 96000, 176400, 192000 },
            SupportedBitDepths: new[] { 16, 24, 32 },
            MaxChannels: maxChannels,
            PreferredSampleRate: 48000,
            PreferredBitDepth: 24
        );
    }

    /// <inheritdoc />
    public bool ValidateDevice(string? deviceId, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            errorMessage = null;
            return true;
        }

        var device = GetDevice(deviceId);
        if (device == null)
        {
            errorMessage = $"Mock device '{deviceId}' not found.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <inheritdoc />
    public void RefreshDevices()
    {
        _logger.LogDebug("Mock device refresh (no-op)");
    }

    /// <inheritdoc />
    public IAudioPlayer CreatePlayer(string? deviceId, ILoggerFactory loggerFactory)
    {
        var device = string.IsNullOrEmpty(deviceId)
            ? GetDefaultDevice()
            : GetDevice(deviceId);

        _logger.LogInformation("Creating mock player for device: {Device}", device?.Name ?? "(default)");

        // Return a null audio player that discards samples
        return new MockAudioPlayer(loggerFactory.CreateLogger<MockAudioPlayer>(), device?.Name);
    }

    /// <inheritdoc />
    public Task<bool> SetVolumeAsync(string? deviceId, int volume, CancellationToken cancellationToken = default)
    {
        var device = string.IsNullOrEmpty(deviceId) ? GetDefaultDevice() : GetDevice(deviceId);
        if (device == null)
            return Task.FromResult(false);

        _volumes[device.Id] = Math.Clamp(volume, 0, 100);
        _logger.LogDebug("Mock volume for {Device} set to {Volume}%", device.Name, volume);
        return Task.FromResult(true);
    }

    private static List<AudioDevice> CreateMockDevices()
    {
        // Get all cards to determine active profiles and channel counts
        var cards = MockCardEnumerator.GetCards().ToList();

        return MockDeviceConfigs.Select(config =>
        {
            // Find the corresponding card by index
            var card = cards.FirstOrDefault(c => c.Index == config.Index);
            var channelCount = GetChannelCountForProfile(card?.ActiveProfile);

            return new AudioDevice(
                Index: config.Index,
                Id: config.Id,
                Name: config.Name,
                MaxChannels: channelCount,
                DefaultSampleRate: 48000,
                DefaultLowLatencyMs: 20,
                DefaultHighLatencyMs: 100,
                IsDefault: config.IsDefault,
                Capabilities: new DeviceCapabilities(
                    SupportedSampleRates: new[] { 44100, 48000, 96000, 192000 },
                    SupportedBitDepths: new[] { 16, 24, 32 },
                    MaxChannels: channelCount,
                    PreferredSampleRate: 48000,
                    PreferredBitDepth: 24
                ),
                Identifiers: new DeviceIdentifiers(
                    Serial: $"MOCK-{config.Index:D4}",
                    BusPath: $"usb-mock-{config.Index}",
                    VendorId: "1234",
                    ProductId: $"000{config.Index}",
                    AlsaLongCardName: config.Description
                )
            );
        }).ToList();
    }
}

/// <summary>
/// Mock audio player that discards audio samples.
/// Used when MOCK_HARDWARE is enabled for testing without real audio output.
/// Implements the full IAudioPlayer interface from SendSpin.SDK.
/// </summary>
public class MockAudioPlayer : IAudioPlayer
{
    private readonly ILogger<MockAudioPlayer> _logger;
    private readonly string _deviceName;
    private IAudioSampleSource? _sampleSource;
    private AudioFormat? _currentFormat;
    private volatile bool _disposed;
    private Timer? _playbackTimer;

    public MockAudioPlayer(ILogger<MockAudioPlayer> logger, string? deviceName)
    {
        _logger = logger;
        _deviceName = deviceName ?? "default";
    }

    public AudioPlayerState State { get; private set; } = AudioPlayerState.Uninitialized;

    public float Volume { get; set; } = 1.0f;

    public bool IsMuted { get; set; }

    public int OutputLatencyMs { get; private set; } = 50;

    public event EventHandler<AudioPlayerState>? StateChanged;
    public event EventHandler<AudioPlayerError>? ErrorOccurred;

    public Task InitializeAsync(AudioFormat format, CancellationToken cancellationToken = default)
    {
        _currentFormat = format;
        State = AudioPlayerState.Stopped;
        StateChanged?.Invoke(this, State);

        _logger.LogInformation(
            "Mock player initialized: {SampleRate}Hz, {Channels}ch, device: {Device}",
            format.SampleRate, format.Channels, _deviceName);

        return Task.CompletedTask;
    }

    public void SetSampleSource(IAudioSampleSource source)
    {
        _sampleSource = source;
        _logger.LogDebug("Sample source set for mock player");
    }

    public void Play()
    {
        if (State == AudioPlayerState.Playing)
            return;

        State = AudioPlayerState.Playing;
        StateChanged?.Invoke(this, State);

        // Start a timer to simulate reading samples (to keep the SDK happy)
        _playbackTimer?.Dispose();
        _playbackTimer = new Timer(SimulatePlayback, null, 0, 20);

        _logger.LogInformation("Mock player started for {Device}", _deviceName);
    }

    public void Pause()
    {
        if (State != AudioPlayerState.Playing)
            return;

        _playbackTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        State = AudioPlayerState.Paused;
        StateChanged?.Invoke(this, State);

        _logger.LogInformation("Mock player paused for {Device}", _deviceName);
    }

    public void Stop()
    {
        _playbackTimer?.Dispose();
        _playbackTimer = null;

        State = AudioPlayerState.Stopped;
        StateChanged?.Invoke(this, State);

        _logger.LogDebug("Mock player stopped for {Device}", _deviceName);
    }

    public Task SwitchDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock player switching device to: {Device}", deviceId ?? "default");
        return Task.CompletedTask;
    }

    private void SimulatePlayback(object? state)
    {
        if (_disposed || State != AudioPlayerState.Playing || _sampleSource == null)
            return;

        // Read and discard samples to keep the SDK buffer flowing
        var buffer = new float[1024];
        try
        {
            var read = _sampleSource.Read(buffer, 0, buffer.Length);
            // Samples are discarded - this is a null sink
        }
        catch
        {
            // Ignore errors in mock player
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        Stop();

        _logger.LogDebug("Mock player disposed for {Device}", _deviceName);
        return ValueTask.CompletedTask;
    }
}
