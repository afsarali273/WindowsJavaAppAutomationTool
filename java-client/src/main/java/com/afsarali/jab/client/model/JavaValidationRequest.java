package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class JavaValidationRequest {
    @JsonProperty private final String repositoryPath;
    @JsonProperty private final List<String> repositoryPaths;
    @JsonProperty private final String objectKey;
    @JsonProperty private final LocatorSuggestion locator;
    @JsonProperty private final String expectedText;
    @JsonProperty private final JavaWindowSelector window;
    @JsonProperty private final ResolutionPolicy resolutionPolicy;
    @JsonProperty private final boolean autoSwitchWindow;
    @JsonProperty private final boolean refreshTree;

    @JsonCreator
    public JavaValidationRequest(
            @JsonProperty("repositoryPath") String repositoryPath,
            @JsonProperty("repositoryPaths") List<String> repositoryPaths,
            @JsonProperty("objectKey") String objectKey,
            @JsonProperty("locator") LocatorSuggestion locator,
            @JsonProperty("expectedText") String expectedText,
            @JsonProperty("window") JavaWindowSelector window,
            @JsonProperty("resolutionPolicy") ResolutionPolicy resolutionPolicy,
            @JsonProperty("autoSwitchWindow") boolean autoSwitchWindow,
            @JsonProperty("refreshTree") boolean refreshTree) {
        this.repositoryPath = repositoryPath;
        this.repositoryPaths = repositoryPaths;
        this.objectKey = objectKey;
        this.locator = locator;
        this.expectedText = expectedText;
        this.window = window;
        this.resolutionPolicy = resolutionPolicy;
        this.autoSwitchWindow = autoSwitchWindow;
        this.refreshTree = refreshTree;
    }

    public static JavaValidationRequest session(
            String objectKey,
            String expectedText,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaValidationRequest(null, null, objectKey, null, expectedText, window, policy, true, false);
    }

    public static JavaValidationRequest session(
            LocatorSuggestion locator,
            String expectedText,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaValidationRequest(null, null, null, locator, expectedText, window, policy, true, false);
    }

    public static JavaValidationRequest oneShot(
            List<String> repositoryPaths,
            String objectKey,
            String expectedText,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaValidationRequest(null, repositoryPaths, objectKey, null, expectedText, window, policy, true, true);
    }

    public static JavaValidationRequest oneShot(
            List<String> repositoryPaths,
            LocatorSuggestion locator,
            String expectedText,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaValidationRequest(null, repositoryPaths, null, locator, expectedText, window, policy, true, true);
    }
}
