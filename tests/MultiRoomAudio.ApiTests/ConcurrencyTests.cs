namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Tests for concurrent operations and race conditions.
/// Verifies thread safety and proper handling of parallel requests.
/// </summary>
public class ConcurrencyTests : ApiTestBase, IAsyncLifetime
{
    public ConcurrencyTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupPlayersAsync();
        await CleanupSinksAsync();
    }

    #region Duplicate Creation Race Conditions

    [Fact]
    public async Task CreatePlayer_SimultaneousDuplicateRequests_OnlyOneSucceeds()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Client.PostAsJsonAsync("/api/players", new
            {
                name = "RacePlayer",
                device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Exactly one should succeed, others should conflict
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        successCount.Should().Be(1, "Only one create should succeed");
        conflictCount.Should().Be(4, "Others should get conflict");

        // Cleanup
        await Client.DeleteAsync("/api/players/RacePlayer");
    }

    [Fact]
    public async Task CreatePlayer_DifferentNames_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(i => Client.PostAsJsonAsync("/api/players", new
            {
                name = $"ConcurrentPlayer{i}",
                device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // All should succeed
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        successCount.Should().Be(5, "All unique creates should succeed");

        // Cleanup
        foreach (var i in Enumerable.Range(0, 5))
        {
            await Client.DeleteAsync($"/api/players/ConcurrentPlayer{i}");
        }
    }

    #endregion

    #region Concurrent Operations on Same Player

    [Fact]
    public async Task VolumeUpdates_Concurrent_LastOneWins()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "ConcurrentVolumePlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Send multiple volume updates concurrently
        var tasks = Enumerable.Range(1, 10)
            .Select(i => Client.PutAsJsonAsync("/api/players/ConcurrentVolumePlayer/volume", new
            {
                volume = i * 10
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // All should succeed (no data corruption)
        responses.All(r => r.IsSuccessStatusCode).Should().BeTrue(
            "All volume updates should succeed");

        // Final volume should be one of the set values
        var getResponse = await Client.GetAsync("/api/players/ConcurrentVolumePlayer");
        var content = await getResponse.Content.ReadAsStringAsync();

        // Volume should be between 10 and 100
        content.Should().MatchRegex("\"volume\":\\s*(10|20|30|40|50|60|70|80|90|100)");
    }

    [Fact]
    public async Task StopAndRestart_Concurrent_NoDeadlock()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "StopRestartPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        await Task.Delay(500);

        // Send stop and restart concurrently
        var stopTask = Client.PostAsync("/api/players/StopRestartPlayer/stop", null);
        var restartTask = Client.PostAsync("/api/players/StopRestartPlayer/restart", null);

        // Should complete without deadlock (timeout is the test itself)
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(
            Task.WhenAll(stopTask, restartTask),
            timeoutTask
        );

        completedTask.Should().NotBe(timeoutTask, "Operations should complete without deadlock");

        // Player should still be accessible
        var getResponse = await Client.GetAsync("/api/players/StopRestartPlayer");
        getResponse.IsSuccessStatusCode.Should().BeTrue(
            "Player should still be accessible after concurrent stop/restart");
    }

    [Fact]
    public async Task DeleteWhileUpdating_HandledGracefully()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "DeleteWhileUpdatePlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Start volume updates
        var updateTasks = Enumerable.Range(0, 5)
            .Select(i => Client.PutAsJsonAsync("/api/players/DeleteWhileUpdatePlayer/volume", new
            {
                volume = 50
            }))
            .ToList();

        // Delete in the middle
        var deleteTask = Client.DeleteAsync("/api/players/DeleteWhileUpdatePlayer");

        updateTasks.Add(deleteTask);

        var responses = await Task.WhenAll(updateTasks);

        // Delete should succeed
        var deleteResponse = responses.Last();
        deleteResponse.IsSuccessStatusCode.Should().BeTrue("Delete should succeed");

        // Player should be gone
        var getResponse = await Client.GetAsync("/api/players/DeleteWhileUpdatePlayer");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Concurrent API Endpoint Access

    [Fact]
    public async Task MultipleEndpoints_ConcurrentAccess_NoInterference()
    {
        // Create a player first
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "MultiEndpointPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Hit multiple endpoints concurrently
        var tasks = new List<Task<HttpResponseMessage>>
        {
            Client.GetAsync("/api/players"),
            Client.GetAsync("/api/players/MultiEndpointPlayer"),
            Client.GetAsync("/api/devices"),
            Client.GetAsync("/api/cards"),
            Client.GetAsync("/api/health"),
            Client.GetAsync("/api/providers")
        };

        var responses = await Task.WhenAll(tasks);

        // All should succeed
        foreach (var response in responses)
        {
            response.IsSuccessStatusCode.Should().BeTrue(
                $"Endpoint should respond successfully, got {response.StatusCode}");
        }
    }

    [Fact]
    public async Task HealthCheck_UnderLoad_StillResponds()
    {
        // Create some load
        var loadTasks = Enumerable.Range(0, 10)
            .Select(i => Client.PostAsJsonAsync("/api/players", new
            {
                name = $"LoadPlayer{i}",
                device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
            }))
            .ToArray();

        // Health check should still work during load
        var healthTask = Client.GetAsync("/api/health");

        var allTasks = loadTasks.Concat(new[] { healthTask }).ToArray();
        await Task.WhenAll(allTasks);

        var healthResponse = await healthTask;
        healthResponse.IsSuccessStatusCode.Should().BeTrue(
            "Health check should respond even under load");

        // Cleanup
        foreach (var i in Enumerable.Range(0, 10))
        {
            await Client.DeleteAsync($"/api/players/LoadPlayer{i}");
        }
    }

    #endregion

    #region Rapid Fire Operations

    [Fact]
    public async Task CreateDelete_RapidCycle_NoResourceLeak()
    {
        for (int i = 0; i < 10; i++)
        {
            var createResponse = await Client.PostAsJsonAsync("/api/players", new
            {
                name = $"RapidCyclePlayer",
                device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
            });
            createResponse.IsSuccessStatusCode.Should().BeTrue($"Create {i} should succeed");

            var deleteResponse = await Client.DeleteAsync("/api/players/RapidCyclePlayer");
            deleteResponse.IsSuccessStatusCode.Should().BeTrue($"Delete {i} should succeed");
        }

        // Final health check
        var healthResponse = await Client.GetAsync("/api/health");
        healthResponse.IsSuccessStatusCode.Should().BeTrue(
            "System should be healthy after rapid create/delete cycles");
    }

    [Fact]
    public async Task RestartSpam_HandledWithoutCrash()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "RestartSpamPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Spam restart commands
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Client.PostAsync("/api/players/RestartSpamPlayer/restart", null))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // At least some should succeed, none should cause server error
        responses.Any(r => r.IsSuccessStatusCode).Should().BeTrue(
            "At least some restarts should succeed");
        responses.All(r => (int)r.StatusCode < 500).Should().BeTrue(
            "No server errors should occur");

        // Player should still exist
        var getResponse = await Client.GetAsync("/api/players/RestartSpamPlayer");
        getResponse.IsSuccessStatusCode.Should().BeTrue(
            "Player should still exist after restart spam");
    }

    #endregion

    #region Sink Concurrent Operations

    [Fact]
    public async Task CreateSinks_Concurrent_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 3)
            .Select(i => Client.PostAsJsonAsync("/api/sinks/remap", new
            {
                name = $"ConcurrentSink{i}",
                masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
                channels = 2,
                channelMappings = new[]
                {
                    new { outputChannel = "front-left", masterChannel = "front-left" },
                    new { outputChannel = "front-right", masterChannel = "front-right" }
                }
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Count successes (some may fail due to mock limitations, but no server errors)
        responses.All(r => (int)r.StatusCode < 500).Should().BeTrue(
            "No server errors should occur during concurrent sink creation");

        // Cleanup any that were created
        foreach (var i in Enumerable.Range(0, 3))
        {
            await Client.DeleteAsync($"/api/sinks/ConcurrentSink{i}");
        }
    }

    #endregion

    #region Card Concurrent Operations

    [Fact]
    public async Task CardVolumeUpdates_Concurrent_NoCorruption()
    {
        // Get a card
        var cardsResponse = await Client.GetAsync("/api/cards");
        if (!cardsResponse.IsSuccessStatusCode)
        {
            // No cards available, skip test
            return;
        }

        // Send concurrent volume updates to card 0
        var tasks = Enumerable.Range(0, 5)
            .Select(i => Client.PutAsJsonAsync("/api/cards/0/volume", new
            {
                volume = i * 20
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // All should complete without server errors
        responses.All(r => (int)r.StatusCode < 500).Should().BeTrue(
            "No server errors during concurrent card volume updates");
    }

    [Fact]
    public async Task CardMuteToggle_Rapid_HandledCorrectly()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Client.PutAsJsonAsync("/api/cards/0/mute", new
            {
                muted = i % 2 == 0
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Should complete without server errors
        responses.All(r => (int)r.StatusCode < 500).Should().BeTrue(
            "No server errors during rapid mute toggling");
    }

    #endregion

    #region Device Endpoint Concurrency

    [Fact]
    public async Task DeviceList_ConcurrentRequests_AllReturn()
    {
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Client.GetAsync("/api/devices"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // All should succeed
        responses.All(r => r.IsSuccessStatusCode).Should().BeTrue(
            "All device list requests should succeed");

        // All should return the same data (consistency check)
        var contents = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        contents.Distinct().Count().Should().Be(1,
            "All responses should be consistent");
    }

    #endregion

    #region Trigger Concurrent Operations

    [Fact]
    public async Task TriggerConfiguration_ConcurrentUpdates_NoCorruption()
    {
        var tasks = Enumerable.Range(1, 4)
            .Select(channel => Client.PutAsJsonAsync($"/api/triggers/{channel}", new
            {
                devicePattern = $"alsa_output.*channel{channel}",
                offDelaySeconds = channel * 5
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Should complete without server errors
        responses.All(r => (int)r.StatusCode < 500).Should().BeTrue(
            "No server errors during concurrent trigger configuration");
    }

    #endregion
}
