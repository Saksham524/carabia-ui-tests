using OpenQA.Selenium;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Carabia.Web.UiTests.Infrastructure;

internal static class FaqChecks
{
    public static void CheckSampleQuestions(BrowserFixture browser, ITestOutputHelper output, string pageName)
    {
        var driver = browser.Driver;
        var script = (IJavaScriptExecutor)driver;
        var items = driver.FindElements(By.CssSelector(
            ".crb-faq-section .crb-faq-item, .crb-seo-faq__item, .crb-seo-content .js-accordion"));
        Assert.True(items.Count > 0, $"{pageName}: no FAQ questions were found. Expected an FAQ section on this page.");
        var sampleCount = Math.Min(3, items.Count);
        output.WriteLine($"{pageName}: checking the first {sampleCount} of {items.Count} FAQs.");
        var failures = new List<string>();

        for (var index = 0; index < sampleCount; index++)
        {
            var context = $"{pageName}, sampled FAQ {index + 1}/{sampleCount}";
            try
            {
                var item = items[index];
                var isNative = item.TagName.Equals("details", StringComparison.OrdinalIgnoreCase);
                var isCheckbox = item.FindElements(By.CssSelector(".crb-faq-toggle")).Count > 0;
                var trigger = item.FindElement(By.CssSelector(isNative ? "summary" : isCheckbox ? ".crb-faq-question" : ".js-accordion-trigger"));
                var answer = item.FindElement(By.CssSelector(isNative ? ".crb-seo-faq__answer" : isCheckbox ? ".crb-faq-answer" : ".accordion__body"));
                var question = trigger.Text.Trim();
                Assert.False(string.IsNullOrWhiteSpace(question), $"{context}: question text is empty.");
                context += $" — {question}";

                bool IsOpen() => isNative
                    ? script.ExecuteScript("return arguments[0].open;", item) is true
                    : isCheckbox ? item.FindElement(By.CssSelector(".crb-faq-toggle")).Selected
                    : script.ExecuteScript("return arguments[0].classList.contains('accordion_state_open');", item) is true;

                browser.ShowStep($"{context}: opening answer", trigger);
                browser.PauseForPresentation();
                if (IsOpen())
                {
                    trigger.Click();
                    browser.WaitFor($"{context}: the initially open answer to close", _ => !IsOpen() && !HasVisibleAnswer(script, answer), 5);
                }

                trigger.Click();
                browser.WaitFor($"{context}: clicking the question to reveal the complete answer", _ =>
                    IsOpen() && HasVisibleAnswer(script, answer)
                    && !string.IsNullOrWhiteSpace(answer.Text)
                    && script.ExecuteScript("return arguments[0].scrollHeight <= arguments[0].clientHeight + 2;", answer) is true, 5);
                browser.ShowStep($"{pageName}: answer {index + 1}/{sampleCount} is open and readable", answer);
                browser.PauseForPresentation();

                // Click the visible question again, rather than changing checkbox/classes in JavaScript.
                browser.ShowStep($"{pageName}: closing sampled FAQ {index + 1}/{sampleCount}", trigger);
                trigger.Click();
                browser.WaitFor($"{context}: clicking again to hide the answer", _ => !IsOpen() && !HasVisibleAnswer(script, answer), 5);
                output.WriteLine($"PASS: {context} opens with an answer and closes again.");
            }
            catch (Exception error) when (error is XunitException or WebDriverException)
            {
                failures.Add($"{context}: {error.Message}");
            }
        }

        Assert.True(failures.Count == 0, $"{pageName}: {failures.Count} of {sampleCount} sampled FAQs failed.\n" + string.Join("\n", failures));
        browser.ShowStep($"PASS: {sampleCount} sampled FAQs open and close on {pageName} ({items.Count} available)");
        browser.PauseForPresentation();
    }

    // Closed <details> can retain layout measurements; also check actual browser visibility.
    private static bool HasVisibleAnswer(IJavaScriptExecutor script, IWebElement answer) =>
        answer.Displayed && script.ExecuteScript("""
            const element = arguments[0];
            const style = getComputedStyle(element);
            return style.display !== 'none' && style.visibility !== 'hidden'
                && element.getBoundingClientRect().height > 1;
            """, answer) is true;
}
