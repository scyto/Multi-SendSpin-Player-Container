# Multi-Room Audio Test Suite

This document provides an overview of the test structure and describes what each test file covers.

## Overview

The test suite is divided into two main projects:

| Project | Purpose | Framework |
|---------|---------|-----------|
| `MultiRoomAudio.ApiTests` | In-process API testing | xUnit + WebApplicationFactory |
| `MultiRoomAudio.E2ETests` | End-to-end browser testing | xUnit + Playwright |

Both test projects run against the application in **mock hardware mode** (`MOCK_HARDWARE=true`), which simulates audio devices without requiring real hardware.

---

## Running Tests

### Prerequisites

```bash
# Build the main application first
dotnet build src/MultiRoomAudio/MultiRoomAudio.csproj

# Install Playwright browsers (first time only)
pwsh tests/MultiRoomAudio.E2ETests/bin/Debug/net8.0/playwright.ps1 install
```

### Commands

```bash
# Run all tests
dotnet test

# Run only API tests
dotnet test tests/MultiRoomAudio.ApiTests

# Run only E2E tests
dotnet test tests/MultiRoomAudio.E2ETests

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~PlayerCrudTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~CreatePlayer_WithValidData_Succeeds"
```

---

## Project Structure

```
tests/
├── MultiRoomAudio.ApiTests/           # In-process API tests
│   ├── ApiTestBase.cs                 # Base class with shared utilities
│   ├── MockHardwareWebApplicationFactory.cs  # Test server factory
│   ├── GlobalUsings.cs                # Shared using statements
│   │
│   │ # Core API Tests
│   ├── HealthTests.cs                 # Health endpoint and device discovery
│   ├── PlayerCrudTests.cs             # Player create/read/update/delete
│   ├── PlayerLifecycleTests.cs        # Player start/stop/restart states
│   │
│   │ # Feature Tests
│   ├── SinkCreationTests.cs           # Custom sink creation
│   ├── CardProfileTests.cs            # Sound card profile switching
│   ├── ZoneControlTests.cs            # Zone/room management
│   ├── SpecialCharacterTests.cs       # Unicode/special character handling
│   │
│   │ # Quality & Security Tests
│   ├── BoundaryValidationTests.cs     # Input boundary validation
│   ├── ConcurrencyTests.cs            # Thread safety and race conditions
│   ├── ErrorHandlingTests.cs          # Error response validation
│   └── SecurityFuzzingTests.cs        # Security/injection testing
│
└── MultiRoomAudio.E2ETests/           # Browser-based E2E tests
    ├── PlaywrightFixture.cs           # Browser/server lifecycle management
    ├── GlobalUsings.cs                # Shared using statements
    │
    │ # UI Flow Tests
    ├── WizardFlowTests.cs             # Onboarding wizard navigation
    ├── PlayerManagementTests.cs       # Player UI interactions
    ├── SoundCardPageTests.cs          # Sound card configuration UI
    │
    │ # Feature UI Tests
    ├── CustomSinksE2ETests.cs         # Custom sink UI and API
    ├── TriggersE2ETests.cs            # 12V trigger relay controls
    ├── LogsViewE2ETests.cs            # Log viewing functionality
    └── PlayerControlsE2ETests.cs      # Player control interactions
```

---

## API Tests (`MultiRoomAudio.ApiTests`)

### Infrastructure Files

#### `ApiTestBase.cs`
Base class providing shared test utilities:
- `Client` - Pre-configured HttpClient for API requests
- `CleanupPlayersAsync()` - Removes all players after tests
- `CleanupSinksAsync()` - Removes all custom sinks after tests
- `AssertSuccessAndGetAsync<T>()` - Asserts success and deserializes response

#### `MockHardwareWebApplicationFactory.cs`
Custom `WebApplicationFactory<Program>` that:
- Sets `MOCK_HARDWARE=true` environment variable
- Creates isolated temp config directory per test run
- Configures test-appropriate logging levels

### Core API Tests

#### `HealthTests.cs`
Verifies basic API connectivity and mock hardware initialization.

| Test | Description |
|------|-------------|
| `Health_ReturnsHealthy` | Health endpoint returns OK status |
| `Devices_ReturnsMockDevices` | Mock audio devices are listed |
| `Cards_ReturnsMockCards` | Mock sound cards are discovered |

#### `PlayerCrudTests.cs`
Tests basic player CRUD operations.

