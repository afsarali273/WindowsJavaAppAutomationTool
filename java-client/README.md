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

    driver.window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
          .element("page_tab_bounding_box_2")
          .click();

    driver.element("button_ok_0").click();
}
```

## Session-independent style

```java
JavaAutomation automation = JavaAutomation.connect(URI.create("http://localhost:5055"))
    .repository("C:/recordings/josm-download.jrecording.json");

automation.window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
          .object("page_tab_bounding_box_2")
          .click();
```

The second style is useful when scripts should re-attach on each action and naturally follow modals/popups by title, class name, process id, VM id, or repository window scope.
