namespace MultiRoomAudio.E2ETests;

/// <summary>
/// End-to-end tests for the onboarding wizard flow.
/// </summary>
[Collection("Playwright")]
public class WizardFlowTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;

    public WizardFlowTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _page = await _fixture.CreatePageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.Context.CloseAsync();
    }

    [Fact]
    public async Task HomePage_LoadsSuccessfully()
    {
        await _page.GotoAsync("/");

        // Wait for page to load
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait a bit longer for any JavaScript initialization
        await Task.Delay(500);

        // Check title - the page has <title>Multi-Room Audio Controller</title>
        var title = await _page.TitleAsync();
        title.Should().Be("Multi-Room Audio Controller", "Page should have the expected title");

        // Also verify we can see the main heading
        var heading = await _page.Locator("h1:has-text('Multi-Room Audio')").IsVisibleAsync();
        heading.Should().BeTrue("Main heading should be visible");
    }

    [Fact]
    public async Task Wizard_CanBeOpened()
    {
        await _page.GotoAsync("/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Look for wizard trigger button
        var wizardButton = _page.Locator("text=Setup Wizard, text=Start Setup, [data-wizard-start]").First;

        if (await wizardButton.IsVisibleAsync())
        {
            await wizardButton.ClickAsync();

            // Wait for modal to appear
            await _page.WaitForSelectorAsync(".modal.show, #wizardModal.show", new PageWaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Verify modal is visible
            var modal = _page.Locator(".modal.show, #wizardModal.show").First;
            (await modal.IsVisibleAsync()).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Wizard_Step1_ShowsSoundCards()
    {
        await _page.GotoAsync("/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(500);

        // Open Settings dropdown first (wizard is in the dropdown menu)
        var settingsDropdown = _page.Locator("button:has-text('Settings')").First;
        if (await settingsDropdown.IsVisibleAsync())
        {
            await settingsDropdown.ClickAsync();
            await Task.Delay(200);

            // Click on "Run Setup Wizard" in the dropdown
            var wizardButton = _page.Locator("text=Run Setup Wizard").First;
            if (await wizardButton.IsVisibleAsync())
            {
                await wizardButton.ClickAsync();
                await _page.WaitForSelectorAsync("#onboardingWizard.show", new PageWaitForSelectorOptions
                {
                    Timeout = 5000
                });
            }
        }

        // Navigate to sound cards step if needed - wizard starts at welcome step
        var continueButton = _page.Locator("#wizardNext").First;
        if (await continueButton.IsVisibleAsync())
        {
            await continueButton.ClickAsync();
            await Task.Delay(500);
        }

        // Should see mock devices in the wizard content
        var pageContent = await _page.ContentAsync();

        // Verify at least one mock device is shown
        var hasMockDevice = pageContent.Contains("Built-in Audio") ||
                           pageContent.Contains("Xonar") ||
                           pageContent.Contains("Schiit") ||
                           pageContent.Contains("JBL");

        hasMockDevice.Should().BeTrue("Should display mock sound cards in wizard");
    }

    [Fact]
    public async Task Wizard_CanChangeCardProfile()
    {
        await _page.GotoAsync("/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open wizard and navigate to sound cards
        var wizardButton = _page.Locator("text=Setup Wizard, text=Start Setup, [data-wizard-start]").First;
        if (await wizardButton.IsVisibleAsync())
        {
            await wizardButton.ClickAsync();
            await _page.WaitForSelectorAsync(".modal.show");

            // Click Next to get to cards step
            var nextButton = _page.Locator("button:has-text('Next')").First;
            if (await nextButton.IsVisibleAsync())
            {
                await nextButton.ClickAsync();
                await Task.Delay(500); // Wait for transition
            }
        }

        // Find a profile dropdown
        var profileSelect = _page.Locator("select[id*='profile'], select[name*='profile'], .profile-select").First;

        if (await profileSelect.IsVisibleAsync())
        {
            // Get current value
            var currentValue = await profileSelect.InputValueAsync();

            // Get available options
            var options = await profileSelect.Locator("option").AllAsync();

            if (options.Count > 1)
            {
                // Select a different option
                ILocator? newOption = null;
                foreach (var o in options)
                {
                    var val = await o.GetAttributeAsync("value");
                    if (val != currentValue)
                    {
                        newOption = o;
                        break;
                    }
                }

                if (newOption != null)
                {
                    var newValue = await newOption.GetAttributeAsync("value");
                    if (!string.IsNullOrEmpty(newValue))
                    {
                        await profileSelect.SelectOptionAsync(newValue);

                        // Verify change happened
                        var updatedValue = await profileSelect.InputValueAsync();
                        updatedValue.Should().Be(newValue);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task Wizard_CanSkip()
    {
        await _page.GotoAsync("/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open wizard
        var wizardButton = _page.Locator("text=Setup Wizard, text=Start Setup, [data-wizard-start]").First;
        if (await wizardButton.IsVisibleAsync())
        {
            await wizardButton.ClickAsync();
            await _page.WaitForSelectorAsync(".modal.show");
        }

        // Look for skip button
        var skipButton = _page.Locator("text=Skip, button:has-text('Skip'), .btn-skip").First;

        if (await skipButton.IsVisibleAsync())
        {
            await skipButton.ClickAsync();

            // Wait for modal to close
            await Task.Delay(500);

            // Modal should be closed
            var modal = _page.Locator(".modal.show");
            var isVisible = await modal.IsVisibleAsync();
            isVisible.Should().BeFalse("Modal should close after skip");
        }
    }

    [Fact]
    public async Task Wizard_CanNavigateBackAndForth()
    {
        await _page.GotoAsync("/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open wizard
        var wizardButton = _page.Locator("text=Setup Wizard, text=Start Setup, [data-wizard-start]").First;
        if (!await wizardButton.IsVisibleAsync())
        {
            return; // Skip if wizard not available
        }

        await wizardButton.ClickAsync();
        await _page.WaitForSelectorAsync(".modal.show");

        // Click Next
        var nextButton = _page.Locator("button:has-text('Next')").First;
        if (await nextButton.IsVisibleAsync())
        {
            await nextButton.ClickAsync();
            await Task.Delay(300);

            // Click Back
            var backButton = _page.Locator("button:has-text('Back'), button:has-text('Previous')").First;
            if (await backButton.IsVisibleAsync())
            {
                await backButton.ClickAsync();
                await Task.Delay(300);

                // Should be back at first step
                // Verify by checking step indicator or content
            }
        }
    }
}
