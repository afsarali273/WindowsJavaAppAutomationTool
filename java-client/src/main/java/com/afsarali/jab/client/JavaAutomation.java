package com.afsarali.jab.client;

import com.afsarali.jab.client.model.*;

import java.net.URI;
import java.time.Duration;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public final class JavaAutomation {
    private static final int DEFAULT_FIND_MINIMUM_SCORE = 70;
    private static final int DEFAULT_FIND_MAX_RESULTS = 20;
    private static final int DEFAULT_CHILD_MAX_DEPTH = 10;
    private static final int DEFAULT_CHILD_MAX_RESULTS = 200;
    private static final boolean DEFAULT_CHILD_INCLUDE_SELF = false;

    private final JabApiClient api;
    private final List<String> repositoryPaths = new ArrayList<>();
    private ResolutionPolicy resolutionPolicy = ResolutionPolicy.standard();

    private JavaAutomation(JabApiClient api) {
        this.api = api;
    }

    public static JavaAutomation connect(URI apiBaseUri) {
        return new JavaAutomation(new JabApiClient(apiBaseUri));
    }

    public JavaAutomation repository(String repositoryPath) {
        this.repositoryPaths.clear();
        if (repositoryPath != null && !repositoryPath.isBlank()) this.repositoryPaths.add(repositoryPath);
        return this;
    }

    public JavaAutomation repositories(String... repositoryPaths) {
        return repositories(Arrays.asList(repositoryPaths));
    }

    public JavaAutomation repositories(List<String> repositoryPaths) {
        this.repositoryPaths.clear();
        if (repositoryPaths != null) {
            repositoryPaths.stream()
                    .filter(path -> path != null && !path.isBlank())
                    .forEach(this.repositoryPaths::add);
        }
        return this;
    }

    public JavaAutomation addRepository(String repositoryPath) {
        if (repositoryPath != null && !repositoryPath.isBlank()) this.repositoryPaths.add(repositoryPath);
        return this;
    }

    public JavaAutomation resolutionPolicy(ResolutionPolicy resolutionPolicy) {
        this.resolutionPolicy = resolutionPolicy;
        return this;
    }

    public JavaWindowScope window(JavaWindowSelector selector) {
        return new JavaWindowScope(this, selector);
    }

    public JavaWindowScope window(JavaWindowSelector selector, RetryOptions waitOptions) {
        JavaWindow window = waitForWindow(selector, waitOptions);
        return new JavaWindowScope(this, JavaWindowWait.selectorFor(window));
    }

    public JavaWindow waitForWindow(JavaWindowSelector selector) {
        return waitForWindow(selector, RetryOptions.defaults());
    }

    public JavaWindow waitForWindow(JavaWindowSelector selector, Duration timeout, Duration pollInterval) {
        return waitForWindow(selector, RetryOptions.of(timeout, pollInterval));
    }

    public JavaWindow waitForWindow(JavaWindowSelector selector, RetryOptions options) {
        return JavaWindowWait.waitForWindow(api, selector, options);
    }

    public JavaLaunchApplicationResult openApplication(JavaLaunchApplicationRequest request) {
        DriverResult result = api.launchApplication(request);
        JavaDriver.ensureSuccess(result);
        return JavaLaunchApplicationResult.from(result.data());
    }

    public JavaLaunchApplicationResult openApplication(String applicationPath) {
        return openApplication(JavaLaunchApplicationRequest.of(applicationPath));
    }

    public JavaLaunchApplicationResult openApplication(String applicationPath, JavaWindowSelector waitForWindow, Duration timeout, Duration pollInterval) {
        return openApplication(JavaLaunchApplicationRequest.of(applicationPath)
                .waitForWindow(waitForWindow)
                .waitTimeout(timeout)
                .waitPollInterval(pollInterval));
    }

    public JavaObject object(String objectKey) {
        return new JavaObject(this, null, objectKey);
    }

    public JavaObject object(LocatorSuggestion locator) {
        return new JavaObject(this, null, locator);
    }

    public JavaObject locator(LocatorSuggestion locator) {
        return object(locator);
    }

    public JavaFormsScope formsScope(JavaWindowSelector window, String scopeName) {
        return new JavaFormsScope(this, window, scopeName, null, null, null);
    }

    public JavaFormsScope internalFrame(JavaWindowSelector window, String frameName) {
        return formsScope(window, frameName);
    }

    public JavaFormsScope formsScopePath(JavaWindowSelector window, String scopePath) {
        return new JavaFormsScope(this, window, null, scopePath, null, null);
    }

    public JavaTable table(String objectKey) {
        return object(objectKey).asTable();
    }

    public JavaTable table(LocatorSuggestion locator) {
        return object(locator).asTable();
    }

    public List<JavaElementHandle> findElements(String objectKey) {
        return findElements(objectKey, DEFAULT_FIND_MINIMUM_SCORE, DEFAULT_FIND_MAX_RESULTS, null);
    }

    public List<JavaElementHandle> findElements(LocatorSuggestion locator) {
        return findElements(locator, DEFAULT_FIND_MINIMUM_SCORE, DEFAULT_FIND_MAX_RESULTS, null);
    }

    public List<JavaElementHandle> findElements(String objectKey, JavaWindowSelector window) {
        return findElements(objectKey, DEFAULT_FIND_MINIMUM_SCORE, DEFAULT_FIND_MAX_RESULTS, window);
    }

    public List<JavaElementHandle> findElements(LocatorSuggestion locator, JavaWindowSelector window) {
        return findElements(locator, DEFAULT_FIND_MINIMUM_SCORE, DEFAULT_FIND_MAX_RESULTS, window);
    }

    public List<JavaElementHandle> findElements(String objectKey, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
        return findElementSnapshots(objectKey, minimumScore, maxResults, window).stream()
                .map(snapshot -> JavaElementHandle.from(this, window, snapshot))
                .collect(java.util.stream.Collectors.toList());
    }

    public List<JavaElementHandle> findElements(LocatorSuggestion locator, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
        return findElementSnapshots(locator, minimumScore, maxResults, window).stream()
                .map(snapshot -> JavaElementHandle.from(this, window, snapshot))
                .collect(java.util.stream.Collectors.toList());
    }

    List<JavaElementSnapshot> findElementSnapshots(String objectKey, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
        JavaFindElementsRequest request = JavaFindElementsRequest.oneShot(
                List.copyOf(repositoryPaths),
                objectKey,
                null,
                window,
                resolutionPolicy,
                minimumScore,
                maxResults);
        DriverResult result = api.findElementsOneShot(request);
        JavaDriver.ensureSuccess(result);
        return JavaDriver.snapshots(result);
    }

    List<JavaElementSnapshot> findElementSnapshots(LocatorSuggestion locator, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
        JavaFindElementsRequest request = JavaFindElementsRequest.oneShot(
                List.copyOf(repositoryPaths),
                null,
                locator,
                window,
                resolutionPolicy,
                minimumScore,
                maxResults);
        DriverResult result = api.findElementsOneShot(request);
        JavaDriver.ensureSuccess(result);
        return JavaDriver.snapshots(result);
    }

    public List<JavaElementHandle> findChildElements(String parentObjectKey) {
        return findChildElements(parentObjectKey, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, DEFAULT_CHILD_INCLUDE_SELF, null);
    }

    public List<JavaElementHandle> findChildElements(LocatorSuggestion parentLocator) {
        return findChildElements(null, parentLocator, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, DEFAULT_CHILD_INCLUDE_SELF, null);
    }

    public List<JavaElementHandle> findChildElements(String parentObjectKey, JavaWindowSelector window) {
        return findChildElements(parentObjectKey, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, DEFAULT_CHILD_INCLUDE_SELF, window);
    }

    public List<JavaElementHandle> findChildElements(LocatorSuggestion parentLocator, JavaWindowSelector window) {
        return findChildElements(null, parentLocator, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, DEFAULT_CHILD_INCLUDE_SELF, window);
    }

    public List<JavaElementHandle> findChildElements(String parentObjectKey, Integer maxDepth, Integer maxResults, boolean includeSelf, JavaWindowSelector window) {
        return findChildElements(parentObjectKey, null, maxDepth, maxResults, includeSelf, window);
    }

    public List<JavaElementHandle> findChildElements(LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf, JavaWindowSelector window) {
        return findChildElements(null, parentLocator, maxDepth, maxResults, includeSelf, window);
    }

    List<JavaElementHandle> findChildElements(String parentObjectKey, LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf, JavaWindowSelector window) {
        return findChildSnapshots(parentObjectKey, parentLocator, maxDepth, maxResults, includeSelf, window).stream()
                .map(snapshot -> JavaElementHandle.from(this, window, snapshot))
                .collect(java.util.stream.Collectors.toList());
    }

    public List<JavaElementHandle> findTableRows(String parentObjectKey, JavaWindowSelector window) {
        return findTableRows(parentObjectKey, window, DEFAULT_CHILD_MAX_RESULTS);
    }

    public List<JavaElementHandle> findTableRows(LocatorSuggestion parentLocator, JavaWindowSelector window) {
        return findTableRows(parentLocator, window, DEFAULT_CHILD_MAX_RESULTS);
    }

    public List<JavaElementHandle> findTableRows(String parentObjectKey, JavaWindowSelector window, Integer maxResults) {
        return synthesizeRows(findChildSnapshots(parentObjectKey, null, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, false, window), window, maxResults);
    }

    public List<JavaElementHandle> findTableRows(LocatorSuggestion parentLocator, JavaWindowSelector window, Integer maxResults) {
        return synthesizeRows(findChildSnapshots(null, parentLocator, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, false, window), window, maxResults);
    }

    public List<JavaElementHandle> findTableCells(String parentObjectKey, JavaWindowSelector window) {
        return findTableCells(parentObjectKey, window, DEFAULT_CHILD_MAX_RESULTS);
    }

    public List<JavaElementHandle> findTableCells(LocatorSuggestion parentLocator, JavaWindowSelector window) {
        return findTableCells(parentLocator, window, DEFAULT_CHILD_MAX_RESULTS);
    }

    public List<JavaElementHandle> findTableCells(String parentObjectKey, JavaWindowSelector window, Integer maxResults) {
        return filterTableNodes(findChildSnapshots(parentObjectKey, null, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, false, window), JavaElementSnapshot::isTableLikeCell, window, maxResults);
    }

    public List<JavaElementHandle> findTableCells(LocatorSuggestion parentLocator, JavaWindowSelector window, Integer maxResults) {
        return filterTableNodes(findChildSnapshots(null, parentLocator, DEFAULT_CHILD_MAX_DEPTH, DEFAULT_CHILD_MAX_RESULTS, false, window), JavaElementSnapshot::isTableLikeCell, window, maxResults);
    }

    public JavaElementHandle findTableCell(String parentObjectKey, int rowIndex, int columnIndex, JavaWindowSelector window) {
        return firstTableMatch(findTableCells(parentObjectKey, window), rowIndex, columnIndex);
    }

    public JavaElementHandle findTableCell(LocatorSuggestion parentLocator, int rowIndex, int columnIndex, JavaWindowSelector window) {
        return firstTableMatch(findTableCells(parentLocator, window), rowIndex, columnIndex);
    }

    public JavaElementHandle findTableCell(String parentObjectKey, int rowIndex, String columnHeader, JavaWindowSelector window) {
        return firstTableMatch(findTableCells(parentObjectKey, window), rowIndex, columnHeader);
    }

    public JavaElementHandle findTableCell(LocatorSuggestion parentLocator, int rowIndex, String columnHeader, JavaWindowSelector window) {
        return firstTableMatch(findTableCells(parentLocator, window), rowIndex, columnHeader);
    }

    public String getTableCellText(String parentObjectKey, int rowIndex, int columnIndex, JavaWindowSelector window) {
        return findTableCell(parentObjectKey, rowIndex, columnIndex, window).getText();
    }

    public String getTableCellText(LocatorSuggestion parentLocator, int rowIndex, int columnIndex, JavaWindowSelector window) {
        return findTableCell(parentLocator, rowIndex, columnIndex, window).getText();
    }

    public String getTableCellText(String parentObjectKey, int rowIndex, String columnHeader, JavaWindowSelector window) {
        return findTableCell(parentObjectKey, rowIndex, columnHeader, window).getText();
    }

    public String getTableCellText(LocatorSuggestion parentLocator, int rowIndex, String columnHeader, JavaWindowSelector window) {
        return findTableCell(parentLocator, rowIndex, columnHeader, window).getText();
    }

    List<JavaElementSnapshot> findChildSnapshots(String parentObjectKey, LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf, JavaWindowSelector window) {
        JavaFindChildElementsRequest request = JavaFindChildElementsRequest.oneShot(
                List.copyOf(repositoryPaths),
                parentObjectKey,
                parentLocator,
                window,
                resolutionPolicy,
                includeSelf,
                maxDepth,
                maxResults);
        DriverResult result = api.findChildElementsOneShot(request);
        JavaDriver.ensureSuccess(result);
        return JavaDriver.snapshots(result);
    }

    private List<JavaElementHandle> filterTableNodes(List<JavaElementSnapshot> snapshots, java.util.function.Predicate<JavaElementSnapshot> predicate, JavaWindowSelector window, Integer maxResults) {
        if (snapshots == null || snapshots.isEmpty()) return List.of();
        return snapshots.stream()
                .filter(predicate)
                .limit(maxResults == null || maxResults < 1 ? Long.MAX_VALUE : maxResults)
                .map(snapshot -> JavaElementHandle.from(this, window, snapshot))
                .collect(java.util.stream.Collectors.toList());
    }

    private List<JavaElementHandle> synthesizeRows(List<JavaElementSnapshot> snapshots, JavaWindowSelector window, Integer maxResults) {
        if (snapshots == null || snapshots.isEmpty()) return List.of();
        List<JavaElementHandle> explicitRows = filterTableNodes(snapshots, JavaElementSnapshot::isTableLikeRow, window, maxResults);
        if (!explicitRows.isEmpty()) return explicitRows;
        return snapshots.stream()
                .filter(JavaElementSnapshot::isTableLikeCell)
                .filter(snapshot -> snapshot.tableLikeRowIndex() >= 0)
                .collect(java.util.stream.Collectors.groupingBy(JavaElementSnapshot::tableLikeRowIndex))
                .entrySet()
                .stream()
                .sorted(java.util.Map.Entry.comparingByKey())
                .limit(maxResults == null || maxResults < 1 ? Long.MAX_VALUE : maxResults)
                .map(entry -> entry.getValue().stream()
                        .sorted(java.util.Comparator.comparingInt(JavaElementSnapshot::tableLikeColumnIndex))
                        .findFirst()
                        .map(snapshot -> JavaElementHandle.from(this, window, snapshot))
                        .orElse(null))
                .filter(java.util.Objects::nonNull)
                .collect(java.util.stream.Collectors.toList());
    }

    private JavaElementHandle firstTableMatch(List<JavaElementHandle> matches, int rowIndex, int columnIndex) {
        return matches.stream()
                .filter(handle -> handle.snapshot().tableLikeRowIndex() == rowIndex && handle.snapshot().tableLikeColumnIndex() == columnIndex)
                .findFirst()
                .orElseThrow(() -> new ApiException(404, "Could not find table cell at row " + rowIndex + ", column " + columnIndex + "."));
    }

    private JavaElementHandle firstTableMatch(List<JavaElementHandle> matches, int rowIndex, String columnHeader) {
        String normalizedHeader = columnHeader == null ? "" : columnHeader.trim();
        return matches.stream()
                .filter(handle -> handle.snapshot().tableLikeRowIndex() == rowIndex)
                .filter(handle -> handle.snapshot().tableLikeColumnHeader() != null && handle.snapshot().tableLikeColumnHeader().equalsIgnoreCase(normalizedHeader))
                .findFirst()
                .orElseThrow(() -> new ApiException(404, "Could not find table cell at row " + rowIndex + ", header '" + normalizedHeader + "'."));
    }

    DriverResult run(JavaAction action, String objectKey, String text, JavaWindowSelector window) {
        return run(action, objectKey, null, text, window);
    }

    DriverResult run(JavaAction action, String objectKey, LocatorSuggestion locator, String text, JavaWindowSelector window) {
        JavaOneShotActionRequest request = objectKey == null && locator == null
                ? JavaOneShotActionRequest.ofWindow(
                    action,
                    List.copyOf(repositoryPaths),
                    window,
                    resolutionPolicy)
                : JavaOneShotActionRequest.of(
                action,
                List.copyOf(repositoryPaths),
                objectKey,
                text,
                window,
                resolutionPolicy);
        if (locator != null) {
            request = JavaOneShotActionRequest.of(
                    action,
                    List.copyOf(repositoryPaths),
                    locator,
                    text,
                    window,
                    resolutionPolicy);
        }
        DriverResult result = api.runOneShot(request);
        JavaDriver.ensureSuccess(result);
        return result;
    }

    JavaValidation validate(String objectKey, String expectedText, JavaWindowSelector window) {
        return validate(objectKey, null, expectedText, window);
    }

    JavaValidation validate(String objectKey, LocatorSuggestion locator, String expectedText, JavaWindowSelector window) {
        JavaValidationRequest request = JavaValidationRequest.oneShot(
                List.copyOf(repositoryPaths),
                objectKey,
                expectedText,
                window,
                resolutionPolicy);
        if (locator != null) {
            request = JavaValidationRequest.oneShot(
                    List.copyOf(repositoryPaths),
                    locator,
                    expectedText,
                    window,
                    resolutionPolicy);
        }
        DriverResult result = api.validateOneShot(request);
        JavaDriver.ensureSuccess(result);
        return JavaValidation.from(result.data());
    }

    DriverResult run(JavaAction action, String objectKey, String text, JavaWindowSelector window, RetryOptions retryOptions) {
        return run(action, objectKey, null, text, window, retryOptions);
    }

    DriverResult run(JavaAction action, String objectKey, LocatorSuggestion locator, String text, JavaWindowSelector window, RetryOptions retryOptions) {
        String label = objectKey != null && !objectKey.isBlank() ? objectKey : "inline locator";
        return Wait.call(
                () -> run(action, objectKey, locator, text, window),
                retryOptions,
                "Timed out executing " + action.apiName() + " on Java element '" + label + "'.");
    }
}
