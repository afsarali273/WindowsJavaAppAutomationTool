package com.afsarali.jab.client.model;

public enum JavaNavigationCommand {
    PAGE_DOWN("PageDown"),
    PAGE_UP("PageUp"),
    DOWN("Down"),
    UP("Up"),
    HOME("Home"),
    END("End");

    private final String apiName;

    JavaNavigationCommand(String apiName) {
        this.apiName = apiName;
    }

    public String apiName() {
        return apiName;
    }
}
