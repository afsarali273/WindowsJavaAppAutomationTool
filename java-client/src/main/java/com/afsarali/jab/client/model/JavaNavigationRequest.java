package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

public final class JavaNavigationRequest {
    @JsonProperty private final String command;
    @JsonProperty private final int count;

    @JsonCreator
    public JavaNavigationRequest(
            @JsonProperty("command") String command,
            @JsonProperty("count") int count) {
        this.command = command;
        this.count = count;
    }

    public static JavaNavigationRequest of(JavaNavigationCommand command, int count) {
        return new JavaNavigationRequest(command.apiName(), count);
    }

    public String command() {
        return command;
    }

    public int count() {
        return count;
    }
}
