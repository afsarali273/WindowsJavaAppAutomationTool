package com.afsarali.jab.client;

import com.afsarali.jab.client.model.LocatorSuggestion;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

public final class JavaElementSnapshot {
    private static final ObjectMapper MAPPER = new ObjectMapper()
            .configure(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES, false);

    private final JsonNode data;

    private JavaElementSnapshot(JsonNode data) {
        this.data = data;
    }

    public static JavaElementSnapshot from(JsonNode data) {
        return new JavaElementSnapshot(data);
    }

    public String displayName() { return text("displayName"); }
    public String role() { return text("role"); }
    public String roleEnUs() { return text("roleEnUs"); }
    public String name() { return text("name"); }
    public String virtualAccessibleName() { return text("virtualAccessibleName"); }
    public String description() { return text("description"); }
    public String states() { return text("states"); }
    public String statesEnUs() { return text("statesEnUs"); }
    public int indexInParent() { return integer("indexInParent"); }
    public int objectDepth() { return integer("objectDepth"); }
    public int childrenCount() { return integer("childrenCount"); }
    public String path() { return text("path"); }
    public String indexPath() { return text("indexPath"); }
    public String xPath() { return text("xPath"); }
    public String parentRole() { return text("parentRole"); }
    public String parentName() { return text("parentName"); }
    public String objectKey() { return text("objectKey"); }
    public String textPreview() { return text("textPreview"); }
    public String currentValue() { return text("currentValue"); }
    public int score() { return integer("score"); }
    public JsonNode raw() { return data; }

    public LocatorSuggestion locator() {
        if (data == null || data.isNull()) {
            return null;
        }

        try {
            return MAPPER.treeToValue(data, LocatorSuggestion.class);
        } catch (Exception ex) {
            throw new ApiException(0, "Could not convert element snapshot into a locator: " + ex.getMessage());
        }
    }

    public JavaElementHandle asHandle(JavaDriver driver) {
        return asHandle(driver, null);
    }

    public JavaElementHandle asHandle(JavaDriver driver, com.afsarali.jab.client.model.JavaWindowSelector window) {
        return JavaElementHandle.from(driver, window, this);
    }

    public JavaElementHandle asHandle(JavaAutomation automation) {
        return asHandle(automation, null);
    }

    public JavaElementHandle asHandle(JavaAutomation automation, com.afsarali.jab.client.model.JavaWindowSelector window) {
        return JavaElementHandle.from(automation, window, this);
    }

    private String text(String field) {
        JsonNode value = data == null ? null : data.get(field);
        return value == null || value.isNull() ? "" : value.asText("");
    }

    private int integer(String field) {
        JsonNode value = data == null ? null : data.get(field);
        return value == null || value.isNull() ? 0 : value.asInt(0);
    }
}
