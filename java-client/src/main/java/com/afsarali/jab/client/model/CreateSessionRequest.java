package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record CreateSessionRequest(
        String hwnd,
        String title,
        Integer processId,
        boolean refreshTree) {

    public static CreateSessionRequest byTitle(String title) {
        return new CreateSessionRequest(null, title, null, true);
    }

    public static CreateSessionRequest byHwnd(String hwnd) {
        return new CreateSessionRequest(hwnd, null, null, true);
    }

    public static CreateSessionRequest byProcessId(int processId) {
        return new CreateSessionRequest(null, null, processId, true);
    }
}
