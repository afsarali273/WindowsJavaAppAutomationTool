package com.afsarali.jab.client;

import com.afsarali.jab.client.model.*;

import java.net.URI;
import java.time.Duration;
import java.util.Arrays;
import java.util.List;
import java.util.Objects;

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

    public boolean exists(String objectKey) {
        try {
            resolve(objectKey, activeWindow);
            return true;
        } catch (ApiException ex) {
            return false;
        }
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

    DriverResult resolve(String objectKey, JavaWindowSelector window) {
        ResolveElementRequest request = new ResolveElementRequest(objectKey, null, window, resolutionPolicy, true, false);
        DriverResult result = api.resolveElement(sessionId, request);
        ensureSuccess(result);
        return result;
    }

    DriverResult execute(JavaAction action, String objectKey, String text, JavaWindowSelector window) {
        JavaActionRequest request = JavaActionRequest.of(action, objectKey, text, window, resolutionPolicy);
        DriverResult result = api.executeAction(sessionId, request);
        ensureSuccess(result);
        return result;
    }

    DriverResult execute(JavaAction action, String objectKey, String text, JavaWindowSelector window, RetryOptions retryOptions) {
        return Wait.call(
                () -> execute(action, objectKey, text, window),
                retryOptions,
                "Timed out executing " + action.apiName() + " on Java object '" + objectKey + "'.");
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
