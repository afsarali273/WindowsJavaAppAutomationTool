package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record JavaValidationRequest(
        String repositoryPath,
        List<String> repositoryPaths,
        String objectKey,
        LocatorSuggestion locator,
        String expectedText,
        JavaWindowSelector window,
        ResolutionPolicy resolutionPolicy,
        boolean autoSwitchWindow,
        boolean refreshTree) {

    public static JavaValidationRequest session(
            String objectKey,
            String expectedText,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaValidationRequest(null, null, objectKey, null, expectedText, window, policy, true, false);
    }

    public static JavaValidationRequest oneShot(
            List<String> repositoryPaths,
            String objectKey,
            String expectedText,
            JavaWindowSelector window,
            ResolutionPolicy policy) {
        return new JavaValidationRequest(null, repositoryPaths, objectKey, null, expectedText, window, policy, true, true);
    }
}
