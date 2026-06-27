package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class ResolutionPolicy {
    @JsonProperty private final Integer minimumScore;
    @JsonProperty private final Integer ambiguityScoreDelta;
    @JsonProperty private final Integer maxCandidates;
    @JsonProperty private final Boolean requireUnique;
    @JsonProperty private final Integer timeoutMs;
    @JsonProperty private final Integer pollIntervalMs;
    @JsonProperty private final Boolean refreshTreeOnFailure;
    @JsonProperty private final Boolean allowCoordinateFallback;

    @JsonCreator
    public ResolutionPolicy(
            @JsonProperty("minimumScore") Integer minimumScore,
            @JsonProperty("ambiguityScoreDelta") Integer ambiguityScoreDelta,
            @JsonProperty("maxCandidates") Integer maxCandidates,
            @JsonProperty("requireUnique") Boolean requireUnique,
            @JsonProperty("timeoutMs") Integer timeoutMs,
            @JsonProperty("pollIntervalMs") Integer pollIntervalMs,
            @JsonProperty("refreshTreeOnFailure") Boolean refreshTreeOnFailure,
            @JsonProperty("allowCoordinateFallback") Boolean allowCoordinateFallback) {
        this.minimumScore = minimumScore;
        this.ambiguityScoreDelta = ambiguityScoreDelta;
        this.maxCandidates = maxCandidates;
        this.requireUnique = requireUnique;
        this.timeoutMs = timeoutMs;
        this.pollIntervalMs = pollIntervalMs;
        this.refreshTreeOnFailure = refreshTreeOnFailure;
        this.allowCoordinateFallback = allowCoordinateFallback;
    }

    public static ResolutionPolicy strict() {
        return new ResolutionPolicy(82, 18, 5, true, 5000, 200, true, false);
    }

    public static ResolutionPolicy standard() {
        return new ResolutionPolicy(72, 18, 5, true, 5000, 200, true, false);
    }

    public static ResolutionPolicy inline() {
        return new ResolutionPolicy(70, 14, 8, true, 7000, 200, true, false);
    }

    public static ResolutionPolicy tolerant() {
        return new ResolutionPolicy(65, 12, 8, true, 7000, 200, true, false);
    }

    public Integer minimumScore() { return minimumScore; }
    public Integer ambiguityScoreDelta() { return ambiguityScoreDelta; }
    public Integer maxCandidates() { return maxCandidates; }
    public Boolean requireUnique() { return requireUnique; }
    public Integer timeoutMs() { return timeoutMs; }
    public Integer pollIntervalMs() { return pollIntervalMs; }
    public Boolean refreshTreeOnFailure() { return refreshTreeOnFailure; }
    public Boolean allowCoordinateFallback() { return allowCoordinateFallback; }

    public ResolutionPolicy timeoutMs(int timeoutMs) {
        return new ResolutionPolicy(minimumScore, ambiguityScoreDelta, maxCandidates, requireUnique, timeoutMs, pollIntervalMs, refreshTreeOnFailure, allowCoordinateFallback);
    }

    public ResolutionPolicy minimumScore(int minimumScore) {
        return new ResolutionPolicy(minimumScore, ambiguityScoreDelta, maxCandidates, requireUnique, timeoutMs, pollIntervalMs, refreshTreeOnFailure, allowCoordinateFallback);
    }

    public ResolutionPolicy ambiguityScoreDelta(int ambiguityScoreDelta) {
        return new ResolutionPolicy(minimumScore, ambiguityScoreDelta, maxCandidates, requireUnique, timeoutMs, pollIntervalMs, refreshTreeOnFailure, allowCoordinateFallback);
    }

    public ResolutionPolicy maxCandidates(int maxCandidates) {
        return new ResolutionPolicy(minimumScore, ambiguityScoreDelta, maxCandidates, requireUnique, timeoutMs, pollIntervalMs, refreshTreeOnFailure, allowCoordinateFallback);
    }

    public ResolutionPolicy requireUnique(boolean requireUnique) {
        return new ResolutionPolicy(minimumScore, ambiguityScoreDelta, maxCandidates, requireUnique, timeoutMs, pollIntervalMs, refreshTreeOnFailure, allowCoordinateFallback);
    }
}
