package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.Duration;
import java.util.Arrays;
import java.util.List;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class JavaLaunchApplicationRequest {
    @JsonProperty private final String applicationPath;
    @JsonProperty private final List<String> arguments;
    @JsonProperty private final String argumentsText;
    @JsonProperty private final String workingDirectory;
    @JsonProperty private final String javaExecutablePath;
    @JsonProperty private final JavaWindowSelector waitForWindow;
    @JsonProperty private final Integer waitTimeoutMs;
    @JsonProperty private final Integer waitPollIntervalMs;
    @JsonProperty private final boolean useShellExecute;
    @JsonProperty private final boolean createNoWindow;

    @JsonCreator
    public JavaLaunchApplicationRequest(
            @JsonProperty("applicationPath") String applicationPath,
            @JsonProperty("arguments") List<String> arguments,
            @JsonProperty("argumentsText") String argumentsText,
            @JsonProperty("workingDirectory") String workingDirectory,
            @JsonProperty("javaExecutablePath") String javaExecutablePath,
            @JsonProperty("waitForWindow") JavaWindowSelector waitForWindow,
            @JsonProperty("waitTimeoutMs") Integer waitTimeoutMs,
            @JsonProperty("waitPollIntervalMs") Integer waitPollIntervalMs,
            @JsonProperty("useShellExecute") boolean useShellExecute,
            @JsonProperty("createNoWindow") boolean createNoWindow) {
        this.applicationPath = applicationPath;
        this.arguments = arguments;
        this.argumentsText = argumentsText;
        this.workingDirectory = workingDirectory;
        this.javaExecutablePath = javaExecutablePath;
        this.waitForWindow = waitForWindow;
        this.waitTimeoutMs = waitTimeoutMs;
        this.waitPollIntervalMs = waitPollIntervalMs;
        this.useShellExecute = useShellExecute;
        this.createNoWindow = createNoWindow;
    }

    public static JavaLaunchApplicationRequest of(String applicationPath) {
        return new JavaLaunchApplicationRequest(applicationPath, null, null, null, null, null, null, null, false, false);
    }

    public static JavaLaunchApplicationRequest jar(String jarPath) {
        return of(jarPath);
    }

    public static JavaLaunchApplicationRequest batch(String batchPath) {
        return of(batchPath);
    }

    public static JavaLaunchApplicationRequest executable(String executablePath) {
        return of(executablePath);
    }

    public String applicationPath() { return applicationPath; }
    public List<String> arguments() { return arguments; }
    public String argumentsText() { return argumentsText; }
    public String workingDirectory() { return workingDirectory; }
    public String javaExecutablePath() { return javaExecutablePath; }
    public JavaWindowSelector waitForWindow() { return waitForWindow; }
    public Integer waitTimeoutMs() { return waitTimeoutMs; }
    public Integer waitPollIntervalMs() { return waitPollIntervalMs; }
    public boolean useShellExecute() { return useShellExecute; }
    public boolean createNoWindow() { return createNoWindow; }

    public JavaLaunchApplicationRequest arguments(String... arguments) {
        return arguments(arguments == null ? null : Arrays.asList(arguments));
    }

    public JavaLaunchApplicationRequest arguments(List<String> arguments) {
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest argumentsText(String argumentsText) {
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest workingDirectory(String workingDirectory) {
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest javaExecutablePath(String javaExecutablePath) {
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest waitForWindow(JavaWindowSelector waitForWindow) {
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest waitTimeout(Duration waitTimeout) {
        Integer timeoutMs = waitTimeout == null ? null : Math.toIntExact(waitTimeout.toMillis());
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                timeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest waitPollInterval(Duration waitPollInterval) {
        Integer pollMs = waitPollInterval == null ? null : Math.toIntExact(waitPollInterval.toMillis());
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                pollMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest useShellExecute(boolean useShellExecute) {
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }

    public JavaLaunchApplicationRequest createNoWindow(boolean createNoWindow) {
        return new JavaLaunchApplicationRequest(
                applicationPath,
                arguments,
                argumentsText,
                workingDirectory,
                javaExecutablePath,
                waitForWindow,
                waitTimeoutMs,
                waitPollIntervalMs,
                useShellExecute,
                createNoWindow);
    }
}
