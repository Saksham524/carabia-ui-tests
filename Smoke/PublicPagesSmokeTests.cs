using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;

namespace Carabia.Web.UiTests.Smoke;

//[Trait("Suite", "PublicPages")]
public sealed class PublicPagesSmokeTests : IClassFixture<BrowserFixture>
{
    private readonly BrowserFixture _browser;

    public PublicPagesSmokeTests(BrowserFixture browser)
    {
        _browser = browser;
    }

    [Theory]
    [Trait("Category", "Smoke")]
    [InlineData("/")]
    [InlineData("/sellings")]
    [InlineData("/auctions")]
    public void Public_page_loads_and_renders_content(string relativePath)
    {
        _browser.Open(relativePath);

        var body = _browser.Driver.FindElement(By.TagName("body"));
        var bodyText = body.Text.Trim();

        Assert.True(body.Displayed, $"The body for '{relativePath}' was not visible.");
        Assert.False(string.IsNullOrWhiteSpace(bodyText),
            $"The body for '{relativePath}' did not contain visible content.");
        Assert.DoesNotContain("ERR_CONNECTION", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("This site can’t be reached", bodyText, StringComparison.OrdinalIgnoreCase);
        _browser.ShowStep($"PASS: public page {relativePath} displays content");
        _browser.PauseForPresentation();
    }
}
