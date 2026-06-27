package com.afsarali.jab.client;

import java.time.Duration;
import java.time.Instant;
import java.util.function.BooleanSupplier;
import java.util.function.Supplier;

final class Wait {
    private Wait() {
    }

    static void until(BooleanSupplier condition, RetryOptions options, String timeoutMessage) {
        call(() -> {
            if (!condition.getAsBoolean()) {
                throw new ApiException(408, timeoutMessage);
            }
            return null;
        }, options, timeoutMessage);
    }

    static <T> T call(Supplier<T> action, RetryOptions options, String timeoutMessage) {
        RetryOptions effective = options == null ? RetryOptions.defaults() : options;
        Instant deadline = Instant.now().plus(effective.timeout());
        RuntimeException lastError = null;

        while (true) {
            try {
                return action.get();
            } catch (RuntimeException ex) {
                lastError = ex;
                if (!effective.retryOnApiFailure() || Instant.now().isAfter(deadline)) {
                    throw new ApiException(408, timeoutMessage + lastMessage(lastError));
                }
                sleep(effective.pollInterval());
            }
        }
    }

    private static String lastMessage(RuntimeException ex) {
        return ex == null || ex.getMessage() == null || ex.getMessage().isBlank()
                ? ""
                : " Last error: " + ex.getMessage();
    }

    private static void sleep(Duration duration) {
        try {
            Thread.sleep(Math.max(10, duration.toMillis()));
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new ApiException(0, "Wait interrupted.");
        }
    }
}
