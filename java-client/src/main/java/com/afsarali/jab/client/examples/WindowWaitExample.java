package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaAutomation;
import com.afsarali.jab.client.JavaDriver;
import com.afsarali.jab.client.RetryOptions;
import com.afsarali.jab.client.model.JavaWindowSelector;

import java.net.URI;
import java.time.Duration;

public final class WindowWaitExample {
    private WindowWaitExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5000");
        String repository = args.length > 1 ? args[1] : "C:\\Users\\Afsar\\Documents\\Download_20260627_114702.jrecording.json";

        RetryOptions dialogWait = RetryOptions.of(Duration.ofSeconds(20), Duration.ofMillis(300));
        JavaWindowSelector downloadDialog = JavaWindowSelector.title("Download").className("SunAwtDialog");

        try (JavaDriver driver = JavaDriver.attachByTitle(api, "Download", repository, dialogWait)) {
            driver.window(downloadDialog, dialogWait)
                    .element("page_tab_bookmarks_1")
                    .click();
        }

        JavaAutomation.connect(api)
                .repository(repository)
                .window(downloadDialog, dialogWait)
                .object("page_tab_bounding_box_2")
                .click();
    }
}
