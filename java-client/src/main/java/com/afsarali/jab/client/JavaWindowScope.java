package com.afsarali.jab.client;

import com.afsarali.jab.client.model.JavaWindowSelector;

public final class JavaWindowScope {
    private final JavaAutomation automation;
    private final JavaWindowSelector selector;

    JavaWindowScope(JavaAutomation automation, JavaWindowSelector selector) {
        this.automation = automation;
        this.selector = selector;
    }

    public JavaObject object(String objectKey) {
        return new JavaObject(automation, selector, objectKey);
    }
}
