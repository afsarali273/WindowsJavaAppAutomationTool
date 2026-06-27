package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class JavaFindChildElementsRequest {
    @JsonProperty private final String repositoryPath;
    @JsonProperty private final List<String> repositoryPaths;
    @JsonProperty private final String parentObjectKey;
    @JsonProperty private final LocatorSuggestion parentLocator;
    @JsonProperty private final JavaWindowSelector window;
    @JsonProperty private final ResolutionPolicy resolutionPolicy;
    @JsonProperty private final boolean autoSwitchWindow;
    @JsonProperty private final boolean refreshTree;
    @JsonProperty private final boolean includeSelf;
    @JsonProperty private final Integer maxDepth;
    @JsonProperty private final Integer maxResults;

    @JsonCreator
    public JavaFindChildElementsRequest(
            @JsonProperty("repositoryPath") String repositoryPath,
            @JsonProperty("repositoryPaths") List<String> repositoryPaths,
            @JsonProperty("parentObjectKey") String parentObjectKey,
            @JsonProperty("parentLocator") LocatorSuggestion parentLocator,
            @JsonProperty("window") JavaWindowSelector window,
            @JsonProperty("resolutionPolicy") ResolutionPolicy resolutionPolicy,
            @JsonProperty("autoSwitchWindow") boolean autoSwitchWindow,
            @JsonProperty("refreshTree") boolean refreshTree,
            @JsonProperty("includeSelf") boolean includeSelf,
            @JsonProperty("maxDepth") Integer maxDepth,
            @JsonProperty("maxResults") Integer maxResults) {
        this.repositoryPath = repositoryPath;
        this.repositoryPaths = repositoryPaths;
        this.parentObjectKey = parentObjectKey;
        this.parentLocator = parentLocator;
        this.window = window;
        this.resolutionPolicy = resolutionPolicy;
        this.autoSwitchWindow = autoSwitchWindow;
        this.refreshTree = refreshTree;
        this.includeSelf = includeSelf;
        this.maxDepth = maxDepth;
        this.maxResults = maxResults;
    }

    public static JavaFindChildElementsRequest session(String parentObjectKey, LocatorSuggestion parentLocator, JavaWindowSelector window, ResolutionPolicy policy, boolean includeSelf, Integer maxDepth, Integer maxResults) {
        return new JavaFindChildElementsRequest(null, null, parentObjectKey, parentLocator, window, policy, true, false, includeSelf, maxDepth, maxResults);
    }

    public static JavaFindChildElementsRequest oneShot(List<String> repositoryPaths, String parentObjectKey, LocatorSuggestion parentLocator, JavaWindowSelector window, ResolutionPolicy policy, boolean includeSelf, Integer maxDepth, Integer maxResults) {
        return new JavaFindChildElementsRequest(null, repositoryPaths, parentObjectKey, parentLocator, window, policy, true, true, includeSelf, maxDepth, maxResults);
    }
}