| Test | Description |
|------|-------------|
| `CreatePlayer_WithValidData_Succeeds` | Valid player creation returns 201 |
| `CreatePlayer_ThenGetPlayer_ReturnsCorrectData` | GET returns created player |
| `CreatePlayer_DuplicateName_Fails` | Duplicate names return 409 Conflict |
| `DeletePlayer_ExistingPlayer_Succeeds` | Deletion removes player |
| `DeletePlayer_NonExistent_ReturnsNotFound` | Missing player returns 404 |
| `ListPlayers_ReturnsAllCreatedPlayers` | List includes all players |
| `CreatePlayer_InvalidDevice_Fails` | Invalid device returns 400 |

#### `PlayerLifecycleTests.cs`
Tests player state transitions and lifecycle operations.

| Test | Description |
|------|-------------|
| `CreatePlayer_ValidRequest_ReturnsCreatedWithCorrectState` | Initial state is valid |
| `StopPlayer_ExistingPlayer_Succeeds` | Stop changes state |
| `StopPlayer_AlreadyStopped_HandledGracefully` | Idempotent stop |
| `RestartPlayer_ExistingPlayer_Succeeds` | Restart transitions correctly |
| `RestartPlayer_StoppedPlayer_StartsSuccessfully` | Restart from stopped |
| `SetVolume_ValidValue_Succeeds` | Volume changes persist |
| `SetOffset_ValidValue_Succeeds` | Delay offset changes |
| `PlayerLifecycle_FullCycle_TransitionsCorrectly` | Full create→stop→restart→delete |
| `MultipleRestarts_HandledCorrectly` | Rapid restart resilience |

### Feature Tests

#### `SinkCreationTests.cs`
Tests custom PulseAudio sink creation (combine-sink, remap-sink).

| Test | Description |
|------|-------------|
| `CreateCombineSink_WithValidSlaves_Succeeds` | Combine multiple outputs |
| `CreateRemapSink_WithValidMappings_Succeeds` | Channel remapping |
| `DeleteSink_ExistingCustomSink_Succeeds` | Sink deletion |
| `ListSinks_ReturnsCreatedSinks` | Sink enumeration |

#### `CardProfileTests.cs`
Tests sound card profile switching.

| Test | Description |
|------|-------------|
| `SetCardProfile_ValidProfile_Succeeds` | Profile change works |
| `SetCardProfile_InvalidProfile_ReturnsBadRequest` | Invalid profile rejected |
| `GetCardDetails_ReturnsProfiles` | Card lists available profiles |

#### `ZoneControlTests.cs`
Tests zone/room management features.

| Test | Description |
|------|-------------|
| `CreateZone_WithPlayers_Succeeds` | Zone grouping |
| `ZoneVolume_AffectsAllPlayers` | Zone-wide volume control |

#### `SpecialCharacterTests.cs`
Tests handling of special characters in names and inputs.

| Test | Description |
|------|-------------|
| `PlayerName_WithUnicode_Succeeds` | Unicode names supported |
| `PlayerName_WithSpaces_Succeeds` | Spaces in names work |
| `PlayerName_WithEmoji_Handled` | Emoji handling |

### Quality & Security Tests

#### `BoundaryValidationTests.cs`
Tests input boundary conditions and numeric limits.

| Test | Description |
|------|-------------|
| `SetPlayerVolume_BoundaryValues_ValidatedCorrectly` | Volume 0-100 bounds |
| `SetDeviceMaxVolume_InvalidValues_RejectedOrClamped` | Max volume limits |
| `ConfigureTrigger_ChannelBoundaries_ValidatedCorrectly` | Channels 1-8 |
| `SetPlayerOffset_BoundaryValues_HandledAppropriately` | Delay ms bounds |
| `PlayerName_LengthBoundaries_ValidatedCorrectly` | Name length limits |
| `PlayerName_Empty_Rejected` | Empty name validation |
| `PlayerName_OnlyWhitespace_Rejected` | Whitespace-only names |
| `CreateRemapSink_ChannelCountBoundaries_Validated` | Channel count limits |
| `SetVolume_FloatValue_HandledGracefully` | Type coercion |
| `SetVolume_StringValue_Rejected` | Type validation |
| `CreateCombineSink_EmptySlaveList_HandledAppropriately` | Empty array handling |

#### `ConcurrencyTests.cs`
Tests thread safety and race condition handling.

