package com.afsarali.jab.client;

import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.LocatorSuggestion;

import java.time.Duration;
import java.util.List;
import java.util.stream.Collectors;

public final class JavaElementHandle {
    private final JavaElementSnapshot snapshot;
    private final ActionTarget target;

    private JavaElementHandle(JavaElementSnapshot snapshot, ActionTarget target) {
        this.snapshot = snapshot;
        this.target = target;
    }

    static JavaElementHandle from(JavaDriver driver, JavaWindowSelector window, JavaElementSnapshot snapshot) {
        LocatorSuggestion locator = snapshot.locator();
        return new JavaElementHandle(snapshot, new SessionTarget(driver, window, locator));
    }

    static JavaElementHandle from(JavaAutomation automation, JavaWindowSelector window, JavaElementSnapshot snapshot) {
        LocatorSuggestion locator = snapshot.locator();
        return new JavaElementHandle(snapshot, new StatelessTarget(automation, window, locator));
    }

    public JavaElementSnapshot snapshot() {
        return snapshot;
    }

    public LocatorSuggestion locator() {
        return snapshot == null ? null : snapshot.locator();
    }

    public String displayName() {
        return snapshot == null ? "" : snapshot.displayName();
    }

    public String label() {
        if (snapshot == null) return "(unresolved)";
        if (snapshot.objectKey() != null && !snapshot.objectKey().isBlank()) return snapshot.objectKey();
        if (snapshot.displayName() != null && !snapshot.displayName().isBlank()) return snapshot.displayName();
        if (snapshot.name() != null && !snapshot.name().isBlank()) return snapshot.name();
        if (snapshot.virtualAccessibleName() != null && !snapshot.virtualAccessibleName().isBlank()) return snapshot.virtualAccessibleName();
        if (snapshot.role() != null && !snapshot.role().isBlank()) return snapshot.role();
        return "(unresolved)";
    }

    public JavaElementHandle focus() {
        target.focus();
        return this;
    }

    public JavaElementHandle focus(RetryOptions retryOptions) {
        target.focus(retryOptions);
        return this;
    }

    public JavaElementHandle click() {
        target.click();
        return this;
    }

    public JavaElementHandle click(RetryOptions retryOptions) {
        target.click(retryOptions);
        return this;
    }

    public JavaElementHandle doubleClick() {
        target.doubleClick();
        return this;
    }

    public JavaElementHandle doubleClick(RetryOptions retryOptions) {
        target.doubleClick(retryOptions);
        return this;
    }

    public JavaElementHandle closeWindow() {
        target.closeWindow();
        return this;
    }

    public JavaElementHandle closeWindow(RetryOptions retryOptions) {
        target.closeWindow(retryOptions);
        return this;
    }

    public JavaElementHandle setText(String text) {
        target.setText(text);
        return this;
    }

    public JavaElementHandle setText(String text, RetryOptions retryOptions) {
        target.setText(text, retryOptions);
        return this;
    }

    public JavaElementHandle typeText(String text) {
        target.typeText(text);
        return this;
    }

    public JavaElementHandle typeText(String text, RetryOptions retryOptions) {
        target.typeText(text, retryOptions);
        return this;
    }

    public String getText() {
        return target.getText();
    }

    public String getText(RetryOptions retryOptions) {
        return target.getText(retryOptions);
    }

    public boolean exists() {
        return target.exists();
    }

    public boolean isExist() {
        return exists();
    }

    public boolean isVisible() {
        return target.isVisible();
    }

    public boolean isShowing() {
        return target.isShowing();
    }

    public boolean isEnabled() {
        return target.isEnabled();
    }

    public boolean isFocusable() {
        return target.isFocusable();
    }

    public boolean isSelected() {
        return target.isSelected();
    }

    public boolean hasText() {
        return target.hasText();
    }

    public boolean hasText(String expectedText) {
        return target.hasText(expectedText);
    }

    public JavaValidation validate() {
        return target.validate();
    }

    public JavaValidation validate(String expectedText) {
        return target.validate(expectedText);
    }

