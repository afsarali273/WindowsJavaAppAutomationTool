package com.afsarali.jab.client;

import com.fasterxml.jackson.databind.JsonNode;

public final class JavaValidation {
    private final JsonNode data;

    private JavaValidation(JsonNode data) {
        this.data = data;
    }

    public static JavaValidation from(JsonNode data) {
        return new JavaValidation(data);
    }

    public boolean exists() {
        return bool("exists");
    }

    public boolean isVisible() {
        return bool("isVisible");
    }

    public boolean isShowing() {
        return bool("isShowing");
    }

    public boolean isEnabled() {
        return bool("isEnabled");
    }

    public boolean isFocusable() {
        return bool("isFocusable");
    }

    public boolean isSelected() {
        return bool("isSelected");
    }

    public boolean hasText() {
        return bool("hasText");
    }

    public boolean textMatches() {
        return bool("textMatches");
    }

    public String text() {
        return text("text");
    }

    public String displayName() {
        return text("displayName");
    }

    public String role() {
        return text("role");
    }

    public String name() {
        return text("name");
    }

    public String states() {
        return text("states");
    }

    public String message() {
        return text("message");
    }

    public JsonNode raw() {
        return data;
    }

    private boolean bool(String field) {
        JsonNode value = data == null ? null : data.get(field);
        return value != null && value.asBoolean(false);
    }

    private String text(String field) {
        JsonNode value = data == null ? null : data.get(field);
        return value == null || value.isNull() ? "" : value.asText("");
    }
}
