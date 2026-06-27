package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record JavaActionRequest(
        String action,
        String objectKey,
        LocatorSuggestion locator,
        String text,
        JavaWindowSelector window,
        ResolutionPolicy resolutionPolicy,
        boolean autoSwitchWindow,
        boolean refreshTree,
        boolean preferAccessibleAction) {

    public static JavaActionRequest of(JavaAction action, String objectKey, String text, JavaWindowSelector window, ResolutionPolicy policy) {
        return new JavaActionRequest(action.apiName(), objectKey, null, text, window, policy, true, false, true);
    }
}
