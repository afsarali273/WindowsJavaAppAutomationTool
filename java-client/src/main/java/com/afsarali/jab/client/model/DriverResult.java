package com.afsarali.jab.client.model;

import com.fasterxml.jackson.databind.JsonNode;

public record DriverResult(
        boolean success,
        String message,
        String sessionId,
        JsonNode data) {

    public String text() {
        return data == null || data.get("text") == null || data.get("text").isNull()
                ? ""
                : data.get("text").asText();
    }
}
