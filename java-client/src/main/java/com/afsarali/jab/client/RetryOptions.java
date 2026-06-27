package com.afsarali.jab.client;

import java.time.Duration;

public final class RetryOptions {
    private final Duration timeout;
    private final Duration pollInterval;
    private final boolean retryOnApiFailure;

    public RetryOptions(Duration timeout, Duration pollInterval, boolean retryOnApiFailure) {
        this.timeout = timeout;
        this.pollInterval = pollInterval;
        this.retryOnApiFailure = retryOnApiFailure;
    }

    public static RetryOptions defaults() {
        return new RetryOptions(Duration.ofSeconds(10), Duration.ofMillis(250), true);
    }

    public static RetryOptions of(Duration timeout, Duration pollInterval) {
        return new RetryOptions(timeout, pollInterval, true);
    }

    public Duration timeout() {
        return timeout;
    }

    public Duration pollInterval() {
        return pollInterval;
    }

    public boolean retryOnApiFailure() {
        return retryOnApiFailure;
    }

    public RetryOptions timeout(Duration timeout) {
        return new RetryOptions(timeout, pollInterval, retryOnApiFailure);
    }

    public RetryOptions pollInterval(Duration pollInterval) {
        return new RetryOptions(timeout, pollInterval, retryOnApiFailure);
    }
}
