namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Tests for sound card profile management.
/// </summary>
public class CardProfileTests : ApiTestBase
{
    public CardProfileTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetCards_ReturnsAllMockCards()
    {
        var response = await Client.GetAsync("/api/cards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CardsListResponse>();
        result.Should().NotBeNull();
        result!.Cards.Should().HaveCountGreaterThanOrEqualTo(7); // 7 mock cards
    }

    [Fact]
    public async Task GetCard_ByIndex_ReturnsCard()
    {
        var response = await Client.GetAsync("/api/cards/0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        card.Should().NotBeNull();
        card!.Index.Should().Be(0);
        card.Description.Should().Be("Built-in Audio");
    }

    [Fact]
    public async Task GetCard_ByName_ReturnsCard()
    {
        var response = await Client.GetAsync("/api/cards/alsa_card.pci-0000_00_1f.3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        card.Should().NotBeNull();
        card!.Name.Should().Contain("pci-0000_00_1f.3");
    }

    [Fact]
    public async Task GetCard_NonExistent_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/cards/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetCardProfile_ValidProfile_Succeeds()
    {
        // Arrange - use Xonar card (index 1) which has 7.1 profile
        var cardIndex = 1;

        // First reset to stereo to ensure known starting state
        await Client.PutAsJsonAsync($"/api/cards/{cardIndex}/profile", new
        {
            profile = "output:analog-stereo"
        });

        // Act - set to 7.1 surround
        var response = await Client.PutAsJsonAsync($"/api/cards/{cardIndex}/profile", new
        {
            profile = "output:analog-surround-71"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify profile changed
        var cardResponse = await Client.GetAsync($"/api/cards/{cardIndex}");
        var card = await cardResponse.Content.ReadFromJsonAsync<CardResponse>();
        card!.ActiveProfile.Should().Be("output:analog-surround-71");
    }

    [Fact]
    public async Task SetCardProfile_InvalidProfile_Fails()
    {
        var response = await Client.PutAsJsonAsync("/api/cards/0/profile", new
        {
            profile = "nonexistent-profile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetCardProfile_ToOff_SetsZeroSinks()
    {
        // Arrange
        var cardIndex = 0;

        // Act - set profile to "off"
        var response = await Client.PutAsJsonAsync($"/api/cards/{cardIndex}/profile", new
        {
            profile = "off"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cardResponse = await Client.GetAsync($"/api/cards/{cardIndex}");
        var card = await cardResponse.Content.ReadFromJsonAsync<CardResponse>();
        card!.ActiveProfile.Should().Be("off");

        // Reset back to working profile
        await Client.PutAsJsonAsync($"/api/cards/{cardIndex}/profile", new
        {
            profile = "output:analog-stereo"
        });
    }

    [Fact]
    public async Task CardProfiles_XonarHas71Option()
    {
        // Get Xonar card
        var response = await Client.GetAsync("/api/cards/1");
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();

        card.Should().NotBeNull();
        card!.Profiles.Should().Contain(p => p.Name == "output:analog-surround-71");
        card.Profiles.Should().Contain(p => p.Name == "output:analog-surround-51");
    }

    [Fact]
    public async Task CardProfiles_BluetoothHasA2dp()
    {
        // Get JBL Flip 5 Bluetooth card (index 4)
        var response = await Client.GetAsync("/api/cards/4");
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();

        card.Should().NotBeNull();
        card!.Profiles.Should().Contain(p => p.Name == "a2dp-sink");
        card.Profiles.Should().Contain(p => p.Name == "headset-head-unit");
    }

    [Fact]
    public async Task ProfileChange_UpdatesDeviceChannelCount()
    {
        // Arrange - set Xonar to stereo first
        await Client.PutAsJsonAsync("/api/cards/1/profile", new { profile = "output:analog-stereo" });

        var stereoDeviceResponse = await Client.GetAsync("/api/devices/1");
        var stereoDevice = await stereoDeviceResponse.Content.ReadFromJsonAsync<DeviceResponse>();
        stereoDevice!.MaxChannels.Should().Be(2);

        // Act - change to 7.1
        await Client.PutAsJsonAsync("/api/cards/1/profile", new { profile = "output:analog-surround-71" });

        // Assert - device now reports 8 channels
        var surroundDeviceResponse = await Client.GetAsync("/api/devices/1");
        var surroundDevice = await surroundDeviceResponse.Content.ReadFromJsonAsync<DeviceResponse>();
        surroundDevice!.MaxChannels.Should().Be(8);
    }

    // DTOs
    private record CardsListResponse(List<CardResponse> Cards, int Count);
    private record CardResponse(
        int Index,
        string Name,
        string Description,
        string? Driver,
        string? ActiveProfile,
        List<ProfileInfo> Profiles
    );

    private record ProfileInfo(string Name, string Description, int Sinks, bool IsAvailable);
    private record DeviceResponse(int Index, string Id, string Name, int MaxChannels);
}
