package com.afsarali.jab.client;

import com.afsarali.jab.client.model.*;

import java.net.URI;
import java.time.Duration;
import java.util.Arrays;
import java.util.List;
import java.util.Objects;
import java.util.stream.Collectors;
import java.util.stream.StreamSupport;

public final class JavaDriver implements AutoCloseable {
    private final JabApiClient api;
    private final String sessionId;
    private JavaWindowSelector activeWindow;
    private ResolutionPolicy resolutionPolicy = ResolutionPolicy.strict();

    private JavaDriver(JabApiClient api, String sessionId, JavaWindowSelector activeWindow) {
        this.api = api;
        this.sessionId = sessionId;
        this.activeWindow = activeWindow;
    }

    public static JavaDriver attachByTitle(URI apiBaseUri, String title) {
        return attach(apiBaseUri, CreateSessionRequest.byTitle(title), JavaWindowSelector.title(title));
    }

    public static JavaDriver attachByTitle(URI apiBaseUri, String title, String repositoryPath) {
        JavaDriver driver = attachByTitle(apiBaseUri, title);
        driver.loadRepository(repositoryPath);
        return driver;
    }

    public static JavaDriver attachByHwnd(URI apiBaseUri, String hwnd) {
        return attach(apiBaseUri, CreateSessionRequest.byHwnd(hwnd), JavaWindowSelector.hwnd(hwnd));
    }

    public static JavaDriver attachByProcessId(URI apiBaseUri, int processId) {
        return attach(apiBaseUri, CreateSessionRequest.byProcessId(processId), JavaWindowSelector.processId(processId));
    }

    public static JavaDriver attach(URI apiBaseUri, CreateSessionRequest request, JavaWindowSelector initialWindow) {
        JabApiClient api = new JabApiClient(apiBaseUri);
        DriverResult result = api.createSession(request);
        ensureSuccess(result);
        return new JavaDriver(api, Objects.requireNonNull(result.sessionId(), "API did not return a session id."), initialWindow);
    }

    public String sessionId() {
        return sessionId;
    }

    public List<JavaWindow> windows() {
        return api.windows();
    }

    public JavaDriver refresh() {
        ensureSuccess(api.refreshSession(sessionId));
        return this;
    }

    public JavaDriver loadRepository(String repositoryPath) {
        ensureSuccess(api.loadRepository(sessionId, repositoryPath));
        return this;
    }

    public JavaDriver loadRepositories(String... repositoryPaths) {
        return loadRepositories(Arrays.asList(repositoryPaths));
    }

    public JavaDriver loadRepositories(List<String> repositoryPaths) {
        ensureSuccess(api.loadRepositories(sessionId, repositoryPaths));
        return this;
    }

    public JavaDriver resolutionPolicy(ResolutionPolicy resolutionPolicy) {
        this.resolutionPolicy = resolutionPolicy;
        return this;
    }

    public JavaDriver switchTo(JavaWindowSelector selector) {
        ensureSuccess(api.switchWindow(sessionId, SwitchWindowRequest.from(selector)));
        activeWindow = selector;
        return this;
    }

    public JavaDriver window(JavaWindowSelector selector) {
        return switchTo(selector);
    }

    public JavaElement element(String objectKey) {
        return new JavaElement(this, objectKey, activeWindow);
    }

    public JavaElement element(LocatorSuggestion locator) {
        return new JavaElement(this, locator, activeWindow);
    }

    public JavaElement locator(LocatorSuggestion locator) {
        return element(locator);
    }

    public boolean exists(String objectKey) {
        return validate(objectKey).exists();
    }

    public boolean exists(LocatorSuggestion locator) {
        return validate(locator).exists();
    }

    public boolean isVisible(String objectKey) {
        return validate(objectKey).isVisible();
    }

    public boolean isVisible(LocatorSuggestion locator) {
        return validate(locator).isVisible();
    }

    public boolean isEnabled(String objectKey) {
        return validate(objectKey).isEnabled();
    }

    public boolean isEnabled(LocatorSuggestion locator) {
        return validate(locator).isEnabled();
    }

    public boolean hasText(String objectKey, String expectedText) {
        return validate(objectKey, expectedText).textMatches();
    }

    public boolean hasText(LocatorSuggestion locator, String expectedText) {
        return validate(locator, expectedText).textMatches();
    }

    public JavaValidation validate(String objectKey) {
        return validate(objectKey, null, activeWindow);
    }

    public JavaValidation validate(LocatorSuggestion locator) {
        return validate(locator, null, activeWindow);
    }

    public JavaValidation validate(String objectKey, String expectedText) {
        return validate(objectKey, expectedText, activeWindow);
    }

    public JavaValidation validate(LocatorSuggestion locator, String expectedText) {
        return validate(locator, expectedText, activeWindow);
    }

    public JavaValidation validate(String objectKey, String expectedText, JavaWindowSelector window) {
        return validate(objectKey, null, expectedText, window);
    }

    public JavaValidation validate(LocatorSuggestion locator, String expectedText, JavaWindowSelector window) {
        return validate(null, locator, expectedText, window);
    }

    JavaValidation validate(String objectKey, LocatorSuggestion locator, String expectedText, JavaWindowSelector window) {
        JavaValidationRequest request = locator == null
                ? JavaValidationRequest.session(objectKey, expectedText, window, resolutionPolicy)
                : JavaValidationRequest.session(locator, expectedText, window, resolutionPolicy);
        DriverResult result = api.validateElement(sessionId, request);
        ensureSuccess(result);
        return JavaValidation.from(result.data());
    }

    public List<JavaElementSnapshot> findElements(String objectKey) {
        return findElements(objectKey, null, null);
    }

