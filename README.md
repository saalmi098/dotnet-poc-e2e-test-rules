# dotnet-poc-e2e-test-rules

Roslyn analyzers (`PWNET001`–`PWNET006`) for Playwright.NET E2E test code. They run in Visual Studio, Rider, VS Code (C# Dev Kit) and `dotnet build`. SonarQube can import their output later.

The analyzer targets `netstandard2.0` and has **no reference to Playwright**. It matches Playwright APIs by namespace and type name (`Microsoft.Playwright.IPage`, ...).

## Layout

```
src/E2ETestRules/               analyzers (netstandard2.0)
tests/E2ETestRules.Tests/       xUnit v3 tests, in-memory compilations
sample/E2E.Sample.Tests/        consumer project; Examples/ holds violating code (not compiled)
```

## Rules

All rules default to **Warning**. The root `.editorconfig` raises PWNET001 and PWNET005 to **error**.
Category for all rules: `Playwright E2E`. Help link: `https://example.com/docs/e2e-test-rules/<ID>` (placeholder).

| ID | Detects |
|----|---------|
| PWNET001 | `Task.Delay` in test code. Use auto-waiting or web-first assertions. |
| PWNET002 | `Locator("...")` on `IPage`/`IFrame`/`ILocator`/`IFrameLocator` with a constant selector starting with `css=`, `xpath=`, `//`, or containing `#`, `.`, `[`, `>`. `GetBy*` calls are never reported. |
| PWNET003 | `[Fact]`/`[Theory]`/`[Test]`/`[TestMethod]` methods with no invocation named `Expect*`/`Assert*`/`Should*` (or called on an `Assert*`/`Should*` receiver such as `Assert.Equal`). Empty bodies and skipped tests are ignored. |
| PWNET004 | Non-local `http(s)://` string literals; identifiers (variables, parameters, properties) and string literals containing `password`, `passwd`, `secret`, `token`, `apiKey` (case-insensitive). Test code only. |
| PWNET005 | Un-awaited task-returning `*Async` calls whose containing type name/namespace contains `Playwright`: bare statements and `_ = ...` discards. |
| PWNET006 | Playwright API calls (any method on a type in `Microsoft.Playwright.*`) inside a **test class**, i.e. any class declaring a `[Fact]`/`[Theory]`/`[Test]`/`[TestMethod]` method, including its helper methods and lambdas. Page object classes (no test methods) may use Playwright freely. `Assertions.Expect(...)` and `*Assertions` types (`ToBeVisibleAsync`, ...) are allowed. |

"Test code" (PWNET001, PWNET004) means: file path contains `/e2e/` or `/tests/` (either slash direction, case-insensitive), or the assembly name ends with `.Tests`.

Suppress with `#pragma warning disable PWNET00x` / `restore`, `[SuppressMessage]`, or an `.editorconfig` severity of `none`.

Choices where the spec was open:
- "Skip" (PWNET003): method name contains `Skip`, attribute has a `Skip` argument, or the body calls a `*Skip*` method.
- Loopback: `localhost` and `127.0.0.1` are exempt from the URL check.
- `https://{host}/x`: nothing is hardcoded after the scheme, so it is not reported.

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
- **PWNET003**: a helper like `await LoginAndVerifyAsync()` hides the assertion and is reported, while a call named `AssertSomething()` that asserts nothing counts as an assertion. Attributes are matched by name only (no check that they come from xUnit/NUnit/MSTest).
- **PWNET004**: `token`/`secret`/`password` anywhere in an identifier or literal matches, so UI labels like `GetByLabel("Password")` or `"token-expired"` are reported. Environment variable names containing those words are reported too. URLs with a non-loopback host are reported even if they are fixtures for external content.
- **PWNET005**: only discarded results are detected (bare call, `_ =`). A task stored in a variable and never awaited is not reported. It overlaps with compiler CS4014 inside `async` methods but also covers non-async methods. Calls that are `.ConfigureAwait(false)`-ed without `await` are not detected.
- **PWNET006**: one report per Playwright call, so `page.GetByRole(..).ClickAsync()` gives two. Property access (`page.Url`) is not detected. `Expect(page.Locator(..))` still reports the inner call. Test base classes holding Playwright setup are flagged if they declare a test method; put setup in a non-test class (see `BrowserSession` in the sample). Invocations in nested types are judged by the nested type, not the outer test class.
- PWNET001/004 depend on path conventions; projects not named `*.Tests` and not under a `tests`/`e2e` folder are skipped.
- `Microsoft.CodeAnalysis.CSharp` 4.11 is referenced, so the host needs VS 17.11 / .NET SDK 8.0.400 or newer.
