package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class JavaActionRequest {
    @JsonProperty private final String action;
    @JsonProperty private final String objectKey;
    @JsonProperty private final LocatorSuggestion locator;
    @JsonProperty private final String text;
    @JsonProperty private final JavaWindowSelector window;
    @JsonProperty private final ResolutionPolicy resolutionPolicy;
    @JsonProperty private final boolean autoSwitchWindow;
    @JsonProperty private final boolean refreshTree;
    @JsonProperty private final boolean preferAccessibleAction;

    @JsonCreator
    public JavaActionRequest(
            @JsonProperty("action") String action,
            @JsonProperty("objectKey") String objectKey,
            @JsonProperty("locator") LocatorSuggestion locator,
            @JsonProperty("text") String text,
            @JsonProperty("window") JavaWindowSelector window,
            @JsonProperty("resolutionPolicy") ResolutionPolicy resolutionPolicy,
            @JsonProperty("autoSwitchWindow") boolean autoSwitchWindow,
            @JsonProperty("refreshTree") boolean refreshTree,
            @JsonProperty("preferAccessibleAction") boolean preferAccessibleAction) {
        this.action = action;
        this.objectKey = objectKey;
        this.locator = locator;
        this.text = text;
        this.window = window;
        this.resolutionPolicy = resolutionPolicy;
        this.autoSwitchWindow = autoSwitchWindow;
        this.refreshTree = refreshTree;
        this.preferAccessibleAction = preferAccessibleAction;
    }

    public static JavaActionRequest of(JavaAction action, String objectKey, String text, JavaWindowSelector window, ResolutionPolicy policy) {
        return new JavaActionRequest(action.apiName(), objectKey, null, text, window, policy, true, false, true);
    }

    public static JavaActionRequest of(JavaAction action, LocatorSuggestion locator, String text, JavaWindowSelector window, ResolutionPolicy policy) {
        return new JavaActionRequest(action.apiName(), null, locator, text, window, policy, true, false, true);
    }

    public static JavaActionRequest ofWindow(JavaAction action, JavaWindowSelector window, ResolutionPolicy policy) {
        return new JavaActionRequest(action.apiName(), null, null, "", window, policy, true, false, true);
    }
}