    public List<JavaElementSnapshot> findElements(LocatorSuggestion locator) {
        return findElements(locator, null, null);
    }

    public List<JavaElementSnapshot> findElements(String objectKey, Integer minimumScore, Integer maxResults) {
        return findElements(objectKey, minimumScore, maxResults, activeWindow);
    }

    public List<JavaElementSnapshot> findElements(LocatorSuggestion locator, Integer minimumScore, Integer maxResults) {
        return findElements(locator, minimumScore, maxResults, activeWindow);
    }

    public List<JavaElementSnapshot> findElements(String objectKey, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
        JavaFindElementsRequest request = JavaFindElementsRequest.session(objectKey, null, window, resolutionPolicy, minimumScore, maxResults);
        DriverResult result = api.findElements(sessionId, request);
        ensureSuccess(result);
        return snapshots(result);
    }

    public List<JavaElementSnapshot> findElements(LocatorSuggestion locator, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
        JavaFindElementsRequest request = JavaFindElementsRequest.session(null, locator, window, resolutionPolicy, minimumScore, maxResults);
        DriverResult result = api.findElements(sessionId, request);
        ensureSuccess(result);
        return snapshots(result);
    }

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey) {
        return findChildElements(parentObjectKey, null, null, false);
    }

    public List<JavaElementSnapshot> findChildElements(LocatorSuggestion parentLocator) {
        return findChildElements(parentLocator, null, null, false);
    }

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return findChildElements(parentObjectKey, null, maxDepth, maxResults, includeSelf);
    }

    public List<JavaElementSnapshot> findChildElements(LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return findChildElements(null, parentLocator, maxDepth, maxResults, includeSelf);
    }

    List<JavaElementSnapshot> findChildElements(String parentObjectKey, LocatorSuggestion parentLocator) {
        return findChildElements(parentObjectKey, parentLocator, null, null, false);
    }

    List<JavaElementSnapshot> findChildElements(String parentObjectKey, LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        JavaFindChildElementsRequest request = JavaFindChildElementsRequest.session(parentObjectKey, parentLocator, activeWindow, resolutionPolicy, includeSelf, maxDepth, maxResults);
        DriverResult result = api.findChildElements(sessionId, request);
        ensureSuccess(result);
        return snapshots(result);
    }

    public JavaDriver waitUntilExists(String objectKey) {
        return waitUntilExists(objectKey, RetryOptions.defaults());
    }

    public JavaDriver waitUntilExists(String objectKey, Duration timeout, Duration pollInterval) {
        return waitUntilExists(objectKey, RetryOptions.of(timeout, pollInterval));
    }

    public JavaDriver waitUntilExists(String objectKey, RetryOptions options) {
        Wait.until(() -> exists(objectKey), options, "Timed out waiting for Java object '" + objectKey + "'.");
        return this;
    }

    public JavaDriver waitUntilExists(LocatorSuggestion locator) {
        return waitUntilExists(locator, RetryOptions.defaults());
    }

    public JavaDriver waitUntilExists(LocatorSuggestion locator, Duration timeout, Duration pollInterval) {
        return waitUntilExists(locator, RetryOptions.of(timeout, pollInterval));
    }

    public JavaDriver waitUntilExists(LocatorSuggestion locator, RetryOptions options) {
        Wait.until(() -> exists(locator), options, "Timed out waiting for Java inline locator.");
        return this;
    }

    DriverResult resolve(String objectKey, JavaWindowSelector window) {
        ResolveElementRequest request = new ResolveElementRequest(objectKey, null, window, resolutionPolicy, true, false);
        DriverResult result = api.resolveElement(sessionId, request);
        ensureSuccess(result);
        return result;
    }

    DriverResult resolve(LocatorSuggestion locator, JavaWindowSelector window) {
        ResolveElementRequest request = new ResolveElementRequest(null, locator, window, resolutionPolicy, true, false);
        DriverResult result = api.resolveElement(sessionId, request);
        ensureSuccess(result);
        return result;
    }

    DriverResult execute(JavaAction action, String objectKey, String text, JavaWindowSelector window) {
        return execute(action, objectKey, null, text, window);
    }

    DriverResult execute(JavaAction action, String objectKey, LocatorSuggestion locator, String text, JavaWindowSelector window) {
        JavaActionRequest request = locator == null
                ? JavaActionRequest.of(action, objectKey, text, window, resolutionPolicy)
                : JavaActionRequest.of(action, locator, text, window, resolutionPolicy);
        DriverResult result = api.executeAction(sessionId, request);
        ensureSuccess(result);
        return result;
    }

    DriverResult execute(JavaAction action, String objectKey, String text, JavaWindowSelector window, RetryOptions retryOptions) {
        return execute(action, objectKey, null, text, window, retryOptions);
    }

    DriverResult execute(JavaAction action, String objectKey, LocatorSuggestion locator, String text, JavaWindowSelector window, RetryOptions retryOptions) {
        String label = objectKey != null && !objectKey.isBlank() ? objectKey : "inline locator";
        return Wait.call(
                () -> execute(action, objectKey, locator, text, window),
                retryOptions,
                "Timed out executing " + action.apiName() + " on Java element '" + label + "'.");
    }

    static List<JavaElementSnapshot> snapshots(DriverResult result) {
        if (result.data() == null || !result.data().isArray()) return List.of();
        return StreamSupport.stream(result.data().spliterator(), false)
                .map(JavaElementSnapshot::from)
                .collect(Collectors.toList());
    }

    static void ensureSuccess(DriverResult result) {
        if (result == null) throw new ApiException(0, "API returned an empty response.");
        if (!result.success()) throw new ApiException(400, result.message());
    }

    @Override
    public void close() {
        api.deleteSession(sessionId);
    }
}