    public JavaElementHandle waitUntilExists() {
        target.waitUntilExists();
        return this;
    }

    public JavaElementHandle waitUntilExists(Duration timeout, Duration pollInterval) {
        target.waitUntilExists(timeout, pollInterval);
        return this;
    }

    public JavaElementHandle waitUntilExists(RetryOptions options) {
        target.waitUntilExists(options);
        return this;
    }

    public List<JavaElementHandle> findChildElements() {
        return wrap(target.findChildSnapshots());
    }

    public List<JavaElementHandle> findChildElements(Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return wrap(target.findChildSnapshots(maxDepth, maxResults, includeSelf));
    }

    private List<JavaElementHandle> wrap(List<JavaElementSnapshot> snapshots) {
        if (snapshots == null || snapshots.isEmpty()) return List.of();
        return snapshots.stream()
                .map(target::child)
                .collect(Collectors.toList());
    }

    private interface ActionTarget {
        void focus();
        void focus(RetryOptions retryOptions);
        void click();
        void click(RetryOptions retryOptions);
        void doubleClick();
        void doubleClick(RetryOptions retryOptions);
        void closeWindow();
        void closeWindow(RetryOptions retryOptions);
        void setText(String text);
        void setText(String text, RetryOptions retryOptions);
        void typeText(String text);
        void typeText(String text, RetryOptions retryOptions);
        String getText();
        String getText(RetryOptions retryOptions);
        boolean exists();
        boolean isVisible();
        boolean isShowing();
        boolean isEnabled();
        boolean isFocusable();
        boolean isSelected();
        boolean hasText();
        boolean hasText(String expectedText);
        JavaValidation validate();
        JavaValidation validate(String expectedText);
        void waitUntilExists();
        void waitUntilExists(Duration timeout, Duration pollInterval);
        void waitUntilExists(RetryOptions options);
        List<JavaElementSnapshot> findChildSnapshots();
        List<JavaElementSnapshot> findChildSnapshots(Integer maxDepth, Integer maxResults, boolean includeSelf);
        JavaElementHandle child(JavaElementSnapshot snapshot);
    }

    private static final class SessionTarget implements ActionTarget {
        private final JavaDriver driver;
        private final JavaWindowSelector window;
        private final LocatorSuggestion locator;
        private final JavaElement element;

        private SessionTarget(JavaDriver driver, JavaWindowSelector window, LocatorSuggestion locator) {
            this.driver = driver;
            this.window = window;
            this.locator = locator;
            this.element = new JavaElement(driver, locator, window);
        }

        @Override public void focus() { element.focus(); }
        @Override public void focus(RetryOptions retryOptions) { element.focus(retryOptions); }
        @Override public void click() { element.click(); }
        @Override public void click(RetryOptions retryOptions) { element.click(retryOptions); }
        @Override public void doubleClick() { element.doubleClick(); }
        @Override public void doubleClick(RetryOptions retryOptions) { element.doubleClick(retryOptions); }
        @Override public void closeWindow() { element.closeWindow(); }
        @Override public void closeWindow(RetryOptions retryOptions) { element.closeWindow(retryOptions); }
        @Override public void setText(String text) { element.setText(text); }
        @Override public void setText(String text, RetryOptions retryOptions) { element.setText(text, retryOptions); }
        @Override public void typeText(String text) { element.typeText(text); }
        @Override public void typeText(String text, RetryOptions retryOptions) { element.typeText(text, retryOptions); }
        @Override public String getText() { return element.getText(); }
        @Override public String getText(RetryOptions retryOptions) { return element.getText(retryOptions); }
        @Override public boolean exists() { return element.exists(); }
        @Override public boolean isVisible() { return element.isVisible(); }
        @Override public boolean isShowing() { return element.isShowing(); }
        @Override public boolean isEnabled() { return element.isEnabled(); }
        @Override public boolean isFocusable() { return element.isFocusable(); }
        @Override public boolean isSelected() { return element.isSelected(); }
        @Override public boolean hasText() { return element.hasText(); }
        @Override public boolean hasText(String expectedText) { return element.hasText(expectedText); }
        @Override public JavaValidation validate() { return element.validate(); }
        @Override public JavaValidation validate(String expectedText) { return element.validate(expectedText); }
        @Override public void waitUntilExists() { element.waitUntilExists(); }
        @Override public void waitUntilExists(Duration timeout, Duration pollInterval) { element.waitUntilExists(timeout, pollInterval); }
        @Override public void waitUntilExists(RetryOptions options) { element.waitUntilExists(options); }
        @Override public List<JavaElementSnapshot> findChildSnapshots() { return driver.findChildSnapshots(null, locator, null, null, false); }
        @Override public List<JavaElementSnapshot> findChildSnapshots(Integer maxDepth, Integer maxResults, boolean includeSelf) { return driver.findChildSnapshots(null, locator, maxDepth, maxResults, includeSelf); }
        @Override public JavaElementHandle child(JavaElementSnapshot snapshot) { return JavaElementHandle.from(driver, window, snapshot); }
    }

