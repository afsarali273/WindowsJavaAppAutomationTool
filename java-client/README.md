# JAB Inspector Java Client

Maven client wrapper for the Java Access Bridge Inspector REST API.

It supports two automation styles:

- Selenium/WebDriver style: create a driver session once, load a repository, then act on elements through that session.
- UFT/TOSCA style: execute stateless actions by window/modal selector and object repository key, without keeping a driver session open.

## Build

```powershell
cd java-client
mvn clean package
```

## Session style

```java
try (JavaDriver driver = JavaDriver.attachByTitle(
        URI.create("http://localhost:5055"),
        "Download",
        "C:/recordings/josm-download.jrecording.json")) {

    driver.loadRepositories(
        "C:/recordings/common-controls.jrecording.json",
        "C:/recordings/josm-download.jrecording.json");

    driver.window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
          .element("page_tab_bounding_box_2")
          .waitUntilExists()
          .click();

    boolean okVisible = driver.element("button_ok_0").isVisible();
    driver.element("button_ok_0").click();
}
```

## Session-independent style

```java
JavaAutomation automation = JavaAutomation.connect(URI.create("http://localhost:5055"))
    .repositories(
        "C:/recordings/common-controls.jrecording.json",
        "C:/recordings/josm-download.jrecording.json");

automation.window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
          .object("page_tab_bounding_box_2")
          .click(RetryOptions.of(Duration.ofSeconds(10), Duration.ofMillis(250)));
```

The second style is useful when scripts should re-attach on each action and naturally follow modals/popups by title, class name, process id, VM id, or repository window scope.

When multiple repositories contain the same `objectKey`, the last repository in the list wins. Use this to layer a shared/common repository first and a screen-specific repository last.

## Validation helpers

Validation is available in both session and session-independent flows:

```java
JavaValidation validation = automation.window(JavaWindowSelector.title("Download"))
    .object("button_ok_0")
    .validate();

if (validation.exists() && validation.isVisible() && validation.isEnabled()) {
    automation.window(JavaWindowSelector.title("Download"))
        .object("button_ok_0")
        .click();
}
```

Convenience methods include:

- `exists()` / `isExist()`
- `isVisible()`
- `isShowing()`
- `isEnabled()`
- `isFocusable()`
- `isSelected()`
- `hasText()`
- `hasText("expected text")`
