Implement p50/p95 via P² quantile estimator per stream.
Compute jitter via Welford online variance/stddev on successful pings.
Track loss% via windowed counters aligned to the buffer size.
Avoid per-tick allocations; recompute derived stats on buffer updates.

