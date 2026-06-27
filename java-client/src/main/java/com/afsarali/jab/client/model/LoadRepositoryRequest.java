package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record LoadRepositoryRequest(String path, List<String> paths) {

    public static LoadRepositoryRequest single(String path) {
        return new LoadRepositoryRequest(path, null);
    }

    public static LoadRepositoryRequest multiple(List<String> paths) {
        return new LoadRepositoryRequest(null, paths);
    }
}
