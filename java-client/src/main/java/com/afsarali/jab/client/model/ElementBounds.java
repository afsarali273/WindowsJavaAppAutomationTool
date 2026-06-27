package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

public final class ElementBounds {
    @JsonProperty private final int x;
    @JsonProperty private final int y;
    @JsonProperty private final int width;
    @JsonProperty private final int height;

    @JsonCreator
    public ElementBounds(
            @JsonProperty("x") int x,
            @JsonProperty("y") int y,
            @JsonProperty("width") int width,
            @JsonProperty("height") int height) {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public int x() { return x; }
    public int y() { return y; }
    public int width() { return width; }
    public int height() { return height; }
}
