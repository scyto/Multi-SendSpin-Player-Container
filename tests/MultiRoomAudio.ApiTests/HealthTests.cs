namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Basic health and connectivity tests.
/// </summary>
public class HealthTests : ApiTestBase
{
    public HealthTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        health.Should().NotBeNull();
        health!.Status.Should().BeOneOf("Healthy", "healthy");
    }

    [Fact]
    public async Task Devices_ReturnsMockDevices()
    {
        var response = await Client.GetAsync("/api/devices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await response.Content.ReadFromJsonAsync<DevicesResponse>();
        devices.Should().NotBeNull();
        devices!.Devices.Should().NotBeEmpty();
        devices.Count.Should().BeGreaterThan(0);

        // Verify mock devices are present
        devices.Devices.Should().Contain(d => d.Name.Contains("Built-in Audio"));
        devices.Devices.Should().Contain(d => d.Name.Contains("Xonar"));
    }

    [Fact]
    public async Task Cards_ReturnsMockCards()
    {
        var response = await Client.GetAsync("/api/cards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CardsListResponse>();
        result.Should().NotBeNull();
        result!.Cards.Should().NotBeEmpty();

        // Verify mock cards include expected types
        result.Cards.Should().Contain(c => c.Name.Contains("pci-")); // PCI cards
        result.Cards.Should().Contain(c => c.Name.Contains("usb-")); // USB cards
        result.Cards.Should().Contain(c => c.Name.StartsWith("bluez_")); // Bluetooth
    }

    // DTOs for health tests
    private record HealthResponse(string Status, DateTime Timestamp, string Version);
    private record DevicesResponse(List<DeviceInfo> Devices, int Count);
    private record DeviceInfo(int Index, string Id, string Name, int MaxChannels);
    private record CardsListResponse(List<CardInfo> Cards, int Count);
    private record CardInfo(int Index, string Name, string Description, string? ActiveProfile);
}
