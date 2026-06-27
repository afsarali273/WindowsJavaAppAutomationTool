package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record ResolutionPolicy(
        Integer minimumScore,
        Integer ambiguityScoreDelta,
        Integer maxCandidates,
        Boolean requireUnique,
        Integer timeoutMs,
        Integer pollIntervalMs,
        Boolean refreshTreeOnFailure,
        Boolean allowCoordinateFallback) {

    public static ResolutionPolicy strict() {
        return new ResolutionPolicy(82, 18, 5, true, 5000, 200, true, false);
    }

    public static ResolutionPolicy tolerant() {
        return new ResolutionPolicy(65, 12, 8, true, 7000, 200, true, false);
    }

    public ResolutionPolicy timeoutMs(int timeoutMs) {
        return new ResolutionPolicy(minimumScore, ambiguityScoreDelta, maxCandidates, requireUnique, timeoutMs, pollIntervalMs, refreshTreeOnFailure, allowCoordinateFallback);
    }
}
