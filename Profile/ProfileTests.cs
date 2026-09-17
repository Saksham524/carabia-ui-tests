using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;

namespace Carabia.Web.UiTests.Profile;

[Collection("Manual login")]
[Trait("Category", "Profile")]
[Trait("Category", "Manual")]
//[Trait("Suite", "Accounts")]
public sealed class ProfileTests
{
    private static readonly string[] Fields = ["FirstName", "LastName", "User.Email", "User.Phone"];
    private readonly BrowserFixture _browser;
    private IWebDriver Driver => _browser.Driver;
    private IJavaScriptExecutor Script => (IJavaScriptExecutor)Driver;

    public ProfileTests(ManualLoginFixture session) => _browser = session.Browser;

    [Fact(DisplayName = "Profile: personal details are shown and initially read-only")]
    public void Profile_displays_personal_information_and_readonly_fields()
    {
        OpenProfile("Checking your personal details and read-only fields");
        var heading = _browser.Visible(By.CssSelector(".settings__title"), "the Personal Information heading");
        Assert.True(heading.Text == "Personal Information", $"Profile heading should be 'Personal Information', but says '{heading.Text}'.");
        var activeLink = Driver.FindElement(By.CssSelector(".content__nav .nav__link_state_active"));
        Assert.True(activeLink.Text.Trim() == "Settings", $"Settings should be the active profile navigation item, but '{activeLink.Text}' is active.");

        foreach (var name in Fields)
        {
            var field = Field(name);
            Assert.True(field.Displayed, $"Profile field '{name}' should be visible.");
            Assert.False(string.IsNullOrWhiteSpace(field.GetDomProperty("value")),
                $"Profile field '{name}' should contain the signed-in client's details.");
            Assert.True(IsReadOnly(field), $"Profile field '{name}' should initially be read-only.");
        }
    }

    [Theory(DisplayName = "Profile: Edit unlocks only the selected field")]
    [InlineData("FirstName")]
    [InlineData("LastName")]
    [InlineData("User.Email")]
    [InlineData("User.Phone")]
    public void Edit_unlocks_only_the_selected_profile_field(string name)
    {
        OpenProfile($"Checking the Edit button for {name}");
        Edit(name);
        Assert.False(IsReadOnly(Field(name)), $"The {name} field is still read-only after clicking Edit.");
        foreach (var other in Fields.Where(field => field != name))
            Assert.True(IsReadOnly(Field(other)), $"Editing '{name}' should not unlock '{other}'.");
    }

    [Theory(DisplayName = "Profile: an empty name fails required-field validation")]
    [InlineData("FirstName")]
    [InlineData("LastName")]
    public void Empty_name_is_invalid_in_the_profile_form(string name)
    {
        OpenProfile($"Checking required-field validation for {name}");
        var field = Edit(name);
        field.Clear();
        Assert.True(Script.ExecuteScript("return arguments[0].validity.valueMissing;", field) is true,
            $"An empty '{name}' should fail the required-field constraint.");
        Assert.True(Script.ExecuteScript("return arguments[0].form.checkValidity();", field) is false,
            "The profile form should be invalid with a missing required name.");
    }

    [Fact(DisplayName = "Profile: email changes require verification before saving")]
    public void Changing_email_requires_verification_before_saving()
    {
        OpenProfile("Checking that email changes require verification");
        var email = Edit("User.Email");
        email.Clear();
        email.SendKeys($"profile-{Guid.NewGuid():N}@example.test");

        _browser.Visible(By.CssSelector(".send_email_confirmation"), "the Send code button after changing the email address");
        Assert.False(SaveButton().Enabled, "Save must stay disabled until the changed email is verified.");
        // Leave the Send code button untouched: this case checks the verification UI only.
    }

    [Fact(DisplayName = "Profile: reloading discards an unsaved name change")]
    public void Reload_discards_unsaved_name_changes()
    {
        OpenProfile("Checking that unsaved name changes are discarded");
        var originalName = Field("FirstName").GetDomProperty("value");
        Replace(Edit("FirstName"), $"Unsaved{Guid.NewGuid():N}");
        OpenProfile();
        Assert.True(originalName == Field("FirstName").GetDomProperty("value"), "The first name changed permanently even though Save was not clicked.");
        Assert.True(IsReadOnly(Field("FirstName")), "The first-name field should return to read-only after reloading.");
    }

