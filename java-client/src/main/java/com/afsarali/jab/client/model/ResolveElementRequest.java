package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class ResolveElementRequest {
    @JsonProperty private final String objectKey;
    @JsonProperty private final LocatorSuggestion locator;
    @JsonProperty private final JavaWindowSelector window;
    @JsonProperty private final ResolutionPolicy resolutionPolicy;
    @JsonProperty private final boolean autoSwitchWindow;
    @JsonProperty private final boolean refreshTree;

    @JsonCreator
    public ResolveElementRequest(
            @JsonProperty("objectKey") String objectKey,
            @JsonProperty("locator") LocatorSuggestion locator,
            @JsonProperty("window") JavaWindowSelector window,
            @JsonProperty("resolutionPolicy") ResolutionPolicy resolutionPolicy,
            @JsonProperty("autoSwitchWindow") boolean autoSwitchWindow,
            @JsonProperty("refreshTree") boolean refreshTree) {
        this.objectKey = objectKey;
        this.locator = locator;
        this.window = window;
        this.resolutionPolicy = resolutionPolicy;
        this.autoSwitchWindow = autoSwitchWindow;
        this.refreshTree = refreshTree;
    }

    public static ResolveElementRequest object(String objectKey, JavaWindowSelector window) {
        return new ResolveElementRequest(objectKey, null, window, ResolutionPolicy.strict(), true, false);
    }
}
