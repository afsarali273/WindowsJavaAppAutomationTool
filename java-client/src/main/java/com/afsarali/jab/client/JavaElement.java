package com.afsarali.jab.client;

import com.afsarali.jab.client.model.DriverResult;
import com.afsarali.jab.client.model.JavaAction;
import com.afsarali.jab.client.model.JavaWindowSelector;

public final class JavaElement {
    private final JavaDriver driver;
    private final String objectKey;
    private final JavaWindowSelector window;

    JavaElement(JavaDriver driver, String objectKey, JavaWindowSelector window) {
        this.driver = driver;
        this.objectKey = objectKey;
        this.window = window;
    }

    public JavaElement focus() {
        driver.execute(JavaAction.FOCUS, objectKey, "", window);
        return this;
    }

    public JavaElement click() {
        driver.execute(JavaAction.CLICK, objectKey, "", window);
        return this;
    }

    public JavaElement doubleClick() {
        driver.execute(JavaAction.DOUBLE_CLICK, objectKey, "", window);
        return this;
    }

    public JavaElement setText(String text) {
        driver.execute(JavaAction.SET_TEXT, objectKey, text, window);
        return this;
    }

    public JavaElement typeText(String text) {
        driver.execute(JavaAction.TYPE_TEXT, objectKey, text, window);
        return this;
    }

    public String getText() {
        DriverResult result = driver.execute(JavaAction.GET_TEXT, objectKey, "", window);
        return result.text();
    }
}
