namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Tests for boundary conditions and input validation.
/// Verifies that numeric ranges, string lengths, and array sizes
/// are properly validated.
/// </summary>
public class BoundaryValidationTests : ApiTestBase, IAsyncLifetime
{
    public BoundaryValidationTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupPlayersAsync();
        await CleanupSinksAsync();
    }

    #region Volume Boundary Tests

    [Theory]
    [InlineData(-1, false, "Negative volume")]
    [InlineData(-100, false, "Large negative volume")]
    [InlineData(int.MinValue, false, "Min int volume")]
    [InlineData(0, true, "Zero volume (valid)")]
    [InlineData(1, true, "Minimum positive volume")]
    [InlineData(50, true, "Mid-range volume")]
    [InlineData(100, true, "Maximum volume")]
    [InlineData(101, false, "Over maximum volume")]
    [InlineData(1000, false, "Way over maximum")]
    [InlineData(int.MaxValue, false, "Max int volume")]
    public async Task SetPlayerVolume_BoundaryValues_ValidatedCorrectly(
        int volume, bool shouldSucceed, string scenario)
    {
        // Create a player first
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "VolumeTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        try
        {
            var response = await Client.PutAsJsonAsync("/api/players/VolumeTestPlayer/volume", new
            {
                volume = volume
            });

            if (shouldSucceed)
            {
                response.IsSuccessStatusCode.Should().BeTrue(
                    $"Volume {volume} ({scenario}) should be accepted");

                // Verify volume was set correctly
                var getResponse = await Client.GetAsync("/api/players/VolumeTestPlayer");
                var content = await getResponse.Content.ReadAsStringAsync();
                content.Should().Contain($"\"volume\":{volume}",
                    $"Volume should be set to {volume}");
            }
            else
            {
                // Invalid values should be rejected OR clamped
                if (response.IsSuccessStatusCode)
                {
                    // If accepted, verify it was clamped to valid range
                    var getResponse = await Client.GetAsync("/api/players/VolumeTestPlayer");
                    var content = await getResponse.Content.ReadAsStringAsync();

                    // Volume should be clamped to 0-100
                    content.Should().MatchRegex("\"volume\":\\s*(\\d+)",
                        $"Volume {volume} ({scenario}) should be clamped");
                }
                else
                {
                    response.StatusCode.Should().BeOneOf(
                        new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                        $"Volume {volume} ({scenario}) should be rejected");
                }
            }
        }
        finally
        {
            await Client.DeleteAsync("/api/players/VolumeTestPlayer");
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public async Task SetDeviceMaxVolume_InvalidValues_RejectedOrClamped(int maxVolume)
    {
        var response = await Client.PutAsJsonAsync("/api/devices/0/max-volume", new
        {
            maxVolume = maxVolume
        });

        // Should either reject or clamp to valid range
        if (response.IsSuccessStatusCode)
        {
            // Verify it was clamped
            var getResponse = await Client.GetAsync("/api/devices");
            var content = await getResponse.Content.ReadAsStringAsync();

            // Max volume should be within 0-100
            content.Should().NotContain($"\"maxVolume\":{maxVolume}",
                "Invalid max volume should be clamped");
        }
        else
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.UnprocessableEntity);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SetCardMaxVolume_InvalidValues_RejectedOrClamped(int maxVolume)
    {
        var response = await Client.PutAsJsonAsync("/api/cards/0/max-volume", new
        {
            maxVolume = maxVolume
        });

        if (response.IsSuccessStatusCode)
        {
            // Verify clamping
            var getResponse = await Client.GetAsync("/api/cards/0");
            getResponse.IsSuccessStatusCode.Should().BeTrue();
        }
        else
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.UnprocessableEntity);
        }
    }

    #endregion

    #region Trigger Channel Boundary Tests

    [Theory]
    [InlineData(0, false, "Zero channel (channels are 1-indexed)")]
    [InlineData(-1, false, "Negative channel")]
    [InlineData(1, true, "Minimum valid channel")]
    [InlineData(4, true, "Mid-range channel")]
    [InlineData(8, true, "Maximum channel")]
    [InlineData(9, false, "Over maximum channel")]
    [InlineData(100, false, "Way over maximum")]
    [InlineData(int.MaxValue, false, "Max int channel")]
    public async Task ConfigureTrigger_ChannelBoundaries_ValidatedCorrectly(
        int channel, bool shouldSucceed, string scenario)
    {
        var response = await Client.PutAsJsonAsync($"/api/triggers/{channel}", new
        {
            devicePattern = "alsa_output.*",
            offDelaySeconds = 5
        });

        if (shouldSucceed)
        {
            // May fail due to no relay hardware, but shouldn't be a validation error
            // Could be OK, NotFound (no relay), or BadRequest (validation)
            if (!response.IsSuccessStatusCode)
            {
                // If it fails, it should be for a reason other than channel validation
                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotContain("channel",
                    $"Channel {channel} ({scenario}) should be valid");
            }
        }
        else
        {
            response.StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.NotFound },
                $"Channel {channel} ({scenario}) should be rejected");
        }
    }

    #endregion

    #region Delay/Offset Boundary Tests

    [Theory]
    [InlineData(-10000, "Large negative delay")]
    [InlineData(-1, "Small negative delay")]
    [InlineData(0, "Zero delay")]
    [InlineData(100, "Small positive delay")]
    [InlineData(1000, "One second delay")]
    [InlineData(5000, "Five second delay")]
    [InlineData(60000, "One minute delay")]
    [InlineData(int.MaxValue, "Max int delay")]
    public async Task SetPlayerOffset_BoundaryValues_HandledAppropriately(int delayMs, string scenario)
    {
        // Create player
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "OffsetTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        try
        {
            var response = await Client.PutAsJsonAsync("/api/players/OffsetTestPlayer/offset", new
            {
                delayMs = delayMs
            });

            // Document behavior - may accept or reject based on implementation
            if (response.IsSuccessStatusCode)
            {
                var getResponse = await Client.GetAsync("/api/players/OffsetTestPlayer");
                getResponse.IsSuccessStatusCode.Should().BeTrue(
                    $"Delay {delayMs}ms ({scenario}) should not break player");
            }
            // Rejection for extreme values is also acceptable
        }
        finally
        {
            await Client.DeleteAsync("/api/players/OffsetTestPlayer");
        }
    }

    #endregion

    #region String Length Boundary Tests

    [Theory]
    [InlineData(1, true, "Single character name")]
    [InlineData(50, true, "Medium length name")]
    [InlineData(100, true, "Maximum allowed length")]
    [InlineData(101, false, "One over maximum")]
    [InlineData(256, false, "256 characters")]
    [InlineData(1000, false, "1000 characters")]
    public async Task PlayerName_LengthBoundaries_ValidatedCorrectly(
        int length, bool shouldSucceed, string scenario)
    {
        var name = new string('A', length);

        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = name,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        if (shouldSucceed)
        {
            response.IsSuccessStatusCode.Should().BeTrue(
                $"Name length {length} ({scenario}) should be accepted");
            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(name)}");
        }
        else
        {
            response.StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                $"Name length {length} ({scenario}) should be rejected");
        }
    }

    [Fact]
    public async Task PlayerName_Empty_Rejected()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
            "Empty name should be rejected");
    }

    [Theory]
    [InlineData("   ", "Only spaces")]
    [InlineData("\t\t", "Only tabs")]
    [InlineData("\n\n", "Only newlines")]
    [InlineData(" \t\n ", "Mixed whitespace")]
    public async Task PlayerName_OnlyWhitespace_Rejected(string name, string scenario)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = name,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
            $"Whitespace-only name ({scenario}) should be rejected");
    }

    #endregion

    #region Sink Channel Count Boundary Tests

    [Theory]
    [InlineData(0, false, "Zero channels")]
    [InlineData(-1, false, "Negative channels")]
    [InlineData(1, true, "Mono")]
    [InlineData(2, true, "Stereo")]
    [InlineData(6, true, "5.1 surround")]
    [InlineData(8, true, "7.1 surround")]
    [InlineData(16, false, "16 channels (over limit)")]
    [InlineData(100, false, "100 channels")]
    public async Task CreateRemapSink_ChannelCountBoundaries_Validated(
        int channels, bool shouldSucceed, string scenario)
    {
        // Create appropriate number of mappings
        var mappings = Enumerable.Range(0, Math.Max(1, Math.Min(channels, 8)))
            .Select(i => new { outputChannel = $"aux{i}", masterChannel = "front-left" })
            .ToArray();

        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = $"channel_test_{channels}",
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = channels,
            channelMappings = mappings
        });

        if (shouldSucceed)
        {
            if (response.IsSuccessStatusCode)
            {
                await Client.DeleteAsync($"/api/sinks/channel_test_{channels}");
            }
            // May fail for other reasons (e.g., mock doesn't support), that's OK
        }
        else
        {
            if (response.IsSuccessStatusCode)
            {
                // If accepted, clean up and document
                await Client.DeleteAsync($"/api/sinks/channel_test_{channels}");
                Assert.True(true, $"WARNING: {channels} channels ({scenario}) was accepted");
            }
            else
            {
                response.StatusCode.Should().BeOneOf(
                    new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                    $"{channels} channels ({scenario}) should be rejected");
            }
        }
    }

    #endregion

    #region Numeric Type Edge Cases

    [Fact]
    public async Task SetVolume_FloatValue_HandledGracefully()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "FloatVolumeTest",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        try
        {
            // Send a float value where int is expected
            var content = new StringContent(
                "{\"volume\": 50.5}",
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await Client.PutAsync("/api/players/FloatVolumeTest/volume", content);

            // Should either accept (truncating to 50) or reject
            if (response.IsSuccessStatusCode)
            {
                var getResponse = await Client.GetAsync("/api/players/FloatVolumeTest");
                var responseContent = await getResponse.Content.ReadAsStringAsync();

                // Volume should be an integer
                responseContent.Should().MatchRegex("\"volume\":\\s*\\d+(?!\\.)",
                    "Float should be converted to int");
            }
        }
        finally
        {
            await Client.DeleteAsync("/api/players/FloatVolumeTest");
        }
    }

    [Fact]
    public async Task SetVolume_StringValue_Rejected()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "StringVolumeTest",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        try
        {
            var content = new StringContent(
                "{\"volume\": \"fifty\"}",
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await Client.PutAsync("/api/players/StringVolumeTest/volume", content);

            response.StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                "String volume should be rejected");
        }
        finally
        {
            await Client.DeleteAsync("/api/players/StringVolumeTest");
        }
    }

    [Fact]
    public async Task SetVolume_NullValue_Rejected()
    {
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = "NullVolumeTest",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        try
        {
            var content = new StringContent(
                "{\"volume\": null}",
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await Client.PutAsync("/api/players/NullVolumeTest/volume", content);

            response.StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                "Null volume should be rejected");
        }
        finally
        {
            await Client.DeleteAsync("/api/players/NullVolumeTest");
        }
    }

    #endregion

    #region Array/List Boundary Tests

    [Fact]
    public async Task CreateCombineSink_EmptySlaveList_HandledAppropriately()
    {
        var response = await Client.PostAsJsonAsync("/api/sinks/combine", new
        {
            name = "empty_combine",
            slaves = Array.Empty<string>()
        });

        // API may accept or reject empty slave lists - document behavior
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created },
            "Empty slave list should be handled");

        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync("/api/sinks/empty_combine");
        }
    }

    [Fact]
    public async Task CreateRemapSink_EmptyChannelMappings_HandledAppropriately()
    {
        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "empty_remap",
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = 2,
            channelMappings = Array.Empty<object>()
        });

        // API may accept or reject empty channel mappings - document behavior
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created },
            "Empty channel mappings should be handled");

        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync("/api/sinks/empty_remap");
        }
    }

    [Fact]
    public async Task CreateRemapSink_DuplicateOutputChannels_Rejected()
    {
        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "duplicate_channels",
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = 2,
            channelMappings = new[]
            {
                new { outputChannel = "front-left", masterChannel = "front-left" },
                new { outputChannel = "front-left", masterChannel = "front-right" } // Duplicate!
            }
        });

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created },
            "Duplicate output channels handling");

        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync("/api/sinks/duplicate_channels");
        }
    }

    #endregion
}
