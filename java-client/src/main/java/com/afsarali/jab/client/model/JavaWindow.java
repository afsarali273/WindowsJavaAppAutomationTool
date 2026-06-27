package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonProperty;

public final class JavaWindow {
    @JsonProperty private final String hwnd;
    @JsonProperty private final String title;
    @JsonProperty private final String className;
    @JsonProperty private final int processId;
    @JsonProperty private final int vmId;

    @JsonCreator
    public JavaWindow(
            @JsonProperty("hwnd") String hwnd,
            @JsonProperty("title") String title,
            @JsonProperty("className") String className,
            @JsonProperty("processId") int processId,
            @JsonProperty("vmId") int vmId) {
        this.hwnd = hwnd;
        this.title = title;
        this.className = className;
        this.processId = processId;
        this.vmId = vmId;
    }

    public String hwnd() { return hwnd; }
    public String title() { return title; }
    public String className() { return className; }
    public int processId() { return processId; }
    public int vmId() { return vmId; }
}
