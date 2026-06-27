package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record SwitchWindowRequest(
        String hwnd,
        String title,
        String className,
        Integer processId,
        Integer vmId,
        boolean exactTitle,
        boolean refreshTree) {

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
