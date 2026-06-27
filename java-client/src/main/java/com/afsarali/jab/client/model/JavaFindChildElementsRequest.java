package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record JavaFindChildElementsRequest(
        String repositoryPath,
        List<String> repositoryPaths,
        String parentObjectKey,
        LocatorSuggestion parentLocator,
        JavaWindowSelector window,
        ResolutionPolicy resolutionPolicy,
        boolean autoSwitchWindow,
        boolean refreshTree,
        boolean includeSelf,
        Integer maxDepth,
        Integer maxResults) {

    public static JavaFindChildElementsRequest session(
            String parentObjectKey,
            LocatorSuggestion parentLocator,
            JavaWindowSelector window,
            ResolutionPolicy policy,
            boolean includeSelf,
            Integer maxDepth,
            Integer maxResults) {
        return new JavaFindChildElementsRequest(null, null, parentObjectKey, parentLocator, window, policy, true, false, includeSelf, maxDepth, maxResults);
    }

    public static JavaFindChildElementsRequest oneShot(
            List<String> repositoryPaths,
            String parentObjectKey,
            LocatorSuggestion parentLocator,
            JavaWindowSelector window,
            ResolutionPolicy policy,
            boolean includeSelf,
            Integer maxDepth,
            Integer maxResults) {
        return new JavaFindChildElementsRequest(null, repositoryPaths, parentObjectKey, parentLocator, window, policy, true, true, includeSelf, maxDepth, maxResults);
    }
}
