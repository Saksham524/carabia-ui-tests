using System.Globalization;
using System.Text.RegularExpressions;
using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Listings;

[Trait("Category", "Listings")]
//[Trait("Suite", "PublicPages")]
public sealed class ListingsPresentationTests : IClassFixture<BrowserFixture>
{
    private readonly BrowserFixture _browser;
    private readonly ITestOutputHelper _output;
    private IWebDriver Driver => _browser.Driver;
    private IJavaScriptExecutor Script => (IJavaScriptExecutor)Driver;

    public ListingsPresentationTests(BrowserFixture browser, ITestOutputHelper output)
    {
        _browser = browser;
        _output = output;
    }

    [Fact(DisplayName = "Listings: page shows a heading and a valid result count")]
    public void Sellings_page_shows_heading_and_result_count()
    {
        var cards = OpenListings("Checking the page heading and result count");
        var heading = _browser.Visible(By.CssSelector(".fd-banner__title"), "the listings page heading to be visible");
        Assert.True(heading.Text.Contains("cars", StringComparison.OrdinalIgnoreCase),
            $"Listings heading should describe cars, but it says '{heading.Text}'.");
        var countText = _browser.Visible(By.CssSelector(".crb-results-count"), "the listings result count to be visible").Text;
        var match = Regex.Match(countText, @"\d[\d,]*");
        Assert.True(match.Success, $"Could not read the listings count from '{countText}'.");
        var count = int.Parse(match.Value.Replace(",", ""), CultureInfo.InvariantCulture);
        Assert.True(count > 0, $"The test needs at least one listing, but the page reports '{countText}'.");
        Assert.True(cards.Count <= count,
            $"The page shows {cards.Count} cards but reports only {count} total results.");
        Step($"PASS: {count} total results; {cards.Count} cards on this page");
    }

