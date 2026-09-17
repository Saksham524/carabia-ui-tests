using System.Globalization;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Infrastructure;

internal sealed class ListingsFilterPage(BrowserFixture browser, ITestOutputHelper output)
{
    private IWebDriver Driver => browser.Driver;
    private const string Results = ".crb-listing-results";

    public void Open()
    {
        browser.Open("/sellings");
        browser.Visible(By.CssSelector(".crb-filter-header"), "the listings filter sidebar");
        Assert.True(Count > 0, "Filter tests need active listings, but /sellings reports zero results.");
        Step("Listings: checking filters against the matching car cards");
    }

    public int Count => (int)Number(Driver.FindElement(By.CssSelector(Results + " .crb-results-count")).Text);

    public void Step(string message, IWebElement? element = null)
    {
        output.WriteLine(message);
        browser.ShowStep(message, element);
        browser.PauseForPresentation();
    }

    public SelectElement Select(string name) => new(Driver.FindElement(By.Name(name)));

    public void Choose(string name, string value)
    {
        var select = Select(name);
        Expand(select.WrappedElement);
        var option = select.Options.FirstOrDefault(o => o.GetAttribute("value") == value);
        Assert.True(option != null, $"Filter '{name}' does not offer value '{value}'. Available: {string.Join(", ", select.Options.Select(o => o.Text))}.");
        Assert.True(select.SelectedOption.GetAttribute("value") != value,
            $"Test setup: '{name}' is already set to '{option!.Text}'; choose a different value to exercise filtering.");
        RefreshAfter($"Set {name.Replace("filters_", "").Replace('_', ' ')} to {option!.Text}", select.WrappedElement,
            () => select.SelectByValue(value));
    }

    public void Make(string make)
    {
        Expand(Select("filters_manufacturer").WrappedElement);
        var option = Select("filters_manufacturer").Options.FirstOrDefault(o => o.Text.Equals(make, StringComparison.OrdinalIgnoreCase));
        Assert.True(option != null, $"Test data needs an available {make} make in the filter dropdown.");
        Choose("filters_manufacturer", option!.GetAttribute("value")!);
    }

    public string ChooseModelFromResults(string make)
    {
        Expand(Select("filters_model").WrappedElement);
        browser.WaitFor($"model loading for {make} to finish; check /System/GetCarModels if it stays on Loading", _ =>
            !Select("filters_model").Options.Any(o => o.Text.Contains("Loading", StringComparison.OrdinalIgnoreCase)));
        Assert.True(Select("filters_model").WrappedElement.Enabled && Select("filters_model").Options.Count > 1,
            $"After selecting {make}, the model dropdown is disabled or has no models. Expected selectable {make} models. " +
            "Check the /System/GetCarModels response and the make/model data; model filtering cannot continue.");
        var cards = Cards();
        // Prefer the longest model name to distinguish, for example, A6 from A6 Allroad.
        var option = Select("filters_model").Options.Where(o => !string.IsNullOrEmpty(o.GetAttribute("value")))
            .OrderByDescending(o => o.Text.Length)
            .FirstOrDefault(o => cards.Any(c => MatchesVehicle(c.Title, make + " " + o.Text)));
        Assert.True(option != null, $"No model option matches the displayed {make} cars. Check model lookup and listing data.");
        var model = option!.Text;
        Choose("filters_model", option.GetAttribute("value")!);
        return model;
    }

    public void Sort(string value)
    {
        var radio = Driver.FindElement(By.CssSelector($"input[name='crb_sort'][value='{value}']"));
        Expand(radio);
        var label = radio.FindElement(By.XPath("ancestor::label[1]"));
        RefreshAfter($"Sort: {label.Text}", label, label.Click);
    }

    public void Clear()
    {
        var button = Driver.FindElement(By.Id("crbClearAllBtn"));
        RefreshAfter("Clear all filters", button, button.Click);
    }

    public (decimal From, decimal To) Range(string field, Func<Card, decimal> read)
    {
        var fromName = $"filters_{field}_from";
        var toName = $"filters_{field}_to";
        var lower = NumericOptions(fromName);
        var upper = NumericOptions(toName);
        // Derive a bounded range from an actual car so that an empty result cannot pass silently.
        foreach (var car in Cards())
        {
            var value = read(car);
            var from = lower.Where(o => o.Value < value && !o.Default).OrderByDescending(o => o.Value).FirstOrDefault();
            var to = upper.Where(o => o.Value >= value && !o.Default && !o.Unbounded).OrderBy(o => o.Value).FirstOrDefault();
            if (from == null || to == null || to.Value <= from.Value) continue;
            Choose(fromName, from.Raw);
            Choose(toName, to.Raw);
            return (from.Value, to.Value);
        }
        Assert.Fail($"Test data needs a car inside the bounded {field} dropdown ranges. No suitable car was found on the current results page.");
        return default;
    }

