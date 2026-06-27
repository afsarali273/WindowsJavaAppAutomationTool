package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record JavaOneShotActionRequest(
        String action,
        String repositoryPath,
        List<String> repositoryPaths,
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
        return of(action, repositoryPath == null ? null : List.of(repositoryPath), objectKey, text, window, policy);
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
}