    [Fact(DisplayName = "Listings: every card has a title, metadata, price and details link")]
    public void Rendered_listing_cards_have_vehicle_information()
    {
        var cards = OpenListings("Checking each car's title, year/mileage, price and link");
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var context = $"Listing {index + 1} of {cards.Count}";
            Step($"{context}: checking vehicle information", card);
            var title = Child(card, ".listing-card__title-link", context, "vehicle title/link");
            Assert.False(string.IsNullOrWhiteSpace(title.Text), $"{context} has an empty vehicle title.");
            var meta = Child(card, ".listing-card__meta", context, "year/mileage");
            Assert.False(string.IsNullOrWhiteSpace(meta.Text), $"{context} ({title.Text}) has no year/mileage text.");
            var price = Child(card, ".listing-card__price", context, "price");
            Assert.True(Regex.IsMatch(price.Text, @"\d"), $"{context} ({title.Text}) has no numeric price. Displayed: '{price.Text}'.");
            Assert.True(Uri.TryCreate(title.GetAttribute("href"), UriKind.Absolute, out var link)
                && (link.Scheme == "http" || link.Scheme == "https") && link.AbsolutePath.StartsWith("/sellings/", StringComparison.OrdinalIgnoreCase),
                $"{context} ({title.Text}) does not have a valid listing details link.");
            _output.WriteLine($"PASS: {context}: {title.Text}");
        }
        Step($"PASS: vehicle information checked on all {cards.Count} rendered cards");
    }

    [Fact(DisplayName = "Listings: car images load (broken images are reported by listing)")]
    public void Rendered_listing_images_load()
    {
        var cards = OpenListings("Checking car images");
        var images = new List<(string Name, IWebElement Image)>();
        for (var index = 0; index < cards.Count; index++)
        {
            var context = $"Listing {index + 1} of {cards.Count}";
            var title = Child(cards[index], ".listing-card__title-link", context, "vehicle title").Text;
            Step($"{context}: checking image for {title}", cards[index]);
            images.Add(($"{context} ({title})", Child(cards[index], ".listing-card-image__img", context, "image")));
        }
        // Scrolling each card triggers lazy loading. Give outstanding images one shared wait.
        try
        {
            new WebDriverWait(Driver, TimeSpan.FromSeconds(30)).Until(_ => images.All(item =>
                Script.ExecuteScript("return arguments[0].complete;", item.Image) is true));
        }
        catch (WebDriverTimeoutException)
        {
            // Report the specific unfinished images below alongside broken ones.
        }

        var errors = new List<string>();
        foreach (var item in images)
        {
            if (Script.ExecuteScript("return arguments[0].complete && arguments[0].naturalWidth > 0;", item.Image) is not true)
                errors.Add($"{item.Name}: image did not load. Image URL: {item.Image.GetAttribute("src")}");
        }
        if (errors.Count > 0)
            Step($"FAIL: {errors.Count} of {images.Count} car images did not load; see the test result for listing names");
        Assert.True(errors.Count == 0,
            $"Listings image check failed for {errors.Count} of {images.Count} cards. The image files may be missing or unreachable.\n" + string.Join("\n", errors));
        Step($"PASS: all {images.Count} car images loaded");
    }

    [Theory(DisplayName = "Listings: top car opens its details and three sampled FAQs work")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Top_listing_opens_details_and_sample_faqs_work(int position)
    {
        var cards = OpenListings($"Opening car {position} of the top four listings");
        Assert.True(cards.Count >= position, $"Expected at least {position} listings, but only {cards.Count} cards are available.");
        var link = Child(cards[position - 1], ".listing-card__title-link", $"Listing {position}", "details link");
        var vehicle = ClickAndCheckDetails(link);
        FaqChecks.CheckSampleQuestions(_browser, _output, $"Listing {position}: {vehicle}");
    }

    [Fact(DisplayName = "Home: click the featured Audi A6 to open its details")]
    public void Home_page_Audi_A6_card_opens_the_requested_details_page()
    {
        _browser.Open("/");
        Step("Home page: finding the featured Audi A6 (listing 1056)");
        var link = _browser.WaitFor("a visible home-page listing card linking to Audi A6 /sellings/1056_audi-a6 (this example listing must still be featured)", driver =>
            driver.FindElements(By.CssSelector(".listing-card__title-link"))
                .FirstOrDefault(element => element.Displayed && Uri.TryCreate(element.GetAttribute("href"), UriKind.Absolute, out var url)
                    && url.AbsolutePath.TrimEnd('/').Equals("/sellings/1056_audi-a6", StringComparison.OrdinalIgnoreCase)));
        var vehicle = ClickAndCheckDetails(link!);
        Assert.True(vehicle.Contains("Audi", StringComparison.OrdinalIgnoreCase) && vehicle.Contains("A6", StringComparison.OrdinalIgnoreCase),
            $"The home-page Audi A6 link opened a different vehicle: {vehicle}.");
    }

    private string ClickAndCheckDetails(IWebElement link)
    {
        var cardTitle = link.Text.Trim();
        var target = link.GetAttribute("href");
        Assert.True(Uri.TryCreate(target, UriKind.Absolute, out _), $"{cardTitle}: the listing has no valid details URL.");
        Step($"Clicking the listing card: {cardTitle}", link);
        link.Click();
        _browser.WaitFor("the selected car's details page to open", driver =>
            string.Equals(new Uri(driver.Url).AbsolutePath.TrimEnd('/'), new Uri(target!).AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            && Script.ExecuteScript("return document.readyState;")?.ToString() == "complete");
        var heading = _browser.Visible(By.CssSelector(".lps-section-title"), "the vehicle details heading to be visible");
        Assert.True(Normalize(heading.Text).Contains(Normalize(cardTitle), StringComparison.OrdinalIgnoreCase),
            $"Clicked '{cardTitle}', but the vehicle details heading says '{heading.Text}'.");
        var specs = Driver.FindElements(By.CssSelector(".lps-specs-grid .lps-spec-item"))
            .ToDictionary(item => item.FindElement(By.CssSelector(".lps-spec-label")).Text.Trim(),
                item => item.FindElement(By.CssSelector(".lps-spec-value")).Text.Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "Make", "Model", "Year" })
            Assert.True(specs.TryGetValue(required, out var value) && !string.IsNullOrWhiteSpace(value),
                $"{cardTitle}: the details page is missing its {required} specification.");
        Assert.True(Regex.IsMatch(specs["Year"], @"^\d{4}$"), $"{cardTitle}: the vehicle year is invalid: '{specs["Year"]}'.");
        var price = _browser.Visible(By.CssSelector(".lps-price-desk .lps-price"), $"the asking price for {cardTitle}");
        Assert.True(Regex.IsMatch(price.Text, @"\d"), $"{cardTitle}: the details page has no numeric asking price.");
        Step($"PASS: {specs["Make"]} {specs["Model"]} details, year and asking price are shown");
        return $"{specs["Make"]} {specs["Model"]}";
    }

    private static string Normalize(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private IReadOnlyList<IWebElement> OpenListings(string description)
    {
        _browser.Open("/sellings");
        Step(description);
        return _browser.WaitFor("at least one car listing on /sellings (check that the test database has active listings)", driver =>
        {
            var cards = driver.FindElements(By.CssSelector(".crb-listing-results .listing-card"));
            return cards.Count > 0 ? cards : null;
        })!;
    }

    private void Step(string description, IWebElement? element = null)
    {
        _output.WriteLine(description);
        _browser.ShowStep(description, element);
        _browser.PauseForPresentation();
    }

    private static IWebElement Child(IWebElement card, string selector, string context, string description)
    {
        var elements = card.FindElements(By.CssSelector(selector));
        Assert.True(elements.Count > 0, $"{context} is missing its {description} element.");
        return elements[0];
    }
}
