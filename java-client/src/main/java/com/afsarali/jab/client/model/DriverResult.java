package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;
import com.fasterxml.jackson.databind.JsonNode;

public final class DriverResult {
    @JsonProperty private final boolean success;
    @JsonProperty private final String message;
    @JsonProperty private final String sessionId;
    @JsonProperty private final JsonNode data;

    @JsonCreator
    public DriverResult(
            @JsonProperty("success") boolean success,
            @JsonProperty("message") String message,
            @JsonProperty("sessionId") String sessionId,
            @JsonProperty("data") JsonNode data) {
        this.success = success;
        this.message = message;
        this.sessionId = sessionId;
        this.data = data;
    }

    public boolean success() { return success; }
    public String message() { return message; }
    public String sessionId() { return sessionId; }
    public JsonNode data() { return data; }

    public String text() {
        return data == null || data.get("text") == null || data.get("text").isNull()
                ? ""
                : data.get("text").asText();
    }
}