    public void CheckRange(string field, (decimal From, decimal To) range, Func<Card, decimal> read)
    {
        var cards = Cards();
        foreach (var card in cards)
        {
            var actual = read(card);
            Assert.True(actual >= range.From && actual <= range.To,
                $"{field} filter {range.From:N0}–{range.To:N0}: '{card.Title}' shows {actual:N0}, outside the selected range. Listing: {card.Url}");
        }
        Step($"PASS: all {cards.Count} matching cards are inside the selected {field} range");
    }

    public List<Card> Cards()
    {
        var root = Driver.FindElement(By.CssSelector(Results));
        var count = Count;
        // Recommendations can follow a 'That's everything' panel even when matches exist.
        // Only direct result grids BEFORE that panel contain cars matching the filters.
        var cards = new List<Card>();
        foreach (var child in root.FindElements(By.XPath("./*")))
        {
            if ((child.GetAttribute("class") ?? "").Split(' ').Contains("nrf-card-container")) break;
            foreach (var element in child.FindElements(By.CssSelector(".listing-card")))
            {
                var title = element.FindElement(By.CssSelector(".listing-card__title-link"));
                var meta = element.FindElements(By.CssSelector(".listing-card__meta > span"));
                cards.Add(new Card(title.Text, title.GetAttribute("href")!, Number(meta.First().Text),
                    Number(meta.Last().Text), Number(element.FindElement(By.CssSelector(".listing-card__price")).Text)));
            }
        }
        Assert.True(count > 0 && cards.Count > 0,
            $"Expected matching cars after filtering, but the page reports {count} results and shows {cards.Count} matching cards. URL: {Driver.Url}");
        Assert.True(cards.Count <= count, $"Found {cards.Count} matching cards but the counter reports only {count} results.");
        output.WriteLine($"Checking {cards.Count} matching cards of {count} total results (recommendations excluded).");
        return cards;
    }

    public void CheckVehicle(string expected)
    {
        var cards = Cards();
        foreach (var card in cards)
            Assert.True(MatchesVehicle(card.Title, expected),
                $"Selected '{expected}', but matching results contain '{card.Title}'. Listing: {card.Url}");
        Step($"PASS: all {cards.Count} matching cards belong to {expected}");
    }

    private void Expand(IWebElement control)
    {
        var section = control.FindElement(By.XPath("ancestor::section[contains(@class,'crb-filter-section')][1]"));
        if (!(section.GetAttribute("class") ?? "").Contains("crb-section--collapsed")) return;
        var title = section.FindElement(By.CssSelector(".crb-section-title"));
        Step($"Open {title.Text.Trim()} filter", title);
        title.Click();
        browser.WaitFor("the filter section to expand", _ => !(section.GetAttribute("class") ?? "").Contains("crb-section--collapsed"));
    }

    private void RefreshAfter(string action, IWebElement control, Action click)
    {
        var previous = Driver.FindElement(By.CssSelector(Results + " .crb-results-count"));
        Step(action, control);
        click();
        browser.WaitFor($"updated listing results after '{action}'", driver =>
        {
            try { _ = previous.TagName; return false; }
            catch (StaleElementReferenceException) { return true; }
        });
        browser.Visible(By.CssSelector(Results + " .crb-results-count"), "the updated result count");
        Step($"{action}: {Count} matching results");
    }

    private List<NumericOption> NumericOptions(string name) => Select(name).Options
        .Where(o => decimal.TryParse(o.GetAttribute("value"), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        .Select(o => new NumericOption(o.GetAttribute("value")!,
            decimal.Parse(o.GetAttribute("value")!, CultureInfo.InvariantCulture),
            o.GetAttribute("data-filter-default") == "true",
            o.GetAttribute("data-price-unbounded") == "true" || o.GetAttribute("data-range-unbounded") == "true"))
        .ToList();

    private static bool MatchesVehicle(string title, string expected) =>
        title.Equals(expected, StringComparison.OrdinalIgnoreCase) || title.StartsWith(expected + " ", StringComparison.OrdinalIgnoreCase);

    private static decimal Number(string text)
    {
        var match = Regex.Match(text, @"\d[\d,]*(?:\.\d+)?");
        Assert.True(match.Success, $"Expected a numeric result count, year, mileage or price, but saw '{text}'.");
        return decimal.Parse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    internal sealed record Card(string Title, string Url, decimal Year, decimal Mileage, decimal Price);
    private sealed record NumericOption(string Raw, decimal Value, bool Default, bool Unbounded);
}
