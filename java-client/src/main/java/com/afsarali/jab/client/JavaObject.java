package com.afsarali.jab.client;

import com.afsarali.jab.client.model.DriverResult;
import com.afsarali.jab.client.model.JavaAction;
import com.afsarali.jab.client.model.JavaWindowSelector;

public final class JavaObject {
    private final JavaAutomation automation;
    private final JavaWindowSelector window;
    private final String objectKey;

    JavaObject(JavaAutomation automation, JavaWindowSelector window, String objectKey) {
        this.automation = automation;
        this.window = window;
        this.objectKey = objectKey;
    }

    public JavaObject focus() {
        automation.run(JavaAction.FOCUS, objectKey, "", window);
        return this;
    }

    public JavaObject click() {
        automation.run(JavaAction.CLICK, objectKey, "", window);
        return this;
    }

    public JavaObject doubleClick() {
        automation.run(JavaAction.DOUBLE_CLICK, objectKey, "", window);
        return this;
    }

    public JavaObject setText(String text) {
        automation.run(JavaAction.SET_TEXT, objectKey, text, window);
        return this;
    }

    public JavaObject typeText(String text) {
        automation.run(JavaAction.TYPE_TEXT, objectKey, text, window);
        return this;
    }

    public String getText() {
        DriverResult result = automation.run(JavaAction.GET_TEXT, objectKey, "", window);
        return result.text();
    }
}
