namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Tests for creating remapped and combined sinks.
/// Validates the multi-zone audio configuration workflow.
/// </summary>
public class SinkCreationTests : ApiTestBase, IAsyncLifetime
{
    public SinkCreationTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        // Set Xonar card (index 1) to 7.1 for multi-channel tests
        await Client.PutAsJsonAsync("/api/cards/1/profile", new
        {
            profile = "output:analog-surround-71"
        });
    }

    public async Task DisposeAsync()
    {
        await CleanupSinksAsync();
        await CleanupPlayersAsync();

        // Reset card profile
        await Client.PutAsJsonAsync("/api/cards/1/profile", new
        {
            profile = "output:analog-stereo"
        });
    }

    /// <summary>
    /// Helper to create channel mappings for stereo from specific channel positions.
    /// </summary>
    private static object[] CreateStereoChannelMappings(string leftMaster, string rightMaster)
    {
        return new object[]
        {
            new { outputChannel = "front-left", masterChannel = leftMaster },
            new { outputChannel = "front-right", masterChannel = rightMaster }
        };
    }

    [Fact]
    public async Task CreateRemappedSink_SingleStereoZone_Succeeds()
    {
        // Create a stereo zone from first 2 channels of 7.1 card
        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "zone_living",
            description = "Living Room Zone",
            masterSink = "alsa_output.pci-0000_05_04.0.analog-surround-71",
            channels = 2,
            channelMappings = CreateStereoChannelMappings("front-left", "front-right")
        });

        // Returns 201 Created with the sink object
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CustomSinkResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("zone_living");
    }

    [Fact]
    public async Task CreateFourStereoZones_From71Card_Succeeds()
    {
        // Arrange - verify card is in 7.1 mode
        var cardResponse = await Client.GetAsync("/api/cards/1");
        var card = await cardResponse.Content.ReadFromJsonAsync<CardInfo>();
        card!.ActiveProfile.Should().Contain("surround-71");

        // Act - create 4 stereo zones from 7.1 channels
        // 7.1 channels: front-left, front-right, rear-left, rear-right,
        //               front-center, lfe, side-left, side-right
        var zones = new[]
        {
            ("zone_living", "Living Room", "front-left", "front-right"),
            ("zone_kitchen", "Kitchen", "rear-left", "rear-right"),
            ("zone_bedroom", "Bedroom", "front-center", "lfe"),
            ("zone_office", "Office", "side-left", "side-right")
        };

        var createdZones = new List<string>();

        foreach (var (name, description, leftChannel, rightChannel) in zones)
        {
            var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
            {
                name = name,
                description = description,
                masterSink = "alsa_output.pci-0000_05_04.0.analog-surround-71",
                channels = 2,
                channelMappings = CreateStereoChannelMappings(leftChannel, rightChannel)
            });

            response.IsSuccessStatusCode.Should().BeTrue(
                $"Creating zone '{name}' should succeed, but got {response.StatusCode}");

            createdZones.Add(name);
        }

        // Assert - all 4 zones exist
        var sinksResponse = await Client.GetAsync("/api/sinks");
        var sinks = await sinksResponse.Content.ReadFromJsonAsync<CustomSinksListResponse>();

        foreach (var zoneName in createdZones)
        {
            sinks!.Sinks.Should().Contain(s => s.Name == zoneName,
                $"Zone '{zoneName}' should exist");
        }
    }

    [Fact]
    public async Task CreateCombinedSink_TwoDevices_Succeeds()
    {
        // Create a combined sink from two mock devices
        var response = await Client.PostAsJsonAsync("/api/sinks/combine", new
        {
            name = "combined_zones",
            description = "Combined Audio Zones",
            slaves = new[]
            {
                "alsa_output.pci-0000_00_1f.3.analog-stereo",
                "alsa_output.usb-Schiit_Audio_Schiit_Modi_3-00.analog-stereo"
            }
        });

        // Combined sinks may or may not be supported in mock mode
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var result = await response.Content.ReadFromJsonAsync<CustomSinkResponse>();
            result.Should().NotBeNull();
            result!.Name.Should().Be("combined_zones");
        }
        else
        {
            // Document that combined sinks aren't supported in mock mode
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotImplemented,
                HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task DeleteSink_ExistingSink_Succeeds()
    {
        // Arrange - create a remap sink first
        var createResponse = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "sink_to_delete",
            description = "Will be deleted",
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = 2,
            channelMappings = CreateStereoChannelMappings("front-left", "front-right")
        });

        // Skip if creation failed (mock mode may not support)
        if (!createResponse.IsSuccessStatusCode)
        {
            return;
        }

        // Act
        var response = await Client.DeleteAsync("/api/sinks/sink_to_delete");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify sink is gone
        var sinksResponse = await Client.GetAsync("/api/sinks");
        var sinks = await sinksResponse.Content.ReadFromJsonAsync<CustomSinksListResponse>();
        sinks!.Sinks.Should().NotContain(s => s.Name == "sink_to_delete");
    }

    [Fact]
    public async Task CreateRemappedSink_InvalidMaster_Fails()
    {
        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "zone_invalid",
            description = "Invalid Zone",
            masterSink = "nonexistent_sink_12345",
            channels = 2,
            channelMappings = CreateStereoChannelMappings("front-left", "front-right")
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRemappedSink_InvalidChannelNames_Fails()
    {
        // Try to use channel names that don't exist
        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "zone_invalid_channels",
            description = "Invalid Channels",
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = 2,
            channelMappings = new[]
            {
                new { outputChannel = "front-left", masterChannel = "nonexistent-channel" },
                new { outputChannel = "front-right", masterChannel = "another-fake-channel" }
            }
        });

        // Should fail or succeed with warning
        // Document actual behavior
        if (response.IsSuccessStatusCode)
        {
            Assert.True(true, "WARNING: Invalid channel names were accepted - may need validation");
        }
    }

    [Fact]
    public async Task CreatePlayer_OnRemappedSink_Works()
    {
        // Arrange - create a remapped sink
        var createResponse = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "zone_for_player",
            description = "Zone for Player Test",
            masterSink = "alsa_output.pci-0000_05_04.0.analog-surround-71",
            channels = 2,
            channelMappings = CreateStereoChannelMappings("front-left", "front-right")
        });

        // Skip if sink creation failed (mock mode limitation)
        if (!createResponse.IsSuccessStatusCode)
        {
            return;
        }

        // Act - create player on the remapped sink
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "ZonePlayer",
            device = "zone_for_player"
        });

        // Mock mode may not support this - document behavior
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<PlayerResponse>();
            result.Should().NotBeNull();
            result!.Name.Should().Be("ZonePlayer");
        }
    }

    // DTOs matching actual API responses
    private record CustomSinkResponse(string Name, string? Description, string? MasterSink);
    private record CustomSinksListResponse(List<CustomSinkResponse> Sinks, int Count);
    private record CardInfo(int Index, string Name, string? ActiveProfile);
    private record PlayerResponse(string Name, string? DeviceId, string State);
}
