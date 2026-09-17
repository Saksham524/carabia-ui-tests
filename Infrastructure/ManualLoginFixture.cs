using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace Carabia.Web.UiTests.Infrastructure;

[CollectionDefinition("Manual login", DisableParallelization = true)]
public sealed class ManualLoginCollection : ICollectionFixture<ManualLoginFixture>
{
}

public sealed class ManualLoginFixture : IDisposable
{
    private BrowserFixture? _browser;
    private readonly Lazy<BrowserFixture> _signedInBrowser;

    public ManualLoginFixture()
    {
        // Lazy initialization also caches login failures, avoiding repeated OTP prompts.
        _signedInBrowser = new Lazy<BrowserFixture>(() =>
        {
            _browser = new BrowserFixture(forceVisible: true);
            SignIn();
            return _browser;
        });
    }

    public BrowserFixture Browser => _signedInBrowser.Value;

    private void SignIn()
    {
        var browser = _browser!;
        LoginForm.OpenWithIndiaSelected(browser);
        browser.ShowStep("Your turn: India (+91) selected. Enter your phone number and OTP. You have 10 minutes to finish login.");

        var loginWait = new WebDriverWait(browser.Driver, TimeSpan.FromMinutes(10))
        {
            PollingInterval = TimeSpan.FromSeconds(2),
            Message = "Manual login was not completed within 10 minutes. Enter your phone number and OTP in Chrome and complete any remaining signup steps."
        };
        // OTP verification and signup can navigate while the authentication check is running.
        loginWait.IgnoreExceptionTypes(typeof(JavaScriptException));

        var authenticatedAsClient = loginWait.Until(driver =>
            ((IJavaScriptExecutor)driver).ExecuteAsyncScript("""
                const done = arguments[arguments.length - 1];
                fetch('/Identity/AuthStatus', { credentials: 'same-origin', cache: 'no-store' })
                    .then(response => response.ok ? response.json() : null)
                    .then(status => done(status?.isAuthenticated === true && status?.isClient === true))
                    .catch(() => done(false));
                """) is true);

        Assert.True(authenticatedAsClient, "Expected an authenticated client after manual login.");
        browser.ShowStep("PASS: signed in as a client. Profile checks will now run automatically.");
        browser.PauseForPresentation();
    }

    public void Dispose() => _browser?.Dispose();
}
