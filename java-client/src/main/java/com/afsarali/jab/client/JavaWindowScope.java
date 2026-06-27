package com.afsarali.jab.client;

import com.afsarali.jab.client.model.JavaWindowSelector;
import java.util.List;

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

    public List<JavaElementSnapshot> findElements(String objectKey) {
        return automation.findElements(objectKey, null, null, selector);
    }

    public List<JavaElementSnapshot> findElements(String objectKey, Integer minimumScore, Integer maxResults) {
        return automation.findElements(objectKey, minimumScore, maxResults, selector);
    }

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey) {
        return automation.findChildElements(parentObjectKey, null, null, false, selector);
    }

    public List<JavaElementSnapshot> findChildElements(String parentObjectKey, Integer maxDepth, Integer maxResults, boolean includeSelf) {
        return automation.findChildElements(parentObjectKey, maxDepth, maxResults, includeSelf, selector);
    }
}
