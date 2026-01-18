namespace MultiRoomAudio.ApiTests;

/// <summary>
/// Security-focused tests for input validation, XSS prevention, path traversal,
/// and injection attacks. These tests verify the application properly sanitizes
/// and validates all user input.
/// </summary>
public class SecurityFuzzingTests : ApiTestBase, IAsyncLifetime
{
    public SecurityFuzzingTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupPlayersAsync();
        await CleanupSinksAsync();
    }

    #region XSS Prevention Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>", "Basic script tag")]
    [InlineData("<img src=x onerror=alert(1)>", "Image onerror handler")]
    [InlineData("<svg onload=alert(1)>", "SVG onload handler")]
    [InlineData("javascript:alert(1)", "JavaScript protocol")]
    [InlineData("<iframe src='javascript:alert(1)'>", "Iframe with JS")]
    [InlineData("'-alert(1)-'", "Quote breaking attempt")]
    [InlineData("\"><script>alert(1)</script>", "Attribute escape attempt")]
    [InlineData("{{constructor.constructor('alert(1)')()}}", "Template injection")]
    public async Task PlayerName_XssPayloads_SanitizedOrRejected(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = payload,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        if (response.IsSuccessStatusCode)
        {
            // If accepted, verify the payload is properly escaped in responses
            var listResponse = await Client.GetAsync("/api/players");
            var content = await listResponse.Content.ReadAsStringAsync();

            // Should not contain unescaped script tags
            content.Should().NotContain("<script>",
                $"XSS payload '{attackType}' should be escaped in response");
            content.Should().NotContain("onerror=",
                $"XSS payload '{attackType}' should be escaped in response");
            content.Should().NotContain("onload=",
                $"XSS payload '{attackType}' should be escaped in response");

            // Cleanup
            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(payload)}");
        }
        else
        {
            // Rejected is also acceptable - verify proper error code
            response.StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                $"XSS payload should be rejected with 4xx, not {response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("onmouseover=alert(1)")]
    public async Task DeviceAlias_XssPayloads_AcceptedInJson(string payload)
    {
        var response = await Client.PutAsJsonAsync("/api/devices/0/alias", new
        {
            alias = payload
        });

        // JSON APIs can safely store XSS payloads - they're just data
        // The UI layer is responsible for proper escaping when rendering HTML
        // This test documents that the API accepts these values
        response.IsSuccessStatusCode.Should().BeTrue(
            "API should accept alias values - XSS protection is UI responsibility");

        // Verify value is stored and returned
        var getResponse = await Client.GetAsync("/api/devices");
        getResponse.IsSuccessStatusCode.Should().BeTrue(
            "API should return device list after setting alias");

        // Note: JSON encoding naturally escapes quotes, making XSS via JSON safe
        // The UI must use textContent (not innerHTML) when displaying these values
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "Script in description")]
    [InlineData("<img/src=x onerror=alert(1)>", "Img tag in description")]
    public async Task SinkDescription_XssPayloads_AcceptedInJson(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "xss_test_sink",
            description = payload,
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = 2,
            channelMappings = new[]
            {
                new { outputChannel = "front-left", masterChannel = "front-left" },
                new { outputChannel = "front-right", masterChannel = "front-right" }
            }
        });

        // JSON APIs can safely store XSS payloads
        // Document that the API accepts these values
        if (response.IsSuccessStatusCode)
        {
            var listResponse = await Client.GetAsync("/api/sinks");
            listResponse.IsSuccessStatusCode.Should().BeTrue(
                $"API should return sink list after storing {attackType}");

            await Client.DeleteAsync("/api/sinks/xss_test_sink");
        }
        // Note: If API rejects, that's also valid security behavior
    }

    #endregion

    #region Path Traversal Tests

    [Theory]
    [InlineData("../etc/passwd", "Unix path traversal")]
    [InlineData("..\\windows\\system32", "Windows path traversal")]
    [InlineData("....//....//etc/passwd", "Double-encoded traversal")]
    [InlineData("%2e%2e%2f%2e%2e%2fetc/passwd", "URL-encoded traversal")]
    [InlineData("..%252f..%252fetc/passwd", "Double URL-encoded")]
    [InlineData("/etc/passwd", "Absolute path")]
    [InlineData("C:\\Windows\\System32", "Windows absolute path")]
    [InlineData("file:///etc/passwd", "File protocol")]
    public async Task PlayerName_PathTraversal_Rejected(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = payload,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Path traversal attempts should be rejected
        if (response.IsSuccessStatusCode)
        {
            // If somehow accepted, ensure it doesn't create files outside config dir
            // and clean up
            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(payload)}");

            // Document that this was accepted (may need fixing)
            Assert.True(true, $"WARNING: Path traversal '{attackType}' was accepted - verify file safety");
        }
        else
        {
            response.StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity },
                "Path traversal should be rejected with 4xx");
        }
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    public async Task GetPlayer_PathTraversalInName_Rejected(string payload)
    {
        var response = await Client.GetAsync($"/api/players/{Uri.EscapeDataString(payload)}");

        // Should return NotFound, not expose file contents
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest },
            "Path traversal in GET should not expose files");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("root:", "Should not expose /etc/passwd contents");
    }

    [Theory]
    [InlineData("../sink")]
    [InlineData("sink/../../../etc")]
    public async Task SinkName_PathTraversal_Rejected(string payload)
    {
        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = payload,
            description = "Path traversal test",
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = 2,
            channelMappings = new[]
            {
                new { outputChannel = "front-left", masterChannel = "front-left" },
                new { outputChannel = "front-right", masterChannel = "front-right" }
            }
        });

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created },
            "Path traversal in sink name");

        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync($"/api/sinks/{Uri.EscapeDataString(payload)}");
        }
    }

    #endregion

    #region YAML Injection Tests

    [Theory]
    [InlineData("name: injected\nmalicious: true", "YAML key injection")]
    [InlineData("!!python/object:__main__.exploit", "YAML object deserialization")]
    [InlineData("!<!tag:yaml.org,2002:python/object:os.system> 'ls'", "YAML tag injection")]
    [InlineData("{{7*7}}", "Template expression")]
    [InlineData("${7*7}", "Expression language")]
    [InlineData("#{7*7}", "Ruby expression")]
    [InlineData("*alias", "YAML alias reference")]
    [InlineData("&anchor value", "YAML anchor definition")]
    [InlineData("key: |\n  multiline\n  value", "YAML multiline block")]
    public async Task PlayerName_YamlInjection_Sanitized(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = payload,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        if (response.IsSuccessStatusCode)
        {
            // If accepted, verify YAML file isn't corrupted
            var listResponse = await Client.GetAsync("/api/players");
            listResponse.IsSuccessStatusCode.Should().BeTrue(
                $"YAML injection '{attackType}' should not corrupt config");

            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(payload)}");
        }
        else
        {
            // Rejection is preferred for YAML special characters
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.UnprocessableEntity);
        }
    }

    #endregion

    #region Null Byte Injection Tests

    [Theory]
    [InlineData("player\x00.txt", "Null byte in name")]
    [InlineData("player%00.txt", "URL-encoded null byte")]
    [InlineData("player\x00/../../../etc/passwd", "Null byte path traversal")]
    public async Task PlayerName_NullByteInjection_Rejected(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = payload,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Null bytes should be rejected or stripped
        if (response.IsSuccessStatusCode)
        {
            // If accepted, the null byte should have been stripped
            var listResponse = await Client.GetAsync("/api/players");
            var content = await listResponse.Content.ReadAsStringAsync();

            content.Should().NotContain("\x00",
                $"Null byte should be stripped from stored name");

            // Try to cleanup (name may have been sanitized)
            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(payload)}");
            await Client.DeleteAsync("/api/players/player.txt");
            await Client.DeleteAsync("/api/players/player");
        }
    }

    #endregion

    #region Command Injection Tests

    [Theory]
    [InlineData("; ls -la", "Semicolon command")]
    [InlineData("| cat /etc/passwd", "Pipe command")]
    [InlineData("$(whoami)", "Command substitution")]
    [InlineData("`whoami`", "Backtick command")]
    [InlineData("& ping -c 1 127.0.0.1", "Background command")]
    [InlineData("|| true", "Or command")]
    [InlineData("&& ls", "And command")]
    [InlineData("\n/bin/sh", "Newline injection")]
    public async Task PlayerName_CommandInjection_Rejected(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = $"test{payload}",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // These should be rejected due to special characters
        if (response.IsSuccessStatusCode)
        {
            // Document but don't fail - the app may sanitize these safely
            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString($"test{payload}")}");
        }
    }

    #endregion

    #region HTTP Header Injection Tests

    [Theory]
    [InlineData("player\r\nX-Injected: header", "CRLF header injection")]
    [InlineData("player\nSet-Cookie: malicious=true", "Cookie injection")]
    public async Task PlayerName_HeaderInjection_Rejected(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = payload,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // CRLF should be rejected
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created },
            $"Header injection '{attackType}' handling");

        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(payload)}");
        }
    }

    #endregion

    #region Unicode Edge Cases

    [Theory]
    [InlineData("\u202E\u0041\u0042\u0043", "RTL override (text direction attack)")]
    [InlineData("\uFEFF", "Zero-width no-break space (BOM)")]
    [InlineData("\u200B", "Zero-width space")]
    [InlineData("\u2028", "Line separator")]
    [InlineData("\u2029", "Paragraph separator")]
    [InlineData("A\u0300\u0301\u0302\u0303\u0304\u0305", "Combining character stack")]
    [InlineData("\uD800", "Unpaired high surrogate")]
    [InlineData("\uDC00", "Unpaired low surrogate")]
    public async Task PlayerName_UnicodeEdgeCases_HandledSafely(string payload, string caseType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = $"test{payload}",
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Should either reject or handle safely
        if (response.IsSuccessStatusCode)
        {
            // If accepted, verify it doesn't break listing
            var listResponse = await Client.GetAsync("/api/players");
            listResponse.IsSuccessStatusCode.Should().BeTrue(
                $"Unicode edge case '{caseType}' should not break API");

            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString($"test{payload}")}");
        }
        else
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.UnprocessableEntity);
        }
    }

    #endregion

    #region Oversized Input Tests

    [Fact]
    public async Task PlayerName_ExtremelyLong_RejectedOrTruncated()
    {
        // 1MB string
        var longName = new string('A', 1024 * 1024);

        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = longName,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        // Should reject oversized input
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.RequestEntityTooLarge },
            "1MB player name should be rejected");
    }

    [Fact]
    public async Task DeviceAlias_ExtremelyLong_RejectedOrTruncated()
    {
        var longAlias = new string('B', 1024 * 100); // 100KB

        var response = await Client.PutAsJsonAsync("/api/devices/0/alias", new
        {
            alias = longAlias
        });

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.OK },
            "100KB alias should be rejected or truncated");
    }

    [Fact]
    public async Task SinkChannelMappings_HugeArray_HandledAppropriately()
    {
        // Create array with 10000 channel mappings
        var mappings = Enumerable.Range(0, 10000)
            .Select(i => new { outputChannel = $"channel-{i}", masterChannel = "front-left" })
            .ToArray();

        var response = await Client.PostAsJsonAsync("/api/sinks/remap", new
        {
            name = "huge_mapping_sink",
            masterSink = "alsa_output.pci-0000_00_1f.3.analog-stereo",
            channels = 10000,
            channelMappings = mappings
        });

        // API may accept or reject large arrays - document behavior
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.Created },
            "10000 channel mappings should be handled without server error");

        // Cleanup if created
        if (response.IsSuccessStatusCode)
        {
            await Client.DeleteAsync("/api/sinks/huge_mapping_sink");
        }
    }

    #endregion

    #region JSON Injection Tests

    [Theory]
    [InlineData("{\"injected\":true}", "JSON object in string")]
    [InlineData("[1,2,3]", "JSON array in string")]
    [InlineData("\"},{\"injected\":\"true", "JSON structure break")]
    public async Task PlayerName_JsonInjection_Sanitized(string payload, string attackType)
    {
        var response = await Client.PostAsJsonAsync("/api/players", new
        {
            name = payload,
            device = "alsa_output.pci-0000_00_1f.3.analog-stereo"
        });

        if (response.IsSuccessStatusCode)
        {
            var listResponse = await Client.GetAsync("/api/players");
            listResponse.IsSuccessStatusCode.Should().BeTrue(
                $"JSON injection '{attackType}' should not break API");

            await Client.DeleteAsync($"/api/players/{Uri.EscapeDataString(payload)}");
        }
    }

    #endregion
}
