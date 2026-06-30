package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaAutomation;
import com.afsarali.jab.client.JavaElementHandle;
import com.afsarali.jab.client.RetryOptions;
import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.ResolutionPolicy;

import java.net.URI;
import java.time.Duration;
import java.util.List;

public final class StatelessUftStyleExample {
    private static final String DEFAULT_REPOSITORY = "C:\\Users\\Afsar\\Documents\\Download_20260627_114702.jrecording.json";

    private static final String DOWNLOAD_TAB = "page_tab_download_from_osm_0";
    private static final String DATA_SOURCES_LABEL = "label_data_sources_and_types_0";
    private static final String OPENSTREETMAP_DATA = "check_box_openstreetmap_data_3";
    private static final String RAW_GPS_DATA = "check_box_raw_gps_data_6";
    private static final String NOTES = "check_box_notes_9";
    private static final String SLIPPY_MAP_TAB = "page_tab_slippy_map_0";
    private static final String BOOKMARKS_TAB = "page_tab_bookmarks_1";
    private static final String BOUNDING_BOX_TAB = "page_tab_bounding_box_2";
    private static final String AREAS_AROUND_PLACES_TAB = "page_tab_areas_around_places_3";
    private static final String TILE_NUMBERS_TAB = "page_tab_tile_numbers_4";

    private StatelessUftStyleExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5000");
        String repository = args.length > 1 ? args[1] : DEFAULT_REPOSITORY;

        JavaAutomation automation = JavaAutomation.connect(api)
                .repository(repository)
                .resolutionPolicy(ResolutionPolicy.strict());

        JavaWindowSelector downloadDialog = JavaWindowSelector.title("Download").className("SunAwtDialog");
        RetryOptions clickRetry = RetryOptions.of(Duration.ofSeconds(5), Duration.ofMillis(200));

        automation.window(downloadDialog)
                .object(DOWNLOAD_TAB)
                .waitUntilExists(Duration.ofSeconds(10), Duration.ofMillis(250))
                .click(clickRetry);

        if (automation.window(downloadDialog).object(DATA_SOURCES_LABEL).isVisible()) {
            System.out.println("Download dialog is ready: Data Sources and Types label is visible.");
        }

        if (automation.window(downloadDialog)
                .object(OPENSTREETMAP_DATA)
                .isEnabled()) {
            automation.window(downloadDialog)
                    .object(OPENSTREETMAP_DATA)
                    .click(clickRetry);
        }

        automation.window(downloadDialog).object(RAW_GPS_DATA).click(clickRetry);
        automation.window(downloadDialog).object(NOTES).click(clickRetry);

        automation.window(downloadDialog).object(SLIPPY_MAP_TAB).click(clickRetry);
        automation.window(downloadDialog).object(BOOKMARKS_TAB).click(clickRetry);
        automation.window(downloadDialog).object(BOUNDING_BOX_TAB).click(clickRetry);
        automation.window(downloadDialog).object(AREAS_AROUND_PLACES_TAB).click(clickRetry);
        automation.window(downloadDialog).object(TILE_NUMBERS_TAB).click(clickRetry);

        List<JavaElementHandle> tabCandidates = automation.window(downloadDialog)
                .findElements(BOOKMARKS_TAB, 70, 10);
        tabCandidates.forEach(candidate -> System.out.println(
                candidate.label()
                        + " | role=" + candidate.snapshot().role()
                        + " | name=" + candidate.snapshot().name()
                        + " | score=" + candidate.snapshot().score()));

        automation.window(JavaWindowSelector.title("Download")).closeWindow();
    }
}
