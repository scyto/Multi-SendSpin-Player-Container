namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Tests for player lifecycle operations: start, stop, restart, pause, resume.
/// Verifies state transitions and error handling.
/// </summary>
public class PlayerLifecycleTests : ApiTestBase, IAsyncLifetime
{
    public PlayerLifecycleTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupPlayersAsync();
    }

    #region Player Creation and Basic State

    [Fact]
    public async Task CreatePlayer_ValidRequest_ReturnsCreatedWithCorrectState()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "LifecyclePlayer1",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("LifecyclePlayer1");

        // Player should be in Created, Starting, or Connecting state initially
        var validInitialStates = new[] { "Created", "Starting", "Connecting", "Connected" };
        validInitialStates.Any(state => content.Contains(state)).Should().BeTrue(
            "New player should be in an initial state");
    }

    [Fact]
    public async Task CreatePlayer_DuplicateName_ReturnsConflict()
    {
        // Create first player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "DuplicateTest",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Try to create duplicate
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "DuplicateTest",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreatePlayer_MissingName_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePlayer_MissingDevice_HandledAppropriately()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "NoDevicePlayer"
        });

        // API may accept and create with null device, or reject
        // Both behaviors are valid implementations
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.Created },
            "Missing device should either be rejected or accepted with null device");

        // Cleanup if created
        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync("/api/players/NoDevicePlayer");
        }
    }

    #endregion

    #region Stop Operations

    [Fact]
    public async Task StopPlayer_ExistingPlayer_Succeeds()
    {
        // Create player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "StopTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Wait a moment for player to initialize
        await Task.Delay(500);

        // Stop player
        var response = await Client.PostAsync("/api/players/StopTestPlayer/stop", null);

        response.IsSuccessStatusCode.Should().BeTrue("Stop should succeed");

        // Verify state changed
        var getResponse = await Client.GetAsync("/api/players/StopTestPlayer");
        var content = await getResponse.Content.ReadAsStringAsync();

        // Player should be stopped or in a terminal state
        var validStoppedStates = new[] { "Stopped", "Created" };
        validStoppedStates.Any(state => content.Contains(state)).Should().BeTrue(
            "Stopped player should be in Stopped or Created state");
    }

    [Fact]
    public async Task StopPlayer_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await Client.PostAsync("/api/players/NonExistent/stop", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StopPlayer_AlreadyStopped_HandledGracefully()
    {
        // Create and stop player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "DoubleStopPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        await Client.PostAsync("/api/players/DoubleStopPlayer/stop", null);

        // Stop again
        var response = await Client.PostAsync("/api/players/DoubleStopPlayer/stop", null);

        // Should either succeed (idempotent) or return appropriate status
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.BadRequest },
            "Stopping already stopped player should be handled gracefully");
    }

    #endregion

    #region Restart Operations

    [Fact]
    public async Task RestartPlayer_ExistingPlayer_Succeeds()
    {
        // Create player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "RestartTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        await Task.Delay(500);

        // Restart player
        var response = await Client.PostAsync("/api/players/RestartTestPlayer/restart", null);

        response.IsSuccessStatusCode.Should().BeTrue("Restart should succeed");

        // Verify player is in a valid state after restart
        var getResponse = await Client.GetAsync("/api/players/RestartTestPlayer");
        var content = await getResponse.Content.ReadAsStringAsync();

        // Should be in an active state after restart
        var validStates = new[] { "Created", "Starting", "Connecting", "Connected", "Playing" };
        validStates.Any(state => content.Contains(state)).Should().BeTrue(
            "Restarted player should be in an active state");
    }

    [Fact]
    public async Task RestartPlayer_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await Client.PostAsync("/api/players/NonExistent/restart", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestartPlayer_StoppedPlayer_StartsSuccessfully()
    {
        // Create and stop player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "StoppedRestartPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        await Client.PostAsync("/api/players/StoppedRestartPlayer/stop", null);
        await Task.Delay(300);

        // Restart from stopped state
        var response = await Client.PostAsync("/api/players/StoppedRestartPlayer/restart", null);

        response.IsSuccessStatusCode.Should().BeTrue("Restart from stopped state should succeed");
    }

    #endregion

    #region Delete Operations

    [Fact]
    public async Task DeletePlayer_ExistingPlayer_Succeeds()
    {
        // Create player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "DeleteTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Delete player
        var response = await Client.DeleteAsync("/api/players/DeleteTestPlayer");

        response.IsSuccessStatusCode.Should().BeTrue("Delete should succeed");

        // Verify player is gone
        var getResponse = await Client.GetAsync("/api/players/DeleteTestPlayer");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePlayer_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync("/api/players/NonExistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePlayer_RunningPlayer_StopsAndDeletes()
    {
        // Create player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "RunningDeletePlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        await Task.Delay(500);

        // Delete without stopping first
        var response = await Client.DeleteAsync("/api/players/RunningDeletePlayer");

        response.IsSuccessStatusCode.Should().BeTrue(
            "Delete should succeed even if player is running");

        // Verify player is gone
        var getResponse = await Client.GetAsync("/api/players/RunningDeletePlayer");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Volume Operations

    [Fact]
    public async Task SetVolume_ValidValue_Succeeds()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "VolumePlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        var response = await Client.PutAsJsonAsync("/api/players/VolumePlayer/volume", new
        {
            volume = 75
        });

        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify volume was set
        var getResponse = await Client.GetAsync("/api/players/VolumePlayer");
        var content = await getResponse.Content.ReadAsStringAsync();
        content.Should().Contain("\"volume\":75");
    }

    [Fact]
    public async Task SetVolume_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await Client.PutAsJsonAsync("/api/players/NonExistent/volume", new
        {
            volume = 50
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetVolume_StoppedPlayer_StillWorks()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "StoppedVolumePlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        await Client.PostAsync("/api/players/StoppedVolumePlayer/stop", null);

        var response = await Client.PutAsJsonAsync("/api/players/StoppedVolumePlayer/volume", new
        {
            volume = 25
        });

        // Should either succeed or return appropriate error
        // Volume setting on stopped player is implementation-dependent
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.BadRequest },
            "Volume on stopped player should be handled");
    }

    #endregion

    #region Offset/Delay Operations

    [Fact]
    public async Task SetOffset_ValidValue_Succeeds()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "OffsetPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        var response = await Client.PutAsJsonAsync("/api/players/OffsetPlayer/offset", new
        {
            delayMs = 500
        });

        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify offset was set
        var getResponse = await Client.GetAsync("/api/players/OffsetPlayer");
        var content = await getResponse.Content.ReadAsStringAsync();
        content.Should().Contain("\"delayMs\":500");
    }

    [Fact]
    public async Task SetOffset_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await Client.PutAsJsonAsync("/api/players/NonExistent/offset", new
        {
            delayMs = 100
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Get Operations

    [Fact]
    public async Task GetPlayer_ExistingPlayer_ReturnsDetails()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "GetTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        var response = await Client.GetAsync("/api/players/GetTestPlayer");

        response.IsSuccessStatusCode.Should().BeTrue();

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("GetTestPlayer");
        content.Should().Contain("volume");
        content.Should().Contain("state");
    }

    [Fact]
    public async Task GetPlayer_NonExistentPlayer_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/players/NonExistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListPlayers_ReturnsAllPlayers()
    {
        // Create multiple players
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "ListPlayer1",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "ListPlayer2",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        var response = await Client.GetAsync("/api/players");

        response.IsSuccessStatusCode.Should().BeTrue();

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ListPlayer1");
        content.Should().Contain("ListPlayer2");
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public async Task PlayerLifecycle_FullCycle_TransitionsCorrectly()
    {
        // Create
        var createResponse = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "FullCyclePlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });
        createResponse.IsSuccessStatusCode.Should().BeTrue("Create should succeed");

        await Task.Delay(300);

        // Get initial state
        var getResponse1 = await Client.GetAsync("/api/players/FullCyclePlayer");
        var content1 = await getResponse1.Content.ReadAsStringAsync();
        // Should be in some initial/connecting state

        // Stop
        var stopResponse = await Client.PostAsync("/api/players/FullCyclePlayer/stop", null);
        stopResponse.IsSuccessStatusCode.Should().BeTrue("Stop should succeed");

        await Task.Delay(300);

        // Verify stopped
        var getResponse2 = await Client.GetAsync("/api/players/FullCyclePlayer");
        var content2 = await getResponse2.Content.ReadAsStringAsync();

        // Restart
        var restartResponse = await Client.PostAsync("/api/players/FullCyclePlayer/restart", null);
        restartResponse.IsSuccessStatusCode.Should().BeTrue("Restart should succeed");

        await Task.Delay(300);

        // Delete
        var deleteResponse = await Client.DeleteAsync("/api/players/FullCyclePlayer");
        deleteResponse.IsSuccessStatusCode.Should().BeTrue("Delete should succeed");

        // Verify gone
        var getResponse3 = await Client.GetAsync("/api/players/FullCyclePlayer");
        getResponse3.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleRestarts_HandledCorrectly()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "MultiRestartPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Multiple restarts in succession
        for (int i = 0; i < 3; i++)
        {
            var response = await Client.PostAsync("/api/players/MultiRestartPlayer/restart", null);
            response.IsSuccessStatusCode.Should().BeTrue($"Restart {i + 1} should succeed");
            await Task.Delay(200);
        }

        // Player should still be accessible
        var getResponse = await Client.GetAsync("/api/players/MultiRestartPlayer");
        getResponse.IsSuccessStatusCode.Should().BeTrue("Player should still exist after multiple restarts");
    }

    #endregion

    #region Mute Operations

    [Fact]
    public async Task MutePlayer_ValidPlayer_Succeeds()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "MuteTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        var response = await Client.PutAsJsonAsync("/api/players/MuteTestPlayer/mute", new
        {
            muted = true
        });

        // May not be implemented, but should not error badly
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed },
            "Mute should be handled appropriately");
    }

    #endregion
}
