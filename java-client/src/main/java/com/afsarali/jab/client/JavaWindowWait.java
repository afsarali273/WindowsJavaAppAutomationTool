package com.afsarali.jab.client;

import com.afsarali.jab.client.model.JavaWindow;
import com.afsarali.jab.client.model.JavaWindowSelector;

import java.util.List;

final class JavaWindowWait {
    private JavaWindowWait() {
    }

    static JavaWindow waitForWindow(JabApiClient api, JavaWindowSelector selector, RetryOptions options) {
        return Wait.call(
                () -> findWindow(api.windows(), selector),
                options,
                "Timed out waiting for Java window '" + label(selector) + "'.");
    }

    static JavaWindow findWindow(List<JavaWindow> windows, JavaWindowSelector selector) {
        if (windows == null || windows.isEmpty()) {
            throw new ApiException(404, "No Java windows are currently visible.");
        }

        return windows.stream()
                .filter(window -> matches(selector, window))
                .findFirst()
                .orElseThrow(() -> new ApiException(404, "No matching Java window was found for selector '" + label(selector) + "'."));
    }

    static JavaWindowSelector selectorFor(JavaWindow window) {
        return JavaWindowSelector.hwnd(window.hwnd())
                .className(window.className())
                .processId(window.processId())
                .vmId(window.vmId());
    }

    private static boolean matches(JavaWindowSelector selector, JavaWindow window) {
        if (selector == null) return true;

        if (!isBlank(selector.hwnd()) && !equalsIgnoreCase(selector.hwnd(), window.hwnd())) return false;
        if (!isBlank(selector.className()) && !equalsIgnoreCase(selector.className(), window.className())) return false;
        if (selector.processId() != null && selector.processId() != window.processId()) return false;
        if (selector.vmId() != null && selector.vmId() != window.vmId()) return false;

        if (!isBlank(selector.title())) {
            if (selector.exactTitle()) {
                if (!equalsIgnoreCase(selector.title(), window.title())) return false;
            } else if (window.title() == null || !window.title().toLowerCase().contains(selector.title().toLowerCase())) {
                return false;
            }
        }

        return true;
    }

    private static String label(JavaWindowSelector selector) {
        if (selector == null) return "(any Java window)";
        if (!isBlank(selector.hwnd())) return "hwnd=" + selector.hwnd();
        if (!isBlank(selector.title())) return "title=" + selector.title();
        if (!isBlank(selector.className())) return "className=" + selector.className();
        if (selector.processId() != null) return "processId=" + selector.processId();
        return "(any Java window)";
    }

    private static boolean equalsIgnoreCase(String left, String right) {
        return left != null && right != null && left.equalsIgnoreCase(right);
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }
}
