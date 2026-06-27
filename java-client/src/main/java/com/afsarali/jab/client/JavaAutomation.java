package com.afsarali.jab.client;

import com.afsarali.jab.client.model.*;

import java.net.URI;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public final class JavaAutomation {
    private final JabApiClient api;
    private final List<String> repositoryPaths = new ArrayList<>();
    private ResolutionPolicy resolutionPolicy = ResolutionPolicy.strict();

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

    public JavaObject object(String objectKey) {
        return new JavaObject(this, null, objectKey);
    }

    DriverResult run(JavaAction action, String objectKey, String text, JavaWindowSelector window) {
        JavaOneShotActionRequest request = JavaOneShotActionRequest.of(
                action,
                List.copyOf(repositoryPaths),
                objectKey,
                text,
                window,
                resolutionPolicy);
        DriverResult result = api.runOneShot(request);
        JavaDriver.ensureSuccess(result);
        return result;
    }

    DriverResult run(JavaAction action, String objectKey, String text, JavaWindowSelector window, RetryOptions retryOptions) {
        return Wait.call(
                () -> run(action, objectKey, text, window),
                retryOptions,
                "Timed out executing " + action.apiName() + " on Java object '" + objectKey + "'.");
    }
}
