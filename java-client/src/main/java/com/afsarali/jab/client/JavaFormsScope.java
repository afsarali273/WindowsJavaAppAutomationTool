package com.afsarali.jab.client;

import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.LocatorSuggestion;

public final class JavaFormsScope {
    private final JavaDriver driver;
    private final JavaAutomation automation;
    private final JavaWindowSelector window;
    private final String scopeName;
    private final String scopePath;
    private final String viewportName;
    private final String viewportPath;

    JavaFormsScope(JavaDriver driver, JavaWindowSelector window, String scopeName, String scopePath, String viewportName, String viewportPath) {
        this.driver = driver;
        this.automation = null;
        this.window = window;
        this.scopeName = scopeName;
        this.scopePath = scopePath;
        this.viewportName = viewportName;
        this.viewportPath = viewportPath;
    }

    JavaFormsScope(JavaAutomation automation, JavaWindowSelector window, String scopeName, String scopePath, String viewportName, String viewportPath) {
        this.driver = null;
        this.automation = automation;
        this.window = window;
        this.scopeName = scopeName;
        this.scopePath = scopePath;
        this.viewportName = viewportName;
        this.viewportPath = viewportPath;
    }

    public JavaFormsScope viewport(String name) {
        return new JavaFormsScope(driver, automation, window, scopeName, scopePath, name, null);
    }

    public JavaFormsScope viewportPath(String path) {
        return new JavaFormsScope(driver, automation, window, scopeName, scopePath, viewportName, path);
    }

    public JavaElement element(String objectKey) {
        ensureSession();
        return driver.element(objectKey);
    }

    public JavaElement element(LocatorSuggestion locator) {
        ensureSession();
        return driver.element(apply(locator));
    }

    public JavaObject object(String objectKey) {
        ensureStateless();
        return new JavaObject(automation, window, objectKey);
    }

    public JavaObject object(LocatorSuggestion locator) {
        ensureStateless();
        return new JavaObject(automation, window, apply(locator));
    }

    public JavaTable table(String objectKey) {
        return driver != null
                ? JavaTable.from(driver.element(objectKey))
                : JavaTable.from(new JavaObject(automation, window, objectKey));
    }

    public JavaTable table(LocatorSuggestion locator) {
        return driver != null
                ? JavaTable.from(driver.element(apply(locator)))
                : JavaTable.from(new JavaObject(automation, window, apply(locator)));
    }

    public LocatorSuggestion locator(LocatorSuggestion locator) {
        return apply(locator);
    }

    private JavaFormsScope(JavaDriver driver, JavaAutomation automation, JavaWindowSelector window, String scopeName, String scopePath, String viewportName, String viewportPath) {
        this.driver = driver;
        this.automation = automation;
        this.window = window;
        this.scopeName = scopeName;
        this.scopePath = scopePath;
        this.viewportName = viewportName;
        this.viewportPath = viewportPath;
    }

    private LocatorSuggestion apply(LocatorSuggestion locator) {
        LocatorSuggestion scoped = locator;
        if (scopeName != null && !scopeName.isBlank()) {
            scoped = JavaLocatorScopes.withinFormsScope(scoped, scopeName);
        }
        if (scopePath != null && !scopePath.isBlank()) {
            scoped = JavaLocatorScopes.withinFormsScopePath(scoped, scopePath);
        }
        if (viewportName != null && !viewportName.isBlank()) {
            scoped = JavaLocatorScopes.withinFormsViewport(scoped, viewportName);
        }
        if (viewportPath != null && !viewportPath.isBlank()) {
            scoped = JavaLocatorScopes.withinFormsViewportPath(scoped, viewportPath);
        }
        return scoped;
    }

    private void ensureSession() {
        if (driver == null) throw new IllegalStateException("This Forms scope is stateless. Use object(...) instead of element(...).");
    }

    private void ensureStateless() {
        if (automation == null) throw new IllegalStateException("This Forms scope is session-based. Use element(...) instead of object(...).");
    }
}
