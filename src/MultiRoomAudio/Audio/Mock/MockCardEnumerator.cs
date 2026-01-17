using MultiRoomAudio.Models;

namespace MultiRoomAudio.Audio.Mock;

/// <summary>
/// Mock card enumerator for testing without real PulseAudio hardware.
/// Provides fake sound cards with typical USB DAC profiles.
/// </summary>
public static class MockCardEnumerator
{
    private static ILogger? _logger;
    private static readonly Dictionary<int, string> _activeProfiles = new();
    private static readonly Dictionary<int, bool> _muteStates = new();

    /// <summary>
    /// Standard surround profiles available on all cards.
    /// These profiles are needed for the remap sink user flow.
    /// </summary>
    private static readonly (string Name, string Description, int Sinks, int Priority, bool Available)[] StandardSurroundProfiles = new[]
    {
        ("output:analog-stereo", "Analog Stereo Output", 1, 5600, true),
        ("output:analog-surround-51", "Analog Surround 5.1 Output", 1, 5500, true),
        ("output:analog-surround-71", "Analog Surround 7.1 Output", 1, 5400, true),
        ("output:iec958-stereo", "Digital Stereo (IEC958) Output", 1, 5300, true),
        ("off", "Off", 0, 0, true)
    };

    /// <summary>
    /// Pre-configured mock cards simulating typical USB DAC setups.
    /// All cards have stereo, 5.1, and 7.1 surround profiles for remap sink testing.
    /// </summary>
    private static readonly List<MockCardConfig> MockCardConfigs = new()
    {
        new(0, "alsa_card.mock_living_room_dac", "Living Room DAC", StandardSurroundProfiles),
        new(1, "alsa_card.mock_kitchen_speakers", "Kitchen Speakers", StandardSurroundProfiles),
        new(2, "alsa_card.mock_bedroom_stereo", "Bedroom Stereo", StandardSurroundProfiles),
        new(3, "alsa_card.mock_office_speakers", "Office Speakers", StandardSurroundProfiles),
        new(4, "alsa_card.mock_garage_audio", "Garage Audio", StandardSurroundProfiles),
        new(5, "alsa_card.mock_patio_speakers", "Patio Speakers", StandardSurroundProfiles),
        new(6, "alsa_card.mock_basement_system", "Basement System", StandardSurroundProfiles),
        new(7, "alsa_card.mock_guest_room", "Guest Room Audio", StandardSurroundProfiles),
    };

    private record MockCardConfig(
        int Index,
        string Name,
        string Description,
        (string Name, string Description, int Sinks, int Priority, bool Available)[] Profiles
    );

    /// <summary>
    /// Configures the logger for card enumeration diagnostics.
    /// </summary>
    public static void SetLogger(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all available mock sound cards with their profiles.
    /// </summary>
    public static IEnumerable<PulseAudioCard> GetCards()
    {
        _logger?.LogDebug("Returning {Count} mock cards", MockCardConfigs.Count);

        return MockCardConfigs.Select(config =>
        {
            var profiles = config.Profiles.Select(p => new CardProfile(
                Name: p.Name,
                Description: p.Description,
                Sinks: p.Sinks,
                Sources: 0,
                Priority: p.Priority,
                IsAvailable: p.Available
            )).ToList();

            var activeProfile = _activeProfiles.GetValueOrDefault(config.Index)
                ?? profiles.FirstOrDefault(p => p.Name != "off")?.Name
                ?? "output:analog-stereo";

            var isMuted = _muteStates.GetValueOrDefault(config.Index, false);

            return new PulseAudioCard(
                Index: config.Index,
                Name: config.Name,
                Driver: "module-alsa-card.c",
                Description: config.Description,
                Profiles: profiles,
                ActiveProfile: activeProfile,
                IsMuted: isMuted,
                BootMuted: null,
                BootMuteMatchesCurrent: false,
                MaxVolume: null
            );
        }).ToList();
    }

    /// <summary>
    /// Gets a specific card by name or index.
    /// </summary>
    public static PulseAudioCard? GetCard(string cardNameOrIndex)
    {
        if (int.TryParse(cardNameOrIndex, out var index))
        {
            return GetCards().FirstOrDefault(c => c.Index == index);
        }

        return GetCards()
            .FirstOrDefault(c =>
                c.Name.Equals(cardNameOrIndex, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(cardNameOrIndex, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sets the active profile for a mock card.
    /// </summary>
    public static bool SetCardProfile(string cardNameOrIndex, string profileName, out string? errorMessage)
    {
        var card = GetCard(cardNameOrIndex);
        if (card == null)
        {
            errorMessage = $"Card '{cardNameOrIndex}' not found.";
            return false;
        }

        var profile = card.Profiles.FirstOrDefault(p =>
            p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

        if (profile == null)
        {
            var availableProfiles = string.Join(", ", card.Profiles.Select(p => p.Name));
            errorMessage = $"Profile '{profileName}' not found. Available: {availableProfiles}";
            return false;
        }

        _activeProfiles[card.Index] = profileName;
        _logger?.LogInformation("Mock card '{Card}' profile set to '{Profile}'", card.Name, profileName);

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Gets all sink names belonging to a specific card.
    /// </summary>
    public static List<string> GetSinksByCard(int cardIndex)
    {
        var config = MockCardConfigs.FirstOrDefault(c => c.Index == cardIndex);
        if (config == null)
            return new List<string>();

        // Return the corresponding mock sink name
        var sinkId = config.Name.Replace("alsa_card.", "mock_");
        return new List<string> { sinkId };
    }

    /// <summary>
    /// Gets mute state for all sinks.
    /// </summary>
    public static Dictionary<string, bool> GetSinksMuteStates()
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var config in MockCardConfigs)
        {
            var sinkId = config.Name.Replace("alsa_card.", "mock_");
            result[sinkId] = _muteStates.GetValueOrDefault(config.Index, false);
        }
        return result;
    }

    /// <summary>
    /// Sets mute state for a card.
    /// </summary>
    public static bool SetMute(int cardIndex, bool muted)
    {
        if (!MockCardConfigs.Any(c => c.Index == cardIndex))
            return false;

        _muteStates[cardIndex] = muted;
        _logger?.LogDebug("Mock card {Index} mute set to {Muted}", cardIndex, muted);
        return true;
    }
}
