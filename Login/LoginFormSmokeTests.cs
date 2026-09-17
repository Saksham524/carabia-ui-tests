using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Login;

//[Trait("Suite", "Accounts")]
//[Trait("Category", "Login")]
public sealed class LoginFormSmokeTests : IClassFixture<BrowserFixture>
{
    private readonly BrowserFixture _browser;
    private readonly ITestOutputHelper _output;

    public LoginFormSmokeTests(BrowserFixture browser, ITestOutputHelper output)
    {
        _browser = browser;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Login_form_supports_India_and_phone_entry()
    {
        try
        {
            var phone = LoginForm.OpenWithIndiaSelected(_browser);
            var wait = new WebDriverWait(_browser.Driver, TimeSpan.FromSeconds(15));
            phone.SendKeys("9876543210");
            wait.Until(driver => driver.FindElement(By.CssSelector("#overlay .step1Continue")).GetAttribute("aria-disabled") == "false");
            // Do not click Continue: this check never requests an OTP.
        }
        finally
        {
            foreach (var entry in _browser.Driver.Manage().Logs.GetLog(LogType.Browser))
                _output.WriteLine(entry.ToString());
        }
    }
}
