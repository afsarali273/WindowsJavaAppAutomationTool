package com.afsarali.jab.client;

import com.afsarali.jab.client.model.*;

import java.net.URI;

public final class JavaAutomation {
    private final JabApiClient api;
    private String repositoryPath;
    private ResolutionPolicy resolutionPolicy = ResolutionPolicy.strict();

    private JavaAutomation(JabApiClient api) {
        this.api = api;
    }

    public static JavaAutomation connect(URI apiBaseUri) {
        return new JavaAutomation(new JabApiClient(apiBaseUri));
    }

    public JavaAutomation repository(String repositoryPath) {
        this.repositoryPath = repositoryPath;
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
                repositoryPath,
                objectKey,
                text,
                window,
                resolutionPolicy);
        DriverResult result = api.runOneShot(request);
        JavaDriver.ensureSuccess(result);
        return result;
    }
}
