package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.Collections;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class JavaOneShotActionRequest {
    @JsonProperty private final String action;
    @JsonProperty private final String repositoryPath;
    @JsonProperty private final List<String> repositoryPaths;
    @JsonProperty private final String objectKey;
    @JsonProperty private final LocatorSuggestion locator;
    @JsonProperty private final String text;
    @JsonProperty private final JavaWindowSelector window;
    @JsonProperty private final ResolutionPolicy resolutionPolicy;
    @JsonProperty private final boolean autoSwitchWindow;
    @JsonProperty private final boolean refreshTree;
    @JsonProperty private final boolean preferAccessibleAction;
    @JsonProperty private final boolean keepSession;

    @JsonCreator
    public JavaOneShotActionRequest(
            @JsonProperty("action") String action,
            @JsonProperty("repositoryPath") String repositoryPath,
            @JsonProperty("repositoryPaths") List<String> repositoryPaths,
            @JsonProperty("objectKey") String objectKey,
            @JsonProperty("locator") LocatorSuggestion locator,
            @JsonProperty("text") String text,
            @JsonProperty("window") JavaWindowSelector window,
            @JsonProperty("resolutionPolicy") ResolutionPolicy resolutionPolicy,
            @JsonProperty("autoSwitchWindow") boolean autoSwitchWindow,
            @JsonProperty("refreshTree") boolean refreshTree,
            @JsonProperty("preferAccessibleAction") boolean preferAccessibleAction,
            @JsonProperty("keepSession") boolean keepSession) {
        this.action = action;
        this.repositoryPath = repositoryPath;
        this.repositoryPaths = repositoryPaths;
        this.objectKey = objectKey;
        this.locator = locator;
        this.text = text;
        this.window = window;
        this.resolutionPolicy = resolutionPolicy;
        this.autoSwitchWindow = autoSwitchWindow;
        this.refreshTree = refreshTree;
        this.preferAccessibleAction = preferAccessibleAction;
        this.keepSession = keepSession;
    }

    public static JavaOneShotActionRequest of(
            JavaAction action,
            String repositoryPath,
            String objectKey,
            String text,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return of(action, repositoryPath == null ? null : Collections.singletonList(repositoryPath), objectKey, text, window, policy);
    }

    public static JavaOneShotActionRequest of(
            JavaAction action,
            List<String> repositoryPaths,
            String objectKey,
            String text,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaOneShotActionRequest(
                action.apiName(),
                null,
                repositoryPaths,
                objectKey,
                null,
                text,
                window,
                policy,
                true,
                true,
                true,
                false);
    }

    public static JavaOneShotActionRequest of(
            JavaAction action,
            List<String> repositoryPaths,
            LocatorSuggestion locator,
            String text,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaOneShotActionRequest(
                action.apiName(),
                null,
                repositoryPaths,
                null,
                locator,
                text,
                window,
                policy,
                true,
                true,
                true,
                false);
    }

    public static JavaOneShotActionRequest ofWindow(
            JavaAction action,
            List<String> repositoryPaths,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaOneShotActionRequest(
                action.apiName(),
                null,
                repositoryPaths,
                null,
                null,
                "",
                window,
                policy,
                true,
                true,
                true,
                false);
    }
}
