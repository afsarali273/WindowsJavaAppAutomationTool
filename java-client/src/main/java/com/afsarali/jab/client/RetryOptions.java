package com.afsarali.jab.client;

import java.time.Duration;

public record RetryOptions(
        Duration timeout,
        Duration pollInterval,
        boolean retryOnApiFailure) {

    public static RetryOptions defaults() {
        return new RetryOptions(Duration.ofSeconds(10), Duration.ofMillis(250), true);
    }

    public static RetryOptions of(Duration timeout, Duration pollInterval) {
        return new RetryOptions(timeout, pollInterval, true);
    }

    public RetryOptions timeout(Duration timeout) {
        return new RetryOptions(timeout, pollInterval, retryOnApiFailure);
    }

    public RetryOptions pollInterval(Duration pollInterval) {
        return new RetryOptions(timeout, pollInterval, retryOnApiFailure);
    }
}
