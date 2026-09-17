using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Listings;

[Trait("Suite", "PublicPages")]
[Trait("Category", "Listings")]
[Trait("Category", "Filters")]
public sealed class ListingsFilterTests : IClassFixture<BrowserFixture>
{
    private readonly BrowserFixture _browser;
    private readonly ListingsFilterPage _page;

    public ListingsFilterTests(BrowserFixture browser, ITestOutputHelper output)
    {
        _browser = browser;
        _page = new ListingsFilterPage(browser, output);
    }

    [Theory(DisplayName = "Filters: selecting a make shows only that make")]
    [InlineData("Audi")]
    [InlineData("BMW")]
    public void Make_filters_matching_cars(string make)
    {
        _page.Open();
        _page.Make(make);
        _page.CheckVehicle(make);
    }

    [Theory(DisplayName = "Filters: selecting a model shows only that make and model")]
    [InlineData("Audi")]
    [InlineData("BMW")]
    public void Model_filters_matching_cars(string make)
    {
        _page.Open();
        _page.Make(make);
        var model = _page.ChooseModelFromResults(make);
        _page.CheckVehicle(make + " " + model);
    }

    [Fact(DisplayName = "Filters: changing make clears the previous model")]
    public void Changing_make_resets_model()
    {
        _page.Open();
        _page.Make("Audi");
        _page.ChooseModelFromResults("Audi");
        _page.Make("BMW");
        Assert.True(_page.Select("filters_model").SelectedOption.GetAttribute("value") == "",
            "Changing from Audi to BMW should reset the selected model to Any.");
        _page.CheckVehicle("BMW");
        var model = _page.ChooseModelFromResults("BMW");
        _page.CheckVehicle("BMW " + model);
    }

    [Theory(DisplayName = "Filters: every matching car is within the selected range")]
    [InlineData("price")]
    [InlineData("year")]
    [InlineData("mileage")]
    public void Numeric_range_filters_matching_cars(string field)
    {
        _page.Open();
        Func<ListingsFilterPage.Card, decimal> read = field switch
        {
            "price" => card => card.Price,
            "year" => card => card.Year,
            _ => card => card.Mileage
        };
        var range = _page.Range(field, read);
        _page.CheckRange(field, range, read);
    }

    [Theory(DisplayName = "Filters: matching cars follow the selected sort order")]
    [InlineData("cheaper", "price", false)]
    [InlineData("expensive", "price", true)]
    [InlineData("mileage", "mileage", false)]
    public void Sort_orders_matching_cars(string sort, string field, bool descending)
    {
        _page.Open();
        _page.Sort(sort);
        var cards = _page.Cards();
        Assert.True(cards.Count >= 2, "Sorting needs at least two matching cars to compare.");
        for (var i = 1; i < cards.Count; i++)
        {
            var previous = field == "price" ? cards[i - 1].Price : cards[i - 1].Mileage;
            var current = field == "price" ? cards[i].Price : cards[i].Mileage;
            Assert.True(descending ? previous >= current : previous <= current,
                $"Sort '{sort}' is incorrect between positions {i} and {i + 1}: '{cards[i - 1].Title}' has {field} {previous:N0}, " +
                $"followed by '{cards[i].Title}' with {current:N0}. Listings: {cards[i - 1].Url} and {cards[i].Url}");
        }
        _page.Step($"PASS: all {cards.Count} matching cards follow {sort} order");
    }

    [Fact(DisplayName = "Filters: make and price work together")]
    public void Combined_make_and_price_filters()
    {
        _page.Open();
        _page.Make("Audi");
        var range = _page.Range("price", card => card.Price);
        _page.CheckVehicle("Audi");
        _page.CheckRange("price", range, card => card.Price);
    }

    [Fact(DisplayName = "Filters: Clear all restores defaults and unfiltered results")]
    public void Clear_all_restores_filters_and_results()
    {
        _page.Open();
        var initialCount = _page.Count;
        var names = new[] { "filters_manufacturer", "filters_model", "filters_price_from", "filters_price_to",
            "filters_year_from", "filters_year_to", "filters_mileage_from", "filters_mileage_to" };
        var defaults = names.ToDictionary(name => name, name => _page.Select(name).SelectedOption.GetAttribute("value"));
        _page.Make("Audi");
        // Model loading is checked separately, so its failure does not block range/reset coverage.
        _page.Sort("cheaper");
        _page.Choose("filters_price_from", "25000");
        _page.Choose("filters_price_to", "50000");
        _page.Choose("filters_year_from", "2016");
        _page.Choose("filters_year_to", "2018");
        _page.Choose("filters_mileage_from", "2500");
        _page.Choose("filters_mileage_to", "5000");
        _page.Clear();
        var resetErrors = new List<string>();
        foreach (var name in names)
        {
            var actual = _page.Select(name).AllSelectedOptions.FirstOrDefault()?.GetAttribute("value");
            if (actual != defaults[name])
                resetErrors.Add($"{name.Replace("filters_", "").Replace('_', ' ')}: expected initial value '{defaults[name]}', " +
                    $"but got {(actual == null ? "no selected option (blank dropdown)" : $"'{actual}'")}.");
        }
        Assert.False(_page.Select("filters_model").WrappedElement.Enabled,
            "After Clear all, model selection should be disabled until a make is selected.");
        var sort = _browser.Driver.FindElement(By.CssSelector("input[name='crb_sort']:checked")).GetAttribute("value");
        Assert.True(sort == "newest", $"Clear all should restore Relevance sorting, but selected '{sort}'.");
        Assert.True(_page.Count == initialCount,
            $"Clear all should restore {initialCount} results, but shows {_page.Count}. Check reset behavior or whether inventory changed during the test.");
        _page.Cards();
        Assert.True(resetErrors.Count == 0, "Clear all restored results but did not reset these dropdowns:\n" + string.Join("\n", resetErrors));
        _page.Step($"PASS: filter defaults restored and all {initialCount} results available again");
    }
}
