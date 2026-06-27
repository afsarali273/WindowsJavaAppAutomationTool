package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class JavaFindElementsRequest {
    @JsonProperty private final String repositoryPath;
    @JsonProperty private final List<String> repositoryPaths;
    @JsonProperty private final String objectKey;
    @JsonProperty private final LocatorSuggestion locator;
    @JsonProperty private final JavaWindowSelector window;
    @JsonProperty private final ResolutionPolicy resolutionPolicy;
    @JsonProperty private final boolean autoSwitchWindow;
    @JsonProperty private final boolean refreshTree;
    @JsonProperty private final Integer minimumScore;
    @JsonProperty private final Integer maxResults;

    @JsonCreator
    public JavaFindElementsRequest(
            @JsonProperty("repositoryPath") String repositoryPath,
            @JsonProperty("repositoryPaths") List<String> repositoryPaths,
            @JsonProperty("objectKey") String objectKey,
            @JsonProperty("locator") LocatorSuggestion locator,
            @JsonProperty("window") JavaWindowSelector window,
            @JsonProperty("resolutionPolicy") ResolutionPolicy resolutionPolicy,
            @JsonProperty("autoSwitchWindow") boolean autoSwitchWindow,
            @JsonProperty("refreshTree") boolean refreshTree,
            @JsonProperty("minimumScore") Integer minimumScore,
            @JsonProperty("maxResults") Integer maxResults) {
        this.repositoryPath = repositoryPath;
        this.repositoryPaths = repositoryPaths;
        this.objectKey = objectKey;
        this.locator = locator;
        this.window = window;
        this.resolutionPolicy = resolutionPolicy;
        this.autoSwitchWindow = autoSwitchWindow;
        this.refreshTree = refreshTree;
        this.minimumScore = minimumScore;
        this.maxResults = maxResults;
    }

    public static JavaFindElementsRequest session(String objectKey, LocatorSuggestion locator, JavaWindowSelector window, ResolutionPolicy policy, Integer minimumScore, Integer maxResults) {
        return new JavaFindElementsRequest(null, null, objectKey, locator, window, policy, true, false, minimumScore, maxResults);
    }

    public static JavaFindElementsRequest oneShot(List<String> repositoryPaths, String objectKey, LocatorSuggestion locator, JavaWindowSelector window, ResolutionPolicy policy, Integer minimumScore, Integer maxResults) {
        return new JavaFindElementsRequest(null, repositoryPaths, objectKey, locator, window, policy, true, true, minimumScore, maxResults);
    }
}
