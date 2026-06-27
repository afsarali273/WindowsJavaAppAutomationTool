package com.afsarali.jab.client;

import com.afsarali.jab.client.model.*;

import java.net.URI;
import java.time.Duration;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public final class JavaAutomation {
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

    public JavaObject object(String objectKey) {
        return new JavaObject(this, null, objectKey);
    }

    public JavaObject object(LocatorSuggestion locator) {
        return new JavaObject(this, null, locator);
    }

    public JavaObject locator(LocatorSuggestion locator) {
        return object(locator);
    }

    public List<JavaElementSnapshot> findElements(String objectKey) {
        return findElements(objectKey, null, null, null);
    }

    public List<JavaElementSnapshot> findElements(LocatorSuggestion locator) {
        return findElements(locator, null, null, null);
    }

    public List<JavaElementSnapshot> findElements(String objectKey, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
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

    public List<JavaElementSnapshot> findElements(LocatorSuggestion locator, Integer minimumScore, Integer maxResults, JavaWindowSelector window) {
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

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey) {
        return findChildElements(parentObjectKey, null, null, false, null);
    }

    public List<JavaElementSnapshot> findChildElements(LocatorSuggestion parentLocator) {
        return findChildElements(null, parentLocator, null, null, false, null);
    }

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey, Integer maxDepth, Integer maxResults, boolean includeSelf, JavaWindowSelector window) {
        return findChildElements(parentObjectKey, null, maxDepth, maxResults, includeSelf, window);
    }

    public List<JavaElementSnapshot> findChildElements(LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf, JavaWindowSelector window) {
        return findChildElements(null, parentLocator, maxDepth, maxResults, includeSelf, window);
    }

    List<JavaElementSnapshot> findChildElements(String parentObjectKey, LocatorSuggestion parentLocator, Integer maxDepth, Integer maxResults, boolean includeSelf, JavaWindowSelector window) {
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

    DriverResult run(JavaAction action, String objectKey, String text, JavaWindowSelector window) {
        return run(action, objectKey, null, text, window);
    }

    DriverResult run(JavaAction action, String objectKey, LocatorSuggestion locator, String text, JavaWindowSelector window) {
        JavaOneShotActionRequest request = JavaOneShotActionRequest.of(
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
