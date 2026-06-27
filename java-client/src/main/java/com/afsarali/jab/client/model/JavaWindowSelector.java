package com.afsarali.jab.client.model;

import com.fasterxml.jackson.annotation.JsonInclude;

@JsonInclude(JsonInclude.Include.NON_NULL)
public record JavaWindowSelector(
        String hwnd,
        String title,
        String className,
        Integer processId,
        Integer vmId,
        boolean exactTitle) {

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
