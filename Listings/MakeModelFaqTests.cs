using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Listings;

[Trait("Category", "Listings")]
[Trait("Category", "FAQ")]
//[Trait("Suite", "PublicPages")]
public sealed class MakeModelFaqTests : IClassFixture<BrowserFixture>
{
    private readonly BrowserFixture _browser;
    private readonly ITestOutputHelper _output;

    public MakeModelFaqTests(BrowserFixture browser, ITestOutputHelper output)
    {
        _browser = browser;
        _output = output;
    }

    [Theory(DisplayName = "Make/model FAQs: first three questions open and close")]
    [InlineData("/brand/audi", "Audi")]
    [InlineData("/brand/bmw", "BMW")]
    [InlineData("/sellings/model-a6", "Audi A6")]
    [InlineData("/sellings/model-x5", "BMW X5")]
    public void Sample_faqs_open_and_close_for_the_selected_make_or_model(string path, string vehicle)
    {
        _browser.Open(path);
        Assert.True(new Uri(_browser.Driver.Url).AbsolutePath.TrimEnd('/').Equals(path, StringComparison.OrdinalIgnoreCase),
            $"The {vehicle} page redirected elsewhere: {_browser.Driver.Url}.");
        var heading = _browser.Visible(By.CssSelector(".fd-banner__title"), $"the {vehicle} page heading");
        Assert.True(heading.Text.Contains(vehicle, StringComparison.OrdinalIgnoreCase),
            $"Expected a page for {vehicle}, but its heading says '{heading.Text}'.");
        FaqChecks.CheckSampleQuestions(_browser, _output, vehicle);
    }
}
