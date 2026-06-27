package com.afsarali.jab.client;

import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.LocatorSuggestion;
import java.util.List;

public final class JavaWindowScope {
    private final JavaAutomation automation;
    private final JavaWindowSelector selector;

    JavaWindowScope(JavaAutomation automation, JavaWindowSelector selector) {
        this.automation = automation;
        this.selector = selector;
    }

    public JavaObject object(String objectKey) {
        return new JavaObject(automation, selector, objectKey);
    }

    public JavaWindowScope waitUntilVisible() {
        automation.waitForWindow(selector);
        return this;
    }

    public JavaWindowScope waitUntilVisible(RetryOptions waitOptions) {
        automation.waitForWindow(selector, waitOptions);
        return this;
    }

    public JavaObject object(LocatorSuggestion locator) {
        return new JavaObject(automation, selector, locator);
    }

    public JavaObject locator(LocatorSuggestion locator) {
        return object(locator);
    }

    public List<JavaElementSnapshot> findElements(String objectKey) {
        return automation.findElements(objectKey, null, null, selector);
    }

    public List<JavaElementSnapshot> findElements(LocatorSuggestion locator) {
        return automation.findElements(locator, null, null, selector);
    }

    public List<JavaElementSnapshot> findElements(String objectKey, Integer minimumScore, Integer maxResults) {
        return automation.findElements(objectKey, minimumScore, maxResults, selector);
    }

    public List<JavaElementSnapshot> findElements(LocatorSuggestion locator, Integer minimumScore, Integer maxResults) {
        return automation.findElements(locator, minimumScore, maxResults, selector);
    }

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey) {
        return automation.findChildElements(parentObjectKey, null, null, false, selector);
    }

    public List<JavaElementSnapshot> findChildElements(LocatorSuggestion parentLocator) {
        return automation.findChildElements(parentLocator, null, null, false, selector);
    }

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return automation.findChildElements(parentObjectKey, maxDepth, maxResults, includeSelf, selector);
    }

    public List<JavaElementSnapshot> findChildElements(LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return automation.findChildElements(parentLocator, maxDepth, maxResults, includeSelf, selector);
    }
}