    private static final class StatelessTarget implements ActionTarget {
        private final JavaAutomation automation;
        private final JavaWindowSelector window;
        private final LocatorSuggestion locator;
        private final JavaObject object;

        private StatelessTarget(JavaAutomation automation, JavaWindowSelector window, LocatorSuggestion locator) {
            this.automation = automation;
            this.window = window;
            this.locator = locator;
            this.object = new JavaObject(automation, window, locator);
        }

        @Override public void focus() { object.focus(); }
        @Override public void focus(RetryOptions retryOptions) { object.focus(retryOptions); }
        @Override public void click() { object.click(); }
        @Override public void click(RetryOptions retryOptions) { object.click(retryOptions); }
        @Override public void doubleClick() { object.doubleClick(); }
        @Override public void doubleClick(RetryOptions retryOptions) { object.doubleClick(retryOptions); }
        @Override public void closeWindow() { object.closeWindow(); }
        @Override public void closeWindow(RetryOptions retryOptions) { object.closeWindow(retryOptions); }
        @Override public void setText(String text) { object.setText(text); }
        @Override public void setText(String text, RetryOptions retryOptions) { object.setText(text, retryOptions); }
        @Override public void typeText(String text) { object.typeText(text); }
        @Override public void typeText(String text, RetryOptions retryOptions) { object.typeText(text, retryOptions); }
        @Override public String getText() { return object.getText(); }
        @Override public String getText(RetryOptions retryOptions) { return object.getText(retryOptions); }
        @Override public boolean exists() { return object.exists(); }
        @Override public boolean isVisible() { return object.isVisible(); }
        @Override public boolean isShowing() { return object.isShowing(); }
        @Override public boolean isEnabled() { return object.isEnabled(); }
        @Override public boolean isFocusable() { return object.isFocusable(); }
        @Override public boolean isSelected() { return object.isSelected(); }
        @Override public boolean hasText() { return object.hasText(); }
        @Override public boolean hasText(String expectedText) { return object.hasText(expectedText); }
        @Override public JavaValidation validate() { return object.validate(); }
        @Override public JavaValidation validate(String expectedText) { return object.validate(expectedText); }
        @Override public void waitUntilExists() { object.waitUntilExists(); }
        @Override public void waitUntilExists(Duration timeout, Duration pollInterval) { object.waitUntilExists(timeout, pollInterval); }
        @Override public void waitUntilExists(RetryOptions options) { object.waitUntilExists(options); }
        @Override public List<JavaElementSnapshot> findChildSnapshots() { return automation.findChildSnapshots(null, locator, null, null, false, window); }
        @Override public List<JavaElementSnapshot> findChildSnapshots(Integer maxDepth, Integer maxResults, boolean includeSelf) { return automation.findChildSnapshots(null, locator, maxDepth, maxResults, includeSelf, window); }
        @Override public JavaElementHandle child(JavaElementSnapshot snapshot) { return JavaElementHandle.from(automation, window, snapshot); }
    }
}
