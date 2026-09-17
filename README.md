# Carabia browser tests

These xUnit tests use Selenium and Chrome to check public pages, listings, manual login, and profile settings.

**Current default: all tests, including login and profile.**

## Run locally

1. Keep Carabia.Web running against your development or test database at `http://localhost:51031`.
2. From the solution folder (`_Carabia_`), run:

```powershell
dotnet test
```

Chrome must be installed. Selenium Manager resolves the Chrome driver automatically.
The suite does not start the web application itself.
By default, all browser tests show Chrome and run one class at a time. A blue test label
shows the current check inside the browser. Visible walkthroughs pause briefly between steps.
Test method order is not guaranteed.

## Test run structure

Tests have a `Suite` trait for selecting a complete group, and `Category` traits for individual features.
Run settings are kept in `TestRuns/`:

| Run | Included tests | Command from the solution folder |
| --- | --- | --- |
| PublicPages | Home, sellings, auctions, listing details, images, filters, and make/model FAQs | `dotnet test -p:TestRun=PublicPages` |
| Accounts | Login form, manual OTP login, logged-out profile access, and signed-in profile | `dotnet test -p:TestRun=Accounts` |
| All (default) | Both groups | `dotnet test` |

The public run never intentionally opens login or edits account details. The default All run
includes every group and requires manual phone/OTP entry once for login/profile.
To change what plain `dotnet test` runs, set the default `TestRun` value to `PublicPages`,
`Accounts`, or `All` in `Carabia.Web.UiTests.csproj`.

When adding tests, place public-page classes in `[Trait("Suite", "PublicPages")]` and any
login/profile classes in `[Trait("Suite", "Accounts")]`. Account tests that need manual OTP
also use the existing `Manual login` collection and `[Trait("Category", "Manual")]`.
New tests must declare a suite to be included in the PublicPages or Accounts runs; All runs every test.

Explicit CLI `--filter` or `--settings` options can override the selected run's filter.
The examples below intentionally select particular features.

## Sell My Car

```powershell
dotnet test --filter "Category=SellCar"
```

`Sell/SellCarValidationTests.cs` adds five PublicPages cases for `/personal/sell`:
empty vehicle details cannot continue, short/long VINs show validation errors,
clearing a VIN restores manual year selection, and VIN help opens and closes.
`Sell/SellCarLookupTests.cs` exercises VIN lookup and manual Year -> Make -> Model -> Trim
-> Regional specification selection, checks the returned vehicle and fills details through
the features step. These seven PublicPages cases do not require login or create listings.

The sample vehicle is VIN `2FMPK4G83GBB65542`, identified by the local service as a 2016
Ford Edge SE. Manual search uses the same vehicle and GCC regional specification.
Sample values are 80,000 km, White exterior, Black interior, no warranty and Dubai
(selected from the real Google Places suggestions). Autodata and Google Maps must work.

`Sell/SellCarSummaryTests.cs` uses the shared manual-login session and continues the VIN
flow through price (AED 40,000), photos, skipped ownership verification, no paid upgrade,
No Boost and the summary. It verifies the displayed values, photo count and cover photo,
and stops before Submit Listing or payment. It also checks the four-photo minimum error.
The test reads supported images from `D:\CarPhotos`; override with `CARABIA_SELL_PHOTO_DIR`.
At least four images are required. Photos remain local browser previews until submission.
This case belongs to Accounts/Manual, so `Category=SellCar` opens a visible browser and
requires phone/OTP entry. No OTP or login credentials are stored in the tests.

```powershell
# VIN/manual lookup and validation, without login:
dotnet test --filter "Category=SellCar&Suite=PublicPages"
# Full VIN walkthrough with manual login, ending at the summary:
dotnet test --filter "FullyQualifiedName~SellCarSummaryTests"
```

Successful summary and failed walkthrough screenshots are saved in the Windows temporary
folder; their exact paths appear in test output. Optional damage reports, warranty expiry,
ownership documents, paid plans and submission/payment are not covered by this walkthrough.

## Sitemap links

`Smoke/SitemapLinkTests.cs` covers `Views/Shared/Partial/_SitemapLinksPartial.cshtml`
separately from the main footer. Run its 15 cases with:

```powershell
dotnet test --filter "Category=Sitemap"
```

These cases are included in PublicPages and All. They verify all six groups render on
the home, listings and BMW brand pages; check every rendered link in each group from
`/sellings` for nonempty text, a local destination in the expected route family, duplicates,
successful HTTP responses and unexpected redirects; and click one expected link per group
to verify navigation and visible destination content. Location coverage follows the live
data and requires at least one location in addition to the UAE link. Failed requests are
collected so the output identifies every affected link in that group.

This checks link navigation and availability, not whether every destination's car inventory
matches its filter. Article and listing-detail placements and mobile layout are not covered.

## Footer links

Run the shared desktop footer checks with:

```powershell
dotnet test --filter "Category=Footer"
```

These 20 cases are included in the PublicPages suite and the default All run. They check
14 navigation destinations (menu links, legal links, Sell My Car and the logo), confirm
successful HTTP responses, click the links and verify the destination URL and nonempty
page content. Another case checks every rendered footer anchor for empty, placeholder or
unsupported destinations. Five cases verify the configured email, WhatsApp and social
addresses and new-tab behavior where appropriate, without opening external apps or
contacting third-party sites. Failures name the affected link and destination.

The shared footer is tested from `/sellings` at the fixture's desktop viewport. Mobile
layout and third-party page availability are not covered. Expected menu destinations
must be updated when the site's intended footer navigation changes.

## Listings walkthrough

The `Listings` category includes these visible walkthroughs:

