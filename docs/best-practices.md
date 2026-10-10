# PWNET rules and Playwright best practices

Sources: [Playwright for .NET docs](https://playwright.dev/dotnet/docs/intro) and [Playwright best practices](https://playwright.dev/docs/best-practices). The best-practices page exists only in the generic (Node.js) docs, but its advice applies to every language. C# rules are backed by the [Microsoft async guidance](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios).

Wording below is paraphrased. Each link points to the exact section on the source page.

## Summary: best practice → analyzer

| Best practice | Source | Analyzer | Status |
|---|---|---|---|
| Use web-first assertions, not one-shot reads | [Assertions](https://playwright.dev/dotnet/docs/test-assertions), [Best practices](https://playwright.dev/docs/best-practices#dont-use-manual-assertions) | PWNET008 | covered |
| No hardcoded waits | [Writing tests](https://playwright.dev/dotnet/docs/writing-tests), [`WaitForTimeoutAsync`](https://playwright.dev/dotnet/docs/api/class-page#page-wait-for-timeout) | PWNET001 | covered |
| Resilient locators instead of CSS/XPath | [Locators](https://playwright.dev/dotnet/docs/locators#locate-by-css-or-xpath) | PWNET002 | covered |
| Avoid forced actions and programmatic clicks | [Actions](https://playwright.dev/dotnet/docs/input#forcing-the-click), [Actionability](https://playwright.dev/dotnet/docs/actionability#forcing-actions) | PWNET009 | covered (review warning) |
| Unstable `Locator.AllAsync()` on dynamic lists | [Locator API](https://playwright.dev/dotnet/docs/api/class-locator#locator-all) | none | **not covered**, candidate rule |
| Isolated tests, no shared browser state | [Browser contexts](https://playwright.dev/dotnet/docs/browser-contexts), [Best practices](https://playwright.dev/docs/best-practices#make-tests-as-isolated-as-possible) | none | **not covered**; static analysis can't prove isolation |
| Await every Playwright call (no floating tasks) | [Best practices: lint](https://playwright.dev/docs/best-practices#lint-your-tests) | PWNET005 | covered |
| No `async void` | [Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios#return-async-void-only-from-event-handlers) | PWNET007 | covered (also xUnit1049) |
| No blocking on async calls | [Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios#yield-for-tasks-in-a-nonblocking-manner) | PWNET005 | covered |
| Page Object Model | [Page object models](https://playwright.dev/dotnet/docs/pom) | PWNET006 | covered (project convention) |
| Swallowed exceptions, missing assertions, empty tests, environment URLs | none (heuristics) | PWNET010, PWNET003, PWNET004 | covered as heuristics |
| No conditional logic in tests; no branching on one-shot state reads | [Assertions](https://playwright.dev/dotnet/docs/test-assertions), [`AddLocatorHandlerAsync`](https://playwright.dev/dotnet/docs/api/class-page#page-add-locator-handler) | PWNET011 | covered (branch count is a heuristic) |

## Analyzers

### PWNET001: avoid fixed delays
- **What:** flags `Task.Delay`, `Thread.Sleep` and `WaitForTimeoutAsync` in test code.
- **Why:** Playwright auto-waits for actionability before every action, so fixed sleeps only add runtime and flakiness. The API docs are explicit: "Never wait for timeout in production. Tests that wait for time are inherently flaky."
- **Source:** [`Page.WaitForTimeoutAsync`](https://playwright.dev/dotnet/docs/api/class-page#page-wait-for-timeout), [Writing tests: auto-waiting](https://playwright.dev/dotnet/docs/writing-tests)

### PWNET002: prefer semantic locators
- **What:** flags `Locator("...")` with CSS/XPath or positional selectors (`#id`, `.class`, `//div`, `:nth-child`). Not flagged: `GetBy*`, `text=`/`role=` engines, `[data-testid=...]`.
- **Why:** Playwright recommends user-facing attributes and explicit contracts (role, label, test id). CSS and XPath are tied to DOM structure and break when it changes.
- **Source:** [Locators: CSS or XPath](https://playwright.dev/dotnet/docs/locators#locate-by-css-or-xpath), [Best practices: user-facing attributes](https://playwright.dev/docs/best-practices#prefer-user-facing-attributes-to-xpath-or-css-selectors)

### PWNET003: require an assertion
- **What:** flags `[Fact]`/`[Theory]` tests with no `Expect*`/`Assert*`/`Should*` call, including empty bodies. Skipped tests are ignored.
- **Why:** a test without verification passes even when the app is broken.
- **Source:** heuristic, no explicit Playwright guidance.

### PWNET004: avoid hardcoded environment data
- **What:** flags every absolute `http(s)://` literal (including `localhost`) and credential-like names (`password`, `token`, `apiKey`, ...) in test code.
- **Why:** environment URLs and secrets belong in configuration or user secrets, not in test source.
- **Source:** heuristic, project convention.

### PWNET005: await Playwright calls
- **What:** flags Playwright `*Async` calls that are discarded (no `await`) or blocked on (`.Wait()`, `.Result`, `.GetAwaiter().GetResult()`), including `Expect(...).ToBe*Async()`.
- **Why:** a missing `await` lets the test continue before the action or assertion finishes, and its failure is lost. Playwright recommends linting for missing awaits. Microsoft warns that blocking on tasks can deadlock.
- **Source:** [Best practices: lint your tests](https://playwright.dev/docs/best-practices#lint-your-tests), [Microsoft: non-blocking await](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios#yield-for-tasks-in-a-nonblocking-manner)

### PWNET006: use page objects
- **What:** flags direct Playwright calls in test classes, except `Expect(...)` assertions.
- **Why:** page objects keep selectors in one place and give tests a higher-level API, which simplifies writing and maintaining them.
- **Source:** [Page object models](https://playwright.dev/dotnet/docs/pom) (presented as one option, not a requirement; enforcing it is a project convention)

### PWNET007: no `async void` tests
- **What:** flags `async void` `[Fact]`/`[Theory]` methods.
- **Why:** an `async void` method can't be awaited and its exceptions can't be caught by the caller, so the runner can't observe failures. Microsoft reserves `async void` for event handlers. Duplicates xUnit's built-in `xUnit1049`.
- **Source:** [Microsoft: async void only for event handlers](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios#return-async-void-only-from-event-handlers), [Async return types](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-return-types#void-return-type)

### PWNET008: prefer web-first assertions
- **What:** flags `Assert.*` over one-shot browser reads (`IsVisibleAsync`, `TextContentAsync`, `InputValueAsync`, `CountAsync`, `page.Url`, ...), directly or via a local variable. The message names the replacement (`ToBeVisibleAsync`, `ToHaveTextAsync`, ...).
- **Why:** Playwright's `Expect` assertions re-check until the condition holds or time out. A one-shot read returns immediately, so the result depends on timing. The API docs for `IsVisibleAsync`, `TextContentAsync` etc. recommend the `Expect` equivalent to avoid flakiness.
- **Source:** [Assertions](https://playwright.dev/dotnet/docs/test-assertions), [Best practices: no manual assertions](https://playwright.dev/docs/best-practices#dont-use-manual-assertions), [`Locator.IsVisibleAsync`](https://playwright.dev/dotnet/docs/api/class-locator#locator-is-visible)

### PWNET009: avoid bypassing actionability checks
- **What:** flags `Force = true`, `DispatchEventAsync(...)` and `EvaluateAsync` scripts that call `.click()`/`dispatchEvent(`.
- **Why:** these skip Playwright's visible/enabled/stable checks, so a test can pass while a real user could not click. Playwright documents them as deliberate escape hatches, so this is a review warning, not a ban.
- **Source:** [Actions: forcing the click](https://playwright.dev/dotnet/docs/input#forcing-the-click), [Actions: programmatic click](https://playwright.dev/dotnet/docs/input#programmatic-click), [Actionability](https://playwright.dev/dotnet/docs/actionability#forcing-actions)

### PWNET010: don't swallow assertion failures
- **What:** in a test, flags a broad `catch` that neither rethrows nor asserts, around a `try` containing an assertion.
- **Why:** the assertion can never fail the test.
- **Source:** heuristic, no explicit Playwright guidance.

### PWNET011: avoid conditional logic in tests
- **What:** (1) flags `[Fact]`/`[Theory]` methods containing any `if`/`switch`/`?:`/loop. (2) In any class, including page objects, flags branches whose condition is a one-shot browser read (`if (await banner.IsVisibleAsync())`).
- **Why:** (1) a branching test hides several scenarios and doesn't always run the same checks; separate tests or a `[Theory]` make each scenario explicit. (2) the read returns immediately, so the path taken depends on timing. Wait for the expected state with `Expect(...)` instead, or handle overlays that may or may not appear with `Page.AddLocatorHandlerAsync`.
- **Source:** (1) heuristic, no explicit Playwright guidance. (2) [Assertions](https://playwright.dev/dotnet/docs/test-assertions) (retry instead of one-shot checks), [`Page.AddLocatorHandlerAsync`](https://playwright.dev/dotnet/docs/api/class-page#page-add-locator-handler).
