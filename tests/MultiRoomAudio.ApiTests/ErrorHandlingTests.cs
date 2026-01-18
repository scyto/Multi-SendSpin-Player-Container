using System.Text;

namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Tests for proper error handling, HTTP status codes, and information disclosure prevention.
/// Verifies that errors are handled gracefully without leaking sensitive information.
/// </summary>
public class ErrorHandlingTests : ApiTestBase, IAsyncLifetime
{
    public ErrorHandlingTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupPlayersAsync();
    }

    #region HTTP Status Code Tests

    [Fact]
    public async Task NotFound_ReturnsProperStatusCode()
    {
        var response = await Client.GetAsync("/api/players/NonExistentPlayer123");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NotFound_DoesNotLeakStackTrace()
    {
        var response = await Client.GetAsync("/api/players/NonExistentPlayer123");
        var content = await response.Content.ReadAsStringAsync();

        content.Should().NotContain("StackTrace");
        content.Should().NotContain("at MultiRoomAudio");
        content.Should().NotContain("Exception");
        content.Should().NotContain(".cs:line");
    }

    [Fact]
    public async Task BadRequest_ForMalformedJson()
    {
        var content = new StringContent(
            "{ invalid json }",
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/players", content);

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
            "Malformed JSON should be rejected");
    }

    [Fact]
    public async Task BadRequest_DoesNotLeakInternalDetails()
    {
        var content = new StringContent(
            "{ invalid json }",
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/players", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        responseContent.Should().NotContain("Newtonsoft");
        responseContent.Should().NotContain("System.Text.Json");
        responseContent.Should().NotContain("InnerException");
    }

    [Fact]
    public async Task MethodNotAllowed_ForInvalidMethod()
    {
        // Try PATCH on an endpoint that doesn't support it
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/players")
        {
            Content = content
        };

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound },
            "Invalid method should be rejected");
    }

    [Fact]
    public async Task UnsupportedMediaType_ForWrongContentType()
    {
        var content = new StringContent(
            "name=test&device=test",
            Encoding.UTF8,
            "application/x-www-form-urlencoded");

        var response = await Client.PostAsync("/api/players", content);

        // May be BadRequest or UnsupportedMediaType
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType },
            "Wrong content type should be handled");
    }

    #endregion

    #region Error Message Content Tests

    [Fact]
    public async Task ErrorResponse_ContainsUsefulMessage()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "" // Empty name
        });

        var content = await response.Content.ReadAsStringAsync();

        // Should have some indication of what went wrong
        var hasUsefulInfo =
            content.Contains("name", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("required", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("device", StringComparison.OrdinalIgnoreCase);

        hasUsefulInfo.Should().BeTrue(
            "Error response should contain useful information about what went wrong");
    }

    [Fact]
    public async Task ErrorResponse_NoSensitivePathsExposed()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "",
            device = ""
        });

        var content = await response.Content.ReadAsStringAsync();

        // Should not expose internal paths
        content.Should().NotContain("/app/");
        content.Should().NotContain("/Users/");
        content.Should().NotContain("C:\\");
        content.Should().NotContain("/home/");
        content.Should().NotContain("/var/");
    }

    [Fact]
    public async Task ErrorResponse_NoConnectionStringsExposed()
    {
        // Try to trigger various errors
        var response = await Client.GetAsync("/api/nonexistent");
        var content = await response.Content.ReadAsStringAsync();

        content.Should().NotContain("ConnectionString");
        content.Should().NotContain("Password=");
        content.Should().NotContain("secret");
        content.ToLower().Should().NotContain("apikey");
    }

    #endregion

    #region Information Disclosure Prevention

    [Fact]
    public async Task HealthEndpoint_DoesNotExposeInternalDetails()
    {
        var response = await Client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        // Health endpoint should be minimal
        content.Should().NotContain("ConnectionString");
        content.Should().NotContain("password");
        content.Should().NotContain("InternalServerError");
        content.Should().NotContain("Exception");
    }

    [Fact]
    public async Task DevicesEndpoint_DoesNotExposeSystemPaths()
    {
        var response = await Client.GetAsync("/api/devices");
        var content = await response.Content.ReadAsStringAsync();

        // Device names are fine, but full system paths should not be exposed
        content.Should().NotContain("/dev/"); // Linux device paths OK in device context
        content.Should().NotContain("/etc/");
        content.Should().NotContain("/root/");
    }

    [Fact]
    public async Task ErrorMessages_DoNotContainSqlQueries()
    {
        // Try invalid inputs that might trigger database errors
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "'; DROP TABLE players; --",
            device = "test"
        });

        var content = await response.Content.ReadAsStringAsync();

        content.Should().NotContain("SELECT");
        content.Should().NotContain("INSERT");
        content.Should().NotContain("UPDATE");
        content.Should().NotContain("DELETE FROM");
        content.Should().NotContain("DROP TABLE");
    }

    [Fact]
    public async Task ServerHeader_DoesNotExposeVersion()
    {
        var response = await Client.GetAsync("/api/health");

        // Check Server header
        if (response.Headers.TryGetValues("Server", out var serverValues))
        {
            var serverHeader = string.Join("", serverValues);

            // Should not expose detailed version info
            serverHeader.Should().NotMatchRegex(@"\d+\.\d+\.\d+",
                "Server header should not expose version numbers");
        }
    }

    [Fact]
    public async Task ResponseHeaders_NoUnnecessaryExposure()
    {
        var response = await Client.GetAsync("/api/players");

        // Should not expose X-Powered-By or similar
        response.Headers.Should().NotContainKey("X-Powered-By");
        response.Headers.Should().NotContainKey("X-AspNet-Version");
        response.Headers.Should().NotContainKey("X-AspNetMvc-Version");
    }

    #endregion

    #region Graceful Degradation Tests

    [Fact]
    public async Task InvalidEndpoint_Returns404NotServerError()
    {
        var response = await Client.GetAsync("/api/completely/invalid/endpoint");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ((int)response.StatusCode).Should().BeLessThan(500,
            "Invalid endpoint should be 4xx not 5xx");
    }

    [Fact]
    public async Task InvalidPlayerId_DoesNotCrashServer()
    {
        // Try various invalid IDs (excluding null bytes which cause .NET framework errors)
        var invalidIds = new[]
        {
            "../../etc/passwd",
            "<script>",
            "' OR 1=1 --",
            new string('A', 10000)
            // Note: Null bytes ("\0") and "%00" are excluded as they cause
            // System.InvalidOperationException in the test framework itself
        };

        foreach (var id in invalidIds)
        {
            var response = await Client.GetAsync($"/api/players/{Uri.EscapeDataString(id)}");

            ((int)response.StatusCode).Should().BeLessThan(500,
                $"Invalid ID '{id.Substring(0, Math.Min(20, id.Length))}...' should not cause server error");
        }

        // Server should still be healthy
        var healthResponse = await Client.GetAsync("/api/health");
        healthResponse.IsSuccessStatusCode.Should().BeTrue(
            "Server should remain healthy after invalid ID attempts");
    }

    [Fact]
    public async Task LargePayload_RejectedGracefully()
    {
        var largeContent = new string('X', 10 * 1024 * 1024); // 10MB

        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = largeContent,
            device = "test"
        });

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.UnprocessableEntity },
            "Large payload should be rejected");

        ((int)response.StatusCode).Should().BeLessThan(500,
            "Large payload should not cause server error");
    }

    #endregion

    #region Content Type Handling

    [Fact]
    public async Task JsonResponse_HasCorrectContentType()
    {
        var response = await Client.GetAsync("/api/players");

        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/json");
    }

    [Fact]
    public async Task ErrorResponse_HasCorrectContentType()
    {
        var response = await Client.GetAsync("/api/players/NonExistent");

        // Error responses should also be JSON
        if (response.Content.Headers.ContentType != null)
        {
            response.Content.Headers.ContentType.MediaType
                .Should().BeOneOf("application/json", "application/problem+json");
        }
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public async Task SameError_ConsistentResponse()
    {
        // Make the same invalid request multiple times
        var responses = await Task.WhenAll(
            Client.GetAsync("/api/players/NonExistent"),
            Client.GetAsync("/api/players/NonExistent"),
            Client.GetAsync("/api/players/NonExistent")
        );

        // All should return the same status code
        var statusCodes = responses.Select(r => r.StatusCode).Distinct().ToList();
        statusCodes.Should().HaveCount(1, "Same error should return consistent status code");
    }

    [Fact]
    public async Task ValidationErrors_ReturnBadRequestNotServerError()
    {
        var invalidRequests = new[]
        {
            new { name = (string?)null, device = "test" },
            new { name = "test", device = (string?)null },
            new { name = "", device = "" }
        };

        foreach (var request in invalidRequests)
        {
            var response = await Client.PostAsJsonAsync("/api/players", request);

            ((int)response.StatusCode).Should().BeLessThan(500,
                "Validation errors should be 4xx not 5xx");
        }
    }

    #endregion

    #region Timeout and Resource Handling

    [Fact]
    public async Task SlowOperation_DoesNotHang()
    {
        // Create player (might be slow in mock mode)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "TimeoutTestPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        }, cts.Token);

        response.Should().NotBeNull("Request should complete within timeout");

        // Cleanup
        await Client.DeleteAsync("/api/players/TimeoutTestPlayer");
    }

    #endregion

    #region Edge Case Handling

    [Fact]
    public async Task EmptyBody_HandledGracefully()
    {
        var content = new StringContent("", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/players", content);

        ((int)response.StatusCode).Should().BeLessThan(500,
            "Empty body should not cause server error");
    }

    [Fact]
    public async Task NullValues_HandledGracefully()
    {
        var content = new StringContent(
            "{\"name\": null, \"device\": null}",
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/players", content);

        ((int)response.StatusCode).Should().BeLessThan(500,
            "Null values should not cause server error");
    }

    [Fact]
    public async Task ExtraFields_Ignored()
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = "ExtraFieldsPlayer",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            unknownField = "should be ignored",
            anotherUnknown = 12345
        });

        // Should either succeed or return validation error, not server error
        ((int)response.StatusCode).Should().BeLessThan(500,
            "Extra fields should not cause server error");

        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync("/api/players/ExtraFieldsPlayer");
        }
    }

    [Fact]
    public async Task WrongFieldTypes_HandledGracefully()
    {
        var content = new StringContent(
            "{\"name\": 12345, \"device\": true}",
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/players", content);

        ((int)response.StatusCode).Should().BeLessThan(500,
            "Wrong field types should not cause server error");
    }

    #endregion
}
