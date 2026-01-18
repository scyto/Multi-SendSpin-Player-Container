using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Test server factory that runs with MOCK_HARDWARE=true.
/// Provides isolated test environment without real audio hardware.
/// </summary>
public class MockHardwareWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _configPath;

    public MockHardwareWebApplicationFactory()
    {
        // Create unique temp directory for each test run
        _configPath = Path.Combine(Path.GetTempPath(), $"multiroom-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configPath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Force mock hardware mode
        Environment.SetEnvironmentVariable("MOCK_HARDWARE", "true");
        Environment.SetEnvironmentVariable("CONFIG_PATH", _configPath);
        Environment.SetEnvironmentVariable("LOG_LEVEL", "Warning"); // Reduce noise in tests

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Any additional test-specific service overrides can go here
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Clean up temp config directory
            try
            {
                if (Directory.Exists(_configPath))
                {
                    Directory.Delete(_configPath, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

/// <summary>
/// Collection definition for tests sharing the same factory instance.
/// Use [Collection("MockHardware")] on test classes that need shared state.
/// </summary>
[CollectionDefinition("MockHardware")]
public class MockHardwareCollection : ICollectionFixture<MockHardwareWebApplicationFactory>
{
}