    [Fact(DisplayName = "Profile: saved names persist, then original names are restored")]
    public void Saved_names_persist_after_reload_and_original_profile_is_restored()
    {
        OpenProfile("Checking name saving and restoration of your original details");
        var originalFirstName = Field("FirstName").GetDomProperty("value");
        var originalLastName = Field("LastName").GetDomProperty("value");
        // defaultValue preserves the server-rendered phone before any display formatting.
        var originalForm = Script.ExecuteScript("""
            const form = document.querySelector('form.settings');
            const data = new FormData(form);
            for (const input of form.querySelectorAll('input[name]')) {
                if (['FirstName', 'LastName', 'User.Phone', 'User.Email'].includes(input.name))
                    data.set(input.name, input.defaultValue);
            }
            return JSON.stringify(Array.from(data.entries()));
            """) as string ?? throw new InvalidOperationException("Could not capture the original profile for cleanup.");
        var settingsUrl = Driver.FindElement(By.CssSelector("form.settings")).GetAttribute("action");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var firstName = $"UiTest{suffix}";
        var lastName = $"Profile{suffix}";
        var saveAttempted = false;

        try
        {
            Replace(Edit("FirstName"), firstName);
            Replace(Edit("LastName"), lastName);
            var oldForm = Driver.FindElement(By.CssSelector("form.settings"));
            var save = _browser.WaitFor("the Save changes button to become visible and enabled after editing the names",
                _ => SaveButton().Displayed && SaveButton().Enabled ? SaveButton() : null);
            saveAttempted = true;
            save!.Click();
            _browser.WaitFor("the profile page to reload after Save changes (check for a validation error or failed save request)", driver =>
            {
                try { _ = oldForm.TagName; return false; }
                catch (StaleElementReferenceException) { return true; }
            });

            // A fresh GET verifies persistence, rather than just the values left in the inputs.
            OpenProfile();
            Assert.True(firstName == Field("FirstName").GetDomProperty("value"), "Save did not persist the changed first name after reloading the profile.");
            Assert.True(lastName == Field("LastName").GetDomProperty("value"), "Save did not persist the changed last name after reloading the profile.");
        }
        finally
        {
            if (saveAttempted)
            {
                // Cleanup uses the controller directly so a broken Save UI cannot block restoration.
                var restored = Script.ExecuteAsyncScript("""
                    const [url, original, done] = arguments;
                    const body = new URLSearchParams(JSON.parse(original));
                    fetch(url, { method: 'POST', credentials: 'same-origin', body })
                        .then(response => response.ok ? response.json() : null)
                        .then(result => done(result?.success === true))
                        .catch(() => done(false));
                    """, settingsUrl, originalForm);
                Assert.True(restored is true,
                    "PROFILE CLEANUP FAILED: the original details could not be restored. Check this test account's profile before using it again.");
                OpenProfile();
                Assert.True(originalFirstName == Field("FirstName").GetDomProperty("value"), "Profile cleanup failed: the original first name was not restored.");
                Assert.True(originalLastName == Field("LastName").GetDomProperty("value"), "Profile cleanup failed: the original last name was not restored.");
            }
        }
    }

    private void OpenProfile(string description = "Checking saved profile details")
    {
        _browser.Open("/Personal/Settings");
        Assert.True(new Uri(Driver.Url).AbsolutePath.TrimEnd('/').Equals("/personal/settings", StringComparison.OrdinalIgnoreCase),
            $"The signed-in profile did not open. Login may have expired. Browser is at {Driver.Url}.");
        _browser.Visible(By.CssSelector("form.settings"), "the signed-in personal information form to be visible");
        _browser.ShowStep(description);
        _browser.PauseForPresentation();
    }

    private IWebElement Field(string name) => _browser.Visible(By.CssSelector($"form.settings input[name='{name}']"), $"the profile field '{name}' to be visible");

    private IWebElement Edit(string name)
    {
        var field = Field(name);
        var control = field.FindElement(By.XPath("./ancestor::div[contains(concat(' ', normalize-space(@class), ' '), ' form-control ')][1]"));
        var buttons = control.FindElements(By.CssSelector(".js-form-control-edit"));
        Assert.True(buttons.Count > 0, $"The profile field '{name}' has no Edit button.");
        buttons[0].Click();
        _browser.WaitFor($"the profile field '{name}' to become editable after clicking Edit", _ => !IsReadOnly(field));
        _browser.ShowStep($"Editing profile field: {name}", field);
        _browser.PauseForPresentation();
        return field;
    }

    private IWebElement SaveButton() => Driver.FindElement(By.CssSelector("form.settings button[data-type='submit']"));
    private bool IsReadOnly(IWebElement field) => Script.ExecuteScript("return arguments[0].readOnly;", field) is true;
    private static void Replace(IWebElement field, string value)
    {
        field.Clear();
        field.SendKeys(value);
    }
}
