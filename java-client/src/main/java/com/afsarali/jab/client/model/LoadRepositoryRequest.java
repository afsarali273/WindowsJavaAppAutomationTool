package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class LoadRepositoryRequest {
    @JsonProperty private final String path;
    @JsonProperty private final List<String> paths;

    @JsonCreator
    public LoadRepositoryRequest(
            @JsonProperty("path") String path,
            @JsonProperty("paths") List<String> paths) {
        this.path = path;
        this.paths = paths;
    }

    public static LoadRepositoryRequest single(String path) {
        return new LoadRepositoryRequest(path, null);
    }

    public static LoadRepositoryRequest multiple(List<String> paths) {
        return new LoadRepositoryRequest(null, paths);
    }
}
