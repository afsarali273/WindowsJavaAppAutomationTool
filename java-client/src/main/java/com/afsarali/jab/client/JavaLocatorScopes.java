package com.afsarali.jab.client;

import com.afsarali.jab.client.model.LocatorSuggestion;

public final class JavaLocatorScopes {
    private JavaLocatorScopes() {
    }

    public static LocatorSuggestion formsScope(String scopeName) {
        return LocatorSuggestion.builder()
                .formsScopeName(scopeName)
                .build();
    }

    public static LocatorSuggestion formsScope(String scopeName, String role) {
        return LocatorSuggestion.builder()
                .role(role)
                .formsScopeName(scopeName)
                .build();
    }

    public static LocatorSuggestion formsViewport(String viewportName) {
        return LocatorSuggestion.builder()
                .formsViewportName(viewportName)
                .build();
    }

    public static LocatorSuggestion withinFormsScope(LocatorSuggestion locator, String scopeName) {
        if (locator == null) return formsScope(scopeName);
        return locator.toBuilder()
                .formsScopeName(scopeName)
                .build();
    }

    public static LocatorSuggestion withinFormsScopePath(LocatorSuggestion locator, String scopePath) {
        if (locator == null) {
            return LocatorSuggestion.builder().formsScopePath(scopePath).build();
        }

        return locator.toBuilder()
                .formsScopePath(scopePath)
                .build();
    }

    public static LocatorSuggestion withinFormsViewport(LocatorSuggestion locator, String viewportName) {
        if (locator == null) return formsViewport(viewportName);
        return locator.toBuilder()
                .formsViewportName(viewportName)
                .build();
    }

    public static LocatorSuggestion withinFormsViewportPath(LocatorSuggestion locator, String viewportPath) {
        if (locator == null) {
            return LocatorSuggestion.builder().formsViewportPath(viewportPath).build();
        }

        return locator.toBuilder()
                .formsViewportPath(viewportPath)
                .build();
    }
}
