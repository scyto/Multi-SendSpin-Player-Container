namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Base class for API tests providing common utilities.
/// </summary>
public abstract class ApiTestBase : IClassFixture<MockHardwareWebApplicationFactory>
{
    protected readonly HttpClient Client;
    protected readonly MockHardwareWebApplicationFactory Factory;

    protected ApiTestBase(MockHardwareWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Asserts that a response is successful and returns the content.
    /// </summary>
    protected async Task<T> AssertSuccessAndGetAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            $"Expected success but got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var result = await response.Content.ReadFromJsonAsync<T>();
        result.Should().NotBeNull();
        return result!;
    }

    /// <summary>
    /// Cleans up any players created during tests.
    /// </summary>
    protected async Task CleanupPlayersAsync()
    {
        var response = await Client.GetAsync("/api/players");
        if (!response.IsSuccessStatusCode) return;

        var result = await response.Content.ReadFromJsonAsync<PlayersListResponse>();
        if (result?.Players == null) return;

        foreach (var player in result.Players)
        {
            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(player.Name)}");
        }
    }

    /// <summary>
    /// Cleans up any custom sinks created during tests.
    /// </summary>
    protected async Task CleanupSinksAsync()
    {
        var response = await Client.GetAsync("/api/sinks");
        if (!response.IsSuccessStatusCode) return;

        var sinks = await response.Content.ReadFromJsonAsync<SinksResponse>();
        if (sinks?.Sinks == null) return;

        foreach (var sink in sinks.Sinks)
        {
            await Client.DeleteAsync($"/api/sinks/{Uri.EscapeDataString(sink.Name)}");
        }
    }

    // Minimal DTOs for test assertions
    protected record PlayerInfo(string Name, string? DeviceId, string State);
    protected record PlayersListResponse(List<PlayerInfo> Players, int Count);
    protected record SinksResponse(List<SinkInfo> Sinks, int Count);
    protected record SinkInfo(string Name, string? Description);
}
