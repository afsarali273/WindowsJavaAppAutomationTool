package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaAutomation;
import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.ResolutionPolicy;

import java.net.URI;

public final class StatelessUftStyleExample {
    private StatelessUftStyleExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5055");
        String repository = args.length > 1 ? args[1] : "C:/recordings/sample.jrecording.json";

        JavaAutomation automation = JavaAutomation.connect(api)
                .repository(repository)
                .resolutionPolicy(ResolutionPolicy.strict());

        automation.window(JavaWindowSelector.title("Download").className("SunAwtDialog"))
                .object("page_tab_download_from_osm_0")
                .click();

        automation.window(JavaWindowSelector.title("Open").className("SunAwtDialog"))
                .object("button_open_0")
                .click();
    }
}
