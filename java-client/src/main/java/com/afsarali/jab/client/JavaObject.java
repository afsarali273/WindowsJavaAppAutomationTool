package com.afsarali.jab.client;

import com.afsarali.jab.client.model.DriverResult;
import com.afsarali.jab.client.model.JavaAction;
import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.LocatorSuggestion;

import java.time.Duration;
import java.util.List;

public final class JavaObject {
    private final JavaAutomation automation;
    private final JavaWindowSelector window;
    private final String objectKey;
    private final LocatorSuggestion locator;

    JavaObject(JavaAutomation automation, JavaWindowSelector window, String objectKey) {
        this.automation = automation;
        this.window = window;
        this.objectKey = objectKey;
        this.locator = null;
    }

    JavaObject(JavaAutomation automation, JavaWindowSelector window, LocatorSuggestion locator) {
        this.automation = automation;
        this.window = window;
        this.objectKey = null;
        this.locator = locator;
    }

    public JavaObject focus() {
        automation.run(JavaAction.FOCUS, objectKey, locator, "", window);
        return this;
    }

    public JavaObject focus(RetryOptions retryOptions) {
        automation.run(JavaAction.FOCUS, objectKey, locator, "", window, retryOptions);
        return this;
    }

    public JavaObject click() {
        automation.run(JavaAction.CLICK, objectKey, locator, "", window);
        return this;
    }

    public JavaObject click(RetryOptions retryOptions) {
        automation.run(JavaAction.CLICK, objectKey, locator, "", window, retryOptions);
        return this;
    }

    public JavaObject doubleClick() {
        automation.run(JavaAction.DOUBLE_CLICK, objectKey, locator, "", window);
        return this;
    }

    public JavaObject doubleClick(RetryOptions retryOptions) {
        automation.run(JavaAction.DOUBLE_CLICK, objectKey, locator, "", window, retryOptions);
        return this;
    }

    public JavaObject closeWindow() {
        automation.run(JavaAction.CLOSE_WINDOW, objectKey, locator, "", window);
        return this;
    }

    public JavaObject closeWindow(RetryOptions retryOptions) {
        automation.run(JavaAction.CLOSE_WINDOW, objectKey, locator, "", window, retryOptions);
        return this;
    }

    public JavaObject setText(String text) {
        automation.run(JavaAction.SET_TEXT, objectKey, locator, text, window);
        return this;
    }

    public JavaObject setText(String text, RetryOptions retryOptions) {
        automation.run(JavaAction.SET_TEXT, objectKey, locator, text, window, retryOptions);
        return this;
    }

    public JavaObject typeText(String text) {
        automation.run(JavaAction.TYPE_TEXT, objectKey, locator, text, window);
        return this;
    }

    public JavaObject typeText(String text, RetryOptions retryOptions) {
        automation.run(JavaAction.TYPE_TEXT, objectKey, locator, text, window, retryOptions);
        return this;
    }

    public String getText() {
        DriverResult result = automation.run(JavaAction.GET_TEXT, objectKey, locator, "", window);
        return result.text();
    }

    public String getText(RetryOptions retryOptions) {
        DriverResult result = automation.run(JavaAction.GET_TEXT, objectKey, locator, "", window, retryOptions);
        return result.text();
    }

    public boolean exists() {
        return validate().exists();
    }

    public boolean isExist() {
        return exists();
    }

    public boolean isVisible() {
        return validate().isVisible();
    }

    public boolean isShowing() {
        return validate().isShowing();
    }

    public boolean isEnabled() {
        return validate().isEnabled();
    }

    public boolean isFocusable() {
        return validate().isFocusable();
    }

    public boolean isSelected() {
        return validate().isSelected();
    }

    public boolean hasText() {
        return validate().hasText();
    }

    public boolean hasText(String expectedText) {
        return validate(expectedText).textMatches();
    }

    public JavaValidation validate() {
        return automation.validate(objectKey, locator, null, window);
    }

    public JavaValidation validate(String expectedText) {
        return automation.validate(objectKey, locator, expectedText, window);
    }

    public JavaObject waitUntilExists() {
        return waitUntilExists(RetryOptions.defaults());
    }

    public JavaObject waitUntilExists(Duration timeout, Duration pollInterval) {
        return waitUntilExists(RetryOptions.of(timeout, pollInterval));
    }

    public JavaObject waitUntilExists(RetryOptions options) {
        Wait.until(this::exists, options, "Timed out waiting for Java element '" + label() + "'.");
        return this;
    }

    public List<JavaElementHandle> findChildElements() {
        return automation.findChildElements(objectKey, locator, null, null, false, window);
    }

    public List<JavaElementHandle> findChildElements(Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return automation.findChildElements(objectKey, locator, maxDepth, maxResults, includeSelf, window);
    }

    private String label() {
        if (objectKey != null && !objectKey.isBlank()) return objectKey;
        if (locator == null) return "(inline locator)";
        if (locator.name() != null && !locator.name().isBlank()) return locator.name();
        if (locator.virtualAccessibleName() != null && !locator.virtualAccessibleName().isBlank()) return locator.virtualAccessibleName();
        if (locator.role() != null && !locator.role().isBlank()) return locator.role();
        return "(inline locator)";
    }
}
