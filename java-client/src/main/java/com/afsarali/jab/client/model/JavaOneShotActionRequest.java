package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record JavaOneShotActionRequest(
        String action,
        String repositoryPath,
        String objectKey,
        LocatorSuggestion locator,
        String text,
        JavaWindowSelector window,
        ResolutionPolicy resolutionPolicy,
        boolean autoSwitchWindow,
        boolean refreshTree,
        boolean preferAccessibleAction,
        boolean keepSession) {

    public static JavaOneShotActionRequest of(
            JavaAction action,
            String repositoryPath,
            String objectKey,
            String text,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaOneShotActionRequest(
                action.apiName(),
                repositoryPath,
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
}
