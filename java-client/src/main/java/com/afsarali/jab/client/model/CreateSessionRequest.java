package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonInclude(JsonInclude.Include.NON_NULL)
public final class CreateSessionRequest {
    @JsonProperty private final String hwnd;
    @JsonProperty private final String title;
    @JsonProperty private final Integer processId;
    @JsonProperty private final boolean refreshTree;

    @JsonCreator
    public CreateSessionRequest(
            @JsonProperty("hwnd") String hwnd,
            @JsonProperty("title") String title,
            @JsonProperty("processId") Integer processId,
            @JsonProperty("refreshTree") boolean refreshTree) {
        this.hwnd = hwnd;
        this.title = title;
        this.processId = processId;
        this.refreshTree = refreshTree;
    }

    public static CreateSessionRequest byTitle(String title) {
        return new CreateSessionRequest(null, title, null, true);
    }

    public static CreateSessionRequest byHwnd(String hwnd) {
        return new CreateSessionRequest(hwnd, null, null, true);
    }

    public static CreateSessionRequest byProcessId(int processId) {
        return new CreateSessionRequest(null, null, processId, true);
    }

    public String hwnd() { return hwnd; }
    public String title() { return title; }
    public Integer processId() { return processId; }
    public boolean refreshTree() { return refreshTree; }
}
