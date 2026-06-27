package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record ResolveElementRequest(
        String objectKey,
        LocatorSuggestion locator,
        JavaWindowSelector window,
        ResolutionPolicy resolutionPolicy,
        boolean autoSwitchWindow,
        boolean refreshTree) {

    public static ResolveElementRequest object(String objectKey, JavaWindowSelector window) {
        return new ResolveElementRequest(objectKey, null, window, ResolutionPolicy.strict(), true, false);
    }
}
