package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.time.OffsetDateTime;

public final class JavaLaunchApplicationResult {
    private static final ObjectMapper MAPPER = new ObjectMapper()
            .findAndRegisterModules()
            .configure(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES, false);

    @JsonProperty private final String applicationPath;
    @JsonProperty private final String launchTarget;
    @JsonProperty private final String launchArguments;
    @JsonProperty private final String workingDirectory;
    @JsonProperty private final int processId;
    @JsonProperty private final OffsetDateTime startedAtUtc;
    @JsonProperty private final boolean waitedForWindow;
    @JsonProperty private final JavaWindow window;

    @JsonCreator
    public JavaLaunchApplicationResult(
            @JsonProperty("applicationPath") String applicationPath,
            @JsonProperty("launchTarget") String launchTarget,
            @JsonProperty("launchArguments") String launchArguments,
            @JsonProperty("workingDirectory") String workingDirectory,
            @JsonProperty("processId") int processId,
            @JsonProperty("startedAtUtc") OffsetDateTime startedAtUtc,
            @JsonProperty("waitedForWindow") boolean waitedForWindow,
            @JsonProperty("window") JavaWindow window) {
        this.applicationPath = applicationPath;
        this.launchTarget = launchTarget;
        this.launchArguments = launchArguments;
        this.workingDirectory = workingDirectory;
        this.processId = processId;
        this.startedAtUtc = startedAtUtc;
        this.waitedForWindow = waitedForWindow;
        this.window = window;
    }

    public static JavaLaunchApplicationResult from(JsonNode data) {
        try {
            return MAPPER.treeToValue(data, JavaLaunchApplicationResult.class);
        } catch (Exception ex) {
            throw new IllegalStateException("Could not read launch result: " + ex.getMessage(), ex);
        }
    }

    public String applicationPath() { return applicationPath; }
    public String launchTarget() { return launchTarget; }
    public String launchArguments() { return launchArguments; }
    public String workingDirectory() { return workingDirectory; }
    public int processId() { return processId; }
    public OffsetDateTime startedAtUtc() { return startedAtUtc; }
    public boolean waitedForWindow() { return waitedForWindow; }
    public JavaWindow window() { return window; }
}
