namespace MultiRoomAudio.Services;

/// <summary>
/// Provides centralized access to application version information.
/// </summary>
public class VersionService
{
    /// <summary>
    /// Gets the application version (e.g., "1.2.3" or "dev").
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the build SHA (short commit hash) if available.
    /// </summary>
    public string? BuildSha { get; }

    /// <summary>
    /// Gets the build date if available.
    /// </summary>
    public string? BuildDate { get; }

    /// <summary>
    /// Gets the formatted model string for display in Music Assistant.
    /// Format: "v{version} ({sha})" or "v{version}" if SHA unavailable.
    /// </summary>
    public string ModelString { get; }

    /// <summary>
    /// Gets the software version (version number without 'v' prefix or SHA).
    /// </summary>
    public string SoftwareVersion { get; }

    public VersionService()
    {
        Version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
        BuildSha = Environment.GetEnvironmentVariable("APP_BUILD_SHA");
        BuildDate = Environment.GetEnvironmentVariable("APP_BUILD_DATE");

        // Format model string: "v1.2.3 (abc123f)" or "v1.2.3"
        ModelString = !string.IsNullOrEmpty(BuildSha)
            ? $"v{Version} ({BuildSha})"
            : $"v{Version}";

        // Software version is just the version number
        SoftwareVersion = Version;
    }
}
