package com.afsarali.jab.client.model;

public enum JavaAction {
    FOCUS("Focus"),
    CLICK("Click"),
    DOUBLE_CLICK("DoubleClick"),
    CLOSE_WINDOW("CloseWindow"),
    SET_TEXT("SetText"),
    TYPE_TEXT("TypeText"),
    GET_TEXT("GetText");

    private final String apiName;

    JavaAction(String apiName) {
        this.apiName = apiName;
    }

    public String apiName() {
        return apiName;
    }
}
