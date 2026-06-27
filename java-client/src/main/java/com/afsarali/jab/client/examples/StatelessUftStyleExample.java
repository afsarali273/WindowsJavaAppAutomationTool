package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaAutomation;
import com.afsarali.jab.client.JavaElementSnapshot;
import com.afsarali.jab.client.RetryOptions;
import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.ResolutionPolicy;

import java.net.URI;
import java.time.Duration;
import java.util.List;

public final class StatelessUftStyleExample {
    private StatelessUftStyleExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5055");
        String repository = args.length > 1 ? args[1] : "C:/recordings/sample.jrecording.json";
        String commonRepository = args.length > 2 ? args[2] : "C:/recordings/common-controls.jrecording.json";

        JavaAutomation automation = JavaAutomation.connect(api)
                .repositories(commonRepository, repository)
                .resolutionPolicy(ResolutionPolicy.strict());

        automation.window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
                .object("page_tab_download_from_osm_0")
                .waitUntilExists(Duration.ofSeconds(10), Duration.ofMillis(250))
                .click(RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200)));

        automation.window(JavaWindowSelector.title("Open").className("SunAwtDialog"))
                .object("button_open_0")
                .waitUntilExists(Duration.ofSeconds(10), Duration.ofMillis(250));

        if (automation.window(JavaWindowSelector.title("Open").className("SunAwtDialog"))
                .object("button_open_0")
                .isEnabled()) {
            automation.window(JavaWindowSelector.title("Open").className("SunAwtDialog"))
                    .object("button_open_0")
                    .click();
        }

        List<JavaElementSnapshot> candidates = automation.window(JavaWindowSelector.title("Open").className("SunAwtDialog"))
                .findElements("button_open_0", 70, 10);
        candidates.forEach(candidate -> System.out.println(candidate.displayName() + " score=" + candidate.score()));
    }
}
