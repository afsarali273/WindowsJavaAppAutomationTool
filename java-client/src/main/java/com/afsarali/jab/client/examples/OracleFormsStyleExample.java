package com.afsarali.jab.client.examples;

import com.afsarali.jab.client.JavaAutomation;
import com.afsarali.jab.client.JavaDriver;
import com.afsarali.jab.client.JavaTable;
import com.afsarali.jab.client.RetryOptions;
import com.afsarali.jab.client.model.JavaWindowSelector;
import com.afsarali.jab.client.model.LocatorSuggestion;

import java.net.URI;
import java.time.Duration;

public final class OracleFormsStyleExample {
    private static final String WINDOW_TITLE = "Opera";

    private OracleFormsStyleExample() {
    }

    public static void main(String[] args) {
        URI api = URI.create(args.length > 0 ? args[0] : "http://localhost:5000");
        String repository = args.length > 1 ? args[1] : "C:\\Users\\Afsar\\Documents\\OperaPmsObjects.jrecording.json";

        JavaWindowSelector operaWindow = JavaWindowSelector.title(WINDOW_TITLE);

        sessionStyle(api, repository, operaWindow);
        statelessStyle(api, repository, operaWindow);
        inlineLocatorStyle(api, operaWindow);
    }

    private static void sessionStyle(URI api, String repository, JavaWindowSelector operaWindow) {
        try (JavaDriver driver = JavaDriver.attachToWindow(
                api,
                operaWindow,
                RetryOptions.of(Duration.ofSeconds(30), Duration.ofMillis(500)))) {

            driver.loadRepository(repository);

            driver.internalFrame("Reservations")
                    .element("guest_name_text")
                    .waitUntilExists()
                    .setText("ALI");

            JavaTable reservations = driver.internalFrame("Reservations")
                    .viewport("Reservation Grid")
                    .table("reservation_results_grid");

            reservations.scrollToRow(15);
            reservations.row(0).cell("Guest Name").doubleClick();
            String room = reservations.row(0).text("Room");
            System.out.println("First reservation room: " + room);
        }
    }

    private static void statelessStyle(URI api, String repository, JavaWindowSelector operaWindow) {
        JavaAutomation automation = JavaAutomation.connect(api)
                .repository(repository);

        automation.window(operaWindow)
                .internalFrame("Billing")
                .table("payment_grid")
                .row(0)
                .cell("Amount")
                .click();
    }

    private static void inlineLocatorStyle(URI api, JavaWindowSelector operaWindow) {
        try (JavaDriver driver = JavaDriver.attachToWindow(
                api,
                operaWindow,
                RetryOptions.of(Duration.ofSeconds(30), Duration.ofMillis(500)))) {

            LocatorSuggestion amountField = LocatorSuggestion.builder()
                    .role("text")
                    .name("Amount")
                    .build();

            driver.internalFrame("Billing")
                    .viewport("Payment Grid")
                    .element(amountField)
                    .click();
        }
    }
}
