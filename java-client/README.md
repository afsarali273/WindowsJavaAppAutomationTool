# JAB Inspector Java Client

Maven client wrapper for the Java Access Bridge Inspector REST API.

The client compiles with Java 11 or newer.

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

## Opening applications

The API and Java client can launch Java apps before attaching to them. Supported startup paths include `.jar`, `.bat` / `.cmd`, and direct executables such as `.exe`.

```java
JavaLaunchApplicationRequest launch = JavaLaunchApplicationRequest
    .jar("C:/apps/freeplane/freeplane.jar")
    .arguments("--someFlag")
    .workingDirectory("C:/apps/freeplane")
    .waitForWindow(JavaWindowSelector.title("Freeplane"))
    .waitTimeout(Duration.ofSeconds(30))
    .waitPollInterval(Duration.ofMillis(500));

JavaLaunchApplicationResult result = JavaAutomation.connect(URI.create("http://localhost:5055"))
    .openApplication(launch);
```

For session-style automation, you can launch and attach in one step:

```java
JavaDriver driver = JavaDriver.launchAndAttach(
    URI.create("http://localhost:5055"),
    launch,
    RetryOptions.of(Duration.ofSeconds(30), Duration.ofMillis(500)));
```

## Finding multiple elements

Use `findElements` when a locator/object repository key may match more than one live element:

```java
List<JavaElementHandle> tabs = automation
    .window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
    .findElements("page_tab_download_from_osm_0");

for (JavaElementHandle tab : tabs) {
    System.out.println(tab.label() + " score=" + tab.snapshot().score());
}
```

The convenience overload uses defaults of `minimumScore=70` and `maxResults=20`. Use the explicit overload when you want to tune matching:

```java
List<JavaElementHandle> tabs = automation
    .window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
    .findElements("page_tab_download_from_osm_0", 70, 20);

tabs.stream().findFirst().ifPresent(tab -> {
    System.out.println(tab.label());
    tab.click();
});
```

Use `findChildElements` to return all descendants under a resolved parent object:

```java
List<JavaElementHandle> descendants = automation
    .window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
    .object("download_dialog_root")
    .findChildElements();
```

The convenience overload uses defaults of `maxDepth=10`, `maxResults=200`, and `includeSelf=false`. Use the explicit overload when you want tighter control:

```java
List<JavaElementHandle> descendants = automation
    .window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
    .object("download_dialog_root")
    .findChildElements(10, 500, false);
```

If no parent object is supplied through the low-level API, child search starts from the current window root.

## Oracle-style grid helpers

For Oracle Forms or other Java apps that expose table-like blocks through JAB, the client supports row and cell helpers, including header-based lookup:

```java
JavaWindowScope download = automation.window(JavaWindowSelector.title("Download").className("SunAwtDialog"));

List<JavaElementHandle> rows = download.findTableRows("download_dialog_root");
JavaElementHandle cell = download.findTableCell("download_dialog_root", 0, "Bounding Box");
String text = download.getTableCellText("download_dialog_root", 0, "Bounding Box");
cell.click();
```

When the live app does not expose actual row containers, the client synthesizes row handles from the first visible cell in each inferred row so Oracle-style multi-record blocks remain navigable.

## Runnable example

See this sample:

- [FindElementsExample.java](C:/Users/Afsar/POC/JavaAutomation/java-client/src/main/java/com/afsarali/jab/client/examples/FindElementsExample.java)

It demonstrates:

- session-independent `automation.window(...).findElements(...)`
- session-independent `automation.window(...).object(...).findChildElements(...)`
- session-based `driver.window(...).findElements(...)`
- session-based `driver.element(...).findChildElements(...)`

Run it with:

```powershell
cd java-client
mvn -q -DskipTests compile
java -cp target/classes com.afsarali.jab.client.examples.FindElementsExample
```