- Page heading and result count.
- Title, year/mileage text, numeric price, and details link on each rendered card.
- Image loading for each rendered card, with failed listing names and image URLs in the result.
- Clicking each of the first four cars from `/sellings`, checking the matching vehicle heading,
  make, model, year and asking price, then opening and closing the first three FAQs on that detail page.
- Clicking the featured Audi A6 card from the home page to reach `/sellings/1056_audi-a6`.
  This specific regression case requires listing 1056 to remain featured on the home page.
- Sampling the first three FAQs on just two make pages (Audi and BMW) and two model pages
  (Audi A6 and BMW X5). Page headings must match the requested vehicle;
  a route displaying another make/model is a failure, not accepted as FAQ coverage.

The card checks scroll through the current page. They do not cover every results page.
A failed image check does not prevent the card-information or details-page tests from running.
Actual missing images still fail; the tests do not replace or ignore broken assets.
FAQ checks click the real question controls, verify a nonempty answer is fully expanded,
then click again and verify it closes. Detail pages currently share general buying/selling
FAQs; the make/model landing pages have vehicle-specific questions. FAQ checks are capped at
the first three questions per page (or all available questions if fewer than three exist).
Each sampled question is checked even if an earlier sampled question fails.

To watch only the listings checks:

```powershell
dotnet test --filter "Category=Listings"
```

## Listings filters

Run just the filters (no login or OTP):

```powershell
dotnet test --filter "Category=Filters"
```

`Listings/ListingsFilterTests.cs` adds 13 cases, also included in the default All run:

- Make selection for Audi and BMW (2 cases).
- Model selection within Audi and BMW (2 cases), using a model found in current inventory.
- Changing make resets the previous model and loads the new make's models.
- Price, year and mileage ranges (3 cases), chosen around an existing car.
- Price ascending, price descending and mileage ascending sorting (3 cases).
- Make and price applied together.
- Clear all restores make/model, all three numeric ranges, sorting and the original result count.

The tests click the desktop sidebar controls, wait for the AJAX results to update, and check
every matching card on the current page. Recommended cars below the end-of-results panel
are excluded from filter assertions. Empty matches cannot silently pass a range or make check.
Failures identify the selected filter, unexpected car/value and listing URL where applicable.
These checks require active Audi and BMW listings and cars inside the available numeric ranges;
missing test data produces an explicit setup failure. Inventory should remain stable during Clear all.
Location, listing type, monthly finance, mobile filters and pagination are not covered by this group yet.

Development-site verification on 2026-09-15: 9 passed, 4 failed. The Audi/BMW model cases
and make-change case could not proceed because the model dropdown stayed disabled;
a separate request to `/System/GetCarModels` returned HTTP 500. Clear all restored the
385 results but left the maximum-price dropdown blank instead of its initial 300,000+ value.
These failures remain enabled to expose the application issues. Application code was not changed.

## Manual login

This group is enabled in the default All run. It is excluded when selecting PublicPages.

The login test opens a visible Chrome window, clicks the home page's Sign in button,
selects **India (+91)**, and focuses the phone field.
Enter your phone number, click Continue, enter the OTP you receive, and complete any additional
signup details yourself. The test waits up to **10 minutes** for an authenticated client session,
then continues automatically with the signed-in checks. It fails with a timeout if login is not completed.

The test does not fill or log your phone number or OTP. It uses the app's normal login flow.
The previous automated test phone and fixed OTP are no longer used by this test.
Manual login always shows Chrome, even if `CARABIA_HEADLESS=true`, and does not run alongside
the public-page, anonymous-access, or listings tests. Login and profile tests share one browser
session, so you enter the OTP only once per run, whichever signed-in test runs first.
Chrome closes after the login/profile group finishes. Public-page and listings tests have separate sessions.

To run only login:

```powershell
dotnet test -p:TestRun=Accounts --filter "Category=Login"
```

To run both groups while excluding tests requiring a person:

```powershell
dotnet test -p:TestRun=All --filter "Category!=Manual"
```

## Profile settings

The `Profile` category covers `/Personal/Settings` in `PersonalController`:

- Anonymous visitors see the login popup and no private settings form. The test confirms
  that the browser is logged out without requiring a particular redirect URL/query string.
- Personal information loads with populated, read-only fields and active Settings navigation.
- Each of the four Edit buttons unlocks only its own field.
- Empty first and last names fail the browser's required-field validation.
- Changing email shows Send code and disables saving until verification (no email is sent by this test).
- Reloading discards an unsaved name change.
- Saving changed first/last names persists after a fresh page load.

Use a development/test account: the save case temporarily changes its first and last names.
It restores the original profile through the controller in a `finally` block, even if the save
assertions fail, then checks the original names on a fresh page load. If the app/browser is
closed during this case, cleanup may fail; the test reports cleanup failures explicitly.
The tests do not automate OTP entry, email confirmation, phone-number changes, or payment methods.

Profile cases are enabled in the default run. To run only profile cases:

```powershell
dotnet test -p:TestRun=Accounts --filter "Category=Profile"
```

## Optional settings

- `CARABIA_BASE_URL`: overrides `http://localhost:51031`. If you previously set it in your terminal, that value still takes precedence.
- `CARABIA_HEADLESS=true`: hides Chrome for unattended tests; Chrome is visible by default.
  If you previously set this to true in your terminal, that override still applies. Manual login always stays visible.
- `CARABIA_PRESENTATION_DELAY_MS`: overrides the pause between walkthrough steps (default 800 ms when visible, 0 when headless).

Categories are `Smoke` (public pages and manual login), `Login`, `Manual`, `Profile`, `Listings`, and `FAQ`.
