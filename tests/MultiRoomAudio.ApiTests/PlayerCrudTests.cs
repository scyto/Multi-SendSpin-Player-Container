namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Tests for player CRUD operations.
/// </summary>
public class PlayerCrudTests : ApiTestBase, IAsyncLifetime
{
    public PlayerCrudTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupPlayersAsync();
    }

    [Fact]
    public async Task CreatePlayer_WithValidData_Succeeds()
    {
        // Arrange
        var request = new
        {
            name = "TestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/players", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<PlayerResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task CreatePlayer_ThenGetPlayer_ReturnsCorrectData()
    {
        // Arrange
        var playerName = "GetTestPlayer";
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = playerName,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Act
        var response = await Client.GetAsync($"/api/players/{playerName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var player = await response.Content.ReadFromJsonAsync<PlayerResponse>();
        player.Should().NotBeNull();
        player!.Name.Should().Be(playerName);
    }

    [Fact]
    public async Task CreatePlayer_DuplicateName_Fails()
    {
        // Arrange
        var playerName = "DuplicatePlayer";
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = playerName,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Act - try to create again
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = playerName,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeletePlayer_ExistingPlayer_Succeeds()
    {
        // Arrange
        var playerName = "DeleteTestPlayer";
        await Client.PostAsJsonAsync("/api/players", new
        {
            name = playerName,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Act
        var deleteResponse = await Client.DeleteAsync($"/api/players/{playerName}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify player is gone
        var getResponse = await Client.GetAsync($"/api/players/{playerName}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePlayer_NonExistent_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync("/api/players/NonExistentPlayer");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListPlayers_ReturnsAllCreatedPlayers()
    {
        // Arrange - create multiple players
        var playerNames = new[] { "ListTest1", "ListTest2", "ListTest3" };
        foreach (var name in playerNames)
        {
            await Client.PostAsJsonAsync("/api/players", new
            {
                name = name,
                device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
            });
        }

        // Act
        var response = await Client.GetAsync("/api/players");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PlayersListResponse>();
        result.Should().NotBeNull();
        result!.Players.Should().NotBeNull();

        foreach (var name in playerNames)
        {
            result.Players.Should().Contain(p => p.Name == name);
        }
    }

    [Fact]
    public async Task CreatePlayer_InvalidDevice_Fails()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "InvalidDevicePlayer",
            device = "nonexistent_device_12345"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // DTOs
    private record PlayerResponse(string Name, string? DeviceId, string State);
    private record PlayersListResponse(List<PlayerResponse> Players, int Count);
}
