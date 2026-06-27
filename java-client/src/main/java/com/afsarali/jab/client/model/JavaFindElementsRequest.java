package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record JavaFindElementsRequest(
        String repositoryPath,
        List<String> repositoryPaths,
        String objectKey,
        LocatorSuggestion locator,
        JavaWindowSelector window,
        ResolutionPolicy resolutionPolicy,
        boolean autoSwitchWindow,
        boolean refreshTree,
        Integer minimumScore,
        Integer maxResults) {

    public static JavaFindElementsRequest session(
            String objectKey,
            LocatorSuggestion locator,
            JavaWindowSelector window,
            ResolutionPolicy policy,
            Integer minimumScore,
            Integer maxResults) {
        return new JavaFindElementsRequest(null, null, objectKey, locator, window, policy, true, false, minimumScore, maxResults);
    }

    public static JavaFindElementsRequest oneShot(
            List<String> repositoryPaths,
            String objectKey,
            LocatorSuggestion locator,
            JavaWindowSelector window,
            ResolutionPolicy policy,
            Integer minimumScore,
            Integer maxResults) {
        return new JavaFindElementsRequest(null, repositoryPaths, objectKey, locator, window, policy, true, true, minimumScore, maxResults);
    }
}
