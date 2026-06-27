package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaAutomation;
import com.afsarali.jab.client.JavaDriver;
import com.afsarali.jab.client.RetryOptions;
import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.LocatorSuggestion;
import com.afsarali.jab.client.model.ResolutionPolicy;

import java.net.URI;
import java.time.Duration;

public final class InlineLocatorExample {
    private InlineLocatorExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5000");
        JavaWindowSelector downloadDialog = JavaWindowSelector.title("Download").className("SunAwtDialog");

        LocatorSuggestion bookmarksTab = LocatorSuggestion.builder()
                .role("page tab")
                .name("Bookmarks")
                .parentRole("page tab list")
                .objectDepth(8)
                .build();

        LocatorSuggestion boundingBoxTab = LocatorSuggestion.roleAndName("page tab", "Bounding Box");

        try (JavaDriver driver = JavaDriver.attachByTitle(api, "Download")
                .resolutionPolicy(ResolutionPolicy.inline())) {
            driver.window(downloadDialog)
                    .element(bookmarksTab)
                    .waitUntilExists(Duration.ofSeconds(10), Duration.ofMillis(250))
                    .click(RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200)));

            driver.element(boundingBoxTab).click();
        }

        JavaAutomation.connect(api)
                .resolutionPolicy(ResolutionPolicy.inline())
                .window(downloadDialog)
                .object(LocatorSuggestion.roleAndName("page tab", "Slippy map"))
                .click(RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200)));
    }
}
