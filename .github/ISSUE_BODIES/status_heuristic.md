Primary status driven by public ping + DNS as tiebreaker.

Thresholds:
- GREEN: loss < 0.5%, jitter < 20 ms, p50 < 60 ms, DNS median < 50 ms
- AMBER (any): loss 0.5–2%, jitter 20–50 ms, p50 60–120 ms, DNS 50–150 ms
- RED (any): loss > 2%, jitter > 50 ms, p50 > 120 ms, DNS > 150 ms

Build a concise status reason string from triggered rules (first 2–3).