| Test | Description |
|------|-------------|
| `CreatePlayer_SimultaneousDuplicateRequests_OnlyOneSucceeds` | Duplicate race |
| `CreatePlayer_DifferentNames_AllSucceed` | Parallel creates |
| `VolumeUpdates_Concurrent_LastOneWins` | Concurrent updates |
| `StopAndRestart_Concurrent_NoDeadlock` | Deadlock prevention |
| `DeleteWhileUpdating_HandledGracefully` | Delete during update |
| `MultipleEndpoints_ConcurrentAccess_NoInterference` | Cross-endpoint |
| `HealthCheck_UnderLoad_StillResponds` | Health availability |
| `CreateDelete_RapidCycle_NoResourceLeak` | Resource cleanup |
| `RestartSpam_HandledWithoutCrash` | Restart flooding |
| `DeviceList_ConcurrentRequests_AllReturn` | List consistency |

#### `ErrorHandlingTests.cs`
Tests error responses and information disclosure prevention.

| Test | Description |
|------|-------------|
| `NotFound_ReturnsProperStatusCode` | 404 for missing resources |
| `NotFound_DoesNotLeakStackTrace` | No stack traces in errors |
| `BadRequest_ForMalformedJson` | Invalid JSON handling |
| `BadRequest_DoesNotLeakInternalDetails` | No library names leaked |
| `MethodNotAllowed_ForInvalidMethod` | Unsupported HTTP methods |
| `ErrorResponse_ContainsUsefulMessage` | Actionable error messages |
| `ErrorResponse_NoSensitivePathsExposed` | No file paths in errors |
| `HealthEndpoint_DoesNotExposeInternalDetails` | Health info minimal |
| `ServerHeader_DoesNotExposeVersion` | No version disclosure |
| `InvalidPlayerId_DoesNotCrashServer` | Malicious ID resilience |
| `LargePayload_RejectedGracefully` | Payload size limits |
| `JsonResponse_HasCorrectContentType` | Content-Type headers |

#### `SecurityFuzzingTests.cs`
Tests security input validation and injection prevention.

| Test | Description |
|------|-------------|
| `PlayerName_XssPayloads_SanitizedOrRejected` | XSS prevention |
| `DeviceAlias_XssPayloads_AcceptedInJson` | JSON XSS safety |
| `PlayerName_PathTraversal_Rejected` | Directory traversal |
| `GetPlayer_PathTraversalInName_Rejected` | GET path traversal |
| `PlayerName_YamlInjection_Sanitized` | YAML injection |
| `PlayerName_NullByteInjection_Rejected` | Null byte attacks |
| `PlayerName_CommandInjection_Rejected` | Shell injection |
| `PlayerName_HeaderInjection_Rejected` | CRLF injection |
| `PlayerName_UnicodeEdgeCases_HandledSafely` | Unicode edge cases |
| `PlayerName_ExtremelyLong_RejectedOrTruncated` | Size limits |
| `PlayerName_JsonInjection_Sanitized` | JSON structure injection |

---

## E2E Tests (`MultiRoomAudio.E2ETests`)

### Infrastructure Files

#### `PlaywrightFixture.cs`
Manages the test server and Playwright browser lifecycle:
- Starts the application as a separate process
- Sets `MOCK_HARDWARE=true` and isolated config paths
- Waits for `/api/health` to confirm server is ready
- Launches Chromium in headless mode
- Provides `CreatePageAsync()` for test isolation
- Cleans up processes and temp files on disposal

Uses xUnit's `ICollectionFixture` to share one server instance across all E2E tests.

### UI Flow Tests

#### `WizardFlowTests.cs`
Tests the onboarding wizard user experience.

| Test | Description |
|------|-------------|
| `HomePage_LoadsSuccessfully` | Page loads with title |
| `Wizard_CanBeOpened` | Wizard modal opens |
| `Wizard_Step1_ShowsSoundCards` | Shows mock devices |
| `Wizard_CanChangeCardProfile` | Profile selection works |
| `Wizard_CanSkip` | Skip dismisses wizard |
| `Wizard_CanNavigateBackAndForth` | Navigation works |

#### `PlayerManagementTests.cs`
Tests the player management UI.

| Test | Description |
|------|-------------|
| `PlayersSection_LoadsSuccessfully` | Players view renders |
| `CreatePlayerForm_HasRequiredFields` | Form has name/device |
| `CreatePlayer_WithValidData_Succeeds` | UI player creation |
| `PlayerCard_ShowsStatus` | Status badge visible |
| `DeletePlayer_RemovesFromList` | UI deletion works |
| `VolumeSlider_UpdatesVolume` | Volume slider interaction |

#### `SoundCardPageTests.cs`
Tests the sound card configuration UI.

