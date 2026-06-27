package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class SwitchWindowRequest {
    @JsonProperty private final String hwnd;
    @JsonProperty private final String title;
    @JsonProperty private final String className;
    @JsonProperty private final Integer processId;
    @JsonProperty private final Integer vmId;
    @JsonProperty private final boolean exactTitle;
    @JsonProperty private final boolean refreshTree;

    @JsonCreator
    public SwitchWindowRequest(
            @JsonProperty("hwnd") String hwnd,
            @JsonProperty("title") String title,
            @JsonProperty("className") String className,
            @JsonProperty("processId") Integer processId,
            @JsonProperty("vmId") Integer vmId,
            @JsonProperty("exactTitle") boolean exactTitle,
            @JsonProperty("refreshTree") boolean refreshTree) {
        this.hwnd = hwnd;
        this.title = title;
        this.className = className;
        this.processId = processId;
        this.vmId = vmId;
        this.exactTitle = exactTitle;
        this.refreshTree = refreshTree;
    }

    public static SwitchWindowRequest from(JavaWindowSelector selector) {
        return new SwitchWindowRequest(
                selector.hwnd(),
                selector.title(),
                selector.className(),
                selector.processId(),
                selector.vmId(),
                selector.exactTitle(),
                true);
    }
}
