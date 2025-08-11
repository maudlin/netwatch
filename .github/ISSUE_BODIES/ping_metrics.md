Collect ICMP RTTs to default gateway, active DNS resolver, and a stable public host.
Cadence: 1 Hz per target, staggered within each second. Timeout 900 ms; on timeout record as loss.
Keep a fixed circular buffer with last 120 samples per target.

