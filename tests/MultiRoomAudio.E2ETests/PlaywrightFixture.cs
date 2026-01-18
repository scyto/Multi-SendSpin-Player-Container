using System.Diagnostics;

namespace MultiRoomAudio.E2ETests;

/// <summary>
/// Fixture that manages both the test server and Playwright browser instances.
/// Starts the actual application as a separate process so Playwright can connect via HTTP.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private HttpClient? _httpClient;
    private string _configPath = string.Empty;
    private int _port;

    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Browser not initialized");
    public HttpClient HttpClient => _httpClient ?? throw new InvalidOperationException("HttpClient not initialized");
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Create temp config directory
        _configPath = Path.Combine(Path.GetTempPath(), $"multiroom-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configPath);

        // Also create logs directory in temp to avoid /app/logs issue
        var logPath = Path.Combine(_configPath, "logs");
        Directory.CreateDirectory(logPath);

        // Find an available port
        _port = GetAvailablePort();
        BaseUrl = $"http://localhost:{_port}";

        // Find the built application DLL
        var projectDir = FindProjectDirectory();
        var appDll = Path.Combine(projectDir, "src", "MultiRoomAudio", "bin", "Debug", "net8.0", "osx-arm64", "MultiRoomAudio.dll");

        if (!File.Exists(appDll))
        {
            // Try without runtime identifier
            appDll = Path.Combine(projectDir, "src", "MultiRoomAudio", "bin", "Debug", "net8.0", "MultiRoomAudio.dll");
        }

        if (!File.Exists(appDll))
        {
            throw new FileNotFoundException($"Could not find MultiRoomAudio.dll. Ensure the project is built. Searched: {appDll}");
        }

        // Get the source directory (for static files/wwwroot)
        var srcDir = Path.Combine(projectDir, "src", "MultiRoomAudio");

        // Start the application as a separate process
        // Working directory must be src/MultiRoomAudio for static files to be found
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{appDll}\"",
            WorkingDirectory = srcDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["MOCK_HARDWARE"] = "true",
                ["CONFIG_PATH"] = _configPath,
                ["LOG_PATH"] = logPath,
                ["LOG_LEVEL"] = "Warning",
                ["WEB_PORT"] = _port.ToString(),
                ["ASPNETCORE_URLS"] = BaseUrl
            }
        };

        _serverProcess = new Process { StartInfo = startInfo };
        _serverProcess.Start();

        // Wait for the server to be ready
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };

        var maxRetries = 30;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/health");
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch
            {
                // Server not ready yet
            }

            if (i == maxRetries - 1)
            {
                throw new TimeoutException($"Server did not start within {maxRetries} seconds");
            }

            await Task.Delay(1000);
        }

        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true, // Set to false for debugging
            SlowMo = 0 // Set to 100+ for debugging
        });
    }

    private static string FindProjectDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "squeezelite-docker.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find project root directory (containing squeezelite-docker.sln)");
    }

    private static int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _httpClient?.Dispose();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill();
            await _serverProcess.WaitForExitAsync();
            _serverProcess.Dispose();
        }

        // Cleanup temp directory
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

    /// <summary>
    /// Creates a new browser page for a test.
    /// </summary>
    public async Task<IPage> CreatePageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });

        return await context.NewPageAsync();
    }
}

/// <summary>
/// Collection definition for E2E tests sharing the same Playwright instance.
/// </summary>
[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
}
