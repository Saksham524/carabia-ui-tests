using Carabia.Web.UiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Carabia.Web.UiTests.Sell;

[Collection("Manual login")]
[Trait("Suite", "Accounts")]
[Trait("Category", "SellCar")]
[Trait("Category", "Manual")]
public sealed class SellCarSummaryTests(ManualLoginFixture session, ITestOutputHelper output)
{
    [Fact(DisplayName = "Sell car: VIN flow verifies the complete summary without submitting")]
    public void Vin_flow_reaches_summary_without_submitting()
    {
        var photos = SellCarPage.PhotoPaths();
        var page = new SellCarPage(session.Browser, output);
        page.RunWithDiagnostics(() =>
        {
            page.OpenVehicle(manual: false);
            page.FillDetailsAndFeatures();
            page.CompleteToSummary(photos);
        });
    }
}
