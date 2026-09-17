using Carabia.Web.UiTests.Infrastructure;
using OpenQA.Selenium;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Smoke;

[Trait("Suite", "PublicPages")]
[Trait("Category", "Footer")]
public sealed class FooterLinkTests(BrowserFixture browser, ITestOutputHelper output) : IClassFixture<BrowserFixture>
{
    private const string Footer = "footer.crb-master-footer";

    [Theory(DisplayName = "Footer: internal link opens the expected page")]
    [InlineData("Used Cars", "/sellings")]
    [InlineData("How Carabia works", "/info/how-it-works")]
    [InlineData("About Us", "/info/about-us")]
    [InlineData("News", "/news")]
    [InlineData("Vehicle Overviews", "/overviews")]
    [InlineData("Contact Us", "/contact-us")]
    [InlineData("Buying Guides", "/buying-guides")]
    [InlineData("Selling Guides", "/selling-guides")]
    [InlineData("Our Partners", "/partners")]
    [InlineData("FAQs", "/info/faq")]
    [InlineData("Privacy Policy", "/info/privacy-policy")]
    [InlineData("User Agreement", "/info/user-agreement")]
    [InlineData("Sell My Car", "/personal/sell")]
    [InlineData("Carabia logo", "/")]
    public async Task Internal_link_opens_expected_page(string label, string path)
    {
        var footer = OpenFooter();
        var link = label == "Carabia logo"
            ? footer.FindElements(By.CssSelector(".crb-footer-brand-logo")).SingleOrDefault()
            : footer.FindElements(By.TagName("a")).SingleOrDefault(a => a.Text.Trim() == label);
        Assert.True(link != null, $"Footer is missing the '{label}' link.");
        var expected = new Uri(new Uri(browser.BaseUrl), path);
        Assert.True(link!.GetAttribute("href") == expected.AbsoluteUri,
            $"Footer '{label}' should point to {expected}, but points to '{link.GetAttribute("href")}'.");

        // A browser can render an error page normally; check the HTTP status as well.
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var response = await client.GetAsync(expected);
        Assert.True(response.IsSuccessStatusCode,
            $"Footer '{label}' -> {expected} returned HTTP {(int)response.StatusCode} ({response.StatusCode}). Final URL: {response.RequestMessage?.RequestUri}");

        Step($"Click footer: {label}", link);
        link.Click();
        browser.WaitFor($"footer '{label}' to open {expected}", driver => new Uri(driver.Url) == expected);
        var content = browser.Visible(By.CssSelector(".page__content"), $"the destination content for footer '{label}'");
        Assert.False(string.IsNullOrWhiteSpace(content.Text), $"Footer '{label}' opened {expected}, but the page content is empty.");
        Step($"PASS: footer {label} opens {expected.AbsolutePath}");
    }

    [Fact(DisplayName = "Footer: every rendered link has a usable destination")]
    public void All_links_have_usable_destinations()
    {
        var links = OpenFooter().FindElements(By.TagName("a"));
        Assert.NotEmpty(links);
        var errors = new List<string>();
        foreach (var link in links)
        {
            var label = link.Text.Trim();
            if (string.IsNullOrEmpty(label)) label = link.GetAttribute("title") ?? link.GetAttribute("class") ?? "unnamed link";
            var raw = link.GetDomAttribute("href");
            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "#")
                errors.Add($"'{label}' has an empty or placeholder destination '{raw}'.");
            else if (!Uri.TryCreate(link.GetAttribute("href"), UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https" or "mailto" or "tel"))
                errors.Add($"'{label}' has an unsupported destination '{raw}'.");
        }
        Assert.True(errors.Count == 0, "Footer link problems:\n" + string.Join("\n", errors));
        Step($"PASS: all {links.Count} footer links have usable destinations");
    }

    [Theory(DisplayName = "Footer: contact and social links point to the correct destination")]
    [InlineData("WhatsApp", "a[href^='https://wa.me/']", "https://wa.me/971557358770", true)]
    [InlineData("Support email", "a[href^='mailto:']", "mailto:contact@carabiacars.com", false)]
    [InlineData("LinkedIn", ".crb-social-icon[href*='linkedin.com']", "https://www.linkedin.com/company/carabiacars", true)]
    [InlineData("TikTok", ".crb-social-icon[href*='tiktok.com']", "https://www.tiktok.com/@carabiacars", true)]
    [InlineData("Instagram", ".crb-social-icon[href*='instagram.com']", "https://www.instagram.com/carabiacars/", true)]
    public void Contact_and_social_links_have_expected_destinations(string label, string selector, string expected, bool newTab)
    {
        var links = OpenFooter().FindElements(By.CssSelector(selector));
        Assert.True(links.Count == 1, $"Expected one footer {label} link, found {links.Count}.");
        var link = links[0];
        Assert.True(link.Displayed, $"Footer {label} link is hidden.");
        Assert.True(link.GetAttribute("href") == expected,
            $"Footer {label} should point to '{expected}', but points to '{link.GetAttribute("href")}'.");
        if (newTab)
            Assert.True(link.GetAttribute("target") == "_blank", $"Footer {label} should open in a new tab.");
        // Do not launch email/WhatsApp apps or depend on third-party availability.
        Step($"PASS: footer {label} points to {expected}", link);
    }

    private IWebElement OpenFooter()
    {
        browser.Open("/sellings");
        var footer = browser.Visible(By.CssSelector(Footer), "the shared footer");
        Step("Check footer links", footer);
        return footer;
    }

    private void Step(string message, IWebElement? element = null)
    {
        output.WriteLine(message);
        browser.ShowStep(message, element);
        browser.PauseForPresentation();
    }
}
