package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaAutomation;
import com.afsarali.jab.client.JavaElementHandle;
import com.afsarali.jab.client.JavaDriver;
import com.afsarali.jab.client.RetryOptions;
import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.ResolutionPolicy;

import java.net.URI;
import java.util.List;

public final class FindElementsExample {
    private static final String DEFAULT_REPOSITORY = "C:\\Users\\Afsar\\Documents\\JabInspector\\ObjectRepositories\\Java OpenStreetMap Editor_20260628_204333.jrecording.json";
    private static final String WINDOW_TITLE = "Java OpenStreetMap Editor";
    private static final String WINDOW_CLASS = "SunAwtFrame";

    private static final String TAB_OBJECT_KEY = "tool_bar_1";
//    private static final String PARENT_OBJECT_KEY = "page_tab_list_bookmarks_0";

    private FindElementsExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5000");
        String repository = args.length > 1 ? args[1] : DEFAULT_REPOSITORY;

        JavaWindowSelector downloadDialog = JavaWindowSelector.title(WINDOW_TITLE).className(WINDOW_CLASS);

        System.out.println("=== Session-independent style ===");
        JavaAutomation automation = JavaAutomation.connect(api)
                .repository(repository)
                .resolutionPolicy(ResolutionPolicy.strict());

        List<JavaElementHandle> matchingTabs = automation.window(downloadDialog)
                .findElements(TAB_OBJECT_KEY);

        System.out.println("findElements results for object key: " + TAB_OBJECT_KEY);
        printHandles(matchingTabs);

        List<JavaElementHandle> descendants = automation.window(downloadDialog)
                .findChildElements(TAB_OBJECT_KEY);
                //.object(TAB_OBJECT_KEY)
                //.findChildElements(3, 50, true);

        System.out.println();
        System.out.println("findChildElements results under parent object key: " + TAB_OBJECT_KEY);
        printHandles(descendants);

        if (!matchingTabs.isEmpty()) {
            System.out.println();
            System.out.println("Actionable search result example:");
            JavaElementHandle firstTab = matchingTabs.get(0);
            System.out.println("  label=" + firstTab.label() + ", role=" + firstTab.snapshot().role());
            // firstTab.click();
        }

        System.out.println();
        System.out.println("=== Session style ===");
        try (JavaDriver driver = JavaDriver.attachByTitle(api, WINDOW_TITLE, repository)) {
            driver.loadRepositories(repository);

            List<JavaElementHandle> sessionMatches = driver.window(downloadDialog)
                    .findElements(TAB_OBJECT_KEY, 70, 10);

            System.out.println("List of actionable tabs found in session: " + sessionMatches.size());
            System.out.println("Session findElements results:");
            if (!sessionMatches.isEmpty()) {
                System.out.println("  first handle label=" + sessionMatches.get(0).label());
            }

            List<JavaElementHandle> sessionDescendants = driver.element(TAB_OBJECT_KEY)
                    .findChildElements();

            System.out.println();
            System.out.println("Session findChildElements results:");
            sessionDescendants.forEach(handle -> {
                System.out.println(
                        handle.label()
                                + " | role=" + handle.snapshot().role()
                                + " | name=" + handle.snapshot().name()
                                + " | path=" + handle.snapshot().path()
                                + " | score=" + handle.snapshot().score());
            });

            sessionDescendants.get(0).click();
        }
    }

    private static void printHandles(List<JavaElementHandle> handles) {
        if (handles == null || handles.isEmpty()) {
            System.out.println("(no matches)");
            return;
        }

        for (JavaElementHandle handle : handles) {
            System.out.println(
                    handle.label()
                            + " | role=" + handle.snapshot().role()
                            + " | name=" + handle.snapshot().name()
                            + " | path=" + handle.snapshot().path()
                            + " | score=" + handle.snapshot().score());
        }
    }
}
