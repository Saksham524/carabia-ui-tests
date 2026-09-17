using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Smoke;

[Trait("Suite", "PublicPages")]
[Trait("Category", "Sitemap")]
public sealed class SitemapLinkTests(BrowserFixture browser, ITestOutputHelper output) : IClassFixture<BrowserFixture>
{
    private const string Section = ".crb-sitemap-section";
    private static readonly string[] Groups =
    ["Trending Searches", "Popular Locations", "Popular Makes", "Popular Budgets", "Popular Body Types", "Popular Models"];

    [Theory(DisplayName = "Sitemap: shared link section renders all six groups")]
    [InlineData("/")]
    [InlineData("/sellings")]
    [InlineData("/brand/bmw")]
    public void Shared_section_renders_on_public_pages(string path)
    {
        var section = OpenSection(path);
        var titles = section.FindElements(By.CssSelector(".crb-sitemap-col-title"))
            .Select(element => element.Text.Trim()).ToArray();
        Assert.Equal(Groups, titles);
        foreach (var group in Groups)
            Assert.NotEmpty(Column(section, group).FindElements(By.CssSelector("a.crb-sitemap-link")));
        Step($"PASS: all six sitemap groups render on {path}");
    }

    [Theory(DisplayName = "Sitemap: every group link has a working local destination")]
    [InlineData("Trending Searches", "/sellings/", 8)]
    [InlineData("Popular Locations", "/sellings", 2)]
    [InlineData("Popular Makes", "/brand/", 18)]
    [InlineData("Popular Budgets", "/sellings/under-", 9)]
    [InlineData("Popular Body Types", "/sellings/body-", 9)]
    [InlineData("Popular Models", "/sellings/model-", 18)]
    public async Task All_group_links_return_success(string group, string prefix, int minimumLinks)
    {
        var links = Column(OpenSection("/sellings"), group).FindElements(By.CssSelector("a.crb-sitemap-link"))
            .Select(link => (Label: link.Text.Trim(), Href: link.GetAttribute("href"))).ToList();
        Assert.True(links.Count >= minimumLinks,
            $"Sitemap '{group}' needs at least {minimumLinks} links, but has {links.Count}. Check the partial and location data.");
        var errors = new List<string>();
        var destinations = new HashSet<string>(StringComparer.Ordinal);
        var origin = new Uri(browser.BaseUrl);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        foreach (var link in links)
        {
            var context = $"Sitemap '{group}' / '{link.Label}' -> {link.Href}";
            if (string.IsNullOrWhiteSpace(link.Label)) errors.Add($"{context}: link text is empty.");
            if (!Uri.TryCreate(link.Href, UriKind.Absolute, out var uri)
                || uri.GetLeftPart(UriPartial.Authority) != origin.GetLeftPart(UriPartial.Authority)
                || !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                errors.Add($"{context}: expected a local '{prefix}' destination without a placeholder fragment.");
                continue;
            }
            if (!destinations.Add(uri.AbsoluteUri)) errors.Add($"{context}: duplicate destination in this group.");
            try
            {
                using var response = await client.GetAsync(uri);
                var final = response.RequestMessage?.RequestUri;
                if (!response.IsSuccessStatusCode)
                    errors.Add($"{context}: HTTP {(int)response.StatusCode} ({response.StatusCode}); final URL: {final}.");
                else if (final != uri)
                    errors.Add($"{context}: unexpectedly redirected to {final}.");
                else
                    output.WriteLine($"PASS: {context} (HTTP {(int)response.StatusCode})");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                errors.Add($"{context}: request failed: {exception.Message}");
            }
        }
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        Step($"PASS: all {links.Count} {group} sitemap destinations respond successfully");
    }

    [Theory(DisplayName = "Sitemap: clicking a group link opens its destination")]
    [InlineData("Trending Searches", "/sellings/evs")]
    [InlineData("Popular Locations", "/sellings")]
    [InlineData("Popular Makes", "/brand/mercedes-benz")]
    [InlineData("Popular Budgets", "/sellings/under-25000")]
    [InlineData("Popular Body Types", "/sellings/body-suv")]
    [InlineData("Popular Models", "/sellings/model-patrol")]
    public void Group_link_can_be_clicked(string group, string path)
    {
        var expected = new Uri(new Uri(browser.BaseUrl), path);
        var link = Column(OpenSection("/sellings"), group).FindElements(By.CssSelector("a.crb-sitemap-link"))
            .SingleOrDefault(element => element.GetAttribute("href") == expected.AbsoluteUri);
        Assert.True(link != null, $"Sitemap '{group}' is missing the expected link to {path}.");
        Step($"Click {group}: {link!.Text}", link);
        var previous = browser.Driver.FindElement(By.TagName("html"));
        link.Click();
        browser.WaitFor($"sitemap navigation to {path}", driver =>
        {
            // Also detect a reload when source and destination are both /sellings.
            try { _ = previous.TagName; return false; }
            catch (StaleElementReferenceException) { return new Uri(driver.Url) == expected; }
        });
        var content = browser.Visible(By.CssSelector(".page__content"), $"content at {path}");
        Assert.False(string.IsNullOrWhiteSpace(content.Text), $"Sitemap destination {path} has no visible page content.");
        Step($"PASS: {group} link opens {path}");
    }

    private IWebElement OpenSection(string path)
    {
        browser.Open(path);
        var section = browser.Visible(By.CssSelector(Section), "the sitemap links section");
        Step("Check sitemap links", section);
        return section;
    }

    private static IWebElement Column(IWebElement section, string group) =>
        section.FindElements(By.CssSelector(".crb-sitemap-col"))
            .Single(column => column.FindElement(By.CssSelector(".crb-sitemap-col-title")).Text.Trim() == group);

    private void Step(string message, IWebElement? element = null)
    {
        output.WriteLine(message);
        browser.ShowStep(message, element);
        browser.PauseForPresentation();
    }
}
