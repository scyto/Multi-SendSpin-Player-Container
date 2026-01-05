using MultiRoomAudio.Models;
using MultiRoomAudio.Utilities;
using Sendspin.SDK.Audio;

namespace MultiRoomAudio.Audio.PulseAudio;

/// <summary>
/// PulseAudio audio backend implementation.
/// Provides device enumeration, player creation, and volume control for PulseAudio sinks.
/// </summary>
public class PulseAudioBackend : IBackend
{
    private readonly ILogger<PulseAudioBackend> _logger;
    private readonly VolumeCommandRunner _volumeRunner;

    public string Name => "pulse";

    public PulseAudioBackend(
        ILogger<PulseAudioBackend> logger,
        VolumeCommandRunner volumeRunner)
    {
        _logger = logger;
        _volumeRunner = volumeRunner;

        // Configure the device enumerator with a logger
        PulseAudioDeviceEnumerator.SetLogger(logger);
    }

    public IEnumerable<AudioDevice> GetOutputDevices()
    {
        return PulseAudioDeviceEnumerator.GetOutputDevices();
    }

    public AudioDevice? GetDevice(string deviceId)
    {
        return PulseAudioDeviceEnumerator.GetDevice(deviceId);
    }

    public AudioDevice? GetDefaultDevice()
    {
        return PulseAudioDeviceEnumerator.GetDefaultDevice();
    }

    public bool ValidateDevice(string? deviceId, out string? errorMessage)
    {
        return PulseAudioDeviceEnumerator.ValidateDevice(deviceId, out errorMessage);
    }

    public void RefreshDevices()
    {
        PulseAudioDeviceEnumerator.RefreshDevices();
    }

    public IAudioPlayer CreatePlayer(string? deviceId, ILoggerFactory loggerFactory)
    {
        _logger.LogDebug("Creating PulseAudio player for sink: {Sink}", deviceId ?? "default");
        return new PulseAudioPlayer(
            loggerFactory.CreateLogger<PulseAudioPlayer>(),
            deviceId);
    }

    public async Task<bool> SetVolumeAsync(string? deviceId, int volume, CancellationToken cancellationToken = default)
    {
        return await _volumeRunner.SetVolumeAsync(deviceId, volume, cancellationToken);
    }
}
