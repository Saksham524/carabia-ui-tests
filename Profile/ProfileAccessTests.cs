using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;

namespace Carabia.Web.UiTests.Profile;

[Trait("Category", "Profile")]
//[Trait("Suite", "Accounts")]
public sealed class ProfileAccessTests : IClassFixture<BrowserFixture>
{
    private readonly BrowserFixture _browser;
    public ProfileAccessTests(BrowserFixture browser) => _browser = browser;

    [Fact(DisplayName = "Profile access: logged-out visitors see login instead of private details")]
    public void Anonymous_visitor_is_redirected_to_login_when_opening_profile()
    {
        _browser.Open("/Personal/Settings");
        _browser.ShowStep("Profile access: checking that a logged-out visitor sees the login popup");
        _browser.Visible(By.CssSelector("#overlay.overlay--active #phoneInput"),
            "the login popup with a visible phone-number field when a logged-out visitor opens their profile");
        Assert.True(_browser.Driver.FindElements(By.CssSelector("form.settings")).Count == 0,
            "Profile access failed: private profile fields were rendered for a logged-out visitor.");
        var isAnonymous = ((IJavaScriptExecutor)_browser.Driver).ExecuteAsyncScript("""
            const done = arguments[arguments.length - 1];
            fetch('/Identity/AuthStatus', { credentials: 'same-origin', cache: 'no-store' })
                .then(response => response.ok ? response.json() : null)
                .then(status => done(status?.isAuthenticated === false))
                .catch(() => done(false));
            """);
        Assert.True(isAnonymous is true,
            "Profile access could not be verified: AuthStatus did not confirm that this browser is logged out.");
        _browser.ShowStep("PASS: login is required and private profile details are not shown");
        _browser.PauseForPresentation();
    }
}
