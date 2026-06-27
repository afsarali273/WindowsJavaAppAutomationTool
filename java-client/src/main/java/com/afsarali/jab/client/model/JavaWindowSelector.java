package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class JavaWindowSelector {
    @JsonProperty private final String hwnd;
    @JsonProperty private final String title;
    @JsonProperty private final String className;
    @JsonProperty private final Integer processId;
    @JsonProperty private final Integer vmId;
    @JsonProperty private final boolean exactTitle;

    @JsonCreator
    public JavaWindowSelector(
            @JsonProperty("hwnd") String hwnd,
            @JsonProperty("title") String title,
            @JsonProperty("className") String className,
            @JsonProperty("processId") Integer processId,
            @JsonProperty("vmId") Integer vmId,
            @JsonProperty("exactTitle") boolean exactTitle) {
        this.hwnd = hwnd;
        this.title = title;
        this.className = className;
        this.processId = processId;
        this.vmId = vmId;
        this.exactTitle = exactTitle;
    }

    public static JavaWindowSelector title(String title) {
        return new JavaWindowSelector(null, title, null, null, null, false);
    }

    public static JavaWindowSelector exactTitle(String title) {
        return new JavaWindowSelector(null, title, null, null, null, true);
    }

    public static JavaWindowSelector hwnd(String hwnd) {
        return new JavaWindowSelector(hwnd, null, null, null, null, false);
    }

    public static JavaWindowSelector processId(int processId) {
        return new JavaWindowSelector(null, null, null, processId, null, false);
    }

    public String hwnd() { return hwnd; }
    public String title() { return title; }
    public String className() { return className; }
    public Integer processId() { return processId; }
    public Integer vmId() { return vmId; }
    public boolean exactTitle() { return exactTitle; }

    public JavaWindowSelector className(String className) {
        return new JavaWindowSelector(hwnd, title, className, processId, vmId, exactTitle);
    }

    public JavaWindowSelector processId(Integer processId) {
        return new JavaWindowSelector(hwnd, title, className, processId, vmId, exactTitle);
    }

    public JavaWindowSelector vmId(Integer vmId) {
        return new JavaWindowSelector(hwnd, title, className, processId, vmId, exactTitle);
    }

    public JavaWindowSelector exactTitle(boolean exactTitle) {
        return new JavaWindowSelector(hwnd, title, className, processId, vmId, exactTitle);
    }
}
