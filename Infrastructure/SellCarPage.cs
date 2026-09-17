using System.Globalization;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Infrastructure;

internal sealed class SellCarPage(BrowserFixture browser, ITestOutputHelper output)
{
    // User-supplied test vehicle, verified against the local VIN lookup service.
    public const string Vin = "2FMPK4G83GBB65542";
    public const string Year = "2016";
    public const string Make = "Ford";
    public const string Model = "Edge";
    public const string Trim = "SE";
    private const string Notes = "UI automation sample - summary verification only.";
    private const string Mileage = "80000";
    private const string Price = "40000";
    private IWebDriver Driver => browser.Driver;
    private IJavaScriptExecutor Script => (IJavaScriptExecutor)Driver;

    public void RunWithDiagnostics(Action run)
    {
        try { run(); }
        catch
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"carabia-sell-failure-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(path);
                output.WriteLine($"Failure screenshot: {path}");
                foreach (var entry in Driver.Manage().Logs.GetLog(LogType.Browser).Where(e => e.Level == LogLevel.Severe))
                    output.WriteLine($"Browser error: {entry.Message}");
            }
            catch (WebDriverException) { /* Preserve the original test failure. */ }
            throw;
        }
    }

    public void OpenVehicle(bool manual)
    {
        browser.Open("/personal/sell");
        WaitForStep(1);
        if (manual)
        {
            Choose("sellCarYearInput", Year);
            Choose("sellCarMakeInput", Make);
            Choose("sellCarModelInput", Model);
            Choose("sellCarSpecInput", Trim);
            Choose("sellCarRegionalSpecInput", "GCC");
            Assert.Equal("", Value("sellCarVinInput"));
            ClickId("sellCarManualNext");
        }
        else
        {
            Fill("sellCarVinInput", Vin);
            ClickId("sellCarVinNext");
        }
        WaitForStep(2);
        var header = Driver.FindElement(By.CssSelector("#sell-car-step-2 .vs-card-sec .vs-card-header"));
        foreach (var expected in new[] { Make, Model, Trim, Year })
            Assert.Contains(expected, header.Text, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(Value("sellCarAdmeId")), "Vehicle lookup must provide an ADME vehicle ID.");
        if (!manual) Assert.Equal(Vin, Value("sellCarDecodedVin"));
        Step($"PASS: {(manual ? "manual search" : "VIN lookup")} identifies {Year} {Make} {Model} {Trim}");
    }

    public void FillDetailsAndFeatures()
    {
        Fill("sellCarMileageInput", Mileage);
        SelectColor("exterior", "White");
        SelectColor("interior", "Black");
        Choose("sellCarWarrantyInput", "No");
        Fill("sellCarLocationInput", "Dubai", blur: false);
        var suggestion = browser.WaitFor("a Google Places suggestion for Dubai (check Maps configuration if none appears)", _ =>
            Driver.FindElements(By.CssSelector(".pac-item")).FirstOrDefault(e => e.Displayed && e.Text.Contains("Dubai", StringComparison.OrdinalIgnoreCase)));
        Click(suggestion!, "Select Dubai from location suggestions");
        browser.WaitFor("a resolved UAE location", _ => Value("sellCarLocationCountry") == "AE" && Value("sellCarLocationPlaceId") != "");
        Assert.Equal("Dubai", Value("sellCarLocationCity"));
        ClickId("sellCarVehicleDetailsNext");
        WaitForStep(3);
        Click(Driver.FindElement(By.CssSelector("button[onclick='openOtherNotesPopup()']")), "Add sample owner notes");
        Fill("otherNotesInput", Notes);
        Click(Driver.FindElement(By.CssSelector("#other-notes-overlay .btn-save")), "Save owner notes");
        browser.WaitFor("owner notes popup to close", _ => !Field("otherNotesInput").Displayed);
        // The chip intentionally truncates long notes; reopen to verify the saved text.
        Assert.False(string.IsNullOrWhiteSpace(Field("other-note-wrap").Text));
        Click(Driver.FindElement(By.CssSelector("button[onclick='openOtherNotesPopup()']")), "Verify saved owner notes");
        Assert.Equal(Notes, Value("otherNotesInput"));
        Click(Driver.FindElement(By.CssSelector("#other-notes-overlay .btn-back")), "Close owner notes");
        Step("PASS: vehicle details accepted and features step reached");
    }

    public void CompleteToSummary(string[] photos)
    {
        Click(Driver.FindElement(By.CssSelector("#sell-car-step-3 [data-sell-car-target-step='4']")), "Continue to asking price");
        WaitForStep(4);
        Fill("ps-price-value", Price);
        Click(Driver.FindElement(By.CssSelector("#sell-car-step-4 [data-step-next='5']")), "Continue to photos");
        WaitForStep(5);

        // Exercise minimum-photo validation before supplying the user's files.
        ClickId("sellCarPhotosNext");
        var error = browser.Visible(By.Id("pug-photo-error"), "the four-photo minimum validation error");
        Assert.Contains("at least 4 photos", error.Text);
        Assert.True(Field("sell-car-step-5").Displayed, "Empty photos must keep the user on the photo step.");
        Field("pug-file-input").SendKeys(string.Join("\n", photos));
        browser.WaitFor($"all {photos.Length} photo previews to load", _ =>
        {
            var previews = Driver.FindElements(By.CssSelector("#pug-grid .pug-cell > img"));
            return previews.Count == photos.Length && previews.All(img =>
                Script.ExecuteScript("return arguments[0].complete && arguments[0].naturalWidth > 0;", img) is true);
        }, 60);
        var coverSource = Driver.FindElement(By.CssSelector("#pug-grid .pug-is-cover > img")).GetAttribute("src");
        Step($"PASS: {photos.Length} photos loaded; cover selected");
        ClickId("sellCarPhotosNext");
        WaitForStep(6);
        ClickId("sellCarOwnerVerificationSkip");
        browser.WaitFor("listing qualification to show plans or boosts", _ => Field("sell-car-step-7").Displayed || Field("sell-car-step-8").Displayed, 60);
        if (Field("sell-car-step-7").Displayed)
        {
            var premium = Driver.FindElement(By.CssSelector("input[name='pricing_plan'][value='Premium']"));
            if (!premium.Selected) Click(premium.FindElement(By.XPath("ancestor::label[1]")), "Select Premium without the paid upgrade");
            ClickId("sellCarPremiumContinueBtn");
        }
        WaitForStep(8);
        var noBoost = Driver.FindElement(By.CssSelector("input[name='boost_plan'][data-no-boost='true']"));
        if (!noBoost.Selected) Click(noBoost.FindElement(By.XPath("ancestor::label[1]")), "Select No Boost");
        ClickId("sellCarBoostContinueBtn");
        WaitForStep(9);

        Assert.Contains(Make, Field("summaryCarName").Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Model, Field("summaryCarName").Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Year, Field("summaryCarMeta").Text);
        Assert.Contains(Trim, Field("summaryCarMeta").Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("80000", Field("summaryCarMeta").Text.Replace(",", ""));
        Assert.Equal(decimal.Parse(Price, CultureInfo.InvariantCulture), Number(Field("summaryAskingPrice").Text));
        Assert.Equal("White, Black", Field("summaryColours").Text);
        Assert.Equal("No", Field("summaryVehicleWarranty").Text);
        Assert.Contains("Dubai", Field("summaryLocation").Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Notes, Field("summaryOwnerNotes").Text);
        Assert.Equal($"{photos.Length} photos uploaded", Field("summaryPhotoCount").Text);
        Assert.Equal("Mulkiya not uploaded", Field("summaryOwnerVerificationText").Text);
        Assert.Equal(0m, Number(Field("summaryPaymentTotal").Text));
        Assert.True(Field("summaryCoverImage").GetAttribute("src") == coverSource, "Summary must preserve the uploaded cover photo.");
        Assert.True(Field("sellCarSummarySubmitBtn").Displayed, "Summary must offer the submission button.");
        Assert.False(Field("sell-car-step-10").Displayed, "The test must stop before payment.");
        Step("PASS: summary matches vehicle, price, colours, location, notes and photos. STOP: listing not submitted.");
        var screenshotPath = Path.Combine(Path.GetTempPath(), "carabia-sell-summary.png");
        ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(screenshotPath);
        output.WriteLine($"Summary screenshot: {screenshotPath}");
        // Deliberately no click on sellCarSummarySubmitBtn: this test never submits a listing.
    }

    public static string[] PhotoPaths()
    {
        var folder = Environment.GetEnvironmentVariable("CARABIA_SELL_PHOTO_DIR") ?? @"D:\CarPhotos";
        Assert.True(Directory.Exists(folder), $"Photo folder '{folder}' is missing. Set CARABIA_SELL_PHOTO_DIR to your test photo folder.");
        var photos = Directory.GetFiles(folder).Where(path =>
            new[] { ".jpg", ".jpeg", ".jfif", ".png", ".webp" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.True(photos.Length >= 4, $"Sell My Car requires at least four supported photos; found {photos.Length} in '{folder}'.");
        return photos;
    }

    private void SelectColor(string group, string color)
    {
        if (group == "interior") Click(Driver.FindElement(By.CssSelector(".tab[onclick*='interior']")), "Open interior colours");
        var item = Driver.FindElements(By.CssSelector($"#{group}-grid .color-item"))
            .SingleOrDefault(e => e.FindElement(By.CssSelector(".color-label")).Text.Equals(color, StringComparison.OrdinalIgnoreCase));
        Assert.True(item != null, $"Expected {group} colour '{color}' to be available.");
        Click(item!, $"Select {color} {group}");
    }

    private void Choose(string id, string text)
    {
        var option = browser.WaitFor($"'{text}' in {id}; check the Autodata dropdown response if missing", _ =>
        {
            var select = Field(id);
            return select.Displayed && select.Enabled ? new SelectElement(select).Options
                .FirstOrDefault(o => o.Text.Trim().Equals(text, StringComparison.OrdinalIgnoreCase)) : null;
        }, 60);
        Step($"Select {text}", Field(id));
        new SelectElement(Field(id)).SelectByValue(option!.GetAttribute("value")!);
    }

    private void Fill(string id, string value, bool blur = true)
    {
        var input = browser.Visible(By.Id(id), id);
        Step($"Fill {id}: {value}", input);
        input.SendKeys(Keys.Control + "a");
        input.SendKeys(value);
        if (blur) input.SendKeys(Keys.Tab);
    }

    private void ClickId(string id) => Click(browser.Visible(By.Id(id), id), id);
    private void Click(IWebElement element, string description)
    {
        Step(description, element);
        browser.WaitFor($"{description} to be enabled", _ => element.Enabled);
        element.Click();
    }

    private void WaitForStep(int number) => browser.WaitFor($"Sell My Car step {number}", _ =>
    {
        var error = Field("sellCarLookupMessage");
        Assert.False(error.Displayed && !string.IsNullOrWhiteSpace(error.Text), $"Vehicle lookup failed: {error.Text}");
        return Field($"sell-car-step-{number}").Displayed;
    }, 60);

    private IWebElement Field(string id) => Driver.FindElement(By.Id(id));
    private string Value(string id) => Field(id).GetDomProperty("value") ?? "";
    private static decimal Number(string text) => decimal.Parse(Regex.Replace(text, @"[^\d.]", ""), CultureInfo.InvariantCulture);
    private void Step(string message, IWebElement? element = null)
    {
        output.WriteLine(message);
        browser.ShowStep(message, element);
        browser.PauseForPresentation();
    }
}
