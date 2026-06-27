package com.afsarali.jab.client;

import com.afsarali.jab.client.model.DriverResult;
import com.afsarali.jab.client.model.JavaAction;
import com.afsarali.jab.client.model.JavaWindowSelector;

import java.time.Duration;
import java.util.List;

public final class JavaElement {
    private final JavaDriver driver;
    private final String objectKey;
    private final JavaWindowSelector window;

    JavaElement(JavaDriver driver, String objectKey, JavaWindowSelector window) {
        this.driver = driver;
        this.objectKey = objectKey;
        this.window = window;
    }

    public JavaElement focus() {
        driver.execute(JavaAction.FOCUS, objectKey, "", window);
        return this;
    }

    public JavaElement focus(RetryOptions retryOptions) {
        driver.execute(JavaAction.FOCUS, objectKey, "", window, retryOptions);
        return this;
    }

    public JavaElement click() {
        driver.execute(JavaAction.CLICK, objectKey, "", window);
        return this;
    }

    public JavaElement click(RetryOptions retryOptions) {
        driver.execute(JavaAction.CLICK, objectKey, "", window, retryOptions);
        return this;
    }

    public JavaElement doubleClick() {
        driver.execute(JavaAction.DOUBLE_CLICK, objectKey, "", window);
        return this;
    }

    public JavaElement doubleClick(RetryOptions retryOptions) {
        driver.execute(JavaAction.DOUBLE_CLICK, objectKey, "", window, retryOptions);
        return this;
    }

    public JavaElement setText(String text) {
        driver.execute(JavaAction.SET_TEXT, objectKey, text, window);
        return this;
    }

    public JavaElement setText(String text, RetryOptions retryOptions) {
        driver.execute(JavaAction.SET_TEXT, objectKey, text, window, retryOptions);
        return this;
    }

    public JavaElement typeText(String text) {
        driver.execute(JavaAction.TYPE_TEXT, objectKey, text, window);
        return this;
    }

    public JavaElement typeText(String text, RetryOptions retryOptions) {
        driver.execute(JavaAction.TYPE_TEXT, objectKey, text, window, retryOptions);
        return this;
    }

    public String getText() {
        DriverResult result = driver.execute(JavaAction.GET_TEXT, objectKey, "", window);
        return result.text();
    }

    public String getText(RetryOptions retryOptions) {
        DriverResult result = driver.execute(JavaAction.GET_TEXT, objectKey, "", window, retryOptions);
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
        return driver.validate(objectKey, null, window);
    }

    public JavaValidation validate(String expectedText) {
        return driver.validate(objectKey, expectedText, window);
    }

    public JavaElement waitUntilExists() {
        return waitUntilExists(RetryOptions.defaults());
    }

    public JavaElement waitUntilExists(Duration timeout, Duration pollInterval) {
        return waitUntilExists(RetryOptions.of(timeout, pollInterval));
    }

    public JavaElement waitUntilExists(RetryOptions options) {
        Wait.until(this::exists, options, "Timed out waiting for Java object '" + objectKey + "'.");
        return this;
    }

    public List<JavaElementSnapshot> findChildElements() {
        return driver.findChildElements(objectKey);
    }

    public List<JavaElementSnapshot> findChildElements(Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return driver.findChildElements(objectKey, maxDepth, maxResults, includeSelf);
    }
}
