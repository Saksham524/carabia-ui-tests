using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;

namespace Carabia.Web.UiTests.Login;

[Collection("Manual login")]
//[Trait("Suite", "Accounts")]
public sealed class LoginSmokeTests
{
    private readonly ManualLoginFixture _session;

    public LoginSmokeTests(ManualLoginFixture session) => _session = session;

    [Fact]
    [Trait("Category", "Login")]
    [Trait("Category", "Smoke")]
    [Trait("Category", "Manual")]
    public void User_can_log_in_manually_with_India_selected()
    {
        var browser = _session.Browser;
        var authenticated = ((IJavaScriptExecutor)browser.Driver).ExecuteAsyncScript("""
            const done = arguments[arguments.length - 1];
            fetch('/Identity/AuthStatus', { credentials: 'same-origin', cache: 'no-store' })
                .then(response => response.json())
                .then(status => done(status.isAuthenticated === true && status.isClient === true))
                .catch(() => done(false));
            """);
        Assert.True(authenticated is true, "Expected an authenticated client after manual login.");
    }
}
