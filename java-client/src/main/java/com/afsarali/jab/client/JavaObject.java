package com.afsarali.jab.client;

import com.afsarali.jab.client.model.DriverResult;
import com.afsarali.jab.client.model.JavaAction;
import com.afsarali.jab.client.model.JavaWindowSelector;

import java.time.Duration;
import java.util.List;

public final class JavaObject {
    private final JavaAutomation automation;
    private final JavaWindowSelector window;
    private final String objectKey;

    JavaObject(JavaAutomation automation, JavaWindowSelector window, String objectKey) {
        this.automation = automation;
        this.window = window;
        this.objectKey = objectKey;
    }

    public JavaObject focus() {
        automation.run(JavaAction.FOCUS, objectKey, "", window);
        return this;
    }

    public JavaObject focus(RetryOptions retryOptions) {
        automation.run(JavaAction.FOCUS, objectKey, "", window, retryOptions);
        return this;
    }

    public JavaObject click() {
        automation.run(JavaAction.CLICK, objectKey, "", window);
        return this;
    }

    public JavaObject click(RetryOptions retryOptions) {
        automation.run(JavaAction.CLICK, objectKey, "", window, retryOptions);
        return this;
    }

    public JavaObject doubleClick() {
        automation.run(JavaAction.DOUBLE_CLICK, objectKey, "", window);
        return this;
    }

    public JavaObject doubleClick(RetryOptions retryOptions) {
        automation.run(JavaAction.DOUBLE_CLICK, objectKey, "", window, retryOptions);
        return this;
    }

    public JavaObject setText(String text) {
        automation.run(JavaAction.SET_TEXT, objectKey, text, window);
        return this;
    }

    public JavaObject setText(String text, RetryOptions retryOptions) {
        automation.run(JavaAction.SET_TEXT, objectKey, text, window, retryOptions);
        return this;
    }

    public JavaObject typeText(String text) {
        automation.run(JavaAction.TYPE_TEXT, objectKey, text, window);
        return this;
    }

    public JavaObject typeText(String text, RetryOptions retryOptions) {
        automation.run(JavaAction.TYPE_TEXT, objectKey, text, window, retryOptions);
        return this;
    }

    public String getText() {
        DriverResult result = automation.run(JavaAction.GET_TEXT, objectKey, "", window);
        return result.text();
    }

    public String getText(RetryOptions retryOptions) {
        DriverResult result = automation.run(JavaAction.GET_TEXT, objectKey, "", window, retryOptions);
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
        return automation.validate(objectKey, null, window);
    }

    public JavaValidation validate(String expectedText) {
        return automation.validate(objectKey, expectedText, window);
    }

    public JavaObject waitUntilExists() {
        return waitUntilExists(RetryOptions.defaults());
    }

    public JavaObject waitUntilExists(Duration timeout, Duration pollInterval) {
        return waitUntilExists(RetryOptions.of(timeout, pollInterval));
    }

    public JavaObject waitUntilExists(RetryOptions options) {
        Wait.until(this::exists, options, "Timed out waiting for Java object '" + objectKey + "'.");
        return this;
    }

    public List<JavaElementSnapshot> findChildElements() {
        return automation.findChildElements(objectKey, null, null, false, window);
    }

    public List<JavaElementSnapshot> findChildElements(Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return automation.findChildElements(objectKey, maxDepth, maxResults, includeSelf, window);
    }
}
