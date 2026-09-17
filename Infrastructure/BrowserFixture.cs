using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit.Sdk;

namespace Carabia.Web.UiTests.Infrastructure;

public sealed class BrowserFixture : IDisposable
{
    private static readonly TimeSpan PageLoadTimeout = TimeSpan.FromSeconds(30);
    private readonly bool _runHeadless;

    public BrowserFixture() : this(forceVisible: false)
    {
    }

    internal BrowserFixture(bool forceVisible)
    {
        BaseUrl = (Environment.GetEnvironmentVariable("CARABIA_BASE_URL")
            ?? "https://dev.carabiacars.com").TrimEnd('/');

        var options = new ChromeOptions();
        _runHeadless = !forceVisible && string.Equals(
            Environment.GetEnvironmentVariable("CARABIA_HEADLESS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (_runHeadless)
            options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AcceptInsecureCertificates = true;

        Driver = new ChromeDriver(options);
        Driver.Manage().Timeouts().PageLoad = PageLoadTimeout;
    }

    public string BaseUrl { get; }

    public IWebDriver Driver { get; }

    public void PauseForPresentation()
    {
        var delay = int.TryParse(Environment.GetEnvironmentVariable("CARABIA_PRESENTATION_DELAY_MS"), out var configured)
            ? configured : (_runHeadless ? 0 : 800);
        if (delay > 0)
            Thread.Sleep(Math.Min(delay, 10_000));
    }

    public void Open(string relativePath)
    {
        var url = $"{BaseUrl}/{relativePath.TrimStart('/')}";
        try
        {
            Driver.Navigate().GoToUrl(url);
        }
        catch (WebDriverException exception)
        {
            throw new XunitException($"Could not open {url}. Check that Carabia.Web is running at {BaseUrl} and the page is reachable. WebDriver: {exception.Message}");
        }

        WaitFor("the page to finish loading", driver => ((IJavaScriptExecutor)driver)
            .ExecuteScript("return document.readyState")?.ToString() == "complete");
    }

    public T WaitFor<T>(string expected, Func<IWebDriver, T> condition, int seconds = 30)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(seconds));
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        try
        {
            return wait.Until(condition);
        }
        catch (WebDriverTimeoutException)
        {
            throw new XunitException($"Expected {expected}, but it did not happen within {seconds} seconds. Page: {Driver.Url}");
        }
    }

    public IWebElement Visible(By selector, string description) => WaitFor(description, driver =>
    {
        var element = driver.FindElement(selector);
        return element.Displayed ? element : null;
    })!;

    public void ShowStep(string description, IWebElement? element = null)
    {
        ((IJavaScriptExecutor)Driver).ExecuteScript("""
            let label = document.getElementById('carabia-test-progress');
            if (!label) {
                label = document.createElement('div');
                label.id = 'carabia-test-progress';
                label.style.cssText = 'position:fixed;bottom:16px;left:16px;z-index:2147483647;max-width:520px;padding:12px 18px;background:#17375e;color:white;font:16px/1.5 sans-serif;border-radius:8px;pointer-events:none;box-shadow:0 2px 12px #0006';
                document.body.appendChild(label);
            }
            label.textContent = 'TEST: ' + arguments[0];
            if (arguments[1]) arguments[1].scrollIntoView({block:'center', behavior:'instant'});
            """, description, element);
    }

    public void Dispose()
    {
        Driver.Quit();
        Driver.Dispose();
    }
}
