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

    public JavaWindowScope closeWindow() {
        automation.run(com.afsarali.jab.client.model.JavaAction.CLOSE_WINDOW, null, null, "", selector);
        return this;
    }

    public JavaWindowScope closeWindow(RetryOptions retryOptions) {
        automation.run(com.afsarali.jab.client.model.JavaAction.CLOSE_WINDOW, null, null, "", selector, retryOptions);
        return this;
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

    public List<JavaElementHandle> findElements(String objectKey) {
        return automation.findElements(objectKey, selector);
    }

    public List<JavaElementHandle> findElements(LocatorSuggestion locator) {
        return automation.findElements(locator, selector);
    }

    public List<JavaElementHandle> findElements(String objectKey, Integer minimumScore, Integer maxResults) {
        return automation.findElements(objectKey, minimumScore, maxResults, selector);
    }

    public List<JavaElementHandle> findElements(LocatorSuggestion locator, Integer minimumScore, Integer maxResults) {
        return automation.findElements(locator, minimumScore, maxResults, selector);
    }

    public List<JavaElementHandle> findChildElements(String parentObjectKey) {
        return automation.findChildElements(parentObjectKey, selector);
    }

    public List<JavaElementHandle> findChildElements(LocatorSuggestion parentLocator) {
        return automation.findChildElements(parentLocator, selector);
    }

    public List<JavaElementHandle> findChildElements(String parentObjectKey, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return automation.findChildElements(parentObjectKey, maxDepth, maxResults, includeSelf, selector);
    }

    public List<JavaElementHandle> findChildElements(LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return automation.findChildElements(parentLocator, maxDepth, maxResults, includeSelf, selector);
    }

    public List<JavaElementHandle> findTableRows(String parentObjectKey) {
        return automation.findTableRows(parentObjectKey, selector);
    }

    public List<JavaElementHandle> findTableRows(LocatorSuggestion parentLocator) {
        return automation.findTableRows(parentLocator, selector);
    }

    public List<JavaElementHandle> findTableCells(String parentObjectKey) {
        return automation.findTableCells(parentObjectKey, selector);
    }

    public List<JavaElementHandle> findTableCells(LocatorSuggestion parentLocator) {
        return automation.findTableCells(parentLocator, selector);
    }

    public JavaElementHandle findTableCell(String parentObjectKey, int rowIndex, int columnIndex) {
        return automation.findTableCell(parentObjectKey, rowIndex, columnIndex, selector);
    }

    public JavaElementHandle findTableCell(LocatorSuggestion parentLocator, int rowIndex, int columnIndex) {
        return automation.findTableCell(parentLocator, rowIndex, columnIndex, selector);
    }

    public String getTableCellText(String parentObjectKey, int rowIndex, int columnIndex) {
        return automation.getTableCellText(parentObjectKey, rowIndex, columnIndex, selector);
    }

    public String getTableCellText(LocatorSuggestion parentLocator, int rowIndex, int columnIndex) {
        return automation.getTableCellText(parentLocator, rowIndex, columnIndex, selector);
    }
}