| Test | Description |
|------|-------------|
| `SoundCardsTab_DisplaysMockCards` | Cards visible in modal |
| `SoundCards_ShowBusTypeIcons` | PCI/USB/BT icons |
| `ProfileDropdown_ShowsAvailableProfiles` | Profile options listed |
| `MuteButton_TogglesState` | Mute toggle works |
| `CardExpand_ShowsDetails` | Expand/collapse cards |

### Feature UI Tests

#### `CustomSinksE2ETests.cs`
Tests custom sink creation via UI and API.

| Test | Description |
|------|-------------|
| `CustomSinksModal_OpensFromMenu` | Modal opens |
| `CreateRemapSink_ViaUI_Works` | UI sink creation |
| `CreatePlayerWithCustomSink_ViaAPI_Succeeds` | Player uses custom sink |
| `PlayTestTone_ViaAPI_Succeeds` | Test tone playback |
| `CustomSinkList_ShowsCreatedSinks` | Sinks listed |

#### `TriggersE2ETests.cs`
Tests 12V trigger relay control features.

| Test | Description |
|------|-------------|
| `TriggersSection_VisibleInCustomSinksModal` | UI section visible |
| `TriggersSection_ShowsChannelList` | Channels 1-8 shown |
| `TriggerMasterToggle_EnablesDisables` | Master toggle works |
| `TriggerChannel_HasDevicePatternInput` | Pattern config UI |
| `ConfigureTrigger_ViaAPI_Succeeds` | API configuration |
| `GetTriggers_ViaAPI_ReturnsStatus` | API status endpoint |
| `TriggerChannel_InvalidChannel_ReturnsBadRequest` | Validation |
| `TriggerOffDelay_RejectsNegativeValues` | Negative delay rejected |
| `MultipleChannels_IndependentConfiguration` | Multi-channel config |

#### `LogsViewE2ETests.cs`
Tests log viewing functionality.

| Test | Description |
|------|-------------|
| `LogsView_AccessibleFromNavigation` | Navigate to logs |
| `LogsView_ShowsLogEntries` | Log entries visible |
| `LogsView_HasLevelFilter` | Filter by log level |
| `LogsView_HasSearchInput` | Search functionality |
| `LogsView_HasExportButton` | Export logs button |
| `LogsView_HasAutoRefreshToggle` | Auto-refresh control |
| `LogEntry_ShowsTimestamp` | Timestamps displayed |
| `LogEntry_LevelHasColorCoding` | Level colors |

#### `PlayerControlsE2ETests.cs`
Tests player control UI interactions.

| Test | Description |
|------|-------------|
| `PlayerControls_VolumeSliderWorks` | Volume adjustment |
| `PlayerControls_StopButtonWorks` | Stop player |
| `PlayerControls_RestartButtonWorks` | Restart player |
| `PlayerControls_DeleteRequiresConfirmation` | Delete confirmation |

---

## Test Categories

Tests can be run by category using `--filter`:

```bash
# Security tests
dotnet test --filter "FullyQualifiedName~Security"

# Concurrency tests
dotnet test --filter "FullyQualifiedName~Concurrency"

# E2E wizard tests
dotnet test --filter "FullyQualifiedName~Wizard"
```

---

## Writing New Tests

### API Tests

```csharp
public class MyNewTests : ApiTestBase, IAsyncLifetime
{
    public MyNewTests(MockHardwareWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupPlayersAsync();
    }

    [Fact]
    public async Task MyTest_DoesExpectedThing()
    {
        // Arrange
        var request = new { name = "Test", device = "alsa_output.pci-0000_00_1f.3.analog-stereo" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/players", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

### E2E Tests

```csharp
[Collection("Playwright")]
public class MyNewE2ETests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;

    public MyNewE2ETests(PlaywrightFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => _page = await _fixture.CreatePageAsync();

    public async Task DisposeAsync() => await _page.Context.CloseAsync();

    [Fact]
    public async Task MyE2ETest_WorksCorrectly()
    {
        await _page.GotoAsync("/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var heading = await _page.Locator("h1").TextContentAsync();
        heading.Should().Contain("Multi-Room Audio");
    }
}
```

---

## Mock Hardware Mode

All tests run with `MOCK_HARDWARE=true`, which provides:

- **6 mock sound cards**: Built-in Audio, Xonar AE, Schiit Modi, JBL Flip 6, Sony WH-1000XM4, NVidia HDMI
- **Multiple bus types**: PCI, USB, Bluetooth, HDMI
- **Simulated profiles**: Stereo, surround, A2DP, HSP/HFP
- **Simulated test tones**: Quick delay instead of actual audio playback
- **Custom sink recognition**: Mock backend recognizes custom sinks for player creation

This enables testing audio functionality without physical hardware.
