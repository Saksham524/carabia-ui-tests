using Carabia.Web.UiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Sell;

[Trait("Suite", "PublicPages")]
[Trait("Category", "SellCar")]
public sealed class SellCarLookupTests(BrowserFixture browser, ITestOutputHelper output) : IClassFixture<BrowserFixture>
{
    [Theory(DisplayName = "Sell car: VIN and manual search identify the expected vehicle and accept details")]
    [InlineData(false)]
    [InlineData(true)]
    public void Vehicle_search_and_details_reach_features(bool manual)
    {
        var page = new SellCarPage(browser, output);
        page.RunWithDiagnostics(() =>
        {
            page.OpenVehicle(manual);
            page.FillDetailsAndFeatures();
        });
    }
}
