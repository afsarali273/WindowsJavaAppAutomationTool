package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaDriver;
import com.afsarali.jab.client.RetryOptions;
import com.afsarali.jab.client.model.JavaWindowSelector;

import java.net.URI;
import java.time.Duration;

public final class SessionStyleExample {
    private SessionStyleExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5055");
        String repository = args.length > 1 ? args[1] : "C:/recordings/sample.jrecording.json";
        String commonRepository = args.length > 2 ? args[2] : "C:/recordings/common-controls.jrecording.json";

        try (JavaDriver driver = JavaDriver.attachByTitle(api, "Download", repository)) {
            driver.loadRepositories(commonRepository, repository);

            driver.window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
                    .element("page_tab_bounding_box_2")
                    .waitUntilExists(Duration.ofSeconds(10), Duration.ofMillis(250))
                    .click(RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200)));

            driver.element("button_ok_0").click();
        }
    }
}
