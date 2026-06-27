package com.afsarali.jab.client.model;

public record JavaWindow(
        String hwnd,
        String title,
        String className,
        int processId,
        int vmId) {
}
