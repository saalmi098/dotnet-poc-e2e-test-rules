# dotnet-poc-e2e-test-rules

Roslyn analyzers (`PWNET001`–`PWNET011`) for Playwright.NET E2E test code. They run in Visual Studio, Rider, VS Code (C# Dev Kit) and `dotnet build`. SonarQube can import their output later.

The analyzer targets `netstandard2.0` and has **no reference to Playwright**. It matches Playwright APIs by namespace and type name (`Microsoft.Playwright.IPage`, ...).

## Layout

```
src/E2ETestRules/               analyzers (netstandard2.0)
tests/E2ETestRules.Tests/       xUnit v3 tests, in-memory compilations
sample/E2E.Sample.Tests/        consumer project; Examples/ holds violating code (not compiled)
```

## Rules

How each rule maps to Playwright and Microsoft best practices: [docs/best-practices.md](docs/best-practices.md).

All rules default to **Warning**. The root `.editorconfig` raises PWNET001, PWNET005 and PWNET007 to **error**. `TreatWarningsAsErrors` is on, but `Directory.Build.props` lists all PWNET IDs in `WarningsNotAsErrors`, so `.editorconfig` decides: `warning` stays a warning, `error` fails the build.

Only **xUnit v3** is supported (`[Fact]`, `[Theory]`, `Xunit.Assert`, `Xunit.Sdk.XunitException`).
Category for all rules: `Playwright E2E`. Help link: `https://example.com/docs/e2e-test-rules/<ID>` (placeholder).

| ID | Detects |
|----|---------|
| PWNET001 | Fixed delays in test code: `Task.Delay`, `Thread.Sleep`, Playwright `WaitForTimeoutAsync`. Use auto-waiting or web-first assertions. |
| PWNET002 | `Locator("...")` on `IPage`/`IFrame`/`ILocator`/`IFrameLocator` with a constant selector starting with `css=`, `xpath=`, `//`, containing `#`, `.`, `[`, `>`, or positional parts (`:nth-child(`, `:nth-of-type(`, `:first-child`, `:last-child`, `nth=`). Not reported: `GetBy*` calls, `text=`/`role=`/`data-testid=` engines and plain test-id attribute selectors like `[data-testid="save"]` (unless chained with `>>` or positional). |
| PWNET003 | Test methods (see *test attributes* below) with no invocation named `Expect*`/`Assert*`/`Should*` (or called on an `Assert*`/`Should*` receiver such as `Assert.Equal`). Empty bodies are reported too (an empty test always passes); skipped/ignored tests are not. |
| PWNET004 | Non-local `http(s)://` string literals; identifiers (variables, parameters, properties) and string literals containing `password`, `passwd`, `secret`, `token`, `apiKey` (case-insensitive). Test code only. |
| PWNET005 | Task-returning `*Async` calls whose containing type name/namespace contains `Playwright` (including `Expect(..).ToBe*Async()`) that are discarded (bare statement, `_ = ...`) or blocked on directly (`.Wait()`, `.Result`, `.GetAwaiter().GetResult()`). |
| PWNET006 | Playwright API calls (any method on a type in `Microsoft.Playwright.*`) inside a **test class**, i.e. any class declaring a `[Fact]`/`[Theory]`/`[Test]`/`[TestMethod]` method, including its helper methods and lambdas. Page object classes (no test methods) may use Playwright freely. `Assertions.Expect(...)` and `*Assertions` types (`ToBeVisibleAsync`, ...) are allowed. |
| PWNET007 | `async void` test methods. The runner cannot await them; xUnit v3 rejects them at runtime. Event handlers (no test attribute) are ignored. |
| PWNET008 | Non-retrying assertions over browser state: xUnit `Assert.*` whose input is a one-shot Playwright read on `ILocator`/`IPage`/`IFrame` (`IsVisibleAsync`, `IsEnabledAsync`, `IsCheckedAsync`, `TextContentAsync`, `InnerTextAsync`, `InputValueAsync`, `GetAttributeAsync`, `CountAsync`, `TitleAsync`, `Url`, ...), directly or via a local initialized from one. The message names the matching web-first assertion (`ToBeVisibleAsync`, `ToHaveTextAsync`, ...). |
| PWNET009 | Interactions that bypass actionability checks: `Force = true` on a Playwright options type, `DispatchEventAsync(...)`, `EvaluateAsync`/`EvalOnSelector*Async` with a constant script calling `.click()` or `dispatchEvent(`. Review warning; suppress with a justification where intended. |
| PWNET010 | In a test method: a broad `catch` (none, `Exception`, `SystemException`, `TimeoutException`, Playwright exceptions, `XunitException`, no `when` filter) that neither rethrows nor asserts, around a `try` that contains an assertion other than `Fail`. The assertion can never fail the test. |
| PWNET011 | (1) Test methods with any branch or loop: `if`, `switch`, `?:`, `for`/`foreach`/`while`/`do`; one report per test. (2) In any class, including page objects: a branch whose condition reads browser state once (`if (await banner.IsVisibleAsync())`), directly or via a local. `??`, `&&`, `\|\|`, `?.` are not counted. |

Test attributes (PWNET003, PWNET006, PWNET007, PWNET010, PWNET011): xUnit `Fact`, `Theory`, matched by name.

"Test code" (PWNET001, PWNET004) means: file path contains `/e2e/` or `/tests/` (either slash direction, case-insensitive), or the assembly name ends with `.Tests`.

Suppress with `#pragma warning disable PWNET00x` / `restore`, `[SuppressMessage]`, or an `.editorconfig` severity of `none`.

Choices where the spec was open:
- "Skip" (PWNET003): method name contains `Skip`, attribute has a `Skip` argument, or the body calls a `*Skip*` method (e.g. `Assert.Skip`).
- Loopback: `localhost` and `127.0.0.1` are exempt from the URL check.
- `https://{host}/x`: nothing is hardcoded after the scheme, so it is not reported.

## Candidates reviewed but not implemented

| Candidate | Decision | Reason |
|-----------|----------|--------|
| Action without assertion | covered by PWNET003 | Per-action tracking would add noise; assertions in helpers are invisible either way. |
| Hardcoded environment URLs | covered by PWNET004 | |
| `.First`/`.Last`/`.Nth` as fragile selectors | rejected | Legitimate for lists/tables; no reliable way to tell brittle from intended. |
| Explicit waits duplicating auto-wait (`WaitForSelectorAsync`, `NetworkIdle`) | deferred | `NetworkIdle` is discouraged by Playwright and is a candidate for PWNET001; validate against real suites first. |
| Scoring (start at 100, deduct per finding) | deferred | Not decided yet; if built, it belongs in its own project reading SARIF/build output, not in the analyzer. Weights need validation against real tests; score per test and rule to avoid double-penalizing. |

## Build and test

```bash
dotnet restore
dotnet build
dotnet test
```

Requires the .NET 10 SDK. The sample's `CompliantExample` is skipped by default (needs Playwright browsers and a running app).

To see the rules fire, temporarily copy a file from `sample/E2E.Sample.Tests/Examples/` up one level and build.

## Consume via ProjectReference

```xml
<ProjectReference Include="../../src/E2ETestRules/E2ETestRules.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Configure severities in `.editorconfig` (`dotnet_diagnostic.PWNET001.severity = error`).

## Package later as NuGet

1. In `E2ETestRules.csproj` set `IsPackable` to `true`, add `PackageId`, `Version`, `Description`, `DevelopmentDependency=true`, `IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`.
2. Pack the DLL into `analyzers/dotnet/cs`:
   ```xml
   <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
   ```
3. `dotnet pack -c Release`, then consumers use `<PackageReference Include="E2ETestRules" PrivateAssets="all" />`.
4. Replace the placeholder help-link URI with real docs.

## SonarQube integration (later)

- **SonarScanner for .NET** imports Roslyn analyzer issues from the MSBuild compiler output (SARIF). Issues from third-party analyzers appear as *external issues*. Build with `dotnet sonarscanner begin ... / dotnet build / end`, with the analyzer referenced by the built projects.
- Rules enabled only through `.editorconfig` are reported; set severity to `warning` or above.
- SonarQube shows them as generic external rules (`external_roslyn:PWNET001`). They are not part of a Quality Profile, so gate on them via the Quality Gate on new issues.
- For a native Sonar rule you need a SonarQube plugin (Java) that re-implements each rule, or use the Roslyn SARIF import as above.

## Known limitations and false-positive risks

- **Heuristic, not semantic.** No test intent is detected.
- **PWNET002**: any `.`, `#`, `[`, `>` in the selector counts, so `text=Save.` or `role=button[name="x"]` are reported. Only constant strings are checked; interpolated or computed selectors are not. Matches by Playwright interface name and namespace only.
- **PWNET003**: a helper like `await LoginAndVerifyAsync()` hides the assertion and is reported, while a call named `AssertSomething()` that asserts nothing counts as an assertion. Attributes are matched by name only (no check that they come from xUnit).
- **PWNET004**: `token`/`secret`/`password` anywhere in an identifier or literal matches, so UI labels like `GetByLabel("Password")` or `"token-expired"` are reported. Environment variable names containing those words are reported too. URLs with a non-loopback host are reported even if they are fixtures for external content.
- **PWNET005**: only discarded results are detected (bare call, `_ =`). A task stored in a variable and never awaited is not reported. It overlaps with compiler CS4014 inside `async` methods but also covers non-async methods. Calls that are `.ConfigureAwait(false)`-ed without `await` are not detected.
- **PWNET006**: one report per Playwright call, so `page.GetByRole(..).ClickAsync()` gives two. Property access (`page.Url`) is not detected. `Expect(page.Locator(..))` still reports the inner call. Test base classes holding Playwright setup are flagged if they declare a test method; put setup in a non-test class (see `BrowserSession` in the sample). Invocations in nested types are judged by the nested type, not the outer test class.
- **PWNET001**: every `WaitForTimeoutAsync`/`Thread.Sleep`/`Task.Delay` in test code is reported, including deliberate ones (e.g. animation settling); suppress those with a justification. Condition-based waits (`WaitForURLAsync`, `WaitForResponseAsync`, ...) and `LoadState.NetworkIdle` are not checked.
- **PWNET002 (positional)**: `.First`, `.Last`, `.Nth(..)` on locators are deliberately not reported; they are often the correct way to address list items.
- **PWNET005 (blocking)**: only blocking directly on the Playwright call is detected; `var t = page.GotoAsync(..); t.Wait();` is not.
- **PWNET007**: duplicates xUnit's own `xUnit1049` analyzer (shipped with xunit.v3); kept for uniform PWNET reporting.
- **PWNET008**: reads hidden in page-object methods (`await login.IsBannerVisibleAsync()`) are not seen. A local reassigned after its initializer is still judged by the initializer. Asserting a value read right after an explicit wait is still reported. In a test class the read call is also reported by PWNET006 (different smell).
- **PWNET009**: only constant scripts are inspected; `.click()` written differently (`el.click( )`, `HTMLElement.prototype.click.call(el)`) is missed.
- **PWNET010**: assertions are recognised by the same name heuristic as PWNET003. Swallowed failures of actions (not assertions) are not reported, to avoid flagging legitimate "dismiss banner if present" patterns. Only test methods are checked, not helpers or page objects.
- **PWNET011**: loops over test data (table rows) count as branches; use `[Theory]` or suppress with a justification. Page objects are only checked for state-dependent branches, since plain logic there (optional parameters, loops over inputs) is normal. A test helper method in the test class is not counted. State reads hidden in page-object methods (`if (await page.IsBannerShownAsync())`) are not seen.
- PWNET001/004 depend on path conventions; projects not named `*.Tests` and not under a `tests`/`e2e` folder are skipped.
- `Microsoft.CodeAnalysis.CSharp` 4.11 is referenced, so the host needs VS 17.11 / .NET SDK 8.0.400 or newer.
