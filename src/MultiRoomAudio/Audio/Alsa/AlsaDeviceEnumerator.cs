using System.Diagnostics;
using System.Text.RegularExpressions;
using MultiRoomAudio.Models;

namespace MultiRoomAudio.Audio.Alsa;

/// <summary>
/// Enumerates available ALSA audio output devices by parsing aplay -L output.
/// Supports both hardware devices (hw:X,Y) and software-defined devices (from asound.conf).
/// </summary>
public static partial class AlsaDeviceEnumerator
{
    private static ILogger? _logger;

    // System devices to filter out (not useful for playback)
    private static readonly HashSet<string> SystemDevices = new(StringComparer.OrdinalIgnoreCase)
    {
        "null",
        "default",
        "pulse",
        "sysdefault",
        "iec958"  // S/PDIF generic device (usually not what users want)
    };

    // Patterns for device name prefixes to filter
    private static readonly string[] IgnorePrefixes =
    {
        "dmix:",     // Direct mix devices (lower level than what we want)
        "dsnoop:",   // Capture sharing
        "surround",  // Surround configs (prefer explicit zone devices)
    };

    /// <summary>
    /// Configures the logger for device enumeration diagnostics.
    /// </summary>
    public static void SetLogger(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all available ALSA audio output devices.
    /// Parses output from 'aplay -L' command.
    /// </summary>
    public static IEnumerable<AudioDevice> GetOutputDevices()
    {
        var devices = new List<AudioDevice>();

        try
        {
            var output = RunAplay("-L");
            if (string.IsNullOrEmpty(output))
            {
                _logger?.LogWarning("aplay -L returned empty output - ALSA may not be available");
                return devices;
            }

            devices = ParseAplayOutput(output).ToList();

            _logger?.LogDebug("Found {Count} ALSA output devices", devices.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to enumerate ALSA devices");
        }

        return devices;
    }

    /// <summary>
    /// Gets a specific audio device by ID.
    /// </summary>
    public static AudioDevice? GetDevice(string deviceId)
    {
        return GetOutputDevices()
            .FirstOrDefault(d =>
                d.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase) ||
                d.Name.Contains(deviceId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the default audio output device.
    /// For ALSA, this is usually the "default" device or the first hw device.
    /// </summary>
    public static AudioDevice? GetDefaultDevice()
    {
        var devices = GetOutputDevices().ToList();

        // First try to find a device marked as default
        var defaultDevice = devices.FirstOrDefault(d => d.IsDefault);
        if (defaultDevice != null)
            return defaultDevice;

        // Otherwise return the first device
        return devices.FirstOrDefault();
    }

    /// <summary>
    /// Validates that a device ID exists.
    /// </summary>
    public static bool ValidateDevice(string? deviceId, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            // Default device is always valid
            errorMessage = null;
            return true;
        }

        // Try to find the device
        var device = GetDevice(deviceId);
        if (device == null)
        {
            errorMessage = $"ALSA device '{deviceId}' not found. Use /api/devices to list available devices.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Refreshes device list. For ALSA, this is a no-op since aplay always queries current state.
    /// </summary>
    public static void RefreshDevices()
    {
        _logger?.LogDebug("ALSA device refresh requested (no-op, aplay queries live state)");
    }

    /// <summary>
    /// Parses aplay -L output into AudioDevice records.
    /// </summary>
    /// <remarks>
    /// aplay -L output format:
    /// device_name
    ///     Description line 1
    ///     Description line 2
    /// another_device
    ///     Its description
    ///
    /// Device names are non-indented, descriptions are indented with spaces/tabs.
    /// </remarks>
    private static IEnumerable<AudioDevice> ParseAplayOutput(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var devices = new List<(string Id, List<string> Description)>();
        string? currentDevice = null;
        List<string>? currentDesc = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Non-indented line = device name
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                // Save previous device
                if (currentDevice != null && currentDesc != null)
                {
                    devices.Add((currentDevice, currentDesc));
                }

                currentDevice = line.Trim();
                currentDesc = new List<string>();
            }
            // Indented line = description
            else if (currentDevice != null && currentDesc != null)
            {
                var desc = line.Trim();
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    currentDesc.Add(desc);
                }
            }
        }

        // Don't forget the last device
        if (currentDevice != null && currentDesc != null)
        {
            devices.Add((currentDevice, currentDesc));
        }

        // Convert to AudioDevice records
        var index = 0;
        foreach (var (id, description) in devices)
        {
            // Skip system devices
            if (ShouldSkipDevice(id))
            {
                _logger?.LogDebug("Skipping system device: {DeviceId}", id);
                continue;
            }

            // Build display name from first description line or device ID
            var name = description.Count > 0 ? description[0] : id;

            // Try to detect sample rate from description (hw devices often mention it)
            var sampleRate = DetectSampleRate(description) ?? 48000;

            // Try to detect channel count
            var channels = DetectChannels(description) ?? 2;

            // Check if this looks like the default
            var isDefault = id.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                           id.StartsWith("sysdefault:", StringComparison.OrdinalIgnoreCase);

            yield return new AudioDevice(
                Index: index++,
                Id: id,
                Name: name,
                MaxChannels: channels,
                DefaultSampleRate: sampleRate,
                DefaultLowLatencyMs: 20,    // ALSA can achieve low latency
                DefaultHighLatencyMs: 100,
                IsDefault: isDefault
            );
        }
    }

    /// <summary>
    /// Determines if a device should be skipped (system/meta devices).
    /// </summary>
    private static bool ShouldSkipDevice(string deviceId)
    {
        // Skip known system devices
        if (SystemDevices.Contains(deviceId))
            return true;

        // Skip devices with certain prefixes
        foreach (var prefix in IgnorePrefixes)
        {
            if (deviceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to detect sample rate from device description.
    /// </summary>
    private static int? DetectSampleRate(List<string> description)
    {
        foreach (var line in description)
        {
            // Look for patterns like "48000Hz" or "44100 Hz" or "rate 48000"
            var match = SampleRateRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var rate))
            {
                return rate;
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to detect channel count from device description.
    /// </summary>
    private static int? DetectChannels(List<string> description)
    {
        foreach (var line in description)
        {
            // Look for patterns like "2ch" or "stereo" or "7.1"
            if (line.Contains("stereo", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (line.Contains("mono", StringComparison.OrdinalIgnoreCase))
                return 1;

            var match = ChannelRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var ch))
            {
                return ch;
            }

            // Check for surround formats
            if (line.Contains("7.1"))
                return 8;
            if (line.Contains("5.1"))
                return 6;
            if (line.Contains("4.0") || line.Contains("quad", StringComparison.OrdinalIgnoreCase))
                return 4;
        }
        return null;
    }

    /// <summary>
    /// Runs aplay command and returns output.
    /// </summary>
    private static string? RunAplay(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "aplay",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
            {
                _logger?.LogWarning("aplay {Args} failed with exit code {ExitCode}: {Error}",
                    arguments, process.ExitCode, error.Trim());
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to run aplay {Args}", arguments);
            return null;
        }
    }

    // Regex patterns
    [GeneratedRegex(@"(\d{4,6})\s*Hz", RegexOptions.IgnoreCase)]
    private static partial Regex SampleRateRegex();

    [GeneratedRegex(@"(\d+)\s*ch", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelRegex();
}
