Every 12 s, run a cache-busted DNS lookup (randomized prefix). Cap timeout at 1500 ms. Timeouts count as slow samples. Maintain a fixed-size buffer with last 60 timings.
