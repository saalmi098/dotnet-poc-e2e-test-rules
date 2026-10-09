Create a production-quality but minimal PoC repository called `dotnet-poc-e2e-test-rules`.

Goal:
Build custom Roslyn diagnostic analyzers for Playwright.NET E2E test code in C#.
The analyzers should be usable in Visual Studio, Rider, VS Code with C# Dev Kit, `dotnet build`, and later SonarQube.

Technical constraints:
- .NET 10 SDK-style projects.
- Analyzer project: `src/E2ETestRules/E2ETestRules.csproj`.
- Analyzer test project: `tests/E2ETestRules.Tests/E2ETestRules.Tests.csproj`.
- Sample consumer project: `sample/E2E.Sample.Tests/E2E.Sample.Tests.csproj`.
- Use xUnit.v3 for analyzer tests.
- Use Microsoft.CodeAnalysis.CSharp and Microsoft.CodeAnalysis.CSharp.Workspaces.
- Target .NET Standard 2.0 for the analyzer project so it can be consumed broadly.
- Do not add unnecessary dependencies.
- The analyzer project must compile independently of Playwright; detect APIs by metadata name, not by hard project reference.
- Use `Microsoft.CodeAnalysis` diagnostic IDs `PWNET001` through `PWNET005`.
- Every diagnostic must have:
  - A stable diagnostic ID.
  - A clear title.
  - A message format.
  - Category `Playwright E2E`.
  - Default severity `Warning`.
  - A help link URI placeholder.
- Add `#pragma warning disable PWNETxxx` support naturally through Roslyn diagnostics.
- Do not attempt to detect business-test intent; only implement deterministic or clearly heuristic checks.

Implement these rules:

1. PWNET001 - AvoidTaskDelayInE2ETests
   - Report an error when `System.Threading.Tasks.Task.Delay` is invoked in a C# file whose path contains `/e2e/`, `\e2e\`, `/tests/`, or `\tests\`, or in a project whose name ends with `.Tests`.
   - Message: “Avoid Task.Delay in Playwright E2E tests. Use Playwright auto-waiting or web-first assertions instead.”

2. PWNET002 - PreferSemanticLocators
   - Report a warning when `Locator(...)` is called with a string literal starting with `css=`, `xpath=`, `//`, or containing likely CSS selectors such as `#`, `.`, `[`, or `>` when used through Playwright `ILocator`, `IPage`, or `IFrame` types.
   - Do not report when the selector is passed to `GetByRole`, `GetByTestId`, `GetByLabel`, `GetByText`, or `GetByPlaceholder`.
   - Message: “Prefer GetByRole, GetByTestId, or GetByLabel over brittle CSS/XPath selectors.”
   - Add an optional analyzer suppression mechanism using `#pragma warning disable PWNET002`.

3. PWNET003 - RequireAssertionInTest
   - For methods marked with `[Fact]`, `[Theory]`, `[Test]`, or `[TestMethod]`, report a warning when the method body contains no invocation whose method name starts with `Expect`, `Assert`, or `Should`.
   - Ignore methods containing `Skip` or empty bodies.
   - Message: “E2E test contains no meaningful assertion.”

4. PWNET004 - AvoidHardcodedEnvironmentData
   - Report a warning for:
     - String literals matching `http://` or `https://`, except `localhost`.
     - Identifiers or string literals containing `password`, `passwd`, `secret`, `token`, or `apiKey`.
   - Only apply this inside test projects/files as defined for PWNET001.
   - Message: “Move environment-specific URLs and credentials to configuration or user secrets.”

5. PWNET005 - AwaitPlaywrightAsyncCalls
   - Report an error when an invocation returns `System.Threading.Tasks.Task` or `Task<T>` and is not awaited, where the invoked method name ends with `Async` and the containing type namespace or name contains `Playwright`.
   - Do not duplicate compiler diagnostics where possible; focus on Playwright-specific invocation patterns.
   - Message: “Await Playwright asynchronous operations to avoid race conditions and unobserved tasks.”

Project setup:
- Create a solution file named `dotnet-poc-e2e-test-rules.sln`.
- In `sample/E2E.Sample.Tests`, reference the analyzer project like this:

  <ProjectReference Include="../../src/E2ETestRules/E2ETestRules.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />

- Create a root `Directory.Build.props` with:
  - `LangVersion latest`
  - `Nullable enable`
  - `TreatWarningsAsErrors true`
  - `EnforceCodeStyleInBuild true`
- Create a root `.editorconfig` that sets:
  - `dotnet_diagnostic.PWNET001.severity = error`
  - `dotnet_diagnostic.PWNET002.severity = warning`
  - `dotnet_diagnostic.PWNET003.severity = warning`
  - `dotnet_diagnostic.PWNET004.severity = warning`
  - `dotnet_diagnostic.PWNET005.severity = error`
- Add a `.gitignore` for .NET.
- Add a `README.md` explaining:
  - What each rule detects.
  - How to build and test.
  - How to consume the analyzer via ProjectReference.
  - How to package it later as a NuGet analyzer package.
  - How to integrate it with SonarQube later.
  - Known limitations and false-positive risks.

Analyzer tests:
- For every rule, create at least:
  - One positive test where the diagnostic is expected.
  - One negative test where no diagnostic is expected.
  - One edge-case test.
- Use Roslyn analyzer test helpers or a lightweight in-memory compilation approach.
- Do not use brittle line/column assertions except where necessary.

Sample project:
- Create sample C# files that trigger each rule, but do not make the sample project fail the overall solution build by default.
- Put intentionally violating examples under `sample/E2E.Sample.Tests/Examples/`.
- Exclude that folder from compilation using `<Compile Remove="Examples/**/*.cs" />`, but retain the files as documentation.
- Add one compliant example file showing recommended Playwright.NET patterns.

Validation:
- Run:
  - `dotnet restore`
  - `dotnet build`
  - `dotnet test`
- Fix all build and test failures.
- Print a short summary of:
  - Rules implemented.
  - Test coverage per rule.
  - Commands to run the PoC.
  - Next steps for SonarQube integration.

Start by creating the repository structure, then implement one analyzer at a time, then add tests, then run the full validation.