using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace Carabia.Web.UiTests.Infrastructure;

internal static class LoginForm
{
    public static IWebElement OpenWithIndiaSelected(BrowserFixture browser)
    {
        browser.Open("/");
        browser.ShowStep("Login: opening Sign in");
        var signIn = browser.WaitFor("the Sign in button to be visible and enabled on the home page", driver =>
        {
            var button = driver.FindElement(By.Id("profileBtn"));
            return button.Displayed && button.Enabled ? button : null;
        });
        signIn!.Click();

        var countrySelect = browser.WaitFor("the login country dropdown to contain India (+91)", driver =>
        {
            var element = driver.FindElement(By.CssSelector("#overlay .step--active .countrySelect"));
            if (!element.Displayed || !element.Enabled)
                return null;

            var select = new SelectElement(element);
            return select.Options.Any(option => option.GetAttribute("value") == "+91") ? select : null;
        });
        countrySelect!.SelectByValue("+91");
        Assert.True(countrySelect.SelectedOption.Text == "India +91", "The login country dropdown did not select India (+91).");
        Assert.True(browser.Driver.FindElement(
            By.CssSelector("#overlay .step--active .form-field__selected-code")).Text == "India +91",
            "The country dropdown selected India, but the visible country label did not update to India (+91).");

        var phoneInput = browser.WaitFor("the login phone-number field to be visible and ready for typing", driver =>
        {
            var element = driver.FindElement(By.Id("phoneInput"));
            return element.Displayed && element.Enabled ? element : null;
        });
        phoneInput!.Click();
        return phoneInput;
    }
}
