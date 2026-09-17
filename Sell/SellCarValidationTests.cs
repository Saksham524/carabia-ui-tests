using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace Carabia.Web.UiTests.Sell;

[Trait("Suite", "PublicPages")]
[Trait("Category", "SellCar")]
public sealed class SellCarValidationTests(BrowserFixture browser) : IClassFixture<BrowserFixture>
{
    [Fact(DisplayName = "Sell car: empty vehicle details cannot continue")]
    public void Empty_vehicle_cannot_continue()
    {
        Open();
        Assert.False(Field("sellCarVinNext").Enabled, "VIN Next should be disabled when VIN is empty.");
        Assert.False(Field("sellCarManualNext").Enabled, "Manual Next should be disabled until vehicle details are complete.");
        Assert.True(Field("sellCarYearInput").Enabled, "Year selection should be available without a VIN.");
        Assert.True(new SelectElement(Field("sellCarYearInput")).Options.Count > 1, "The form needs available vehicle years.");
        Assert.False(Field("sell-car-step-2").Displayed, "Vehicle details step should not open without a vehicle.");
    }

    [Theory(DisplayName = "Sell car: invalid VIN length shows an error and stays on step one")]
    [InlineData("ABC123")]
    [InlineData("123456789012345678")]
    public void Invalid_vin_length_is_rejected(string vin)
    {
        Open();
        Field("sellCarVinInput").SendKeys(vin);
        browser.WaitFor("VIN Next to become enabled", _ => Field("sellCarVinNext").Enabled);
        Field("sellCarVinNext").Click();
        var error = browser.Visible(By.Id("sellCarLookupMessage"), "the VIN validation error");
        Assert.Contains("VIN must be 17 characters", error.Text);
        Assert.True(Field("sell-car-step-1").Displayed, "Invalid VIN must keep the user on step one.");
        Assert.False(Field("sell-car-step-2").Displayed, "Invalid VIN must not advance to vehicle details.");
    }

    [Fact(DisplayName = "Sell car: clearing VIN restores manual year selection")]
    public void Clearing_vin_restores_manual_selection()
    {
        Open();
        var vin = Field("sellCarVinInput");
        vin.SendKeys("ABC123");
        Assert.False(Field("sellCarYearInput").Enabled, "Entering a VIN should disable manual year selection.");
        vin.SendKeys(Keys.Control + "a");
        vin.SendKeys(Keys.Backspace);
        browser.WaitFor("manual year selection after clearing VIN", _ => Field("sellCarYearInput").Enabled);
        Assert.False(Field("sellCarVinNext").Enabled, "VIN Next must be disabled again after clearing the VIN.");
    }

    [Fact(DisplayName = "Sell car: VIN help expands and collapses")]
    public void Vin_help_toggles()
    {
        Open();
        var help = browser.Driver.FindElement(By.CssSelector(".van-find-text"));
        var answer = browser.Driver.FindElement(By.CssSelector(".van-find-text-ans"));
        Assert.False(answer.Displayed, "VIN help should start collapsed.");
        help.Click();
        browser.WaitFor("VIN help to open", _ => answer.Displayed);
        Assert.Contains("17-character VIN", answer.Text);
        help.Click();
        browser.WaitFor("VIN help to close", _ => !answer.Displayed);
    }

    private IWebElement Field(string id) => browser.Driver.FindElement(By.Id(id));

    private void Open()
    {
        browser.Open("/personal/sell");
        var step = browser.Visible(By.Id("sell-car-step-1"), "the initial Sell My Car step");
        browser.ShowStep("Check Sell My Car validation", step);
        browser.PauseForPresentation();
    }
}
